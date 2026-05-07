using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Orients a DirectionalLight3D so that light rays travel from the star toward the planet.
///
/// Pipeline position: after all chunk systems, runs every frame.
///
/// Finds the entity with CelestialSun tag, finds the primary planet,
/// and rotates the DirectionalLight3D so it shines from star → planet.
/// Also updates the sky shader's sun_direction parameter.
/// </summary>
public class SunDirectionSystem : BaseSystem
{
    private EntityStore _store;
    private Entity _starEntity;
    private bool _initialized;

    /// <summary>
    /// Reference to the DirectionalLight3D node in the scene tree.
    /// Must be assigned before the system runs.
    /// </summary>
    public DirectionalLight3D SunLight { get; set; }

    /// <summary>
    /// Reference to the viewer (player) Node3D for position tracking.
    /// </summary>
    public Node3D Viewer { get; set; }

    /// <summary>
    /// Optional reference to the WorldEnvironment for sky shader updates.
    /// </summary>
    public WorldEnvironment WorldEnvironment { get; set; }

    protected override void OnAddStore(EntityStore store)
    {
        _store = store;
    }

    protected override void OnUpdateGroup()
    {
        if (SunLight == null || Viewer == null)
            return;

        if (!_initialized)
        {
            FindStar();
            _initialized = true;
        }

        if (!_starEntity.IsNull)
        {
            UpdateLightDirection();
        }
    }

    private void FindStar()
    {
        foreach (var entity in _store.Entities)
        {
            if (entity.Tags.Has<CelestialSun>())
            {
                _starEntity = entity;
                GD.Print("[SunDirectionSystem] >> Found star entity");
                return;
            }
        }

        GD.Print("[SunDirectionSystem] >> Star entity not found yet, will retry");
        _initialized = false;
    }

    private void UpdateLightDirection()
    {
        // Star is at world origin (0,0,0)
        Vector3 starPosition = Vector3.Zero;

        // Find the primary planet to compute light direction
        Vector3 planetPosition = FindPrimaryPlanetPosition();
        Vector3 toPlanet = planetPosition - starPosition;
        if (toPlanet.LengthSquared() < 0.001f)
            return;

        // Light direction: from star toward planet
        Vector3 lightDirection = toPlanet.Normalized();
        Vector3 skySunDirection = -lightDirection;

        // Avoid degenerate up vectors when the direction is nearly vertical.
        Vector3 up = Vector3.Up;
        if (Mathf.Abs(lightDirection.Dot(up)) > 0.999f)
            up = Vector3.Forward;

        // Rotate the directional light to shine from star → planet.
        // DirectionalLight3D's -Z axis is its forward direction.
        // Use Basis.LookingAt to point -Z along lightDirection.
        SunLight.GlobalBasis = Basis.LookingAt(lightDirection, up);

        // Update sky shader sun_direction if WorldEnvironment is available
        if (WorldEnvironment != null)
        {
            var sky = WorldEnvironment.Environment?.Sky;
            if (sky?.SkyMaterial is ShaderMaterial skyMat)
            {
                skyMat.SetShaderParameter("sun_direction", skySunDirection);
            }
        }
    }

    private Vector3 FindPrimaryPlanetPosition()
    {
        foreach (var entity in _store.Entities)
        {
            if (entity.Tags.Has<CelestialPlanet>() && entity.Tags.Has<CelestialActive>())
            {
                if (entity.TryGetComponent<CelestialTransform>(out var transform))
                    return transform.Position;
            }
        }
        return Vector3.Zero;
    }
}
