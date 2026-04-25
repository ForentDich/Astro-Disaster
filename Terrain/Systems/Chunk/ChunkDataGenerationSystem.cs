using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;

/// <summary>
/// Generates chunk data for chunks that were not found on disk.
/// Data layout per chunk: 33x33 heights + 32x32 surface bytes.
/// </summary>
public class ChunkDataGenerationSystem : QuerySystem<ChunkInfo>
{
	private NoiseGenerator _noiseGenerator;
	private EntityStore _store;

	private int[] _selectedEntityIds;
	private int[] _selectedDistances;
	private int _selectedCount;

	public int MaxPerFrame { get; set; } = 4;
	public Node3D Viewer { get; set; }
	public NoiseSettings NoiseSettings { get; set; }
	public float HeightScale { get; set; } = 0.25f;
	public int SeaLevelHeight { get; set; }

	/// <summary>Reference to segment creator for resolving .seg destination path.</summary>
	public SystemSegmentCreator SegmentCreator { get; set; }

	public ChunkDataGenerationSystem()
		=> Filter.AllTags(Tags.Get<ChunkPending>())
			.WithoutAnyTags(Tags.Get<PendingRemoval>());

	protected override void OnAddStore(EntityStore store)
	{
		base.OnAddStore(store);
		_store = store;
		EnsureNoiseGenerator();
	}

	public void ReapplySettings()
	{
		EnsureNoiseGenerator();
		_noiseGenerator = new NoiseGenerator(NoiseSettings);
	}

	protected override void OnUpdate()
	{
		if (MaxPerFrame <= 0)
			return;

		EnsureNoiseGenerator();
		var commandBuffer = CommandBuffer;

		if (Viewer == null)
		{
			int generated = 0;
			foreach (var entity in Query.Entities)
			{
				if (generated >= MaxPerFrame)
					break;

				TryGenerateChunk(entity.Id, ref entity.GetComponent<ChunkInfo>(), commandBuffer);
				generated++;
			}
			return;
		}

		(int centerX, int centerZ) = NearestChunkSelectionTool.GetViewerChunkCoords(Viewer, ChunkConstants.CHUNK_WORLD_SIZE);

		NearestChunkSelectionTool.EnsureCapacity(ref _selectedEntityIds, ref _selectedDistances, MaxPerFrame);
		_selectedCount = 0;

		foreach (var entity in Query.Entities)
		{
			ref var info = ref entity.GetComponent<ChunkInfo>();
			int dist = Math.Max(Math.Abs(info.X - centerX), Math.Abs(info.Z - centerZ));
			NearestChunkSelectionTool.TryInsertNearest(
				ref _selectedCount,
				_selectedEntityIds,
				_selectedDistances,
				entity.Id,
				dist,
				MaxPerFrame);
		}

		for (int i = 0; i < _selectedCount; i++)
		{
			int entityId = _selectedEntityIds[i];
			if (!_store.TryGetEntityById(entityId, out var entity) || entity.IsNull)
				continue;

			ref var info = ref entity.GetComponent<ChunkInfo>();
			TryGenerateChunk(entityId, ref info, commandBuffer);
		}
	}

	private void TryGenerateChunk(int entityId, ref ChunkInfo info, CommandBuffer commandBuffer)
	{
		try
		{
			byte[] terrainData = GenerateChunkData(ref info);

			commandBuffer.AddComponent(entityId, new ChunkTerrain { Data = terrainData });
			commandBuffer.RemoveTag<ChunkPending>(entityId);
			commandBuffer.AddTag<ChunkDataReady>(entityId);
			commandBuffer.RemoveTag<ChunkError>(entityId);

			SaveChunkToSegment(ref info, terrainData);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[ChunkDataGen] Error generating chunk ({info.X},{info.Z}): {ex.Message}");
			commandBuffer.RemoveTag<ChunkPending>(entityId);
			commandBuffer.AddTag<ChunkError>(entityId);
		}
	}

	private void EnsureNoiseGenerator()
	{
		if (NoiseSettings == null)
			NoiseSettings = NoiseSettings.CreateDefault();

		_noiseGenerator ??= new NoiseGenerator(NoiseSettings);
	}

