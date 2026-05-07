using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.IO;

/// <summary>
/// Creates celestial bodies (planets, moons) from SolarSystemConfig.
///
/// Pipeline position: after SystemStarCreator.
///
/// Reads the first solar system from config and creates all its bodies.
/// Each body gets: identity, geometry, transform, orbit, atmosphere, surface,
/// gravity source, and face entities.
/// </summary>
public class SystemCelestialCreator : BaseSystem
{
    private EntityStore _store;
    private bool _celestialCreated;

    protected override void OnAddStore(EntityStore store)
    {
        _store = store;
    }

    protected override void OnUpdateGroup()
    {
        if (_celestialCreated)
            return;

        Entity world = _store.GetUniqueEntity("World");
        if (!world.Tags.Has<WorldNeedsCelestial>())
            return;

        CreateCelestialsForWorld(world);
        world.RemoveTag<WorldNeedsCelestial>();
        _celestialCreated = true;
        Enabled = false;
    }

    private void CreateCelestialsForWorld(Entity world)
    {
        try
        {
            GD.Print("[CelestialCreator] >> Creating celestials from config...");

            if (SolarSystemConfig.Count == 0)
            {
                GD.PrintErr("[CelestialCreator] No solar systems loaded!");
                return;
            }

            ref var sysDef = ref SolarSystemConfig.Systems[0];
            ref var worldData = ref world.GetComponent<WorldData>();

            for (int i = 0; i < sysDef.Bodies.Length; i++)
            {
                ref var bodyDef = ref sysDef.Bodies[i];
                CreateBody(world, ref worldData, ref bodyDef, i);
            }

            GD.Print($"[CelestialCreator] >> Created {sysDef.Bodies.Length} celestial body(ies)");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[CelestialCreator] Error: {ex.Message}");
        }
    }

    private void CreateBody(Entity world, ref WorldData worldData, ref SolarSystemConfig.BodyDef bodyDef, int index)
    {
        int celestialId = GenerateCelestialId(worldData, index);
        string celestialPath = Path.Combine(worldData.SavePath, $"Celestial_{celestialId}");

        // Create folder if needed
        string absPath = ProjectSettings.GlobalizePath(celestialPath);
        if (!DirAccess.DirExistsAbsolute(absPath))
        {
            CreateFolder(celestialPath);
        }

        Entity celestial = _store.CreateEntity(new UniqueEntity($"Celestial_{celestialId}"));

        // ── Identity ──
        CelestialType type = bodyDef.Type switch
        {
            "Moon" => CelestialType.Moon,
            _ => CelestialType.Planet
        };

        celestial.AddComponent(new CelestialIdentity
        {
            Id = celestialId,
            Type = type
        });

        // ── Geometry ──
        float radius = ConstantsCelestial.ComputeRadius(bodyDef.SegmentsPerSide);
        celestial.AddComponent(new CelestialGeometry { Radius = radius });

        // ── Initial position (on orbit) ──
        float angleRad = Mathf.DegToRad(bodyDef.Orbit.InitialAngle);
        Vector3 startPos = new Vector3(
            bodyDef.Orbit.Distance * Mathf.Cos(angleRad),
            0f,
            bodyDef.Orbit.Distance * Mathf.Sin(angleRad)
        );



        celestial.AddComponent(new CelestialTransform
        {
            Position = startPos,
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        });

        // ── Status (gravity) ──
        celestial.AddComponent(new CelestialStatus { Gravity = bodyDef.Gravity });

        // ── Gravity source ──
        celestial.AddComponent(new GravitySource
        {
            Center = startPos,
            Radius = radius,
            GM = bodyDef.Gravity * radius * radius
        });

        // ── Orbit ──
        celestial.AddComponent(new OrbitData
        {
            Distance = bodyDef.Orbit.Distance,
            Speed = bodyDef.Orbit.Speed,
            InitialAngle = angleRad,
            CurrentAngle = angleRad,
            AxialTilt = bodyDef.Orbit.AxialTilt
        });

        // ── Atmosphere ──
        if (bodyDef.Atmosphere.Enabled)
        {
            celestial.AddComponent(new AtmosphereData
            {
                Height = bodyDef.Atmosphere.Height,
                Color = bodyDef.Atmosphere.Color,
                Density = bodyDef.Atmosphere.Density
            });
            celestial.AddTag<CelestialHasAtmosphere>();
        }

        // ── Surface ──
        celestial.AddComponent(new SurfaceData
        {
            Seed = bodyDef.Surface.Seed,
            RotationSpeed = bodyDef.Surface.RotationSpeed
        });

        // ── Parent ──
        celestial.AddComponent(new CelestialParent { World = world });

        // ── Tags ──
        celestial.AddTag<CelestialActive>();
        if (type == CelestialType.Planet)
            celestial.AddTag<CelestialPlanet>();
        else if (type == CelestialType.Moon)
            celestial.AddTag<CelestialMoon>();


        celestial.AddComponent(new PlanetProxySettings
        {
            Enabled = true,
            ResolutionDiv = 4,
            InnerRadius = 0f,
            OuterRadius = 0f,
            ProxySink = 8.0f,
            ProxyDiscardRadius = 250.0f
        });

        celestial.AddTag<CelestialNeedsFaces>();

        // ── Faces ──
        CreateFacesForCelestial(celestial, bodyDef.SegmentsPerSide);

        celestial.RemoveTag<CelestialNeedsFaces>();
        celestial.AddTag<CelestialHasFaces>();

        GD.Print($"[CelestialCreator] >> '{bodyDef.Name}' (id={celestialId}, type={type}) created: " +
                 $"radius={radius:F1}m, pos=({startPos.X:F0}, {startPos.Y:F0}, {startPos.Z:F0})");
    }

