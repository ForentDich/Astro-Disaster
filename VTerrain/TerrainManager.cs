using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class TerrainManager : Node3D
{
    [Export] public Node3D Viewer;
    [Export] public int RenderDistance = 4;
    [Export] public float WorldHeightScale = 1.0f;
    
    [Export] public int Lod1Distance = 2;
    [Export] public int Lod2Distance = 4;
    [Export] public int Lod4Distance = 6;

    [ExportGroup("Perlin Noise Settings")]
    [Export] public float NoiseScale = 0.2f;
    [Export] public int Octaves = 4;
    [Export] public float Persistence = 0.5f;
    [Export] public float Lacunarity = 2.0f;
    [Export] public Vector2 NoiseOffset = Vector2.Zero;

    [ExportGroup("Height Settings")]
    [Export] public int MaxHeight = 10;

    [Export]
    public bool Regenerate
    {
        get => false;
        set { if (value) RegenerateWorld(); }
    }

    private readonly Dictionary<Vector2I, TerrainChunk> _chunks = new();
    private readonly Dictionary<Vector2I, MeshInstance3D> _chunkMeshes = new();
    private readonly Dictionary<Vector2I, int> _chunkLODs = new();
    private readonly HashSet<Vector2I> _chunksInRange = new();
    private readonly List<Vector2I> _chunksToRemove = new();
    private readonly FastNoiseLite _sharedNoise = new FastNoiseLite();
    private Vector2I _lastViewerChunkCoord = new Vector2I(int.MaxValue, int.MaxValue);

    public override void _Ready()
    {
        RegenerateWorld();
    }

    public override void _Process(double delta)
    {
        if (Viewer == null) return;
        UpdateLODs();
    }

    public void RegenerateWorld()
    {
        ClearWorld();
        _lastViewerChunkCoord = new Vector2I(int.MaxValue, int.MaxValue);
        if (Viewer != null) UpdateLODs();
    }

    private void UpdateLODs()
    {
        var viewerChunkCoord = GetViewerChunkCoord();
        if (viewerChunkCoord == _lastViewerChunkCoord) return;
        _lastViewerChunkCoord = viewerChunkCoord;

        _chunksInRange.Clear();

        for (int x = -RenderDistance; x <= RenderDistance; x++)
        {
            for (int y = -RenderDistance; y <= RenderDistance; y++)
            {
                var offset = new Vector2I(x, y);
                Vector2I coord = viewerChunkCoord + offset;
                _chunksInRange.Add(coord);

                int distance = Mathf.Max(Mathf.Abs(offset.X), Mathf.Abs(offset.Y));
                int lod = DetermineLod(distance);

                EnsureChunkData(coord);
                EnsureChunkMesh(coord, lod);
            }
        }

        _chunksToRemove.Clear();
        foreach (var coord in _chunks.Keys)
        {
            if (!_chunksInRange.Contains(coord))
            {
                _chunksToRemove.Add(coord);
            }
        }

        foreach (var coord in _chunksToRemove)
        {
            RemoveChunk(coord);
        }
    }

    private void ClearWorld()
    {
        _chunksToRemove.Clear();
        foreach (var coord in _chunks.Keys)
        {
            _chunksToRemove.Add(coord);
        }

        foreach (var coord in _chunksToRemove)
        {
            RemoveChunk(coord);
        }

        _chunksInRange.Clear();
    }

    private TerrainChunk CreateChunk(Vector2I coord)
    {
        int padding = 1;
        int genSize = TerrainChunk.ChunkSize + (padding * 2);
        
        Vector2 chunkOffset = NoiseOffset + (new Vector2(coord.X, coord.Y) * TerrainChunk.ChunkSize) - new Vector2(padding, padding);
        int[,] fullHeightmap = HeightMapGenerator.Generate(
            genSize, genSize, MaxHeight,
            NoiseScale, Octaves, Persistence, Lacunarity,
            chunkOffset, _sharedNoise);

        var chunk = new TerrainChunk();
        
        for (int x = 0; x < TerrainChunk.ChunkSize; x++)
        {
            for (int y = 0; y < TerrainChunk.ChunkSize; y++)
            {
                int hx = x + padding;
                int hy = y + padding;
                
                var (tileType, baseHeight) = TileAutoMapper.DetermineTileType(fullHeightmap, hx, hy);
                chunk.SetTile(x, y, tileType, baseHeight);
            }
        }

        return chunk;
    }

    private void BuildChunkMesh(Vector2I coord, TerrainChunk chunk, int lodLevel)
    {
        if (_chunkMeshes.ContainsKey(coord))
        {
            _chunkMeshes[coord].QueueFree();
            _chunkMeshes.Remove(coord);
        }

        int size = TerrainChunk.ChunkSize / lodLevel;
        var tiles = new TileType[size, size];
        var heights = new int[size, size];

        if (lodLevel == 1)
        {
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    var (t, h) = chunk.GetTile(x, y);
                    tiles[x, y] = t;
                    heights[x, y] = h;
                }
            }
        }
        else
        {
            int paddedSize = size + 1;
            var paddedHeights = new int[paddedSize, paddedSize];

            for (int x = 0; x < paddedSize; x++)
            {
                for (int y = 0; y < paddedSize; y++)
                {
                    int globalX = (coord.X * TerrainChunk.ChunkSize) + (x * lodLevel);
                    int globalY = (coord.Y * TerrainChunk.ChunkSize) + (y * lodLevel);

                    int rawHeight = HeightMapGenerator.SampleHeight(
                        globalX,
                        globalY,
                        NoiseScale,
                        Octaves,
                        Persistence,
                        Lacunarity,
                        NoiseOffset,
                        MaxHeight,
                        _sharedNoise
                    );
                    paddedHeights[x, y] = rawHeight / lodLevel;
                }
            }

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    var (t, h) = TileAutoMapper.DetermineTileType(paddedHeights, x, y);
                    tiles[x, y] = t;
                    heights[x, y] = h;
                }
            }
        }

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        surfaceTool.SetSmoothGroup(uint.MaxValue);

        float lodHeightScale = WorldHeightScale * lodLevel;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                var vertices = TileMeshes.GetVertices(tiles[x, y]);
                var offset = new Vector3(x, heights[x, y] * lodHeightScale, y);

                foreach (var vertex in vertices)
                {
                    surfaceTool.AddVertex(vertex * new Vector3(1, lodHeightScale, 1) + offset);
                }
            }
        }

        WallAutoMapper.GenerateWalls(surfaceTool, tiles, heights, lodHeightScale);

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        var meshInstance = new MeshInstance3D();
        meshInstance.Mesh = mesh;
        meshInstance.Position = new Vector3(coord.X * TerrainChunk.ChunkSize, 0, coord.Y * TerrainChunk.ChunkSize);
        meshInstance.Scale = new Vector3(lodLevel, 1, lodLevel);
        
        if (lodLevel == 1)
        {
            meshInstance.CreateTrimeshCollision();
        }

        AddChild(meshInstance);
        _chunkMeshes[coord] = meshInstance;
    }

    private Vector2I GetViewerChunkCoord()
    {
        Vector3 viewerPos = Viewer.GlobalPosition;
        return new Vector2I(
            Mathf.FloorToInt(viewerPos.X / TerrainChunk.ChunkSize),
            Mathf.FloorToInt(viewerPos.Z / TerrainChunk.ChunkSize)
        );
    }

    private int DetermineLod(int distance)
    {
        if (distance <= Lod1Distance) return 1;
        if (distance <= Lod2Distance) return 2;
        if (distance <= Lod4Distance) return 4;
        return 8;
    }

    private void EnsureChunkData(Vector2I coord)
    {
        if (_chunks.ContainsKey(coord)) return;
        _chunks[coord] = CreateChunk(coord);
    }

    private void EnsureChunkMesh(Vector2I coord, int lod)
    {
        if (_chunkLODs.TryGetValue(coord, out int currentLod) && currentLod == lod) return;
        BuildChunkMesh(coord, _chunks[coord], lod);
        _chunkLODs[coord] = lod;
    }

    private void RemoveChunk(Vector2I coord)
    {
        if (_chunkMeshes.TryGetValue(coord, out var mesh))
        {
            mesh.QueueFree();
            _chunkMeshes.Remove(coord);
        }

        _chunks.Remove(coord);
        _chunkLODs.Remove(coord);
    }
}
