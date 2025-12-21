
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (TileType tileType, int baseHeight) DetermineTileType(int[,] heightmap, int x, int y)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);

        int nw = heightmap[x, y];
        int ne = (x < width - 1) ? heightmap[x + 1, y] : nw;
        int sw = (y < height - 1) ? heightmap[x, y + 1] : nw;
        int se = (x < width - 1 && y < height - 1) ? heightmap[x + 1, y + 1] : nw;

        int baseHeight = Math.Min(Math.Min(nw, ne), Math.Min(sw, se));
        
        int mask = ((nw - baseHeight) << 6) | 
                   ((ne - baseHeight) << 4) | 
                   ((se - baseHeight) << 2) | 
                   (sw - baseHeight);

        return (_maskToTileType[mask & 0xFF], baseHeight);
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
        int sourceWidth)  // Ширина исходного heightmap
    {
        // Проверка границ
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
                // Вычисляем индексы для плоского массива
                int idxNW = y * sourceWidth + x;
                int idxNE = idxNW + 1;
                int idxSW = (y + 1) * sourceWidth + x;
                int idxSE = idxSW + 1;
                
                // Проверяем границы
                bool canReadNE = x + 1 < sourceWidth;
                bool canReadSW = y + 1 < sourceHeight;
                bool canReadSE = canReadNE && canReadSW;
                
                int nw = heightmap[idxNW];
                int ne = canReadNE ? heightmap[idxNE] : nw;
                int sw = canReadSW ? heightmap[idxSW] : nw;
                int se = canReadSE ? heightmap[idxSE] : nw;

                // Находим базовую высоту
                int baseHeight = nw;
                if (ne < baseHeight) baseHeight = ne;
                if (sw < baseHeight) baseHeight = sw;
                if (se < baseHeight) baseHeight = se;
                
                // Создаем маску
                int mask = ((nw - baseHeight) << 6) | 
                           ((ne - baseHeight) << 4) | 
                           ((se - baseHeight) << 2) | 
                           (sw - baseHeight);

                // Записываем результат
                outTypes[outIndex] = _maskToTileType[mask & 0xFF];
                outHeights[outIndex] = baseHeight;
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

                int baseHeight = Math.Min(Math.Min(nw, ne), Math.Min(sw, se));
                
                int mask = ((nw - baseHeight) << 6) | 
                           ((ne - baseHeight) << 4) | 
                           ((se - baseHeight) << 2) | 
                           (sw - baseHeight);

                outTypes[outIndex] = _maskToTileType[mask & 0xFF];
                outHeights[outIndex] = baseHeight;
                outIndex++;
            }
        }
    }
}