	private byte[] GenerateChunkData(ref ChunkInfo info)
	{
		int chunkSize = ChunkConstants.CHUNK_SIZE;
		int vertexSize = ChunkConstants.CHUNK_VERTEX_SIZE;
		int vertexCount = ChunkConstants.HEIGHT_COUNT;
		int maxHeight = ConstantsCelestial.MAX_HEIGHT;

		int[] heights = new int[vertexCount];
		byte[] zones = new byte[vertexCount];
		byte[] erosions = new byte[vertexCount];

		int worldOffsetX = info.X * chunkSize * ChunkConstants.TILE_SIZE;
		int worldOffsetZ = info.Z * chunkSize * ChunkConstants.TILE_SIZE;

		_noiseGenerator.GenerateHeightmap(
			heights.AsSpan(),
			zones.AsSpan(),
			erosions.AsSpan(),
			worldOffsetX,
			worldOffsetZ,
			vertexSize,
			vertexSize,
			maxHeight,
			HeightScale,
			ChunkConstants.TILE_SIZE
		);

		byte[] data = new byte[ChunkConstants.CHUNK_DATA_SIZE];

		for (int i = 0; i < vertexCount; i++)
		{
			int height = Math.Clamp(heights[i], ConstantsCelestial.MIN_HEIGHT, maxHeight);
			data[ChunkConstants.HEIGHTS_OFFSET + i] = (byte)height;
		}

		for (int z = 0; z < chunkSize; z++)
		{
			int vertexRow = z * vertexSize;
			int cellRow = z * chunkSize;

			for (int x = 0; x < chunkSize; x++)
			{
				int vertexIndex = vertexRow + x;
				int height = Math.Clamp(heights[vertexIndex], ConstantsCelestial.MIN_HEIGHT, maxHeight);
				int zone = zones[vertexIndex];
				float erosion = erosions[vertexIndex] / 255f;
				int biomeIndex = BiomeRegistry.GetBiome(zone, erosion);

				byte surfaceByte = DetermineSurfaceByte(height, biomeIndex, SeaLevelHeight);
				data[ChunkConstants.CELLS_OFFSET + cellRow + x] = surfaceByte;
			}
		}

		return data;
	}

	private void SaveChunkToSegment(ref ChunkInfo info, byte[] terrainData)
	{
		if (SegmentCreator == null)
			return;

		string facePath = SegmentCreator.FaceStoragePath;
		if (string.IsNullOrEmpty(facePath))
			return;

		try
		{
			string segPath = SegmentFile.GetSegmentFilePath(facePath, info.X, info.Z);
			var (localX, localZ) = SegmentFile.ChunkToLocal(info.X, info.Z);
			SegmentFile.WriteChunk(segPath, localX, localZ, terrainData);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[ChunkDataGen] Failed to save chunk ({info.X},{info.Z}): {ex.Message}");
		}
	}

	private static byte DetermineSurfaceByte(int height, int biomeIndex, int seaLevelHeight)
	{
		byte surface = 0;
		bool hasWater = false;

		if (biomeIndex >= 0 && biomeIndex < BiomeRegistry.Count)
		{
			ref var biome = ref BiomeRegistry.Biomes[biomeIndex];
			surface = ResolveSurfaceIndex(biome.HeightRules, height);
			hasWater = biome.HasWater;
		}

		if (hasWater && height < seaLevelHeight)
			surface |= ChunkConstants.WATER_FLAG;

		return surface;
	}

	private static byte ResolveSurfaceIndex(BiomeRegistry.HeightRule[] rules, int height)
	{
		if (rules == null || rules.Length == 0)
			return 0;

		for (int i = 0; i < rules.Length; i++)
		{
			ref var rule = ref rules[i];
			if (height >= rule.MinHeight && height <= rule.MaxHeight)
				return (byte)Math.Clamp(rule.SurfaceIndex, 0, ChunkConstants.SURFACE_MASK);
		}

		return (byte)Math.Clamp(rules[0].SurfaceIndex, 0, ChunkConstants.SURFACE_MASK);
	}
}
