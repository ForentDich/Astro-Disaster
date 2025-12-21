using Godot;
using System;
using System.Runtime.CompilerServices;

public static class TileMeshes
{
    private static readonly int TileTypeCount = Enum.GetValues(typeof(TileType)).Length;
    private static readonly Vector3[][] _precomputedVertices = new Vector3[TileTypeCount][]; 
    private static readonly Vector3[][] _precomputedNormals = new Vector3[TileTypeCount][];
    private static readonly float[][] _precomputedHeights = new float[TileTypeCount][];


    static TileMeshes()
    {
        for (int i = 0; i < TileTypeCount; i++)
        {
            PrecomputeTileData((TileType)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3[] GetVertices(TileType type) => _precomputedVertices[(int)type];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3[] GetVertices(byte type) => _precomputedVertices[type % TileTypeCount];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3[] GetNormals(TileType type) => _precomputedNormals[(int)type];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3[] GetNormals(byte type) => _precomputedNormals[type % TileTypeCount];
    
    public static float[] GetHeights(TileType type) => _precomputedHeights[(int)type];

    private static void PrecomputeTileData(TileType type)
    {
        var (heights, inverted) = GetHeightsForType(type);
        var vertices = CreateVertices(heights, inverted);
        _precomputedVertices[(int)type] = vertices;
        _precomputedNormals[(int)type] = ComputeNormals(vertices);
        _precomputedHeights[(int)type] = heights;
    }
    
    private static Vector3[] ComputeNormals(Vector3[] vertices)
    {
        var normals = new Vector3[vertices.Length];
        
        for (int i = 0; i < vertices.Length; i += 3)
        {
            var v0 = vertices[i];
            var v1 = vertices[i + 1];
            var v2 = vertices[i + 2];
            
            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var normal = edge1.Cross(edge2).Normalized();
            
            normals[i] = normal;
            normals[i + 1] = normal;
            normals[i + 2] = normal;
        }
        
        return normals;
    }

    private static (float[] heights, bool inverted) GetHeightsForType(TileType type)
    {
        return type switch
        {
            TileType.Flat => ([0f, 0f, 0f, 0f], false),

            TileType.SlopeN => ([1f, 1f, 0f, 0f], false),
            TileType.SlopeE => ([0f, 1f, 1f, 0f], false),
            TileType.SlopeS => ([0f, 0f, 1f, 1f], false),
            TileType.SlopeW => ([1f, 0f, 0f, 1f], false),
            
            TileType.CornerNW => ([1f, 0f, 0f, 0f], true),
            TileType.CornerNE => ([0f, 1f, 0f, 0f], false),
            TileType.CornerSW => ([0f, 0f, 0f, 1f], false),
            TileType.CornerSE => ([0f, 0f, 1f, 0f], true),

            TileType.CornerSW_Inverted => ([1f, 0f, 1f, 1f], false),
            TileType.CornerSE_Inverted => ([0f, 1f, 1f, 1f], true),
            TileType.CornerNE_Inverted => ([1f, 1f, 0f, 1f], true),
            TileType.CornerNW_Inverted => ([1f, 1f, 1f, 0f], false),

            TileType.SteepSW => ([1f, 0f, 1f, 2f], false),
            TileType.SteepSE => ([0f, 1f, 2f, 1f], true),
            TileType.SteepNW => ([2f, 1f, 0f, 1f], true),
            TileType.SteepNE => ([1f, 2f, 1f, 0f], false),

            TileType.SaddleNESW => ([0f, 1f, 0f, 1f], false),
            TileType.SaddleNWSE => ([1f, 0f, 1f, 0f], true),

            
            _ => ([0f, 0f, 0f, 0f], false)
        };
    }

    private static Vector3[] CreateVertices(float[] heights, bool inverted = false)
    {
        return inverted ? 
        [
            new Vector3(1, heights[1], 0),
            new Vector3(0, heights[3], 1), 
            new Vector3(0, heights[0], 0),
            new Vector3(1, heights[1], 0),
            new Vector3(1, heights[2], 1),
            new Vector3(0, heights[3], 1)
        ] : 
        [
            new Vector3(0, heights[0], 0),
            new Vector3(1, heights[1], 0),
            new Vector3(1, heights[2], 1),
            new Vector3(0, heights[0], 0),
            new Vector3(1, heights[2], 1),
            new Vector3(0, heights[3], 1)
        ];
    }
}