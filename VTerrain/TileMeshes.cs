using Godot;
using System;

public static class TileMeshes
{
    private static readonly int TileTypeCount = Enum.GetValues(typeof(TileType)).Length;
    private static readonly Vector3[][] _precomputedVertices = new Vector3[TileTypeCount][]; 
    private static readonly float[][] _precomputedHeights = new float[TileTypeCount][];


    static TileMeshes()
    {
        for (int i = 0; i < TileTypeCount; i++)
        {
            PrecomputeTileData((TileType)i);
        }
    }

    public static Vector3[] GetVertices(TileType type) => _precomputedVertices[(int)type];
    public static float[] GetHeights(TileType type) => _precomputedHeights[(int)type];

    private static void PrecomputeTileData(TileType type)
    {
        var (heights, inverted) = GetHeightsForType(type);
        _precomputedVertices[(int)type] = CreateVertices(heights, inverted);
        _precomputedHeights[(int)type] = heights;
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