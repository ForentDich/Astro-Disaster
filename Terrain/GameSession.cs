using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

public partial class GameSession : Node
{
	[ExportGroup("View")]
	[Export] public Node3D Viewer { get; set; }
	[Export] public int RenderDistance { get; set; } = 5;
	[Export] public int CollisionDistance { get; set; } = 1;
	[Export] public float SphericalLoadRadius { get; set; } = 0f;

	[ExportGroup("Generation")]
	[Export] public NoiseSettings NoiseSettings { get; set; }

	[Export(PropertyHint.Range, "0.0,1.0")]
	public float HeightScale { get; set; } = 0.3f;

	[ExportGroup("Performance")]
	[Export] public int MaxCreatePerFrame { get; set; } = 4;
	[Export] public int MaxDataGenPerFrame { get; set; } = 4;
	[Export] public int MaxMeshBuildPerFrame { get; set; } = 2;
	[Export] public int MaxCollisionBuildPerFrame { get; set; } = 2;
	[Export] public int MaxRemovalPerFrame { get; set; } = 8;
	[Export] public bool AutoAdjustBudgets { get; set; } = true;

	[ExportGroup("Rendering")]
	[Export] public Material TerrainMaterial { get; set; }
	[Export] public string TexturePackDirectory { get; set; } = "";
	[Export] public Material WaterMaterial { get; set; }

	[ExportGroup("Sun")]
	[Export] public DirectionalLight3D SunLight { get; set; }
	[Export] public WorldEnvironment WorldEnvironment { get; set; }


	[ExportGroup("World")]
	[Export] public string WorldName { get; set; } = "MyWorld";
	[Export] public int WorldSeed { get; set; } = 12345;

	public static GameSession Instance { get; private set; }
	public EntityStore Store => _store;

	private EntityStore _store;
	private SystemRoot _systems;
	private SystemSegmentCreator _segmentCreator;
	private NoiseGenerator _noiseGenerator;
	private ChunkDataGenerationSystem _chunkDataGen;
	private ChunkMeshBuildSystem _meshBuildSystem;
	private ChunkCollisionBuildSystem _collisionBuildSystem;
	private PlanetProxySystem _planetProxySystem;

	private ChunkVisibilitySystem _visibilitySystem;
	private Label _biomeLabel;
	private bool _debugVisible;
	private int _tick;


	private void SetupNoiseSettings()
	{
		if (NoiseSettings == null)
		{
			NoiseSettings = new NoiseSettings();
			GD.Print("[TerrainWorld] Created default NoiseSettings");
		}

		_noiseGenerator = new NoiseGenerator(NoiseSettings);
	}

	private void SetupTerrain()
	{
		SurfaceRegistry.TextureDirectoryOverride = string.IsNullOrWhiteSpace(TexturePackDirectory)
			? null
			: TexturePackDirectory;

		SurfaceRegistry.Load();
		BiomeRegistry.Load();
		SolarSystemConfig.Load();
	}

	private void SetupTerrainMaterial()

	{
		if (TerrainMaterial is ShaderMaterial shaderMaterial)
		{
			TerrainTextureLoader.Apply(shaderMaterial);
			return;
		}

		if (TerrainMaterial != null)
		{
			GD.Print("[TerrainWorld] TerrainMaterial is not a ShaderMaterial. Keeping current material.");
			return;
		}

		Shader shader = GD.Load<Shader>("res://Terrain/Shaders/terrain_tiles.gdshader");
		if (shader == null)
		{
			GD.PrintErr("[TerrainWorld] Missing terrain shader: res://Terrain/Shaders/terrain_tiles.gdshader");
			return;
		}

		ShaderMaterial autoMaterial = new ShaderMaterial { Shader = shader };
		TerrainTextureLoader.Apply(autoMaterial);
		TerrainMaterial = autoMaterial;
		GD.Print("[TerrainWorld] Auto-created terrain material with texture array.");
	}

	private int ComputeSeaLevelHeight()
	{
		NoiseSettings.EnsureCurves();
		float coast = NoiseSettings.ContinentCurve.Sample(NoiseSettings.CoastStart);
		return Mathf.RoundToInt(coast * HeightScale * ConstantsCelestial.MAX_HEIGHT);
	}

