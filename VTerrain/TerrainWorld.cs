using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;

public partial class TerrainWorld : Node3D
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

	private EntityStore _store;
	private SystemRoot _systems;
	private ChunkMeshBuildSystem _meshBuildSystem;

	private float _smoothedFrameMs = 16.6f;
	private float _budgetTimer;
	private int _meshBudget;


	private void SetupNoiseSettings()
	{
		if (NoiseSettings == null)
		{
			NoiseSettings = new NoiseSettings();
			GD.Print("[TerrainWorld] Created default NoiseSettings");
		}
	}


	public override void _Ready()
	{
		SetupNoiseSettings();

		_store = new EntityStore();

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

		var dataGenSystem = new ChunkDataGenerationSystem
		{
			Viewer = Viewer,
			MaxPerFrame = MaxDataGenPerFrame,
			NoiseSettings = NoiseSettings,  
			HeightScale = HeightScale

		};

		_meshBuildSystem = new ChunkMeshBuildSystem
		{
			Viewer = Viewer,
			MaxPerFrame = MaxMeshBuildPerFrame,
			TerrainMaterial = TerrainMaterial,
			ParentNode = this
		};

		var collisionBuildSystem = new ChunkCollisionBuildSystem
		{
			Viewer = Viewer,
			MaxPerFrame = MaxCollisionBuildPerFrame,
			ParentNode = this
		};

		_meshBudget = Mathf.Max(1, MaxMeshBuildPerFrame);

		_systems = new SystemRoot(_store)
		{
			visibilitySystem,
			removalSystem,
			dataGenSystem,
			_meshBuildSystem,
			collisionBuildSystem,
		};
	}

	private int _tick;

	public override void _Process(double delta)
	{
		if (AutoAdjustBudgets)
			AutoTuneBudgets((float)delta);

		_systems.Update(new UpdateTick(_tick++, (float)delta));
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
		}
	}
}
