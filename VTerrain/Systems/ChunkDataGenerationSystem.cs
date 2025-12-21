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

    private readonly ArrayPool<int> _heightPool = ArrayPool<int>.Shared;

    [Export] public int MaxPerFrame { get; set; } = 4;
    [Export] public Node3D Viewer { get; set; }
    
    [Export] public NoiseSettings NoiseSettings { get; set; }
    
    [ExportCategory("Height Settings")]
    [Export(PropertyHint.Range, "0.0,1.0")] 
    public float HeightScale { get; set; } = 0.25f; // Используем 25% от MaxHeight

    public ChunkDataGenerationSystem() => Filter.AllTags(Tags.Get<ChunkPending>());

    protected override void OnAddStore(EntityStore store)
    {
        base.OnAddStore(store);
        _store = store;
        
        if (NoiseSettings == null)
        {
            NoiseSettings = NoiseSettings.CreateDefault();
            GD.Print("[ChunkDataGenerationSystem] Created default NoiseSettings");
        }
        
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

        (int centerX, int centerZ) = NearestChunkSelectionTool.GetViewerChunkCoords(Viewer, ChunkConstants.ChunkSize);

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
        int size = ChunkConstants.ChunkSize;
        int maxHeight = ChunkConstants.MaxHeight;
        int paddedSize = size + 1;
        int totalElements = paddedSize * paddedSize;

        int[] tempHeightArray = _heightPool.Rent(totalElements);
        
        try
        {
            Span<int> heights = tempHeightArray.AsSpan(0, totalElements);
            GenerateHeightmap(ref info, heights, size, maxHeight);
            byte[] terrainData = ProcessTerrainData(heights, size, maxHeight);
             
            return terrainData;
        }
        finally
        {
            _heightPool.Return(tempHeightArray);
        }
    }

    private void GenerateHeightmap(ref ChunkInfo info, Span<int> heights, int size, int maxHeight)
    {
        int paddedSize = size + 1;
        int worldOffsetX = info.X * size;
        int worldOffsetZ = info.Z * size;

        _noiseGenerator.GenerateHeightmap(
            heights,
            worldOffsetX, 
            worldOffsetZ,
            paddedSize, 
            paddedSize,
            maxHeight,
            HeightScale
        );
    }

    private byte[] ProcessTerrainData(Span<int> flatHeights, int size, int maxHeight)
    {
        int paddedSize = size + 1; 
        
        Span<TileType> tileTypes = stackalloc TileType[size * size];
        Span<int> baseHeights = stackalloc int[size * size];

        TileAutoMapper.DetermineTileTypesBatch(
            flatHeights,
            tileTypes,
            baseHeights,
            0, 0, size,
            paddedSize 
        );

        byte[] data = new byte[size * size * 2];
        Span<byte> dataSpan = data;

        for (int i = 0; i < tileTypes.Length; i++)
        {
            int dataIndex = i * 2;
            dataSpan[dataIndex] = (byte)Math.Clamp(baseHeights[i], 0, maxHeight);
            dataSpan[dataIndex + 1] = (byte)tileTypes[i];
        }

        return data;
    }
}