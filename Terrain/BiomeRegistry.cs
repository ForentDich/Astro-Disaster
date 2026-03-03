using Godot;
using System;
using System.Text.Json;

/// <summary>
/// Loads biome definitions from VTerrain/Data/biomes.json.
///
/// Biome selection: Zone (from C) × Erosion (from E).
///   Zone 0 (Ocean)     + any E     → Океан
///   Zone 1 (Coast)     + any E     → Берег
///   Zone 2 (Inland)    + high E    → Равнина  (flat grasslands)
///   Zone 2 (Inland)    + low E     → Холмы   (rolling hills)
///   Zone 3 (FarInland) + high E    → Плато   (flat highlands)
///   Zone 3 (FarInland) + low E     → Горы    (mountains)
/// </summary>
public static class BiomeRegistry
{
    private const string BIOMES_PATH = "res://Terrain/Data/biomes.json";

    public struct HeightRule
    {
        public int MinHeight;
        public int MaxHeight;
        public int SurfaceIndex;
    }

    public struct BiomeDef
    {
        public string Name;
        public int Zone;
        public float ErosionMin;
        public float ErosionMax;
        public HeightRule[] HeightRules;
        /// <summary>If true, tiles below sea level in this biome get water on top.</summary>
        public bool HasWater;
    }

    public static BiomeDef[] Biomes { get; private set; } = Array.Empty<BiomeDef>();
    public static int Count => Biomes.Length;

    public static void Load()
    {
        if (!FileAccess.FileExists(BIOMES_PATH))
        {
            GD.PrintErr($"[BiomeRegistry] Missing {BIOMES_PATH}");
            return;
        }

        string json = FileAccess.Open(BIOMES_PATH, FileAccess.ModeFlags.Read).GetAsText();
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;

        Biomes = new BiomeDef[arr.GetArrayLength()];
        for (int i = 0; i < Biomes.Length; i++)
        {
            var el = arr[i];
            var rulesEl = el.GetProperty("heightRules");
            var rules = new HeightRule[rulesEl.GetArrayLength()];
            for (int j = 0; j < rules.Length; j++)
            {
                var r = rulesEl[j];
                rules[j] = new HeightRule
                {
                    MinHeight    = r.GetProperty("minHeight").GetInt32(),
                    MaxHeight    = r.GetProperty("maxHeight").GetInt32(),
                    SurfaceIndex = r.GetProperty("surfaceIndex").GetInt32()
                };
            }

            Biomes[i] = new BiomeDef
            {
                Name        = el.GetProperty("name").GetString(),
                Zone        = el.GetProperty("zone").GetInt32(),
                ErosionMin  = el.GetProperty("erosionMin").GetSingle(),
                ErosionMax  = el.GetProperty("erosionMax").GetSingle(),
                HeightRules = rules,
                HasWater    = el.TryGetProperty("hasWater", out var hw) && hw.GetBoolean()
            };
        }

        GD.Print($"[BiomeRegistry] Loaded {Biomes.Length} biomes: " +
                 string.Join(", ", Array.ConvertAll(Biomes, b => $"{b.Name}(z{b.Zone} e{b.ErosionMin:F1}..{b.ErosionMax:F1})")));
    }

    /// <summary>
    /// Returns biome index for the given zone and erosion value.
    /// Scans biomes for matching zone + erosion range. Falls back to 0.
    /// </summary>
    public static int GetBiome(int zone, float erosion)
    {
        for (int i = 0; i < Biomes.Length; i++)
        {
            ref var b = ref Biomes[i];
            if (b.Zone == zone && erosion >= b.ErosionMin && erosion < b.ErosionMax)
                return i;
        }
        return 0;
    }

    /// <summary>
    /// Overload for zone-only lookup (uses erosion=0.5 as default).
    /// </summary>
    public static int GetBiomeForZone(int zone) => GetBiome(zone, 0.5f);
}
