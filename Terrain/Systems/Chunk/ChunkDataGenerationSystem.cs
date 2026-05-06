using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;

/// <summary>
/// Generates chunk data for chunks that are not yet in memory.
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

	/// <summary>Planet position in world space. Used to offset viewer position for local-space calculations.</summary>
	public Vector3 PlanetPosition { get; set; } = Vector3.Zero;

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

		// Use local viewer position relative to planet for chunk coordinate selection
		Vector3 localViewerPos = Viewer.GlobalPosition - PlanetPosition;
		(int centerX, int centerZ) = NearestChunkSelectionTool.GetViewerChunkCoords(localViewerPos, ChunkConstants.CHUNK_WORLD_SIZE);

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
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[ChunkDataGen] Error generating chunk ({info.X},{info.Z}) face {info.FaceIndex}: {ex.Message}");
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

		// Get face orientation for 3D noise sampling
		FaceOrientation orientation = default;
		int segmentsPerSide = 1;

		if (SegmentCreator != null)
		{
			var faceOrientation = SegmentCreator.GetFaceOrientation(info.FaceIndex);
			if (faceOrientation.HasValue)
			{
				orientation = faceOrientation.Value;
			}
			segmentsPerSide = SegmentCreator.SegmentsPerSide;
		}

		int faceResolution = CubeSphereProjection.GetFaceResolution(segmentsPerSide);
		float planetRadius = ConstantsCelestial.ComputeRadius(segmentsPerSide);

		// Compute chunk offset in face-local coordinates
		// info.X and info.Z are in chunk units, convert to vertex indices
		int chunkOffsetX = info.X * chunkSize;
		int chunkOffsetZ = info.Z * chunkSize;

		_noiseGenerator.GenerateHeightmap3D(
			heights.AsSpan(),
			zones.AsSpan(),
			erosions.AsSpan(),
			faceResolution,
			orientation,
			planetRadius,
			chunkOffsetX,
			chunkOffsetZ,
			vertexSize,
			vertexSize,
			maxHeight,
			HeightScale,
			1
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
