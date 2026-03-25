// GameSession.cs
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public partial class GameSession : Node
{
	[ExportGroup("View")]
	[Export] public Node3D Viewer { get; set; }
	[Export] public int RenderDistance { get; set; } = 5;
	[Export] public int CollisionDistance { get; set; } = 1;

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
	[Export] public Material WaterMaterial { get; set; }
	[Export] public Material TrunkMaterial { get; set; }
	[Export] public Material CanopyMaterial { get; set; }

	// Добавляем параметры для мира
	[ExportGroup("World")]
	[Export] public string WorldName { get; set; } = "MyWorld";
	[Export] public int WorldSeed { get; set; } = 12345;

	private EntityStore _store;
	private SystemRoot _systems;

	// ── System references (for regeneration) ──
	private ChunkMeshBuildSystem _meshBuildSystem;
	private SystemSegmentCreator _segmentCreator;
	private SegmentDataGenerationSystem _segmentDataGen;
	private ChunkDataGenerationSystem _chunkDataGen;

	private float _smoothedFrameMs = 16.6f;
	private float _budgetTimer;
	private int _meshBudget;

	// ── Debug overlay ──
	private Label _biomeLabel;
	private bool _debugVisible;


	private void SetupNoiseSettings()
	{
		if (NoiseSettings == null)
		{
			NoiseSettings = new NoiseSettings();
			GD.Print("[TerrainWorld] Created default NoiseSettings");
		}
	}

	/// <summary>
	/// Computes sea level in tile-height units: coastLevel × HeightScale × MAX_HEIGHT.
	/// </summary>
	private int ComputeSeaLevelTile()
	{
		NoiseSettings.EnsureCurves();
		float coastLevel = NoiseSettings.ContinentCurve.Sample(NoiseSettings.CoastStart);
		return Mathf.RoundToInt(coastLevel * HeightScale * ConstantsCelestial.MAX_HEIGHT);
	}

	/// <summary>
	/// Loads surfaces and height rules from JSON, builds shader LUT textures.
	/// </summary>
	private void SetupTerrain()
	{
		// Load surface definitions and height rules from JSON
		SurfaceRegistry.Load();

		// Load biome definitions (must be before SurfaceMapper.Initialize)
		BiomeRegistry.Load();

		// Build lookup tables for fast surface assignment
		SurfaceMapper.Initialize();

		// Load tree type materials (textures + colors)
		TreeTypeRegistry.Load();

		// Build Texture2DArray + LUT textures and assign to shader
		if (TerrainMaterial is ShaderMaterial shaderMat)
		{
			TerrainTextureLoader.Apply(shaderMat);
		}
	}

	public override void _Ready()
	{
		SetupNoiseSettings();
		SetupTerrain();

		_store = new EntityStore();

		var worldCreator = new SystemWorldCreator
		{
			WorldName = WorldName,
			WorldSeed = WorldSeed,
			CreateOnStart = true
		};

		var celestialCreator = new SystemCelestialCreator();

		_segmentCreator = new SystemSegmentCreator
		{
			Viewer = Viewer,
			LoadRadius = ConstantsSegment.LOAD_RADIUS,
			UnloadRadius = ConstantsSegment.UNLOAD_RADIUS
		};

		_segmentDataGen = new SegmentDataGenerationSystem
		{
			NoiseSettings = NoiseSettings,
			HeightScale = HeightScale,
			MaxPerFrame = 1,
			SeaLevelTile = ComputeSeaLevelTile()
		};

		var visibilitySystem = new ChunkVisibilitySystem
		{
			Viewer = Viewer,
			RenderDistance = RenderDistance,
			CollisionDistance = CollisionDistance,
			MaxPerFrame = MaxCreatePerFrame
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
			SeaLevelTile = ComputeSeaLevelTile()
		};

		var chunkLoadSystem = new ChunkLoadSystem
		{
			Viewer = Viewer,
			MaxPerFrame = MaxDataGenPerFrame,
			SegmentCreator = _segmentCreator
		};

		_meshBuildSystem = new ChunkMeshBuildSystem
		{
			Viewer = Viewer,
			MaxPerFrame = MaxMeshBuildPerFrame,
			TerrainMaterial = TerrainMaterial,
			WaterMaterial = WaterMaterial,
			SeaLevelTile = ComputeSeaLevelTile(),
			ParentNode = this
		};

		var collisionBuildSystem = new ChunkCollisionBuildSystem
		{
			Viewer = Viewer,
			MaxPerFrame = MaxCollisionBuildPerFrame,
			ParentNode = this
		};

		// ── Trees ──
		var treeRoot = new Node3D { Name = "Trees" };
		AddChild(treeRoot);

		var treeSpawnSystem = new TreeSpawnSystem
		{
			SeaLevelTile     = ComputeSeaLevelTile(),
			SurfaceTreeMap   = SurfaceRegistry.TreeTypeMap,
			SurfaceDensityMap = SurfaceRegistry.TreeDensityMap
		};

		var treeRenderSystem = new TreeRenderSystem
		{
			TreeRoot = treeRoot
		};

		_meshBudget = Mathf.Max(1, MaxMeshBuildPerFrame);

		_systems = new SystemRoot(_store)
		{
			worldCreator,
			celestialCreator,
			_segmentCreator,
			_segmentDataGen,
			visibilitySystem,
			removalSystem,
			chunkLoadSystem,
			_chunkDataGen,
			_meshBuildSystem,
			collisionBuildSystem,
			treeSpawnSystem,
			treeRenderSystem,
		};
	}

	private int _tick;

	public override void _Process(double delta)
	{
		if (AutoAdjustBudgets)
			AutoTuneBudgets((float)delta);

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
		var noiseGen = _segmentDataGen?.NoiseGenerator;

		string zoneName = "N/A";
		string cValue = "—";
		string eValue = "—";
		string biomeName = "N/A";
		if (noiseGen != null)
		{
			float C = noiseGen.GetContinentalness(pos.X, pos.Z);
			float E = noiseGen.GetErosion(pos.X, pos.Z);
			var zone = noiseGen.GetZone(C);
			cValue = C.ToString("F3");
			eValue = E.ToString("F3");
			zoneName = zone switch
			{
				ContinentalZone.Ocean     => "Океан",
				ContinentalZone.Coast     => "Берег",
				ContinentalZone.Inland    => "Суша",
				ContinentalZone.FarInland => "Глубина континента",
				ContinentalZone.River     => "Река",
				_ => zone.ToString()
			};

			int biomeIdx = BiomeRegistry.GetBiome((int)zone, E);
			if (biomeIdx < BiomeRegistry.Count)
				biomeName = BiomeRegistry.Biomes[biomeIdx].Name;
		}

		_biomeLabel.Text = $"Зона: {zoneName} (C={cValue})\n" +
						   $"Эрозия: {eValue}\n" +
						   $"Биом: {biomeName}\n" +
						   $"Координаты: {pos.X:F0}, {pos.Y:F0}, {pos.Z:F0}";
	}
	

	private void AutoTuneBudgets(float delta)
	{
		float frameMs = delta * 1000f;
		_smoothedFrameMs = Mathf.Lerp(_smoothedFrameMs, frameMs, 0.10f);

		_budgetTimer += delta;
		if (_budgetTimer < 0.25f)
			return;
		_budgetTimer = 0f;

		int maxMesh = Mathf.Max(1, MaxMeshBuildPerFrame);
		int newBudget = _meshBudget;

		if (_smoothedFrameMs > 22f)
			newBudget = Math.Max(1, _meshBudget - 1);
		else if (_smoothedFrameMs < 14f)
			newBudget = Math.Min(maxMesh, _meshBudget + 1);

		if (newBudget != _meshBudget)
		{
			_meshBudget = newBudget;
			_meshBuildSystem.MaxPerFrame = _meshBudget;
		}
	}

	public override void _ExitTree()
	{
		foreach (var entity in _store.Entities)
		{
			if (entity.TryGetComponent<ChunkMesh>(out var mesh))
				mesh.GetMesh()?.QueueFree();
			if (entity.TryGetComponent<ChunkCollider>(out var collider))
				collider.GetBody()?.QueueFree();
			if (entity.TryGetComponent<ChunkTreeMesh>(out var treeMesh) && treeMesh.InstanceIds != null)
			{
				foreach (ulong id in treeMesh.InstanceIds)
					(GodotObject.InstanceFromId(id) as Node)?.QueueFree();
			}
		}
	}
}
