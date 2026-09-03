using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Renders atmosphere as a fullscreen quad and updates shader parameters
/// for celestial bodies.
/// </summary>
public class AtmosphereSystem : QuerySystem<CelestialGeometry, CelestialTransform, AtmosphereSettings>
{
    private const string ShaderPath = "res://Terrain/Shaders/atmosphere_fullscreen.gdshader";
    private const int MaxPlanets = 8;
    private const float QuadDistance = 1.0f;

    private MeshInstance3D _fullscreenQuad;
    private ShaderMaterial _fullscreenMaterial;

    public Node ParentNode { get; set; }
    public Node3D Viewer { get; set; }

    public AtmosphereSystem()
        => Filter.AllTags(Tags.Get<CelestialPlanet, CelestialActive>());

    protected override void OnUpdate()
    {
        if (ParentNode == null || Viewer == null)
            return;

        var camera = ResolveCamera();
        if (camera == null)
            return;

        EnsureFullscreenQuad(camera);
        if (_fullscreenMaterial == null)
            return;

        var centers = new Godot.Collections.Array();
        var topParams = new Godot.Collections.Array();
        var rayleighParams = new Godot.Collections.Array();
        var mieParams = new Godot.Collections.Array();
        int planetCount = 0;

        foreach (var entity in Query.Entities)
        {
            ref var geometry = ref entity.GetComponent<CelestialGeometry>();
            ref var transform = ref entity.GetComponent<CelestialTransform>();
            ref var settings = ref entity.GetComponent<AtmosphereSettings>();

            CleanupLegacyMesh(entity);

            if (planetCount >= MaxPlanets)
                continue;

            float atmosphereHeight = Mathf.Max(0.1f, settings.AtmosphereHeight);
            float bottomRadius = geometry.Radius;
            float topRadius = bottomRadius + atmosphereHeight;
            float hr = settings.RayleighScaleHeight > 0f
                ? settings.RayleighScaleHeight
                : atmosphereHeight * 0.125f;
            float hm = settings.MieScaleHeight > 0f
                ? settings.MieScaleHeight
                : atmosphereHeight * 0.018f;

            Vector3 center = transform.Position;
            centers.Add(new Vector4(center.X, center.Y, center.Z, bottomRadius));
            topParams.Add(new Vector4(topRadius, hr, hm, settings.MiePhaseG));
            rayleighParams.Add(new Vector4(
                settings.RayleighScattering.X,
                settings.RayleighScattering.Y,
                settings.RayleighScattering.Z,
                settings.SunIntensity));
            mieParams.Add(new Vector4(
                settings.MieScattering.X,
                settings.MieScattering.Y,
                settings.MieScattering.Z,
                0f));

            planetCount++;
        }

        _fullscreenMaterial.SetShaderParameter("planet_count", planetCount);
        _fullscreenMaterial.SetShaderParameter("planet_center_radius", centers);
        _fullscreenMaterial.SetShaderParameter("planet_top_hr_hm_g", topParams);
        _fullscreenMaterial.SetShaderParameter("planet_rayleigh_intensity", rayleighParams);
        _fullscreenMaterial.SetShaderParameter("planet_mie", mieParams);
        _fullscreenMaterial.SetShaderParameter("sun_direction", SunDirectionSystem.SunDirectionWorld.Normalized());
    }

    private Camera3D ResolveCamera()
    {
        if (Viewer is Camera3D viewerCamera)
            return viewerCamera;

        return Viewer.GetViewport()?.GetCamera3D();
    }

    private void EnsureFullscreenQuad(Camera3D camera)
    {
        if (_fullscreenQuad != null && GodotObject.IsInstanceValid(_fullscreenQuad))
        {
            if (_fullscreenQuad.GetParent() != camera)
            {
                _fullscreenQuad.GetParent()?.RemoveChild(_fullscreenQuad);
                camera.AddChild(_fullscreenQuad);
            }

            return;
        }

        Shader shader = GD.Load<Shader>(ShaderPath);
        if (shader == null)
        {
            GD.PrintErr($"[AtmosphereSystem] Missing shader: {ShaderPath}");
            return;
        }

        var quadMesh = new QuadMesh
        {
            Size = new Vector2(2f, 2f)
        };

        _fullscreenMaterial = new ShaderMaterial { Shader = shader };
        _fullscreenMaterial.RenderPriority = 100;

        var meshInstance = new MeshInstance3D
        {
            Name = "Atmosphere_Fullscreen",
            Mesh = quadMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            SortingOffset = 1000.0f,
            ExtraCullMargin = 1000000.0f
        };

        meshInstance.MaterialOverride = _fullscreenMaterial;
        camera.AddChild(meshInstance);
        meshInstance.Position = new Vector3(0f, 0f, -QuadDistance);

        _fullscreenQuad = meshInstance;
    }

    private void CleanupLegacyMesh(Entity entity)
    {
        if (!entity.TryGetComponent<AtmosphereMesh>(out var mesh))
            return;

        mesh.GetMesh()?.QueueFree();
        CommandBuffer.RemoveComponent<AtmosphereMesh>(entity.Id);
    }
}
