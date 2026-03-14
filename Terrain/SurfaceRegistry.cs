using Godot;
using System;
using System.Text.Json;

/// <summary>
/// Loads surface definitions from surfaces.json.
///
/// Adding a new material:
///   1. Add a line to surfaces.json (name, tint, texture, slopeVariant)
///   2. Put texture in VTerrain/Textures/tiles/  (optional, tint works without it)
///   3. Done — no code changes!
/// </summary>
public static class SurfaceRegistry
{
    private const string SURFACES_PATH = "res://Terrain/Data/surfaces.json";
    private const string TEXTURE_DIR   = "res://Terrain/Textures/tiles/";

    public struct SurfaceEntry
    {
        public string Name;
        public Color Tint;
        public int SlopeVariant;     // -1 = self
        public float NoiseStrength;  // 0.0 = no noise tint, 1.0 = full
        public string TexturePath;   // null = tint only
        public TreeType TreeType;    // tree type for this surface
        public float TreeDensity;    // 0..1 probability per flat tile
    }

    public static SurfaceEntry[] Surfaces { get; private set; } = Array.Empty<SurfaceEntry>();
    public static int Count => Surfaces.Length;

    /// <summary>
    /// Loads surfaces.json. Call once at startup before SurfaceMapper.Initialize().
    /// </summary>
    public static void Load()
    {
        LoadSurfaces();
        BuildTreeMaps();
        GD.Print($"[SurfaceRegistry] Loaded {Surfaces.Length} surfaces");
    }

    /// <summary>Surface index → TreeType lookup (parallel to Surfaces).</summary>
    public static TreeType[] TreeTypeMap { get; private set; } = Array.Empty<TreeType>();
    /// <summary>Surface index → tree density lookup (parallel to Surfaces).</summary>
    public static float[] TreeDensityMap { get; private set; } = Array.Empty<float>();

    private static void BuildTreeMaps()
    {
        TreeTypeMap    = new TreeType[Surfaces.Length];
        TreeDensityMap = new float[Surfaces.Length];
        for (int i = 0; i < Surfaces.Length; i++)
        {
            TreeTypeMap[i]    = Surfaces[i].TreeType;
            TreeDensityMap[i] = Surfaces[i].TreeDensity;
        }
    }

    private static void LoadSurfaces()
    {
        if (!FileAccess.FileExists(SURFACES_PATH))
        {
            GD.PrintErr($"[SurfaceRegistry] Missing {SURFACES_PATH}");
            return;
        }

        string json = FileAccess.Open(SURFACES_PATH, FileAccess.ModeFlags.Read).GetAsText();
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;

        Surfaces = new SurfaceEntry[arr.GetArrayLength()];
        for (int i = 0; i < Surfaces.Length; i++)
        {
            var el = arr[i];
            var tintArr = el.GetProperty("tint");
            float r = tintArr[0].GetSingle();
            float g = tintArr[1].GetSingle();
            float b = tintArr[2].GetSingle();

            string texFile = null;
            if (el.TryGetProperty("texture", out var texProp) && texProp.ValueKind != JsonValueKind.Null)
                texFile = texProp.GetString();

            Surfaces[i] = new SurfaceEntry
            {
                Name = el.GetProperty("name").GetString(),
                Tint = new Color(r, g, b),
                SlopeVariant = el.GetProperty("slopeVariant").GetInt32(),
                NoiseStrength = el.TryGetProperty("noiseStrength", out var nsProp)
                    ? nsProp.GetSingle() : 1.0f,
                TexturePath = texFile != null ? $"{TEXTURE_DIR}{texFile}" : null,
                TreeType = el.TryGetProperty("treeType", out var ttProp)
                    ? Enum.Parse<TreeType>(ttProp.GetString(), true) : TreeType.None,
                TreeDensity = el.TryGetProperty("treeDensity", out var tdProp)
                    ? tdProp.GetSingle() : 0f,
            };
        }
    }

}
