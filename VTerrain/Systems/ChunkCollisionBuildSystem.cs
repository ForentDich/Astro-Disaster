using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.Collections.Generic;

public class ChunkCollisionBuildSystem : QuerySystem<ChunkInfo, ChunkTerrain>
{
	public int MaxPerFrame { get; set; } = 4;
	public Node ParentNode { get; set; }
	public Node3D Viewer { get; set; }
	public bool DebugCollision { get; set; } = true; 
	
	private EntityStore _store;
	private int[] _selectedEntityIds;
	private int[] _selectedDistances;
	private int _selectedCount;

	private ArchetypeQuery<ChunkInfo, ChunkCollider> _removalQuery;
	public ChunkCollisionBuildSystem() => Filter.AllTags(Tags.Get<NeedsCollision, ChunkComplete>());

	protected override void OnAddStore(EntityStore store)
	{
		base.OnAddStore(store);
		_store = store;

		_removalQuery = store.Query<ChunkInfo, ChunkCollider>()
			.WithoutAnyTags(Tags.Get<NeedsCollision>());
	}

	protected override void OnUpdate()
	{
		var buffer = CommandBuffer;

		RemoveOutOfRangeCollisions(buffer);


		if (MaxPerFrame > 0 && ParentNode != null)
			BuildNewCollisions(buffer);
	}

	private void RemoveOutOfRangeCollisions(CommandBuffer buffer)
	{
		foreach (var entity in _removalQuery.Entities)
		{
			if (!entity.TryGetComponent<ChunkCollider>(out var collider))
				continue;

			var body = collider.GetBody();
			body?.QueueFree();

			buffer.RemoveComponent<ChunkCollider>(entity.Id);
		}
	}

	private void BuildNewCollisions(CommandBuffer buffer)
	{
		if (Viewer == null) return;

		(int centerX, int centerZ) = NearestChunkSelectionTool.GetViewerChunkCoords(Viewer, ChunkConstants.ChunkSize);

		NearestChunkSelectionTool.EnsureCapacity(ref _selectedEntityIds, ref _selectedDistances, MaxPerFrame);
		_selectedCount = 0;

		foreach (var entity in Query.Entities)
		{
			if (!entity.HasComponent<ChunkMesh>())
				continue;

			ref var info = ref entity.GetComponent<ChunkInfo>();
			int dist = Math.Max(Math.Abs(info.X - centerX), Math.Abs(info.Z - centerZ));
			NearestChunkSelectionTool.TryInsertNearest(ref _selectedCount, _selectedEntityIds, _selectedDistances, entity.Id, dist, MaxPerFrame);
		}

		for (int i = 0; i < _selectedCount; i++)
		{
			int entityId = _selectedEntityIds[i];

			if (!_store.TryGetEntityById(entityId, out var entity) || entity.IsNull)
				continue;

			// Если уже есть коллизия - пропускаем
			if (entity.HasComponent<ChunkCollider>())
				continue;

			// Дополнительная проверка наличия меша
			if (!entity.HasComponent<ChunkMesh>())
				continue;

			try
			{
				ref var info = ref entity.GetComponent<ChunkInfo>();
				ref var terrain = ref entity.GetComponent<ChunkTerrain>();

				var body = BuildTileCollisionBody(terrain.Data, info);
				
				if (body != null)
				{
					buffer.AddComponent(entityId, new ChunkCollider { BodyId = body.GetInstanceId() });
					
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ChunkCollisionBuildSystem] >> Error building collision: {ex}");
			}
		}
	}

	private StaticBody3D BuildTileCollisionBody(byte[] terrainData, ChunkInfo info)
	{
		if (ParentNode == null) 
		{
			GD.PrintErr("[ChunkCollisionBuildSystem] ParentNode is null!");
			return null;
		}

		int size = ChunkConstants.ChunkSize;
		var triangles = new List<Vector3>();


		for (int z = 0; z < size; z++)
		{
			for (int x = 0; x < size; x++)
			{
				int tileIndex = z * size + x;
				int dataOffset = tileIndex * 2;
				
				int baseHeight = terrainData[dataOffset];
				TileType tileType = (TileType)terrainData[dataOffset + 1];
				
				Vector3[] tileVertices = TileMeshes.GetVertices(tileType);
				Vector3 tileOffset = new Vector3(x, baseHeight, z);
				
				for (int v = 0; v < tileVertices.Length; v++)
				{
					triangles.Add(tileVertices[v] + tileOffset);
				}
			}
		}

		var concaveShape = new ConcavePolygonShape3D();
		concaveShape.SetFaces(triangles.ToArray());

		var collisionShape = new CollisionShape3D
		{
			Shape = concaveShape,
			Name = $"Collision_{info.X}_{info.Z}"
		};

		var staticBody = new StaticBody3D
		{
			Name = $"ChunkBody_{info.X}_{info.Z}",
			CollisionLayer = 1,
			CollisionMask = 1
		};

		staticBody.AddChild(collisionShape);
		staticBody.Position = new Vector3(info.X * size, 0, info.Z * size);

		if (DebugCollision)
		{
			AddCollisionDebugVisual(staticBody, triangles, info);
		}

		ParentNode.AddChild(staticBody);
		return staticBody;
	}

	private void AddCollisionDebugVisual(StaticBody3D body, List<Vector3> triangles, ChunkInfo info)
	{
		var surfaceTool = new SurfaceTool();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
		
		for (int i = 0; i < triangles.Count; i += 3)
		{
			if (i + 2 >= triangles.Count) break;
			
			surfaceTool.AddVertex(triangles[i]);
			surfaceTool.AddVertex(triangles[i + 1]);
			surfaceTool.AddVertex(triangles[i + 2]);
		}
		
		var material = new StandardMaterial3D();
		material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		material.AlbedoColor = new Color(1, 1, 0, 0.2f);
		material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		
		surfaceTool.SetMaterial(material);
		var debugMesh = surfaceTool.Commit();
		
		var meshInstance = new MeshInstance3D
		{
			Mesh = debugMesh,
			Name = $"DebugCollision_{info.X}_{info.Z}",
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		
		body.AddChild(meshInstance);
	}
}