	public override void _Ready()
	{
		Instance = this;

		SetupNoiseSettings();
		SetupTerrain();
		SetupTerrainMaterial();


		_store = new EntityStore();

		var worldCreator = new SystemWorldCreator
		{
			WorldName = WorldName,
			WorldSeed = WorldSeed,
			CreateOnStart = true
		};

		var starCreator = new SystemStarCreator();

		var celestialCreator = new SystemCelestialCreator();

		var worldPlanetSync = new SystemWorldPlanetSync();

		_segmentCreator = new SystemSegmentCreator
		{
			Viewer = Viewer,
			LoadRadius = ConstantsSegment.LOAD_RADIUS,
			UnloadRadius = ConstantsSegment.UNLOAD_RADIUS
		};

		int seaLevelHeight = ComputeSeaLevelHeight();

		_visibilitySystem = new ChunkVisibilitySystem
		{
			Viewer = Viewer,
			RenderDistance = RenderDistance,
			CollisionDistance = CollisionDistance,
			MaxPerFrame = MaxCreatePerFrame,
			SegmentCreator = _segmentCreator,
			SphericalLoadRadius = SphericalLoadRadius
		};

		var removalSystem = new ChunkRemovalSystem
		{
			MaxPerFrame = MaxRemovalPerFrame
		};

		_chunkDataGen = new ChunkDataGenerationSystem
		{
			Viewer = Viewer,
			MaxPerFrame = MaxDataGenPerFrame,
			NoiseSettings = NoiseSettings,
			HeightScale = HeightScale,
			SegmentCreator = _segmentCreator,
			SeaLevelHeight = seaLevelHeight
		};

		_meshBuildSystem = new ChunkMeshBuildSystem
		{
			Viewer = Viewer,
			MaxPerFrame = MaxMeshBuildPerFrame,
			TerrainMaterial = TerrainMaterial,
			ParentNode = this,
			SegmentCreator = _segmentCreator
		};

		_collisionBuildSystem = new ChunkCollisionBuildSystem
		{
			Viewer = Viewer,
			MaxPerFrame = MaxCollisionBuildPerFrame,
			ParentNode = this,
			SegmentCreator = _segmentCreator
		};

		_planetProxySystem = new PlanetProxySystem
		{
			Viewer = Viewer,
			ParentNode = this,
			NoiseSettings = NoiseSettings,
			HeightScale = HeightScale,
			TerrainMaterial = TerrainMaterial,
			LoadRadius = SphericalLoadRadius > 0f
				? SphericalLoadRadius
				: RenderDistance * ChunkConstants.CHUNK_WORLD_SIZE
		};

		var sunDirectionSystem = new SunDirectionSystem
		{
			SunLight = SunLight,
			Viewer = Viewer,
			WorldEnvironment = WorldEnvironment
		};

		_systems = new SystemRoot(_store)
		{
			worldCreator,
			starCreator,
			celestialCreator,
			worldPlanetSync,
			_segmentCreator,
			_planetProxySystem,
			_visibilitySystem,
			removalSystem,
			_chunkDataGen,
			_meshBuildSystem,
			_collisionBuildSystem,
			sunDirectionSystem,
			
		};
	}

	public override void _Process(double delta)
	{
		_systems.Update(new UpdateTick((float)delta, _tick++));

		UpdateDebugLabel();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F3)
		{
			_debugVisible = !_debugVisible;
			EnsureDebugLabel();
			_biomeLabel.Visible = _debugVisible;
			GetViewport().SetInputAsHandled();
		}
	}

	private void EnsureDebugLabel()
	{
		if (_biomeLabel != null) return;

		var layer = new CanvasLayer { Layer = 100 };
		AddChild(layer);

		_biomeLabel = new Label();
		_biomeLabel.Position = new Vector2(12, 12);
		_biomeLabel.AddThemeFontSizeOverride("font_size", 16);
		_biomeLabel.AddThemeColorOverride("font_color", Colors.White);
		_biomeLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
		_biomeLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		_biomeLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		_biomeLabel.Visible = false;
		layer.AddChild(_biomeLabel);
	}

	private void UpdateDebugLabel()
	{
		if (!_debugVisible || _biomeLabel == null || Viewer == null) return;

		var pos = Viewer.GlobalPosition;
		var noiseGen = _noiseGenerator;

		string zoneName = "N/A";
		string cValue = "-";
		string eValue = "-";
		string biomeName = "N/A";

		if (noiseGen != null)
		{
			// Use 3D noise sampled at the viewer's position on the sphere
			// Normalize to unit sphere for consistent noise sampling
			float len = pos.Length();
			float nx = len > 0.001f ? pos.X / len : 0f;
			float ny = len > 0.001f ? pos.Y / len : 0f;
			float nz = len > 0.001f ? pos.Z / len : 0f;

			float c = noiseGen.GetContinentalness3D(nx, ny, nz);
			float e = noiseGen.GetErosion3D(nx, ny, nz);
			var zone = noiseGen.GetZone(c);

			cValue = c.ToString("F3");
			eValue = e.ToString("F3");
			zoneName = zone switch
			{
				ContinentalZone.Ocean => "Океан",
				ContinentalZone.Coast => "Берег",
				ContinentalZone.Inland => "Суша",
				ContinentalZone.FarInland => "Глубина континента",
				ContinentalZone.River => "Река",
				_ => zone.ToString()
			};

			int biomeIdx = BiomeRegistry.GetBiome((int)zone, e);
			if (biomeIdx >= 0 && biomeIdx < BiomeRegistry.Count)
				biomeName = BiomeRegistry.Biomes[biomeIdx].Name;
		}

		_biomeLabel.Text =
			"Mode: 3D Noise (Spherical)\n" +
			$"Зона: {zoneName} (C={cValue})\n" +
			$"Эрозия: {eValue}\n" +
			$"Биом: {biomeName}\n" +
			$"Coords: {pos.X:F0}, {pos.Y:F0}, {pos.Z:F0}";
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;

		if (_store == null) return;

		foreach (var entity in _store.Entities)
		{
			if (entity.TryGetComponent<ChunkMesh>(out var mesh))
			{
				mesh.GetMesh()?.QueueFree();
				entity.RemoveComponent<ChunkMesh>();
			}
			if (entity.TryGetComponent<ChunkCollider>(out var collider))
			{
				collider.GetBody()?.QueueFree();
				entity.RemoveComponent<ChunkCollider>();
			}
		}
	}
}
