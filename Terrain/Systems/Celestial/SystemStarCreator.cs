using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;

/// <summary>
/// Creates a star entity from SolarSystemConfig.
///
/// Pipeline position: after SystemWorldCreator, before SystemCelestialCreator.
///
/// The star is placed at world origin (0,0,0):
///   - No gravity, no collision, no mesh
///   - Just an ECS entity with StarData, CelestialSun tag
///   - Used by SunDirectionSystem to orient directional light
/// </summary>
public class SystemStarCreator : BaseSystem
{
    private EntityStore _store;
    private bool _starCreated;

    protected override void OnAddStore(EntityStore store)
    {
        _store = store;
    }

    protected override void OnUpdateGroup()
    {
        if (_starCreated)
            return;

        Entity world = _store.GetUniqueEntity("World");
        if (!world.Tags.Has<WorldCreated>())
            return;

        CreateStar(world);
        _starCreated = true;
        Enabled = false;
    }

    private void CreateStar(Entity world)
    {
        try
        {
            GD.Print("[StarCreator] >> Creating star from config...");

            if (SolarSystemConfig.Count == 0)
            {
                GD.PrintErr("[StarCreator] No solar systems loaded!");
                return;
            }

            // Use the first system's star
            ref var sysDef = ref SolarSystemConfig.Systems[0];
            ref var starDef = ref sysDef.Star;

            Entity star = _store.CreateEntity(new UniqueEntity("Star"));

            star.AddComponent(new CelestialIdentity
            {
                Id = 1,
                Type = CelestialType.Star
            });

            star.AddComponent(new StarData
            {
                Radius      = starDef.Radius,
                Temperature = starDef.Temperature,
                Luminosity  = starDef.Luminosity
            });

            star.AddComponent(new CelestialParent { World = world });

            star.AddTag<CelestialSun>();

            GD.Print($"[StarCreator] >> Star '{starDef.Name}' created: " +
                     $"radius={starDef.Radius}m, temp={starDef.Temperature}K, lum={starDef.Luminosity}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[StarCreator] Error: {ex.Message}");
        }
    }
}
