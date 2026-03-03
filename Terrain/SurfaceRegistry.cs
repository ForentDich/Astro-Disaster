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
    }

    public static SurfaceEntry[] Surfaces { get; private set; } = Array.Empty<SurfaceEntry>();
    public static int Count => Surfaces.Length;

    /// <summary>
    /// Loads surfaces.json. Call once at startup before SurfaceMapper.Initialize().
    /// </summary>
    public static void Load()
    {
        LoadSurfaces();
        GD.Print($"[SurfaceRegistry] Loaded {Surfaces.Length} surfaces");
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
                TexturePath = texFile != null ? $"{TEXTURE_DIR}{texFile}" : null
            };
        }
    }

}
