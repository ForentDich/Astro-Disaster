using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;

/// <summary>
/// Builds and updates proxy spheres for planets using ECS data.
/// </summary>
public class PlanetProxySystem : QuerySystem<CelestialGeometry, CelestialTransform, SurfaceData, PlanetProxySettings>
{
    private EntityStore _store;
    private NoiseGenerator _noiseGenerator;

    public Node ParentNode { get; set; }
    public Node3D Viewer { get; set; }
    public NoiseSettings NoiseSettings { get; set; }
    public float HeightScale { get; set; } = 0.3f;
    public float LoadRadius { get; set; } = 0f;

    public PlanetProxySystem()
        => Filter.AllTags(Tags.Get<CelestialPlanet, CelestialActive>());

    protected override void OnAddStore(EntityStore store)
    {
        base.OnAddStore(store);
        _store = store;
        EnsureNoiseGenerator();
    }

    protected override void OnUpdate()
    {
        if (ParentNode == null)
            return;

        EnsureNoiseGenerator();

        foreach (var entity in Query.Entities)
        {
            ref var settings = ref entity.GetComponent<PlanetProxySettings>();
            if (!settings.Enabled)
                continue;

            ref var transform = ref entity.GetComponent<CelestialTransform>();
            ref var geometry = ref entity.GetComponent<CelestialGeometry>();

            MeshInstance3D meshInstance = null;
            if (entity.TryGetComponent<PlanetProxyMesh>(out var proxyMesh))
                meshInstance = proxyMesh.GetMesh();

            if (meshInstance == null)
            {
                meshInstance = CreateProxyMesh(entity, ref geometry, ref transform, ref settings);
                if (meshInstance != null)
                    CommandBuffer.AddComponent(entity.Id, new PlanetProxyMesh { InstanceId = meshInstance.GetInstanceId() });
            }

            if (meshInstance == null)
                continue;

            meshInstance.Position = transform.Position;

            if (meshInstance.MaterialOverride is ShaderMaterial shader)
                UpdateShaderParams(shader, ref settings);
        }
    }

    private void EnsureNoiseGenerator()
    {
        if (NoiseSettings == null)
            NoiseSettings = NoiseSettings.CreateDefault();

        _noiseGenerator ??= new NoiseGenerator(NoiseSettings);
    }

    private MeshInstance3D CreateProxyMesh(Entity entity, ref CelestialGeometry geometry, ref CelestialTransform transform, ref PlanetProxySettings settings)
    {
        int segmentsPerSide = ResolveSegmentsPerSide(entity);
        int fullResolution = CubeSphereProjection.GetFaceResolution(segmentsPerSide);
        int resolutionDiv = settings.ResolutionDiv <= 0 ? 16 : settings.ResolutionDiv;
        int proxyResolution = Mathf.Max(8, fullResolution / resolutionDiv);
        int step = Mathf.Max(1, fullResolution / proxyResolution);

        SurfaceTool st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetSmoothGroup(uint.MaxValue);

        Basis planetBasis = new Basis(transform.Rotation);
        Vector3[] localNormals = new Vector3[]
        {
            Vector3.Forward,
            Vector3.Right,
            Vector3.Back,
            Vector3.Left,
            Vector3.Up,
            Vector3.Down
        };

        Vector3[] localUpVectors = new Vector3[]
        {
            Vector3.Up,
            Vector3.Up,
            Vector3.Up,
            Vector3.Up,
            Vector3.Back,
            Vector3.Back
        };

        float sink = settings.ProxySink > 0f ? settings.ProxySink : 3.0f;

        for (int faceIndex = 0; faceIndex < ConstantsCelestial.FACE_COUNT; faceIndex++)
        {
            Vector3 worldNormal = planetBasis * localNormals[faceIndex];
            Vector3 worldUp = planetBasis * localUpVectors[faceIndex];
            Vector3 worldRight = worldNormal.Cross(worldUp).Normalized();

            var orientation = new FaceOrientation
            {
                Normal = worldNormal,
                Up = worldUp,
                Right = worldRight
            };

            for (int z = 0; z < proxyResolution; z++)
            {
                int gz0 = Math.Min(fullResolution, z * step);
                int gz1 = Math.Min(fullResolution, (z + 1) * step);

                for (int x = 0; x < proxyResolution; x++)
                {
                    int gx0 = Math.Min(fullResolution, x * step);
                    int gx1 = Math.Min(fullResolution, (x + 1) * step);

                    Vector3 p00 = CubeSphereProjection.GetSpherePoint(gx0, gz0, fullResolution, orientation, geometry.Radius);
                    Vector3 p10 = CubeSphereProjection.GetSpherePoint(gx1, gz0, fullResolution, orientation, geometry.Radius);
                    Vector3 p01 = CubeSphereProjection.GetSpherePoint(gx0, gz1, fullResolution, orientation, geometry.Radius);
                    Vector3 p11 = CubeSphereProjection.GetSpherePoint(gx1, gz1, fullResolution, orientation, geometry.Radius);

                    var v00 = BuildVertex(p00, geometry.Radius, sink);
                    var v10 = BuildVertex(p10, geometry.Radius, sink);
                    var v01 = BuildVertex(p01, geometry.Radius, sink);
                    var v11 = BuildVertex(p11, geometry.Radius, sink);

                    AddTriangle(st, v00, v10, v11);
                    AddTriangle(st, v00, v11, v01);
                }
            }
        }

        st.Index();
        st.GenerateNormals();

        ArrayMesh mesh = st.Commit();
        if (mesh == null || mesh.GetSurfaceCount() == 0)
            return null;

        ShaderMaterial material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://Terrain/Shaders/planet_proxy.gdshader")
        };

