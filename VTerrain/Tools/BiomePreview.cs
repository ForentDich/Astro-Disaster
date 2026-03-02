using Godot;
using System;

/// <summary>
/// [Tool] script for real-time biome/height preview in the editor.
/// Attach to a TextureRect — it renders a 512×512 map showing biomes,
/// height, and zone boundaries without launching the game.
///
/// Usage:
///   1. Add TextureRect to scene, attach this script
///   2. Assign NoiseSettings resource
///   3. Toggle Regenerate checkbox to redraw
///   4. Tweak noise settings → toggle Regenerate again
/// </summary>
[Tool]
[GlobalClass]
public partial class BiomePreview : TextureRect
{
	private const int MAP_SIZE = 512;

	[ExportCategory("Preview")]
	/// <summary>
	/// Path to NoiseSettings .tres resource.
	/// Exported as file path because [Tool] scripts can't reliably cast
	/// C# Resource types in the editor — Godot loads them as base Resource.
	/// </summary>
	[Export(PropertyHint.File, "*.tres")]
	public string NoiseSettingsPath { get; set; } = "res://VTerrain/terr.tres";

	[Export(PropertyHint.Range, "0.0,1.0,0.01")]
	public float HeightScale { get; set; } = 1.0f;

	/// <summary>World-space offset for panning the preview.</summary>
	[Export] public Vector2I Offset { get; set; } = Vector2I.Zero;

	/// <summary>Scale factor: each pixel = Step world units. 3 = tile resolution.</summary>
	[Export(PropertyHint.Range, "1,30,1")]
	public int Step { get; set; } = 3;

	public enum PreviewMode { Biome, Height, Continentalness, Erosion, Zone }

	[Export] public PreviewMode Mode { get; set; } = PreviewMode.Biome;

	/// <summary>Toggle this to regenerate the preview.</summary>
	[Export] public bool Regenerate
	{
		get => false;
		set { if (value) Generate(); }
	}

	// ── Biome colors (matching biomes.json order) ──
	private static readonly Color[] BiomeColors = new Color[]
	{
		new Color(0.15f, 0.30f, 0.70f),  // 0: Океан     — тёмно-синий
		new Color(0.90f, 0.85f, 0.55f),  // 1: Берег     — песочный
		new Color(0.30f, 0.75f, 0.20f),  // 2: Равнина   — зелёный
		new Color(0.50f, 0.70f, 0.25f),  // 3: Холмы    — тёмно-зелёный
		new Color(0.55f, 0.50f, 0.40f),  // 4: Плато    — коричневый
		new Color(0.85f, 0.85f, 0.90f),  // 5: Горы     — снежно-белый
	};

	// ── Zone colors ──
	private static readonly Color[] ZoneColors = new Color[]
	{
		new Color(0.10f, 0.20f, 0.60f),  // Ocean
		new Color(0.80f, 0.75f, 0.45f),  // Coast
		new Color(0.35f, 0.65f, 0.25f),  // Inland
		new Color(0.60f, 0.40f, 0.30f),  // FarInland
	};

	private void Generate()
	{
		if (string.IsNullOrEmpty(NoiseSettingsPath))
		{
			GD.PrintErr("[BiomePreview] NoiseSettingsPath is empty!");
			return;
		}

		// [Tool] scripts can't cast C# Resource types — Godot loads .tres
		// as base Resource before C# assemblies are ready.
		// Workaround: load as Resource, read properties via Get().
		var res = ResourceLoader.Load(NoiseSettingsPath, "", ResourceLoader.CacheMode.Ignore);
		if (res == null)
		{
			GD.PrintErr($"[BiomePreview] Failed to load {NoiseSettingsPath}");
			return;
		}

		var settings = LoadNoiseSettingsFromResource(res);
		settings.EnsureCurves();

		// Load biomes if not loaded yet (editor context)
		if (BiomeRegistry.Count == 0)
			BiomeRegistry.Load();

		var sw = System.Diagnostics.Stopwatch.StartNew();

		var noise = new NoiseGenerator(settings);
		var image = Image.CreateEmpty(MAP_SIZE, MAP_SIZE, false, Image.Format.Rgb8);

		int maxHeight = ConstantsCelestial.MAX_HEIGHT;
		float coastLevel = settings.ContinentCurve.Sample(settings.CoastStart);

		for (int y = 0; y < MAP_SIZE; y++)
		{
			for (int x = 0; x < MAP_SIZE; x++)
			{
				float worldX = Offset.X + x * Step;
				float worldZ = Offset.Y + y * Step;

				float C = noise.GetContinentalness(worldX, worldZ);
				float E = noise.GetErosion(worldX, worldZ);

				Color color;

				switch (Mode)
				{
					case PreviewMode.Biome:
						color = GetBiomeColor(noise, C, E, worldX, worldZ, maxHeight, coastLevel);
						break;

					case PreviewMode.Height:
						float h = noise.GetNoise(worldX, worldZ) * HeightScale;
						// Sea = blue tint, land = grayscale
						if (h < coastLevel)
							color = new Color(0.1f, 0.15f, 0.3f + h * 1.5f);
						else
							color = new Color(h, h, h);
						break;

					case PreviewMode.Continentalness:
						color = new Color(C, C, C);
						break;

					case PreviewMode.Erosion:
						color = new Color(E, E, E);
						break;

					case PreviewMode.Zone:
						int zone = (int)noise.GetZone(C);
						color = ZoneColors[Math.Clamp(zone, 0, ZoneColors.Length - 1)];
						// Darken by erosion for visibility
						float eFactor = 0.6f + 0.4f * (1f - E);
						color = new Color(color.R * eFactor, color.G * eFactor, color.B * eFactor);
						break;

					default:
						color = Colors.Magenta;
						break;
				}

				image.SetPixel(x, y, color);
			}
		}

		var tex = ImageTexture.CreateFromImage(image);
		Texture = tex;

		sw.Stop();
		GD.Print($"[BiomePreview] Generated {MAP_SIZE}×{MAP_SIZE} ({Mode}) in {sw.ElapsedMilliseconds}ms, " +
				 $"offset=({Offset.X},{Offset.Y}), step={Step}");
	}

