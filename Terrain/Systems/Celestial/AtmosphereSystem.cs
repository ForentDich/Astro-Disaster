using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Renders a fullscreen post-process atmosphere for the primary planet.
///
/// Pipeline position: after PlanetProxySystem, runs every frame.
///
/// Creates a fullscreen triangle mesh once and updates shader parameters
/// from the active planet each frame.
/// </summary>
public class AtmosphereSystem : QuerySystem<CelestialGeometry, CelestialTransform, AtmosphereData>
{
    private EntityStore _store;

    public Node ParentNode { get; set; }
    public Node3D Viewer { get; set; }


    public AtmosphereSystem()
        => Filter.AllTags(Tags.Get<CelestialPlanet, CelestialActive, CelestialHasAtmosphere>());

    protected override void OnAddStore(EntityStore store)
    {
        base.OnAddStore(store);
        _store = store;
    }

    protected override void OnUpdate()
    {
        if (ParentNode == null || Viewer == null)
            return;

        Vector3 starPosition = Vector3.Zero;

        foreach (var entity in Query.Entities)
        {
            ref var transform = ref entity.GetComponent<CelestialTransform>();
            ref var geometry = ref entity.GetComponent<CelestialGeometry>();
            ref var atmosphere = ref entity.GetComponent<AtmosphereData>();

            MeshInstance3D meshInstance = null;
            if (entity.TryGetComponent<AtmosphereMesh>(out var atmMesh))
                meshInstance = atmMesh.GetMesh();

            if (meshInstance == null)
            {
                meshInstance = CreateAtmosphereMesh(entity, ref geometry, ref atmosphere, transform.Position);
                if (meshInstance != null)
                    CommandBuffer.AddComponent(entity.Id, new AtmosphereMesh { InstanceId = meshInstance.GetInstanceId() });
            }

            if (meshInstance == null)
                continue;

            meshInstance.Position = transform.Position;

            Vector3 planetPos = transform.Position;
            Vector3 sunDir = (starPosition - planetPos).Normalized();
            if (sunDir.LengthSquared() < 0.001f)
                sunDir = Vector3.Up;

            float planetRadius = geometry.Radius;
            float atmoRadius = planetRadius + atmosphere.Height;

            if (meshInstance.Mesh is BoxMesh box)
                box.Size = Vector3.One * (atmoRadius * 2.1f);

            if (meshInstance.MaterialOverride is ShaderMaterial shader)
            {
                Color baseColor = atmosphere.Color;
                Color backColor = new Color(baseColor.R * 0.4f, baseColor.G * 0.4f, baseColor.B * 0.6f, 0.7f);
                shader.SetShaderParameter("PlanetCentre", planetPos);
                shader.SetShaderParameter("DirToSun", sunDir);
                shader.SetShaderParameter("sea_level", planetRadius);
                shader.SetShaderParameter("atmosphere_radius", atmoRadius);
                shader.SetShaderParameter("atmosphere_density", Mathf.Max(0.01f, atmosphere.Density));
                shader.SetShaderParameter("height_color_main", baseColor);
                shader.SetShaderParameter("height_color_back", backColor);
                shader.SetShaderParameter("direction_color_main", Colors.White);
                shader.SetShaderParameter("direction_color_back", backColor);
                shader.SetShaderParameter("density_falloff", 4.0f);
            }
        }
    }

    private MeshInstance3D CreateAtmosphereMesh(Entity entity, ref CelestialGeometry geometry, ref AtmosphereData atmosphere, Vector3 planetPosition)
    {
        float planetRadius = geometry.Radius;
        float atmoRadius = planetRadius + atmosphere.Height;

        BoxMesh boxMesh = new BoxMesh
        {
            Size = Vector3.One * (atmoRadius * 2.1f)
        };

        Shader shader = GD.Load<Shader>("res://Terrain/Shaders/planet_atmosphere.gdshader");
        if (shader == null)
        {
            GD.PrintErr("[AtmosphereSystem] Missing shader: res://Terrain/Shaders/planet_atmosphere.gdshader");
            return null;
        }

        ShaderMaterial material = new ShaderMaterial { Shader = shader };

        MeshInstance3D meshInstance = new MeshInstance3D
        {
            Name = $"Atmosphere_{entity.Id}",
            Mesh = boxMesh,
            Position = planetPosition,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };

        ParentNode.AddChild(meshInstance);
        return meshInstance;
    }
}
