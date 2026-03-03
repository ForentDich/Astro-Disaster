using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;

/// <summary>
/// Tries to load chunk data from .seg files on disk.
///
/// Pipeline: runs BEFORE ChunkDataGenerationSystem.
///
///   ChunkNeedsLoad → [try read .seg] → ChunkDataReady (hit) 
///                                     → ChunkPending   (miss → generation)
/// </summary>
public class ChunkLoadSystem : QuerySystem<ChunkInfo>
{
    private EntityStore _store;

    private int[] _selectedEntityIds;
    private int[] _selectedDistances;
    private int _selectedCount;

    /// <summary>Query for chunks that already have their mesh built.</summary>
    private ArchetypeQuery<ChunkInfo> _completedQuery;

    public int MaxPerFrame { get; set; } = 8;
    public Node3D Viewer { get; set; }

    /// <summary>Set by GameSession from SystemSegmentCreator.FaceStoragePath</summary>
    public SystemSegmentCreator SegmentCreator { get; set; }

    public ChunkLoadSystem() => Filter.AllTags(Tags.Get<ChunkNeedsLoad>());

    protected override void OnAddStore(EntityStore store)
    {
        base.OnAddStore(store);
        _store = store;
        _completedQuery = store.Query<ChunkInfo>()
            .AllTags(Tags.Get<ChunkComplete>())
            .WithoutAnyTags(Tags.Get<PendingRemoval>());
    }

    protected override void OnUpdate()
    {
        if (Viewer == null || SegmentCreator == null) return;

        string facePath = SegmentCreator.FaceStoragePath;
        if (string.IsNullOrEmpty(facePath)) return;

        var commandBuffer = CommandBuffer;

        (int centerX, int centerZ) = NearestChunkSelectionTool.GetViewerChunkCoords(
            Viewer, ChunkConstants.CHUNK_WORLD_SIZE);

        NearestChunkSelectionTool.EnsureCapacity(
            ref _selectedEntityIds, ref _selectedDistances, MaxPerFrame);
        _selectedCount = 0;

        foreach (var entity in Query.Entities)
        {
            ref var info = ref entity.GetComponent<ChunkInfo>();
            int dist = Math.Max(Math.Abs(info.X - centerX), Math.Abs(info.Z - centerZ));
            NearestChunkSelectionTool.TryInsertNearest(
                ref _selectedCount, _selectedEntityIds, _selectedDistances,
                entity.Id, dist, MaxPerFrame);
        }

        for (int i = 0; i < _selectedCount; i++)
        {
            int entityId = _selectedEntityIds[i];
            if (!_store.TryGetEntityById(entityId, out var entity) || entity.IsNull)
                continue;

            ref var info = ref entity.GetComponent<ChunkInfo>();

            try
            {
                string segPath = SegmentFile.GetSegmentFilePath(facePath, info.X, info.Z);
                var (localX, localZ) = SegmentFile.ChunkToLocal(info.X, info.Z);

                byte[] data = SegmentFile.ReadChunk(segPath, localX, localZ);

                commandBuffer.RemoveTag<ChunkNeedsLoad>(entityId);

                if (data != null)
                {
                    commandBuffer.AddComponent(entityId, new ChunkTerrain { Data = data });
                    commandBuffer.AddTag<ChunkDataReady>(entityId);
                    commandBuffer.AddTag<NeedsMeshUpdate>(entityId);

                    // Mark left / top neighbors for mesh rebuild
                    MarkNeighborsForRebuild(ref info, commandBuffer);
                }
                else
                {
                    // Cache miss — forward to generation pipeline
                    commandBuffer.AddTag<ChunkPending>(entityId);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[ChunkLoadSystem] Error loading chunk ({info.X},{info.Z}): {ex.Message}");
                commandBuffer.RemoveTag<ChunkNeedsLoad>(entityId);
                commandBuffer.AddTag<ChunkPending>(entityId);
            }
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
