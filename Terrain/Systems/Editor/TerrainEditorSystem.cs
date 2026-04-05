using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.Collections.Generic;

public class TerrainEditorSystem : QuerySystem<TerrainEditRequest>
{
    private ArchetypeQuery<ChunkInfo, ChunkTerrain> _chunks;

    protected override void OnAddStore(EntityStore store)
    {
        base.OnAddStore(store);
        _chunks = store.Query<ChunkInfo, ChunkTerrain>();
    }

    protected override void OnUpdate()
    {
        if (Query.Entities.Count == 0) return;

        // Кэш для поиска чанков по координатам
        var chunkMap = new Dictionary<Vector2I, Entity>();
        foreach (var entity in _chunks.Entities)
        {
            var info = entity.GetComponent<ChunkInfo>();
            chunkMap[new Vector2I(info.X, info.Z)] = entity;
        }

        var buffer = CommandBuffer;

        foreach (var entity in Query.Entities)
        {
            var req = entity.GetComponent<TerrainEditRequest>();

            // Точка (PointX, PointZ) является общим углом для 4 тайлов
            // [0]: СЗ тайл (Точка - ЮВ угол)
            // [1]: СВ тайл (Точка - ЮЗ угол)
            // [2]: ЮВ тайл (Точка - СЗ угол)
            // [3]: ЮЗ тайл (Точка - СВ угол)
            
            var affectedTiles = new (int tx, int tz, int cornerIdx)[]
            {
                (req.PointX - 1, req.PointZ - 1, 2), // SE
                (req.PointX,     req.PointZ - 1, 3), // SW
                (req.PointX,     req.PointZ,     0), // NW
                (req.PointX - 1, req.PointZ,     1)  // NE
            };

            HashSet<int> updatedChunkIds = new HashSet<int>();

            foreach (var t in affectedTiles)
            {
                int chunkSize = ChunkConstants.CHUNK_SIZE;
                int cx = Mathf.FloorToInt((float)t.tx / chunkSize);
                int cz = Mathf.FloorToInt((float)t.tz / chunkSize);
                
                int lx = t.tx - cx * chunkSize;
                int lz = t.tz - cz * chunkSize;

                if (lx >= 0 && lx < chunkSize && lz >= 0 && lz < chunkSize)
                {
                    if (chunkMap.TryGetValue(new Vector2I(cx, cz), out var chunkEntity))
                    {
                        if (ModifyTile(chunkEntity, lx, lz, t.cornerIdx, req.PointY, req.DeltaHeight))
                        {
                            updatedChunkIds.Add(chunkEntity.Id);
                        }
                    }
                }
            }

            foreach (int id in updatedChunkIds)
            {
                // Помечаем чанк на перестройку меша
                buffer.AddTag<NeedsMeshUpdate>(id);
                buffer.RemoveTag<ChunkComplete>(id);

                // Если у чанка уже есть коллизия, нам нужно удалить старый Body из Godot
                // и удалить компонент, чтобы система коллизий собрала его заново
                if (_chunks.Store.TryGetEntityById(id, out var chunkEntity) && 
                    chunkEntity.TryGetComponent<ChunkCollider>(out var collider))
                {
                    collider.GetBody()?.QueueFree();
                    buffer.RemoveComponent<ChunkCollider>(id);
                    
                    // Удостоверимся, что тег NeedsCollision висит на сущности
                    buffer.AddTag<NeedsCollision>(id);
                }
            }

            entity.DeleteEntity();
        }
    }

    private bool ModifyTile(Entity chunk, int lx, int lz, int cornerIdx, int targetY, int delta)
    {
        ref var terrain = ref chunk.GetComponent<ChunkTerrain>();
        int stride = ChunkConstants.BYTES_PER_TILE;
        int idx = (lz * ChunkConstants.CHUNK_SIZE + lx) * stride;

        byte baseH = terrain.Data[idx];
        TileType tileType = (TileType)terrain.Data[idx + 1];

        // Получаем абсолютные высоты 4-х углов тайла
        var heights = TileMeshes.GetHeights(tileType);
        int[] h = new int[4]; // 0: NW, 1: NE, 2: SE, 3: SW
        for (int i = 0; i < 4; i++) h[i] = baseH + Mathf.RoundToInt(heights[i]);

        // Фильтр: игнорируем нижние тайлы (под обрывом), если клик был по вершине
        if (h[cornerIdx] != targetY) return false;

        // Изменяем нужный угол
        h[cornerIdx] += delta;

        // Позволяем TileAutoMapper самому определить базовую высоту и новый тип тайла
        var (newType, newBaseH) = TileAutoMapper.Classify(h[0], h[1], h[2], h[3]);
        
        // Ограничиваем, чтобы не уйти ниже 0 и не превысить лимит байта
        if (newBaseH < 0 || newBaseH > 250) return false;

        // Записываем обратно
        terrain.Data[idx] = (byte)newBaseH;
        terrain.Data[idx + 1] = (byte)newType;
        
        return true;
    }

    // Helper: Вы можете вызвать этот метод из Gameplay (например, по клику мыши)
    public static void RequestEdit(int pointX, int pointY, int pointZ, int deltaHeight)
    {
        if (GameSession.Instance == null) return;

        GameSession.Instance.Store.CreateEntity(new TerrainEditRequest
        {
            PointX = pointX,
            PointY = pointY, // Сохраняем высоту
            PointZ = pointZ,
            DeltaHeight = deltaHeight
        });
    }
}
