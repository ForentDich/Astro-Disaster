using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.Buffers;
using System.Runtime.InteropServices;


public class ChunkDataGenerationSystem : QuerySystem<ChunkInfo>
{
	private NoiseGenerator _noiseGenerator;
	private EntityStore _store;

	private int[] _selectedEntityIds;
	private int[] _selectedDistances;
	private int _selectedCount;

	private readonly ArrayPool<int> _heightPool    = ArrayPool<int>.Shared;
	private readonly ArrayPool<byte> _zonePool     = ArrayPool<byte>.Shared;
	private readonly ArrayPool<byte> _erosionPool  = ArrayPool<byte>.Shared;

	/// <summary>Query for chunks that already have their mesh built.</summary>
	private ArchetypeQuery<ChunkInfo> _completedQuery;

	public int MaxPerFrame { get; set; } = 4;
	public Node3D Viewer { get; set; }
	
	public NoiseSettings NoiseSettings { get; set; }

	public float HeightScale { get; set; } = 0.25f; // Используем 25% от MaxHeight

	/// <summary>Sea level in tile height units. Used to set water flag on tiles.</summary>
	public int SeaLevelTile { get; set; }

	/// <summary>Reference to segment creator for resolving .seg file paths.</summary>
	public SystemSegmentCreator SegmentCreator { get; set; }

	public ChunkDataGenerationSystem() => Filter.AllTags(Tags.Get<ChunkPending>());

	protected override void OnAddStore(EntityStore store)
	{
		base.OnAddStore(store);
		_store = store;
		_completedQuery = store.Query<ChunkInfo>()
			.AllTags(Tags.Get<ChunkComplete>())
			.WithoutAnyTags(Tags.Get<PendingRemoval>());
		
		if (NoiseSettings == null)
		{
			NoiseSettings = NoiseSettings.CreateDefault();
			GD.Print("[ChunkDataGenerationSystem] Created default NoiseSettings");
		}
		
		_noiseGenerator = new NoiseGenerator(NoiseSettings);
	}

	/// <summary>
	/// Recreates the internal NoiseGenerator from current NoiseSettings.
	/// Call after modifying NoiseSettings properties to apply changes.
	/// </summary>
	public void ReapplySettings()
	{
		_noiseGenerator = new NoiseGenerator(NoiseSettings);
	}

