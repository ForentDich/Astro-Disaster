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
///
/// Exposes SunDirectionWorld as a static field so other systems
/// can read the current sun direction without needing a star entity.
/// </summary>
public class SunDirectionSystem : BaseSystem
{

    public static Vector3 SunDirectionWorld { get; private set; } = Vector3.Up;

    private EntityStore _store;
    private Entity _starEntity;
    private bool _initialized;

    public DirectionalLight3D SunLight { get; set; }
    public Node3D Viewer { get; set; }
    public WorldEnvironment WorldEnvironment { get; set; }

    protected override void OnAddStore(EntityStore store) => _store = store;

    protected override void OnUpdateGroup() {
        if (SunLight == null || Viewer == null) return;
        if (!_initialized) { FindStar(); _initialized = true; }
        if (!_starEntity.IsNull) UpdateLightDirection();
    }

    private void FindStar() {
        foreach (var entity in _store.Entities) {
            if (entity.Tags.Has<CelestialSun>()) {
                _starEntity = entity;
                GD.Print("[SunDirectionSystem] >> Found star entity");
                return;
            }
        }
        _initialized = false;
    }

    private void UpdateLightDirection() {
        Vector3 starPosition = Vector3.Zero;
        Vector3 planetPosition = FindPrimaryPlanetPosition();
        
        Vector3 toSun = (starPosition - planetPosition).Normalized();
        Vector3 lightRayDir = -toSun;
        SunDirectionWorld = toSun;

        Vector3 up = Mathf.Abs(lightRayDir.Dot(Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up;
        SunLight.GlobalBasis = Basis.LookingAt(lightRayDir, up);

        if (WorldEnvironment?.Environment?.Sky?.SkyMaterial is ShaderMaterial skyMat) {
            skyMat.SetShaderParameter("sun_direction", toSun);
        }
    }

    private Vector3 FindPrimaryPlanetPosition() {
        foreach (var entity in _store.Entities) {
            if (entity.Tags.Has<CelestialPrimary>() && entity.Tags.Has<CelestialActive>()) {
                if (entity.TryGetComponent<CelestialTransform>(out var transform))
                    return transform.Position;
            }
        }

        foreach (var entity in _store.Entities) {
            if (entity.Tags.Has<CelestialPlanet>() && entity.Tags.Has<CelestialActive>()) {
                if (entity.TryGetComponent<CelestialTransform>(out var transform))
                    return transform.Position;
            }
        }
        return Vector3.Zero;
    }
}
