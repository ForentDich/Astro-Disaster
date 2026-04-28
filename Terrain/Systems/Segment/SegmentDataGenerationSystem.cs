using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;

/// <summary>
/// Generates full segment data and writes all chunk payloads to one .seg file.
/// Data layout per chunk: 33x33 heights + 32x32 surface bytes.
/// Uses 3D noise sampled at sphere positions for seamless spherical terrain.
/// </summary>
public class SegmentDataGenerationSystem : QuerySystem<SegmentIdentity, SegmentStorage>
{
    private NoiseGenerator _noiseGenerator;

    public NoiseGenerator NoiseGenerator => _noiseGenerator;

    public NoiseSettings NoiseSettings { get; set; }
    public float HeightScale { get; set; } = 0.25f;
    public int SeaLevelHeight { get; set; }
    public int MaxPerFrame { get; set; } = 1;

    /// <summary>Reference to segment creator for resolving face orientation.</summary>
    public SystemSegmentCreator SegmentCreator { get; set; }

    public SegmentDataGenerationSystem()
        => Filter.AllTags(Tags.Get<SegmentNeedsGenerate>());

    protected override void OnAddStore(EntityStore store)
    {
        base.OnAddStore(store);
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
        var buffer = CommandBuffer;

        int processed = 0;
        foreach (var entity in Query.Entities)
        {
            if (processed >= MaxPerFrame)
                break;

            try
            {
                ref var identity = ref entity.GetComponent<SegmentIdentity>();
                ref var storage = ref entity.GetComponent<SegmentStorage>();

                GenerateFullSegment(ref identity, storage.FilePath);

                buffer.RemoveTag<SegmentNeedsGenerate>(entity.Id);
                buffer.AddTag<SegmentDataReady>(entity.Id);
                buffer.AddTag<SegmentDataClean>(entity.Id);
                buffer.RemoveTag<SegmentDataDirty>(entity.Id);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SegmentDataGen] Failed at entity {entity.Id}: {ex.Message}");
                buffer.RemoveTag<SegmentNeedsGenerate>(entity.Id);
            }

            processed++;
        }
    }

    private void EnsureNoiseGenerator()
    {
        if (NoiseSettings == null)
            NoiseSettings = NoiseSettings.CreateDefault();

        _noiseGenerator ??= new NoiseGenerator(NoiseSettings);
    }

    private void GenerateFullSegment(ref SegmentIdentity identity, string segmentFilePath)
    {
        const int side = ConstantsSegment.SIDE;
        const int chunkSize = ChunkConstants.CHUNK_SIZE;

        int segmentCells = side * chunkSize;
        int hmSize = segmentCells + 1;
        int total = hmSize * hmSize;

        int[] heights = new int[total];
        byte[] zones = new byte[total];
        byte[] erosions = new byte[total];

        // Get face orientation from the segment creator
        int faceIndex = identity.FaceIndex;
        FaceOrientation orientation = default;
        int segmentsPerSide = 1;

        if (SegmentCreator != null)
        {
            var faceOrientation = SegmentCreator.GetFaceOrientation(faceIndex);
            if (faceOrientation.HasValue)
            {
                orientation = faceOrientation.Value;
            }
            segmentsPerSide = SegmentCreator.SegmentsPerSide;
        }

        int faceResolution = CubeSphereProjection.GetFaceResolution(segmentsPerSide);
        float planetRadius = ConstantsCelestial.ComputeRadius(segmentsPerSide);

        // Compute chunk offset in face-local coordinates
        // segmentGrid is in segment units, convert to chunk units
        int chunkOffsetX = identity.GridPosition.X * side * chunkSize;
        int chunkOffsetZ = identity.GridPosition.Y * side * chunkSize;

        _noiseGenerator.GenerateHeightmap3D(
            heights.AsSpan(),
            zones.AsSpan(),
            erosions.AsSpan(),
            faceResolution,
            orientation,
            planetRadius,
            chunkOffsetX,
            chunkOffsetZ,
            hmSize,
            hmSize,
            ConstantsCelestial.MAX_HEIGHT,
            HeightScale,
            1
        );

        byte[][] allChunks = new byte[ConstantsSegment.TOTAL_CHUNKS][];
        int vertexSize = ChunkConstants.CHUNK_VERTEX_SIZE;

        for (int cz = 0; cz < side; cz++)
        {
            for (int cx = 0; cx < side; cx++)
            {
                int chunkIndex = cz * side + cx;
                byte[] data = new byte[ChunkConstants.CHUNK_DATA_SIZE];
                int startX = cx * chunkSize;
                int startZ = cz * chunkSize;

                // 1) Store 33x33 corner heights for fully local mesh/collision reconstruction.
                for (int vz = 0; vz < vertexSize; vz++)
                {
                    int hmRowStart = (startZ + vz) * hmSize + startX;
                    int outRowStart = ChunkConstants.HEIGHTS_OFFSET + vz * vertexSize;

                    for (int vx = 0; vx < vertexSize; vx++)
                    {
                        int hmIndex = hmRowStart + vx;
                        int h = Math.Clamp(heights[hmIndex], ConstantsCelestial.MIN_HEIGHT, ConstantsCelestial.MAX_HEIGHT);
                        data[outRowStart + vx] = (byte)h;
                    }
                }

                for (int z = 0; z < chunkSize; z++)
                {
                    int hmRowStart = (startZ + z) * hmSize + startX;
                    int outRowStart = ChunkConstants.CELLS_OFFSET + z * chunkSize;

                    for (int x = 0; x < chunkSize; x++)
                    {
                        int hmIndex = hmRowStart + x;
                        int height = Math.Clamp(heights[hmIndex], ConstantsCelestial.MIN_HEIGHT, ConstantsCelestial.MAX_HEIGHT);

                        int zone = zones[hmIndex];
                        float erosion = erosions[hmIndex] / 255f;
                        int biomeIndex = BiomeRegistry.GetBiome(zone, erosion);

                        byte surfaceByte = DetermineSurfaceByte(height, biomeIndex, SeaLevelHeight);
                        data[outRowStart + x] = surfaceByte;
                    }
                }

                allChunks[chunkIndex] = data;
            }
        }

        SegmentFile.WriteFull(segmentFilePath, allChunks);
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