	protected override void OnUpdate()
	{
		var commandBuffer = CommandBuffer;

		if (MaxPerFrame <= 0)
			return;

		if (Viewer == null)
		{
			GD.PrintErr("[ChunkDataGenerationSystem] Viewer is not set!");
			return;
		}

		(int centerX, int centerZ) = NearestChunkSelectionTool.GetViewerChunkCoords(Viewer, ChunkConstants.CHUNK_WORLD_SIZE);

		NearestChunkSelectionTool.EnsureCapacity(ref _selectedEntityIds, ref _selectedDistances, MaxPerFrame);
		_selectedCount = 0;

		foreach (var entity in Query.Entities)
		{
			ref var info = ref entity.GetComponent<ChunkInfo>();
			int dist = Math.Max(Math.Abs(info.X - centerX), Math.Abs(info.Z - centerZ));
			NearestChunkSelectionTool.TryInsertNearest(ref _selectedCount, _selectedEntityIds, _selectedDistances, entity.Id, dist, MaxPerFrame);
		}

		for (int i = 0; i < _selectedCount; i++)
		{
			int entityId = _selectedEntityIds[i];

			if (!_store.TryGetEntityById(entityId, out var entity) || entity.IsNull)
				continue;

			try
			{
				ref var info = ref entity.GetComponent<ChunkInfo>();
				var terrainData = GenerateChunkData(ref info);

				commandBuffer.AddComponent(entityId, new ChunkTerrain { Data = terrainData });
				commandBuffer.RemoveTag<ChunkPending>(entityId);
				commandBuffer.AddTag<ChunkDataReady>(entityId);
				commandBuffer.AddTag<NeedsMeshUpdate>(entityId);

				SaveChunkToSegment(ref info, terrainData);

				// Mark left / top neighbors for mesh rebuild so they
				// can now build correct boundary walls using our data.
				MarkNeighborsForRebuild(ref info, commandBuffer);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ChunkDataGenerationSystem] Error generating chunk data: {ex}");
				commandBuffer.RemoveTag<ChunkPending>(entityId);
				commandBuffer.AddTag<ChunkError>(entityId);
			}
		}
	}

	private byte[] GenerateChunkData(ref ChunkInfo info)
	{
		int size = ChunkConstants.CHUNK_SIZE;
		int maxHeight = ConstantsCelestial.MAX_HEIGHT;
		// 32×32 tiles need 33×33 height corners
		int paddedSize = size + 1;
		int totalElements = paddedSize * paddedSize;

		int[] tempHeightArray = _heightPool.Rent(totalElements);
		byte[] tempZoneArray = _zonePool.Rent(totalElements);
		byte[] tempErosionArray = _erosionPool.Rent(totalElements);
		
		try
		{
			Span<int> heights = tempHeightArray.AsSpan(0, totalElements);
			Span<byte> zones = tempZoneArray.AsSpan(0, totalElements);
			Span<byte> erosion = tempErosionArray.AsSpan(0, totalElements);

			int ts = ChunkConstants.TILE_SIZE;
			int worldOffsetX = info.X * size * ts;
			int worldOffsetZ = info.Z * size * ts;

			_noiseGenerator.GenerateHeightmap(
				heights,
				zones,
				erosion,
				worldOffsetX,
				worldOffsetZ,
				paddedSize,
				paddedSize,
				maxHeight,
				HeightScale,
				ts
			);

			return ProcessTerrainData(heights, zones, erosion, size, maxHeight, paddedSize);
		}
		finally
		{
			_heightPool.Return(tempHeightArray);
			_zonePool.Return(tempZoneArray);
			_erosionPool.Return(tempErosionArray);
		}
	}

	private byte[] ProcessTerrainData(Span<int> flatHeights, Span<byte> zones, Span<byte> erosion, int size, int maxHeight, int paddedSize)
	{
		// 32×32 tiles from 33×33 height points
		int tileCount = size * size;
		Span<TileType> tileTypes = stackalloc TileType[tileCount];
		Span<int> baseHeights = stackalloc int[tileCount];

		TileAutoMapper.DetermineTileTypesBatch(
			flatHeights,
			tileTypes,
			baseHeights,
			0, 0, size,
			paddedSize
		);

		int stride = ChunkConstants.BYTES_PER_TILE;
		byte[] data = new byte[tileCount * stride];
		Span<byte> dataSpan = data;

		for (int z = 0; z < size; z++)
		{
			for (int x = 0; x < size; x++)
			{
				int i = z * size + x;
				int di = i * stride;
				int bh = Math.Clamp(baseHeights[i], 0, maxHeight);
				int hmIdx = z * paddedSize + x;
				int zone = zones[hmIdx];
				float E = erosion[hmIdx] / 255f;
				int biomeIdx = BiomeRegistry.GetBiome(zone, E);
				byte surfaceByte = (byte)SurfaceMapper.DetermineSurface(bh, tileTypes[i], biomeIdx);

				// Water flag: from biome definition (hasWater) + below sea level
				if (bh < SeaLevelTile && BiomeRegistry.Biomes[biomeIdx].HasWater)
					surfaceByte |= ChunkConstants.WATER_FLAG;

				dataSpan[di]     = (byte)bh;
				dataSpan[di + 1] = (byte)tileTypes[i];
				dataSpan[di + 2] = surfaceByte;
			}
		}

		return data;
	}

	private void SaveChunkToSegment(ref ChunkInfo info, byte[] terrainData)
	{
		if (SegmentCreator == null) return;

		string facePath = SegmentCreator.FaceStoragePath;
		if (string.IsNullOrEmpty(facePath)) return;

		try
		{
			string segPath = SegmentFile.GetSegmentFilePath(facePath, info.X, info.Z);
			var (localX, localZ) = SegmentFile.ChunkToLocal(info.X, info.Z);
			SegmentFile.WriteChunk(segPath, localX, localZ, terrainData);
		}
		catch (System.Exception ex)
		{
			GD.PrintErr($"[ChunkDataGen] Failed to save chunk ({info.X},{info.Z}) to .seg: {ex.Message}");
		}
	}

	/// <summary>
	/// Marks the left (X-1) and top (Z-1) neighbors for mesh rebuild
	/// so they can build correct boundary walls using our fresh data.
	/// </summary>
	private void MarkNeighborsForRebuild(ref ChunkInfo info, CommandBuffer buffer)
	{
		foreach (var entity in _completedQuery.Entities)
		{
			ref var n = ref entity.GetComponent<ChunkInfo>();
			if ((n.X == info.X - 1 && n.Z == info.Z) ||
				(n.X == info.X && n.Z == info.Z - 1))
			{
				buffer.AddTag<NeedsMeshUpdate>(entity.Id);
			}
		}
	}
}
