using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Orients a DirectionalLight3D to point toward the star (sun).
///
/// Pipeline position: after all chunk systems, runs every frame.
///
/// Finds the entity with CelestialSun tag, computes direction from
/// the viewer (player) to the star, and rotates the DirectionalLight3D
/// accordingly. Also updates the sky shader's sun_direction parameter.
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
        Vector3 viewerPosition = Viewer.GlobalPosition;

        // Direction from viewer to star
        Vector3 direction = (starPosition - viewerPosition).Normalized();

        // Rotate the directional light to point toward the star
        SunLight.GlobalRotation = Vector3.Zero;
        SunLight.LookAt(starPosition);

        // Update sky shader sun_direction if WorldEnvironment is available
        if (WorldEnvironment != null)
        {
            var sky = WorldEnvironment.Environment?.Sky;
            if (sky?.SkyMaterial is ShaderMaterial skyMat)
            {
                skyMat.SetShaderParameter("sun_direction", direction);
            }
        }
    }
}
