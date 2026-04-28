using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.IO;

public class SystemCelestialCreator : BaseSystem
{
	private EntityStore _store;

	/// <summary>Number of segments along one side of each face (1, 3, 5...). Must be odd.</summary>
	public int SegmentsPerSide { get; set; } = 1;

	protected override void OnAddStore(EntityStore store)
	{
		_store = store;
	}

	protected override void OnUpdateGroup()
	{
		Entity world = _store.GetUniqueEntity("World");

		if (!world.Tags.Has<WorldNeedsCelestial>())
			return;

		_CreateCelestialForWorld(world);
		world.RemoveTag<WorldNeedsCelestial>();
	}

	private void _CreateCelestialForWorld(Entity world)
	{
		try
		{
			GD.Print("[CelestialCreator] >> Creating celestial for world...");

			ref var worldData = ref world.GetComponent<WorldData>();
			int celestialId = _GenerateCelestialId(worldData);
			
			string celestialPath = Path.Combine(worldData.SavePath, $"Celestial_{celestialId}");

			// Only create folder if it doesn't exist yet
			string absPath = ProjectSettings.GlobalizePath(celestialPath);
			if (!DirAccess.DirExistsAbsolute(absPath))
			{
				_CreateCelestialFolder(celestialPath);
				GD.Print($"[CelestialCreator] >> New celestial folder created");
			}
			else
			{
				GD.Print($"[CelestialCreator] >> Celestial folder already exists, reusing");
			}

			Entity celestial = _store.CreateEntity(new UniqueEntity($"Celestial_{celestialId}"));

			celestial.AddComponent(new CelestialIdentity 
			{ 
				Id = celestialId,
				Type = CelestialType.Planet
			});
			
			float radius = ConstantsCelestial.ComputeRadius(SegmentsPerSide);
			celestial.AddComponent(new CelestialGeometry
			{
				Radius = radius
			});
			
			celestial.AddComponent(new CelestialTransform
			{
				Position = Vector3.Zero,
				Rotation = Quaternion.Identity,
				Scale = Vector3.One
			});
			
			celestial.AddComponent(new CelestialStatus
			{
				Gravity = 9.8f
			});
			
			celestial.AddComponent(new CelestialParent { World = world });

			celestial.AddTag<CelestialActive>();
			celestial.AddTag<CelestialPlanet>(); 
			celestial.AddTag<CelestialHasAtmosphere>(); 
			celestial.AddTag<CelestialNeedsFaces>();

			_CreateFacesForCelestial(celestial);

			celestial.RemoveTag<CelestialNeedsFaces>();
			celestial.AddTag<CelestialHasFaces>();

			GD.Print($"[CelestialCreator] >> Celestial '{celestialId}' created");
		}
		catch(Exception ex)
		{
			GD.PrintErr($"[CelestialCreator] Error: {ex.Message}");
		}
	}

	private void _CreateFacesForCelestial(Entity celestial)
	{
		ref var celestialTransform = ref celestial.GetComponent<CelestialTransform>();
		ref var celestialGeometry = ref celestial.GetComponent<CelestialGeometry>();
		
		float radius = celestialGeometry.Radius;
		Basis planetBasis = new Basis(celestialTransform.Rotation);
		Vector3 planetPosition = celestialTransform.Position;
		

		Vector3[] localNormals = new Vector3[]
		{
			Vector3.Forward,  // 0: Front (face_0 — боковая грань, чтобы XZ координаты работали)
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
			Vector3.Up,       // 0: Front — Forward × Up = Right ✓
			Vector3.Up,       // 1: Right — Right × Up = Back
			Vector3.Up,       // 2: Back — Back × Up = Left
			Vector3.Up,       // 3: Left — Left × Up = Forward
			Vector3.Back,     // 4: Top — Up × Back = Right ✓
			Vector3.Back      // 5: Bottom — Down × Back = Right ✓
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
			
			_CreateFaceEntity(celestial, i, worldPosition, worldNormal, worldUp, faceNames[i]);
		}
	}

	private void _CreateFaceEntity(Entity celestial, int faceIndex, 
							   Vector3 worldPosition, Vector3 worldNormal, 
							   Vector3 worldUp, string faceName)
	{
		ref var celestialIdentity = ref celestial.GetComponent<CelestialIdentity>();
		string celestialPath = Path.Combine(
			celestial.GetComponent<CelestialParent>().World.GetComponent<WorldData>().SavePath,
			$"Celestial_{celestialIdentity.Id}",
			$"Face_{faceIndex}"
		);

		// Only create folder if it doesn't exist yet
		string absFacePath = ProjectSettings.GlobalizePath(celestialPath);
		if (!DirAccess.DirExistsAbsolute(absFacePath))
			_CreateFaceFolder(celestialPath);

		// Right = Normal × Up gives the correct tangent direction for the face.
		// This ensures the face's local X axis points in the right direction
		// for the cube→sphere projection.
		Vector3 worldRight = worldNormal.Cross(worldUp).Normalized();

		Entity face = _store.CreateEntity(new UniqueEntity($"{celestial.Id}_Face_{faceIndex}"));
		
		face.AddComponent(new FaceIdentity { Index = faceIndex, SegmentsPerSide = SegmentsPerSide });
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

		GD.Print($"[CelestialCreator] Created face {faceName} at {worldPosition}");
	}

	private void _CreateCelestialFolder(string path)
	{
		string absolutePath = ProjectSettings.GlobalizePath(path);
		
		if (DirAccess.MakeDirRecursiveAbsolute(absolutePath) == Error.Ok)
		{
			GD.Print($"[CelestialCreator] Celestial folder created: {absolutePath}");
		}
		else
		{
			GD.PrintErr($"[CelestialCreator] Failed to create celestial folder: {absolutePath}");
		}
	}

	private void _CreateFaceFolder(string path)
	{
		string absolutePath = ProjectSettings.GlobalizePath(path);
		
		if (DirAccess.MakeDirRecursiveAbsolute(absolutePath) == Error.Ok)
		{
			GD.Print($"[CelestialCreator] Face folder created: {absolutePath}");
		}
		else
		{
			GD.PrintErr($"[CelestialCreator] Failed to create face folder: {absolutePath}");
		}
	}

	private int _GenerateCelestialId(WorldData worldData)
	{
		// Deterministic: same world seed → same celestial ID, always
		unchecked
		{
			int h = worldData.Seed * 31 + 7;
			return (h ^ worldData.WorldId) & 0x7FFFFFFF;
		}
	}
}
