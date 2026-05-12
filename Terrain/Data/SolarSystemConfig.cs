using Godot;
using System;
using System.Text.Json;

/// <summary>
/// Loads solar system definitions from Terrain/Data/solar_systems.json.
///
/// Usage:
///   SolarSystemConfig.Load();
///   var system = SolarSystemConfig.Systems[0]; // first system
///   var primary = system.GetPrimaryBody();
/// </summary>
public static class SolarSystemConfig
{
    private const string CONFIG_PATH = "res://Terrain/Data/solar_systems.json";

    // ── Data structures ──

    public struct StarDef
    {
        public string Name;
        public float Radius;
        public float Temperature;
        public float Luminosity;
        public Color Color;
    }

    public struct OrbitDef
    {
        public float Distance;
        public float Speed;
        public float InitialAngle;
        public float AxialTilt;
    }

    public struct SurfaceDef
    {
        public int Seed;
        public float RotationSpeed;
    }

    public struct BodyDef
    {
        public string Name;
        public string Type; // "Planet", "Moon"
        public bool IsPrimary;
        public int SegmentsPerSide;
        public float Gravity;
        public OrbitDef Orbit;
        public SurfaceDef Surface;
    }

    public struct SystemDef
    {
        public string Name;
        public StarDef Star;
        public BodyDef[] Bodies;

        /// <summary>Returns the first body with isPrimary=true, or null.</summary>
        public BodyDef? GetPrimaryBody()
        {
            for (int i = 0; i < Bodies.Length; i++)
            {
                if (Bodies[i].IsPrimary)
                    return Bodies[i];
            }
            return Bodies.Length > 0 ? Bodies[0] : null;
        }
    }

    // ── Registry ──

    public static SystemDef[] Systems { get; private set; } = Array.Empty<SystemDef>();
    public static int Count => Systems.Length;

    public static void Load()
    {
        if (!FileAccess.FileExists(CONFIG_PATH))
        {
            GD.PrintErr($"[SolarSystemConfig] Missing {CONFIG_PATH}");
            return;
        }

        string json = FileAccess.Open(CONFIG_PATH, FileAccess.ModeFlags.Read).GetAsText();
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;

        Systems = new SystemDef[arr.GetArrayLength()];
        for (int i = 0; i < Systems.Length; i++)
        {
            Systems[i] = ParseSystem(arr[i]);
        }

        GD.Print($"[SolarSystemConfig] Loaded {Systems.Length} system(s)");
        for (int i = 0; i < Systems.Length; i++)
        {
            ref var sys = ref Systems[i];
            GD.Print($"  [{i}] {sys.Name} — star: {sys.Star.Name}, bodies: {sys.Bodies.Length}");
        }
    }

    private static SystemDef ParseSystem(JsonElement el)
    {
        var starEl = el.GetProperty("star");
        var bodiesEl = el.GetProperty("bodies");

        var bodies = new BodyDef[bodiesEl.GetArrayLength()];
        for (int j = 0; j < bodies.Length; j++)
        {
            bodies[j] = ParseBody(bodiesEl[j]);
        }

        return new SystemDef
        {
            Name   = el.GetProperty("name").GetString(),
            Star   = ParseStar(starEl),
            Bodies = bodies
        };
    }

    private static StarDef ParseStar(JsonElement el)
    {
        var colorArr = el.GetProperty("color");
        return new StarDef
        {
            Name        = el.GetProperty("name").GetString(),
            Radius      = el.GetProperty("radius").GetSingle(),
            Temperature = el.GetProperty("temperature").GetSingle(),
            Luminosity  = el.GetProperty("luminosity").GetSingle(),
            Color       = new Color(
                colorArr[0].GetSingle(),
                colorArr[1].GetSingle(),
                colorArr[2].GetSingle(),
                colorArr[3].GetSingle()
            )
        };
    }

    private static BodyDef ParseBody(JsonElement el)
    {
        var orbitEl   = el.GetProperty("orbit");
        var surfaceEl = el.GetProperty("surface");

        return new BodyDef
        {
            Name            = el.GetProperty("name").GetString(),
            Type            = el.GetProperty("type").GetString(),
            IsPrimary       = el.GetProperty("isPrimary").GetBoolean(),
            SegmentsPerSide = el.GetProperty("segmentsPerSide").GetInt32(),
            Gravity         = el.GetProperty("gravity").GetSingle(),
            Orbit = new OrbitDef
            {
                Distance     = orbitEl.GetProperty("distance").GetSingle(),
                Speed        = orbitEl.GetProperty("speed").GetSingle(),
                InitialAngle = orbitEl.GetProperty("initialAngle").GetSingle(),
                AxialTilt    = orbitEl.GetProperty("axialTilt").GetSingle()
            },
            Surface = new SurfaceDef
            {
                Seed           = surfaceEl.GetProperty("seed").GetInt32(),
                RotationSpeed  = surfaceEl.GetProperty("rotationSpeed").GetSingle()
            }
        };
    }
}