        MeshInstance3D meshInstance = new MeshInstance3D
        {
            Name = $"PlanetProxy_{entity.Id}",
            Mesh = mesh,
            Position = transform.Position
        };

        meshInstance.MaterialOverride = material;

        ParentNode.AddChild(meshInstance);
        return meshInstance;
    }

    private int ResolveSegmentsPerSide(Entity entity)
    {
        if (_store == null)
            return 1;

        var query = _store.Query<FaceIdentity, FaceParent>().AllTags(Tags.Get<FaceCreated>());
        foreach (var face in query.Entities)
        {
            ref var parent = ref face.GetComponent<FaceParent>();
            if (parent.Celestial.Id != entity.Id)
                continue;

            ref var faceId = ref face.GetComponent<FaceIdentity>();
            return Math.Max(1, faceId.SegmentsPerSide);
        }

        return 1;
    }

    private (Vector3 position, Color color) BuildVertex(Vector3 spherePos, float radius, float sink)
    {
        float wx = spherePos.X;
        float wy = spherePos.Y;
        float wz = spherePos.Z;

        float c = _noiseGenerator.GetContinentalness3D(wx, wy, wz);
        float e = _noiseGenerator.GetErosion3D(wx, wy, wz);
        float noiseValue = _noiseGenerator.GetNoise3D(wx, wy, wz);

        int heightValue = Mathf.RoundToInt(noiseValue * HeightScale * ConstantsCelestial.MAX_HEIGHT);
        heightValue = Math.Clamp(heightValue, ConstantsCelestial.MIN_HEIGHT, ConstantsCelestial.MAX_HEIGHT);
        float heightOffset = heightValue * ChunkConstants.TILE_HEIGHT;

        float baseRadius = Mathf.Max(0f, radius - sink);
        Vector3 position = spherePos.Normalized() * (baseRadius + heightOffset);

        int zone = (int)_noiseGenerator.GetZoneWithRiver3D(c, e, wx, wy, wz);
        int biomeIndex = BiomeRegistry.GetBiome(zone, e);
        int surfaceIndex = ResolveSurfaceIndex(biomeIndex, heightValue);
        Color color = ResolveSurfaceColor(surfaceIndex);

        return (position, color);
    }

    private static int ResolveSurfaceIndex(int biomeIndex, int height)
    {
        if (biomeIndex < 0 || biomeIndex >= BiomeRegistry.Count)
            return 0;

        ref var biome = ref BiomeRegistry.Biomes[biomeIndex];
        var rules = biome.HeightRules;
        if (rules == null || rules.Length == 0)
            return 0;

        for (int i = 0; i < rules.Length; i++)
        {
            ref var rule = ref rules[i];
            if (height >= rule.MinHeight && height <= rule.MaxHeight)
                return Math.Clamp(rule.SurfaceIndex, 0, ChunkConstants.SURFACE_MASK);
        }

        return Math.Clamp(rules[0].SurfaceIndex, 0, ChunkConstants.SURFACE_MASK);
    }

    private static Color ResolveSurfaceColor(int surfaceIndex)
    {
        if (surfaceIndex >= 0 && surfaceIndex < SurfaceRegistry.Count)
            return SurfaceRegistry.Surfaces[surfaceIndex].Tint;

        return new Color(0.45f, 0.72f, 0.42f, 1f);
    }

    private static void AddTriangle(SurfaceTool st, (Vector3 position, Color color) a, (Vector3 position, Color color) b, (Vector3 position, Color color) c)
    {
        st.SetColor(a.color);
        st.AddVertex(a.position);

        st.SetColor(b.color);
        st.AddVertex(b.position);

        st.SetColor(c.color);
        st.AddVertex(c.position);
    }

    private void UpdateShaderParams(ShaderMaterial shader, ref PlanetProxySettings settings)
    {
        if (Viewer == null)
            return;

        float discardRadius = settings.ProxyDiscardRadius > 0f ? settings.ProxyDiscardRadius : 250.0f;
        shader.SetShaderParameter("player_pos", Viewer.GlobalPosition);
        shader.SetShaderParameter("discard_radius", discardRadius);
    }

}
