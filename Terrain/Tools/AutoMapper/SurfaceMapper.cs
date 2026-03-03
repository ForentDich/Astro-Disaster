using Godot;

/// <summary>
/// Determines surface material for tiles using a precomputed height lookup table.
/// One table per biome (fast O(1) lookup). All tile types (flat, slope, steep)
/// use the same rules — surface is determined by height only.
/// Call Initialize() after both SurfaceRegistry.Load() and BiomeRegistry.Load().
/// </summary>
public static class SurfaceMapper
{
    // [biomeIndex][height 0..255]
    private static byte[][] _lookup;
    private static bool _initialized;

    public static void Initialize()
    {
        int numBiomes = BiomeRegistry.Count;
        if (numBiomes == 0)
        {
            GD.PrintErr("[SurfaceMapper] No biomes loaded!");
            return;
        }

        _lookup = new byte[numBiomes][];
        for (int b = 0; b < numBiomes; b++)
        {
            _lookup[b] = new byte[256];
            var rules = BiomeRegistry.Biomes[b].HeightRules;
            for (int h = 0; h < 256; h++)
                _lookup[b][h] = FindSurface(rules, h);
        }

        _initialized = true;
        GD.Print($"[SurfaceMapper] Initialized with {numBiomes} biome(s)");
    }

    private static byte FindSurface(BiomeRegistry.HeightRule[] rules, int height)
    {
        foreach (var rule in rules)
        {
            if (height >= rule.MinHeight && height <= rule.MaxHeight)
                return (byte)rule.SurfaceIndex;
        }
        return 0;
    }

    /// <summary>
    /// Returns surface index for a tile. Fast O(1) array lookup by height.
    /// </summary>
    public static byte DetermineSurface(int baseHeight, TileType tileType, int biomeIndex = 0)
    {
        if (!_initialized) return 0;

        int h = System.Math.Clamp(baseHeight, 0, 255);
        int b = System.Math.Clamp(biomeIndex, 0, _lookup.Length - 1);
        return _lookup[b][h];
    }
}