	private Color GetBiomeColor(NoiseGenerator noise, float C, float E,
								 float worldX, float worldZ,
								 int maxHeight, float coastLevel)
	{
		int zone = (int)noise.GetZone(C);
		int biomeIdx = BiomeRegistry.GetBiome(zone, E);

		// Get base biome color
		Color baseColor = biomeIdx < BiomeColors.Length
			? BiomeColors[biomeIdx]
			: Colors.Magenta;

		// Compute actual height for shading
		float h = noise.GetNoise(worldX, worldZ) * HeightScale;
		int heightInt = Mathf.RoundToInt(h * maxHeight);

		// Apply height-based shading
		float shade;
		if (zone == 0) // Ocean
		{
			// Ocean: darker = deeper
			shade = 0.4f + 0.6f * Mathf.Clamp(h / coastLevel, 0f, 1f);
		}
		else
		{
			// Land: height shading (0.5 to 1.2)
			shade = 0.5f + 0.7f * Mathf.Clamp((h - coastLevel) / (1f - coastLevel), 0f, 1f);

			// Snow overlay at high altitudes (from biome heightRules)
			if (biomeIdx < BiomeRegistry.Count)
			{
				var rules = BiomeRegistry.Biomes[biomeIdx].HeightRules;
				foreach (var rule in rules)
				{
					if (heightInt >= rule.MinHeight && heightInt <= rule.MaxHeight)
					{
						// Snow surface = index 4
						if (rule.SurfaceIndex == 4)
							baseColor = baseColor.Lerp(new Color(0.95f, 0.95f, 1.0f), 0.8f);
						// Stone surface = index 0
						else if (rule.SurfaceIndex == 0)
							baseColor = baseColor.Lerp(new Color(0.5f, 0.48f, 0.45f), 0.5f);
						break;
					}
				}
			}
		}

		return new Color(
			Mathf.Clamp(baseColor.R * shade, 0f, 1f),
			Mathf.Clamp(baseColor.G * shade, 0f, 1f),
			Mathf.Clamp(baseColor.B * shade, 0f, 1f)
		);
	}

	// ────────────────── Resource → NoiseSettings via Get() ──────────────────
	/// <summary>
	/// Reads NoiseSettings properties from a base Resource using Godot's Get().
	/// This bypasses the C# type cast issue in [Tool] mode.
	/// </summary>
	private static NoiseSettings LoadNoiseSettingsFromResource(Resource res)
	{
		// Try direct cast first (works at runtime, fails in editor)
		if (res is NoiseSettings direct)
			return direct;

		// Fallback: read properties via Godot reflection
		var s = new NoiseSettings();

		s.Seed               = GetInt(res, "Seed", s.Seed);
		s.NoiseType          = (FastNoiseLite.NoiseTypeEnum)GetInt(res, "NoiseType", (int)s.NoiseType);
		s.Frequency          = GetFloat(res, "Frequency", s.Frequency);
		s.DetailFrequency    = GetFloat(res, "DetailFrequency", s.DetailFrequency);
		s.DetailStrength     = GetFloat(res, "DetailStrength", s.DetailStrength);
		s.ErosionFrequency   = GetFloat(res, "ErosionFrequency", s.ErosionFrequency);
		s.DomainWarpAmplitude = GetFloat(res, "DomainWarpAmplitude", s.DomainWarpAmplitude);
		s.DomainWarpFrequency = GetFloat(res, "DomainWarpFrequency", s.DomainWarpFrequency);
		s.FractalType        = (FastNoiseLite.FractalTypeEnum)GetInt(res, "FractalType", (int)s.FractalType);
		s.Octaves            = GetInt(res, "Octaves", s.Octaves);
		s.Persistence        = GetFloat(res, "Persistence", s.Persistence);
		s.Lacunarity         = GetFloat(res, "Lacunarity", s.Lacunarity);
		s.CoastStart         = GetFloat(res, "CoastStart", s.CoastStart);
		s.InlandStart        = GetFloat(res, "InlandStart", s.InlandStart);
		s.FarInlandStart     = GetFloat(res, "FarInlandStart", s.FarInlandStart);

		var cc = res.Get("ContinentCurve");
		if (cc.VariantType != Variant.Type.Nil && cc.AsGodotObject() is Curve curve)
			s.ContinentCurve = curve;

		var ec = res.Get("ErosionCurve");
		if (ec.VariantType != Variant.Type.Nil && ec.AsGodotObject() is Curve erosionCurve)
			s.ErosionCurve = erosionCurve;

		return s;
	}

	private static int GetInt(Resource res, string name, int fallback)
	{
		var v = res.Get(name);
		return v.VariantType != Variant.Type.Nil ? v.AsInt32() : fallback;
	}

	private static float GetFloat(Resource res, string name, float fallback)
	{
		var v = res.Get(name);
		return v.VariantType != Variant.Type.Nil ? v.AsSingle() : fallback;
	}
}
