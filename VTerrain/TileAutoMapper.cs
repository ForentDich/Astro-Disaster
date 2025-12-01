using Godot;
using System;

public static class TileAutoMapper
{
    public static (TileType tileType, int baseHeight) DetermineTileType(int[,] heightmap, int x, int y)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);

        int nw = heightmap[x, y];
        int ne = (x < width - 1) ? heightmap[x + 1, y] : nw;
        int sw = (y < height - 1) ? heightmap[x, y + 1] : nw;
        int se = (x < width - 1 && y < height - 1) ? heightmap[x + 1, y + 1] : nw;

        int baseHeight = Math.Min(Math.Min(nw, ne), Math.Min(sw, se));

        int[] corners = {
            nw - baseHeight,
            ne - baseHeight,
            se - baseHeight,
            sw - baseHeight
        };

        int mask = (corners[0] << 6) | (corners[1] << 4) | (corners[2] << 2) | corners[3];

        TileType tileType = DetermineFromMask(mask);
        return (tileType, baseHeight);
    }

    private static TileType DetermineFromMask(int mask)
    {
        return mask switch
        {
            0b00_00_00_00 => TileType.Flat,

            0b01_01_00_00 => TileType.SlopeN,
            0b00_01_01_00 => TileType.SlopeE,
            0b00_00_01_01 => TileType.SlopeS,
            0b01_00_00_01 => TileType.SlopeW,

            0b00_01_00_00 => TileType.CornerNE,
            0b01_00_00_00 => TileType.CornerNW,
            0b00_00_01_00 => TileType.CornerSE,
            0b00_00_00_01 => TileType.CornerSW,

            0b01_01_01_00 => TileType.CornerNW_Inverted,
            0b01_01_00_01 => TileType.CornerNE_Inverted,
            0b01_00_01_01 => TileType.CornerSW_Inverted,
            0b00_01_01_01 => TileType.CornerSE_Inverted,

            0b01_00_01_00 => TileType.SaddleNWSE,
            0b00_01_00_01 => TileType.SaddleNESW,

            0b10_01_00_01 => TileType.SteepNW,
            0b01_10_01_00 => TileType.SteepNE,
            0b01_00_01_10 => TileType.SteepSW,
            0b00_01_10_01 => TileType.SteepSE,

            _ => TileType.Flat
        };
    }
}