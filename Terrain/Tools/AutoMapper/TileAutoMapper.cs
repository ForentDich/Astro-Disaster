
using Godot;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public static class TileAutoMapper
{
    private static readonly TileType[] _maskToTileType = new TileType[256];

    static TileAutoMapper()
    {
        Array.Fill(_maskToTileType, TileType.Flat);
        _maskToTileType[0b01_01_00_00] = TileType.SlopeN;
        _maskToTileType[0b00_01_01_00] = TileType.SlopeE;
        _maskToTileType[0b00_00_01_01] = TileType.SlopeS;
        _maskToTileType[0b01_00_00_01] = TileType.SlopeW;
        _maskToTileType[0b00_01_00_00] = TileType.CornerNE;
        _maskToTileType[0b01_00_00_00] = TileType.CornerNW;
        _maskToTileType[0b00_00_01_00] = TileType.CornerSE;
        _maskToTileType[0b00_00_00_01] = TileType.CornerSW;
        _maskToTileType[0b01_01_01_00] = TileType.CornerNW_Inverted;
        _maskToTileType[0b01_01_00_01] = TileType.CornerNE_Inverted;
        _maskToTileType[0b01_00_01_01] = TileType.CornerSW_Inverted;
        _maskToTileType[0b00_01_01_01] = TileType.CornerSE_Inverted;

        _maskToTileType[0b01_00_01_00] = TileType.SaddleNWSE;
        _maskToTileType[0b00_01_00_01] = TileType.SaddleNESW;
        _maskToTileType[0b10_01_00_01] = TileType.SteepNW;
        _maskToTileType[0b01_10_01_00] = TileType.SteepNE;
        _maskToTileType[0b01_00_01_10] = TileType.SteepSW;
        _maskToTileType[0b00_01_10_01] = TileType.SteepSE;
    }

    /// <summary>
    /// Resolves tile type from 4 corner heights.
    /// If the raw delta pattern isn't registered (height difference too large),
    /// normalizes deltas proportionally to find the best matching tile shape.
    /// The tile will slope in the correct direction even across height breaks,
    /// though its actual geometry won't span the full height difference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (TileType type, int baseHeight) Classify(int nw, int ne, int se, int sw)
    {
        int baseHeight = nw;
        if (ne < baseHeight) baseHeight = ne;
        if (se < baseHeight) baseHeight = se;
        if (sw < baseHeight) baseHeight = sw;

        int dNW = nw - baseHeight;
        int dNE = ne - baseHeight;
        int dSE = se - baseHeight;
        int dSW = sw - baseHeight;

        // All corners equal → flat
        if ((dNW | dNE | dSE | dSW) == 0)
            return (TileType.Flat, baseHeight);

        int maxD = dNW;
        if (dNE > maxD) maxD = dNE;
        if (dSE > maxD) maxD = dSE;
        if (dSW > maxD) maxD = dSW;

        // Fast path: direct lookup when deltas fit in 2 bits
        if (maxD <= 2)
        {
            int mask = (dNW << 6) | (dNE << 4) | (dSE << 2) | dSW;
            var direct = _maskToTileType[mask];
            if (direct != TileType.Flat)
                return (direct, baseHeight);
        }

        // ── Pattern not registered — normalize to find best matching tile ──

        // Try scale to max=2 (can match Steep tiles for more detail)
        {
            int s2NW = (int)(dNW * 2f / maxD + 0.5f);
            int s2NE = (int)(dNE * 2f / maxD + 0.5f);
            int s2SE = (int)(dSE * 2f / maxD + 0.5f);
            int s2SW = (int)(dSW * 2f / maxD + 0.5f);
            int mask2 = (s2NW << 6) | (s2NE << 4) | (s2SE << 2) | s2SW;
            var type2 = _maskToTileType[mask2 & 0xFF];
            if (type2 != TileType.Flat)
                return (type2, baseHeight);
        }

        // Scale to max=1: binary high/low split.
        // All 14 possible {0,1} patterns (with at least one 0 and one 1) are
        // registered, so this is guaranteed to find a matching tile type.
        {
            float half = maxD * 0.5f;
            int s1NW = dNW >= half ? 1 : 0;
            int s1NE = dNE >= half ? 1 : 0;
            int s1SE = dSE >= half ? 1 : 0;
            int s1SW = dSW >= half ? 1 : 0;
            int mask1 = (s1NW << 6) | (s1NE << 4) | (s1SE << 2) | s1SW;
            return (_maskToTileType[mask1], baseHeight);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (TileType tileType, int baseHeight) DetermineTileType(int[,] heightmap, int x, int y)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);

        int nw = heightmap[x, y];
        int ne = (x < width - 1) ? heightmap[x + 1, y] : nw;
        int sw = (y < height - 1) ? heightmap[x, y + 1] : nw;
        int se = (x < width - 1 && y < height - 1) ? heightmap[x + 1, y + 1] : nw;

        return Classify(nw, ne, se, sw);
    }

    // Старый метод для совместимости
    public static void DetermineTileTypesBatch(
        int[,] heightmap,
        TileType[] outTypes,
        int[] outHeights,
        int startX, int startY, int size)
    {
        DetermineTileTypesBatch(
            MemoryMarshal.CreateSpan(ref heightmap[0, 0], heightmap.Length),
            outTypes.AsSpan(),
            outHeights.AsSpan(),
            startX, startY, size,
            heightmap.GetLength(0)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DetermineTileTypesBatch(
        Span<int> heightmap,
        Span<TileType> outTypes,
        Span<int> outHeights,
        int startX, int startY, int size,
        int sourceWidth)
    {
        if (outTypes.Length < size * size)
            throw new ArgumentException("outTypes too small");
        if (outHeights.Length < size * size)
            throw new ArgumentException("outHeights too small");

        int sourceHeight = heightmap.Length / sourceWidth;
        int outIndex = 0;

        for (int y = startY; y < startY + size; y++)
        {
            for (int x = startX; x < startX + size; x++)
            {
                int idxNW = y * sourceWidth + x;
                int idxNE = idxNW + 1;
                int idxSW = (y + 1) * sourceWidth + x;
                int idxSE = idxSW + 1;

                bool canReadNE = x + 1 < sourceWidth;
                bool canReadSW = y + 1 < sourceHeight;
                bool canReadSE = canReadNE && canReadSW;

                int nw = heightmap[idxNW];
                int ne = canReadNE ? heightmap[idxNE] : nw;
                int sw = canReadSW ? heightmap[idxSW] : nw;
                int se = canReadSE ? heightmap[idxSE] : nw;

                var (type, baseH) = Classify(nw, ne, se, sw);
                outTypes[outIndex] = type;
                outHeights[outIndex] = baseH;
                outIndex++;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DetermineTileTypesBatch(
        int[,] heightmap,
        Span<TileType> outTypes,
        Span<int> outHeights,
        int startX, int startY, int size)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        int outIndex = 0;

        for (int y = startY; y < startY + size; y++)
        {
            for (int x = startX; x < startX + size; x++)
            {
                int nw = heightmap[x, y];
                int ne = (x < width - 1) ? heightmap[x + 1, y] : nw;
                int sw = (y < height - 1) ? heightmap[x, y + 1] : nw;
                int se = (x < width - 1 && y < height - 1) ? heightmap[x + 1, y + 1] : nw;

                var (type, baseH) = Classify(nw, ne, se, sw);
                outTypes[outIndex] = type;
                outHeights[outIndex] = baseH;
                outIndex++;
            }
        }
    }
}