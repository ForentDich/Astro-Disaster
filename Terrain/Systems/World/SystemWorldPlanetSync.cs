using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Syncs the primary planet transform into WorldPlanet and keeps chunk nodes aligned.
/// </summary>
public class SystemWorldPlanetSync : BaseSystem
{
    private EntityStore _store;
    private Entity _world;
    private Vector3 _lastCenter;
    private bool _hasCenter;

    protected override void OnAddStore(EntityStore store)
    {
        _store = store;
    }

    protected override void OnUpdateGroup()
    {
        if (_store == null)
            return;

        if (_world.IsNull)
            _world = _store.GetUniqueEntity("World");
        if (_world.IsNull)
            return;

        Entity planet = FindPrimaryPlanet();
        if (planet.IsNull)
            return;

        ref var transform = ref planet.GetComponent<CelestialTransform>();
        ref var geometry = ref planet.GetComponent<CelestialGeometry>();

        Vector3 center = transform.Position;
        float radius = geometry.Radius;

        if (_world.HasComponent<WorldPlanet>())
        {
            ref var worldPlanet = ref _world.GetComponent<WorldPlanet>();
            worldPlanet.Center = center;
            worldPlanet.Radius = radius;
        }
        else
        {
            _world.AddComponent(new WorldPlanet { Center = center, Radius = radius });
        }

        if (!_hasCenter)
        {
            _lastCenter = center;
            _hasCenter = true;
            return;
        }

        Vector3 delta = center - _lastCenter;
        _lastCenter = center;

        if (delta.LengthSquared() < 0.001f)
            return;

        ApplyPlanetDelta(delta);
    }

    private Entity FindPrimaryPlanet()
    {
        var primaryQuery = _store.Query<CelestialTransform, CelestialGeometry>()
            .AllTags(Tags.Get<CelestialPrimary, CelestialActive>());

        foreach (var entity in primaryQuery.Entities)
            return entity;

        var fallbackQuery = _store.Query<CelestialTransform, CelestialGeometry>()
            .AllTags(Tags.Get<CelestialPlanet, CelestialActive>());

        foreach (var entity in fallbackQuery.Entities)
            return entity;

        return default;
    }

    private void ApplyPlanetDelta(Vector3 delta)
    {
        var meshQuery = _store.Query<ChunkMesh>();
        foreach (var entity in meshQuery.Entities)
        {
            if (!entity.TryGetComponent<ChunkMesh>(out var mesh))
                continue;

            var meshInstance = mesh.GetMesh();
            if (meshInstance != null)
                meshInstance.Position += delta;
        }

        var colliderQuery = _store.Query<ChunkCollider>();
        foreach (var entity in colliderQuery.Entities)
        {
            if (!entity.TryGetComponent<ChunkCollider>(out var collider))
                continue;

            var body = collider.GetBody();
            if (body != null)
                body.Position += delta;
        }
    }
}
