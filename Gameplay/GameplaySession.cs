using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Gameplay ECS hub. Add to the scene tree — it creates the EntityStore,
/// registers all gameplay systems, and ticks them every frame.
///
/// Player / Camera nodes are placed in the scene **by hand** and register
/// themselves via <see cref="RegisterPlayer"/> / <see cref="RegisterCamera"/>.
/// </summary>
public partial class GameplaySession : Node
{
	// ── Singleton ───────────────────────────────────────────────────────
	public static GameplaySession Instance { get; private set; }

	/// <summary>The gameplay Friflo EntityStore (separate from terrain).</summary>
	public EntityStore Store => _store;

	// ── Internals ───────────────────────────────────────────────────────
	private EntityStore _store;
	private SystemRoot  _systems;
	private Entity      _inputEntity;
	private Entity      _playerEntity;

	private Vector2 _mouseMotionAccum;
	private float   _scrollAccum;
	private int     _tick;

	// ════════════════════════════════════════════════════════════════════
	//  LIFECYCLE
	// ════════════════════════════════════════════════════════════════════

	public override void _Ready()
	{
		Instance = this;
		Input.MouseMode = Input.MouseModeEnum.Captured;

		_store = new EntityStore();

		// Input singleton entity — written every frame, read by systems
		_inputEntity = _store.CreateEntity(
			new InputState(),
			Tags.Get<InputSingleton>()
		);

		// ── Systems (execution order matters) ──
		_systems = new SystemRoot(_store)
		{
			new PlayerInputSystem(),
			new GameplayCameraInputSystem(),
			new GameplayOrbitBoundarySystem(),
			new GameplayPlanetAlignSystem(),
			new GameplayGravitySystem(),
			new GameplayJumpSystem(),
			new GameplayMovementSystem(),
			new GameplayEntityRotationSystem(),
			new GameplayCameraFollowSystem(),
		};
	}

	// ════════════════════════════════════════════════════════════════════
	//  REGISTRATION  (called by PlayerNode / OrbitalCameraNode)
	// ════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Creates a Friflo entity for a CharacterBody3D placed in the scene.
	/// Called once from <see cref="PlayerNode._Ready"/>.
	/// </summary>
	public Entity RegisterPlayer(CharacterBody3D body,
								  float speed, float gravity, float maxFall,
								  float jumpForce, float jumpBuffer,
								  float noclipSpeedMul, float noclipVertSpeed,
								  float rotationSpeed)
	{
		var entity = _store.CreateEntity(
			new GodotBody      { InstanceId = body.GetInstanceId() },
			new PlayerVelocity { Speed = speed },
			new PlayerGravity  { Force = gravity, MaxFallSpeed = maxFall, Direction = Vector3.Down },
			new PlayerOrbitBoundary
			{
				LowOrbitHeightFactor = 0.5f,
				BlendHeightFactor = 0.01f,
				MinCornerSafeRadiusFactor = 1.73f,
			},
			new PlayerOrbitState(),
			new PlayerJump     { JumpForce = jumpForce, BufferDuration = jumpBuffer },
			new PlayerNoclip   { SpeedMultiplier = noclipSpeedMul, VerticalSpeed = noclipVertSpeed },
			new PlayerHealth   { Current = 100f, Maximum = 100f },
			new PlayerRotation { Speed = rotationSpeed },
			Tags.Get<PlayerTag>()
		);

		_playerEntity = entity;
		GD.Print($"[Gameplay] Player entity {entity.Id} registered");
		return entity;
	}

	/// <summary>
	/// Creates a Friflo entity for a Camera3D placed in the scene.
	/// Called once from <see cref="OrbitalCameraNode._Ready"/>.
	/// </summary>
	public Entity RegisterCamera(Camera3D camera, Entity playerEntity,
								  float distance, float sensitivity,
								  Vector3 shoulderOffset)
	{
		var entity = _store.CreateEntity(
			new GodotCamera { InstanceId = camera.GetInstanceId() },
			new OrbitalCameraData
			{
				Distance          = distance,
				TargetDistance     = -1f,
				DistanceLerpSpeed = 8f,
				ShoulderOffset    = shoulderOffset,
				Sensitivity       = sensitivity,
			},
			new CameraFollowsPlayer { Target = playerEntity },
			Tags.Get<CameraTag>()
		);
		GD.Print($"[Gameplay] Camera entity {entity.Id} registered → follows player {playerEntity.Id}");
		return entity;
	}

	// ════════════════════════════════════════════════════════════════════
	//  INPUT  (Node callback → writes InputState each frame)
	// ════════════════════════════════════════════════════════════════════

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotion
			&& Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			_mouseMotionAccum = mouseMotion.Relative;
		}

		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed
			&& Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			switch (mouseButton.ButtonIndex)
			{
				case MouseButton.WheelUp:   _scrollAccum -= 0.5f; break;
				case MouseButton.WheelDown: _scrollAccum += 0.5f; break;
			}
		}

		if (Input.IsActionJustPressed("ui_cancel"))
		{
			Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
				? Input.MouseModeEnum.Visible
				: Input.MouseModeEnum.Captured;
		}
	}

	// ════════════════════════════════════════════════════════════════════
	//  PROCESS
	// ════════════════════════════════════════════════════════════════════

	public override void _Process(double delta)
	{
		_inputEntity.AddComponent(new InputState
		{
			MouseDeltaX = _mouseMotionAccum.X,
			MouseDeltaY = _mouseMotionAccum.Y,
			ScrollDelta = _scrollAccum,
		});
		_mouseMotionAccum = Vector2.Zero;
		_scrollAccum      = 0f;

		// Try to link player to planet if not yet linked
		if (!_playerEntity.IsNull && !_playerEntity.HasComponent<GravityAffected>())
		{
			Entity planet = FindPlanetEntity();
			if (!planet.IsNull)
			{
				_playerEntity.AddComponent(new GravityAffected { Planet = planet });
				GD.Print($"[Gameplay] Player {_playerEntity.Id} linked to planet {planet.Id}");
			}
			else
			{
				GD.Print($"[Gameplay] Waiting for planet... GameSession.Instance={(GameSession.Instance != null ? "OK" : "null")}");
			}
		}

		_systems.Update(new UpdateTick((float)delta, _tick++));
	}

	private Entity FindPlanetEntity()
	{
		var terrainSession = GameSession.Instance;
		if (terrainSession == null) return default;

		var terrainStore = terrainSession.Store;
		if (terrainStore == null) return default;

		foreach (var entity in terrainStore.Entities)
		{
			if (entity.HasComponent<GravitySource>())
				return entity;
		}
		return default;
	}

	// ════════════════════════════════════════════════════════════════════
	//  CLEANUP
	// ════════════════════════════════════════════════════════════════════

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}
}