    private void CreateFacesForCelestial(Entity celestial, int segmentsPerSide)
    {
        ref var celestialTransform = ref celestial.GetComponent<CelestialTransform>();
        ref var celestialGeometry = ref celestial.GetComponent<CelestialGeometry>();

        float radius = celestialGeometry.Radius;
        Basis planetBasis = new Basis(celestialTransform.Rotation);
        Vector3 planetPosition = celestialTransform.Position;

        Vector3[] localNormals = new Vector3[]
        {
            Vector3.Forward,  // 0: Front
            Vector3.Right,    // 1: Right
            Vector3.Back,     // 2: Back
            Vector3.Left,     // 3: Left
            Vector3.Up,       // 4: Top
            Vector3.Down      // 5: Bottom
        };

        Vector3[] localPositions = new Vector3[]
        {
            new Vector3(0, 0, -radius),     // 0: Front
            new Vector3(radius, 0, 0),      // 1: Right
            new Vector3(0, 0, radius),      // 2: Back
            new Vector3(-radius, 0, 0),     // 3: Left
            new Vector3(0, radius, 0),      // 4: Top
            new Vector3(0, -radius, 0)      // 5: Bottom
        };

        Vector3[] localUpVectors = new Vector3[]
        {
            Vector3.Up,       // 0: Front
            Vector3.Up,       // 1: Right
            Vector3.Up,       // 2: Back
            Vector3.Up,       // 3: Left
            Vector3.Back,     // 4: Top
            Vector3.Back      // 5: Bottom
        };

        string[] faceNames = new string[]
        {
            "Front", "Right", "Back", "Left", "Top", "Bottom"
        };

        for (int i = 0; i < ConstantsCelestial.FACE_COUNT; i++)
        {
            Vector3 worldPosition = planetPosition + planetBasis * localPositions[i];
            Vector3 worldNormal = planetBasis * localNormals[i];
            Vector3 worldUp = planetBasis * localUpVectors[i];

            CreateFaceEntity(celestial, i, worldPosition, worldNormal, worldUp, faceNames[i], segmentsPerSide);
        }
    }

    private void CreateFaceEntity(Entity celestial, int faceIndex,
                                   Vector3 worldPosition, Vector3 worldNormal,
                                   Vector3 worldUp, string faceName, int segmentsPerSide)
    {
        ref var celestialIdentity = ref celestial.GetComponent<CelestialIdentity>();
        string celestialPath = Path.Combine(
            celestial.GetComponent<CelestialParent>().World.GetComponent<WorldData>().SavePath,
            $"Celestial_{celestialIdentity.Id}",
            $"Face_{faceIndex}"
        );

        string absFacePath = ProjectSettings.GlobalizePath(celestialPath);
        if (!DirAccess.DirExistsAbsolute(absFacePath))
            CreateFolder(celestialPath);

        Vector3 worldRight = worldNormal.Cross(worldUp).Normalized();

        Entity face = _store.CreateEntity(new UniqueEntity($"{celestial.Id}_Face_{faceIndex}"));

        face.AddComponent(new FaceIdentity { Index = faceIndex, SegmentsPerSide = segmentsPerSide });
        face.AddComponent(new FaceName { Value = faceName });
        face.AddComponent(new FacePosition { WorldPosition = worldPosition });
        face.AddComponent(new FaceOrientation
        {
            Normal = worldNormal,
            Up = worldUp,
            Right = worldRight
        });
        face.AddComponent(new FaceStorage { SavePath = celestialPath });
        face.AddComponent(new FaceParent { Celestial = celestial });

        face.AddTag<FaceCreated>();
        face.AddTag<FaceNeedsSegments>();
    }

    private void CreateFolder(string path)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        if (DirAccess.MakeDirRecursiveAbsolute(absolutePath) == Error.Ok)
            GD.Print($"[CelestialCreator] Folder created: {absolutePath}");
        else
            GD.PrintErr($"[CelestialCreator] Failed to create folder: {absolutePath}");
    }

    private int GenerateCelestialId(WorldData worldData, int bodyIndex)
    {
        unchecked
        {
            int h = worldData.Seed * 31 + 7 + bodyIndex * 13;
            return (h ^ worldData.WorldId) & 0x7FFFFFFF;
        }
    }
}
