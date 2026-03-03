using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.Buffers;

/// <summary>
/// Generates ALL chunk data for a segment at once and writes the full .seg file.
///
/// Pipeline: runs after SegmentCreator (which tags SegmentNeedsGenerate),
///           before ChunkVisibilitySystem.
///
/// Why full-segment generation matters:
///   1. Gradient clamping works across the entire 513×513 heightmap,
///      not just per-chunk 33×33 — smoother terrain at chunk borders.
///   2. .seg file is written in one operation (WriteFull) — no partial fills.
///   3. ChunkLoadSystem always finds data on disk — no fallback to generation.
///
/// Cost: ~5-10ms per segment (513×513 noise + 256 chunk packing + one file write).
/// </summary>
public class SegmentDataGenerationSystem : QuerySystem<SegmentIdentity>
{
    private NoiseGenerator _noiseGenerator;
    private EntityStore _store;

    /// <summary>Access the noise generator (for biome queries etc.).</summary>
    public NoiseGenerator NoiseGenerator => _noiseGenerator;

    private readonly ArrayPool<int>  _heightPool    = ArrayPool<int>.Shared;
    private readonly ArrayPool<byte> _zonePool      = ArrayPool<byte>.Shared;
    private readonly ArrayPool<byte> _erosionPool   = ArrayPool<byte>.Shared;

    /// <summary>Max segments to generate per frame. 1 is usually fine (~5-10ms each).</summary>
    public int MaxPerFrame { get; set; } = 1;

    public NoiseSettings NoiseSettings { get; set; }
    public float HeightScale { get; set; } = 0.25f;

    /// <summary>Sea level in tile height units. Used to set water flag on tiles.</summary>
    public int SeaLevelTile { get; set; }

    public SegmentDataGenerationSystem()
        => Filter.AllTags(Tags.Get<SegmentNeedsGenerate>());

    protected override void OnAddStore(EntityStore store)
    {
        base.OnAddStore(store);
        _store = store;

        if (NoiseSettings == null)
        {
            NoiseSettings = NoiseSettings.CreateDefault();
            GD.Print("[SegmentDataGen] Created default NoiseSettings");
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
        if (MaxPerFrame <= 0) return;

        var buffer = CommandBuffer;
        int processed = 0;

        foreach (var entity in Query.Entities)
        {
            if (processed >= MaxPerFrame) break;

            ref var identity = ref entity.GetComponent<SegmentIdentity>();
            ref var storage  = ref entity.GetComponent<SegmentStorage>();

            try
            {
                GenerateFullSegment(identity.GridPosition, storage.FilePath);

                buffer.RemoveTag<SegmentNeedsGenerate>(entity.Id);
                buffer.AddTag<SegmentDataReady>(entity.Id);

                GD.Print($"[SegmentDataGen] Generated full segment ({identity.GridPosition.X},{identity.GridPosition.Y}) → 256 chunks");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SegmentDataGen] Error at ({identity.GridPosition.X},{identity.GridPosition.Y}): {ex.Message}");
                buffer.RemoveTag<SegmentNeedsGenerate>(entity.Id);
            }

            processed++;
        }
    }

    // ────────────────────── Core generation ──────────────────────

    private void GenerateFullSegment(Vector2I segGrid, string segFilePath)
    {
        const int side       = ConstantsSegment.SIDE;        // 16 chunks per side
        const int chunkSize  = ChunkConstants.CHUNK_SIZE;    // 32 tiles per chunk
        const int tileSize   = ChunkConstants.TILE_SIZE;     // 3 world units per tile
        const int maxHeight  = ConstantsCelestial.MAX_HEIGHT;

        // Full segment heightmap: 16*32 = 512 tiles per side + 1 padding = 513
        int segTiles   = side * chunkSize;   // 512
        int hmSize     = segTiles + 1;       // 513
        int hmElements = hmSize * hmSize;    // 263,169

        // World offset of this segment's origin
        int worldOffsetX = segGrid.X * side * chunkSize * tileSize;
        int worldOffsetZ = segGrid.Y * side * chunkSize * tileSize;

        // Rent large heightmap buffers
        int[] hmArray       = _heightPool.Rent(hmElements);
        byte[] zoneArray    = _zonePool.Rent(hmElements);
        byte[] erosionArray = _erosionPool.Rent(hmElements);

        try
        {
            Span<int> heights = hmArray.AsSpan(0, hmElements);

            // 1. Generate noise for entire segment (includes gradient clamping)
            _noiseGenerator.GenerateHeightmap(
                heights,
                zoneArray.AsSpan(0, hmElements),
                erosionArray.AsSpan(0, hmElements),
                worldOffsetX,
                worldOffsetZ,
                hmSize, hmSize,
                maxHeight,
                HeightScale,
                tileSize
            );

            // 2. Process each chunk and collect terrain data
            byte[][] allChunks = new byte[ConstantsSegment.TOTAL_CHUNKS][];

            Span<TileType> tileTypes   = stackalloc TileType[chunkSize * chunkSize];
            Span<int>      baseHeights = stackalloc int[chunkSize * chunkSize];

            for (int cz = 0; cz < side; cz++)
            {
                for (int cx = 0; cx < side; cx++)
                {
                    int startX = cx * chunkSize;
                    int startY = cz * chunkSize;

                    // Map tiles from the full heightmap
                    TileAutoMapper.DetermineTileTypesBatch(
                        heights,
                        tileTypes,
                        baseHeights,
                        startX, startY, chunkSize,
                        hmSize  // source width = 513
                    );

                    // Pack into byte[] [baseHeight, tileType, surfaceId] × 32×32
                    byte[] data = new byte[ChunkConstants.CHUNK_DATA_SIZE];
                    for (int i = 0; i < chunkSize * chunkSize; i++)
                    {
                        int bh  = Math.Clamp(baseHeights[i], 0, maxHeight);
                        int idx = i * ChunkConstants.BYTES_PER_TILE;
                        // Tile (tx, tz) within chunk → heightmap index
                        int tx    = i % chunkSize;
                        int tz    = i / chunkSize;
                        int hmIdx = (startY + tz) * hmSize + (startX + tx);
                        int zone = zoneArray[hmIdx];
                        float E = erosionArray[hmIdx] / 255f;
                        int biomeIdx = BiomeRegistry.GetBiome(zone, E);
                        byte surfaceByte = (byte)SurfaceMapper.DetermineSurface(bh, tileTypes[i], biomeIdx);

                        // Water flag: from biome definition (hasWater) + below sea level
                        if (bh < SeaLevelTile && BiomeRegistry.Biomes[biomeIdx].HasWater)
                            surfaceByte |= ChunkConstants.WATER_FLAG;

                        data[idx]     = (byte)bh;
                        data[idx + 1] = (byte)tileTypes[i];
                        data[idx + 2] = surfaceByte;
                    }

                    allChunks[cz * side + cx] = data;
                }
            }

            // 3. Write entire segment in one file operation
            SegmentFile.WriteFull(segFilePath, allChunks);
        }
        finally
        {
            _heightPool.Return(hmArray);
            _zonePool.Return(zoneArray);
            _erosionPool.Return(erosionArray);
        }
    }
}
