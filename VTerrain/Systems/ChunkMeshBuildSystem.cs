using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.Buffers;

public class ChunkMeshBuildSystem : QuerySystem<ChunkInfo, ChunkTerrain>
{
    public Material TerrainMaterial { get; set; }
    public int MaxPerFrame { get; set; } = 2;
    public Node ParentNode { get; set; }
    public Node3D Viewer { get; set; }

    private EntityStore _store;

    private int[] _selectedEntityIds;
    private int[] _selectedDistances;
    private int _selectedCount;

    private readonly ArrayPool<Vector3> _vertexPool = ArrayPool<Vector3>.Shared;
    private readonly ArrayPool<Vector3> _normalPool = ArrayPool<Vector3>.Shared;

    public ChunkMeshBuildSystem() => Filter.AllTags(Tags.Get<NeedsMeshUpdate>());

    protected override void OnAddStore(EntityStore store)
    {
        base.OnAddStore(store);
        _store = store;
    }

    protected override void OnUpdate()
    {
        var buffer = CommandBuffer;

        if (MaxPerFrame <= 0)
            return;

        (int centerX, int centerZ) = NearestChunkSelectionTool.GetViewerChunkCoords(Viewer, ChunkConstants.ChunkSize);

        NearestChunkSelectionTool.EnsureCapacity(ref _selectedEntityIds, ref _selectedDistances, MaxPerFrame);
        _selectedCount = 0;

        foreach (var entity in Query.Entities)
        {
            if (entity.Tags.Has<PendingRemoval>())
                continue;
                
            ref var info = ref entity.GetComponent<ChunkInfo>();
            int dist = Math.Max(Math.Abs(info.X - centerX), Math.Abs(info.Z - centerZ));
            NearestChunkSelectionTool.TryInsertNearest(ref _selectedCount, _selectedEntityIds, _selectedDistances, entity.Id, dist, MaxPerFrame);
        }

        for (int i = 0; i < _selectedCount; i++)
        {
            int entityId = _selectedEntityIds[i];

            if (!_store.TryGetEntityById(entityId, out var entity) || entity.IsNull)
                continue;

            if (entity.Tags.Has<PendingRemoval>())
                continue;

            bool success = false;
            Exception error = null;
            
            try
            {
                ref var info = ref entity.GetComponent<ChunkInfo>();
                ref var terrain = ref entity.GetComponent<ChunkTerrain>();

                Mesh mesh = BuildMeshFromData(terrain.Data);

                if (entity.TryGetComponent<ChunkMesh>(out var chunkMesh))
                {
                    var existing = chunkMesh.GetMesh();
                    if (existing != null)
                    {
                        existing.Mesh = mesh;
                        if (TerrainMaterial != null)
                            existing.MaterialOverride = TerrainMaterial;
                        existing.Name = $"Chunk_{info.X}_{info.Z}";
                        existing.Position = new Vector3(info.X * ChunkConstants.ChunkSize, 0, info.Z * ChunkConstants.ChunkSize);
                    }
                    else
                    {
                        var meshInstance = CreateMeshInstance(mesh, info);
                        buffer.AddComponent(entityId, new ChunkMesh { InstaceId = meshInstance.GetInstanceId() });
                    }
                }
                else
                {
                    var meshInstance = CreateMeshInstance(mesh, info);
                    buffer.AddComponent(entityId, new ChunkMesh { InstaceId = meshInstance.GetInstanceId() });
                }

                success = true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[ChunkMeshBuildSystem] >> Error building mesh: {ex}");
                error = ex;
            }

            if (success)
            {
                buffer.RemoveTag<ChunkDataReady>(entityId);
                buffer.RemoveTag<NeedsMeshUpdate>(entityId);
                buffer.AddTag<ChunkComplete>(entityId);
            }
            else if (error != null)
            {
                buffer.RemoveTag<ChunkDataReady>(entityId);
                buffer.RemoveTag<NeedsMeshUpdate>(entityId);
                buffer.AddTag<ChunkError>(entityId);
            }
        }
    }

    private Mesh BuildMeshFromData(byte[] terrainData)
    {
        ReadOnlySpan<byte> dataSpan = terrainData;
        
        int size = ChunkConstants.ChunkSize;
        int tileCount = size * size;
        int totalVertices = tileCount * 6;
        
        Vector3[] verticesArray = _vertexPool.Rent(totalVertices);
        Vector3[] normalsArray = _normalPool.Rent(totalVertices);
        
        try
        {
            Span<Vector3> vertices = verticesArray.AsSpan(0, totalVertices);
            Span<Vector3> normals = normalsArray.AsSpan(0, totalVertices);
            
            int vertexIndex = 0;
            
            for (int i = 0; i < tileCount; i++)
            {
                int offset = i * 2;
                int baseHeight = dataSpan[offset];
                TileType tileType = (TileType)dataSpan[offset + 1];
                
                ReadOnlySpan<Vector3> tileVertices = TileMeshes.GetVertices(tileType).AsSpan();
                ReadOnlySpan<Vector3> tileNormals = TileMeshes.GetNormals(tileType).AsSpan();
                
                int tileX = i % size;
                int tileZ = i / size;
                Vector3 tileOffset = new Vector3(tileX, baseHeight, tileZ);
                
                for (int v = 0; v < tileVertices.Length; v++)
                {
                    vertices[vertexIndex + v] = tileVertices[v] + tileOffset;
                    normals[vertexIndex + v] = tileNormals[v];
                }
                
                vertexIndex += tileVertices.Length;
            }
            
            SurfaceTool surfaceTool = new SurfaceTool();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
            surfaceTool.SetSmoothGroup(uint.MaxValue);
            
            for (int i = 0; i < vertexIndex; i++)
            {
                surfaceTool.SetNormal(normals[i]);
                surfaceTool.AddVertex(vertices[i]);
            }
            
            surfaceTool.GenerateNormals();
            return surfaceTool.Commit();
        }
        finally
        {
            _vertexPool.Return(verticesArray);
            _normalPool.Return(normalsArray);
        }
    }

    private MeshInstance3D CreateMeshInstance(Mesh mesh, ChunkInfo chunkInfo)
    {
        int size = ChunkConstants.ChunkSize;
        var meshInstance = new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = TerrainMaterial,
            Name = $"Chunk_{chunkInfo.X}_{chunkInfo.Z}"
        };

        meshInstance.Position = new Vector3(chunkInfo.X * size, 0, chunkInfo.Z * size);

        ParentNode?.AddChild(meshInstance);
        return meshInstance;
    }
}