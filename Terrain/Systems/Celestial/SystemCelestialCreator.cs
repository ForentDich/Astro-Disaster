using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.IO;

public class SystemCelestialCreator : BaseSystem
{
	private EntityStore _store;

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
			
			celestial.AddComponent(new CelestialGeometry
			{
				Radius = ConstantsCelestial.BASE_RADIUS
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
			Vector3.Up,       
			Vector3.Down,     
			Vector3.Right,    
			Vector3.Left,     
			Vector3.Forward,  
			Vector3.Back      
		};


		Vector3[] localPositions = new Vector3[]
		{
			new Vector3(0, radius, 0),     
			new Vector3(0, -radius, 0),    
			new Vector3(radius, 0, 0),     
			new Vector3(-radius, 0, 0),    
			new Vector3(0, 0, radius),     
			new Vector3(0, 0, -radius)     
		};


		Vector3[] localUpVectors = new Vector3[]
		{
			Vector3.Forward,  
			Vector3.Forward,  
			Vector3.Up,       
			Vector3.Up,       
			Vector3.Up,       
			Vector3.Up        
		};

		string[] faceNames = new string[]
		{
			"Top", "Bottom", "Right", "Left", "Front", "Back"
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

		Vector3 worldRight = worldUp.Cross(worldNormal).Normalized();

		Entity face = _store.CreateEntity(new UniqueEntity($"{celestial.Id}_Face_{faceIndex}"));
		
		face.AddComponent(new FaceIdentity { Index = faceIndex });
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
