using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Builds chunk collision from the same grid triangulation as ChunkMeshBuildSystem.
/// Supports spherical (cube→sphere) projection with tangent correction.
/// Supports all 6 faces via FaceIndex from ChunkInfo.
/// </summary>
public class ChunkCollisionBuildSystem : QuerySystem<ChunkInfo, ChunkTerrain>
{
	private EntityStore _store;
	private ArchetypeQuery<ChunkInfo, ChunkCollider> _removalQuery;

	private int[] _selectedEntityIds;
	private int[] _selectedDistances;
	private int _selectedCount;

	public int MaxPerFrame { get; set; } = 4;
	public Node ParentNode { get; set; }
	public Node3D Viewer { get; set; }

	/// <summary>Planet position in world space. Used to offset collision bodies.</summary>
	public Vector3 PlanetPosition { get; set; } = Vector3.Zero;

	/// <summary>Reference to segment creator for face resolution and orientation.</summary>
	public SystemSegmentCreator SegmentCreator { get; set; }

	/// <summary>Planet radius for spherical projection.</summary>
	public float PlanetRadius { get; set; } = 1000f;

	public ChunkCollisionBuildSystem()
		=> Filter.AllTags(Tags.Get<NeedsCollision, ChunkComplete>())
			.WithoutAnyTags(Tags.Get<PendingRemoval>());

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
		RemoveDisabledColliders(buffer);

		if (ParentNode == null || MaxPerFrame <= 0)
			return;

		BuildColliders(buffer);
	}

	private void RemoveDisabledColliders(CommandBuffer buffer)
	{
		foreach (var entity in _removalQuery.Entities)
		{
			if (!entity.TryGetComponent<ChunkCollider>(out var collider))
				continue;

			collider.GetBody()?.QueueFree();
			buffer.RemoveComponent<ChunkCollider>(entity.Id);
		}
	}

	private void BuildColliders(CommandBuffer buffer)
	{
		// Use local viewer position relative to planet for chunk coordinate selection
		Vector3 localViewerPos = Viewer != null
			? Viewer.GlobalPosition - PlanetPosition
			: Vector3.Zero;

		(int centerX, int centerZ) = Viewer != null
			? NearestChunkSelectionTool.GetViewerChunkCoords(localViewerPos, ChunkConstants.CHUNK_WORLD_SIZE)
			: (0, 0);

		NearestChunkSelectionTool.EnsureCapacity(ref _selectedEntityIds, ref _selectedDistances, MaxPerFrame);
		_selectedCount = 0;

		foreach (var entity in Query.Entities)
		{
			if (entity.HasComponent<ChunkCollider>())
				continue;

			ref var info = ref entity.GetComponent<ChunkInfo>();
			int dist = Viewer != null
				? Math.Max(Math.Abs(info.X - centerX), Math.Abs(info.Z - centerZ))
				: 0;

			NearestChunkSelectionTool.TryInsertNearest(
				ref _selectedCount,
				_selectedEntityIds,
				_selectedDistances,
				entity.Id,
				dist,
				MaxPerFrame);
		}

		for (int i = 0; i < _selectedCount; i++)
		{
			int entityId = _selectedEntityIds[i];
			if (!_store.TryGetEntityById(entityId, out var entity) || entity.IsNull)
				continue;

			if (entity.HasComponent<ChunkCollider>())
				continue;

			try
			{
				ref var info = ref entity.GetComponent<ChunkInfo>();
				ref var terrain = ref entity.GetComponent<ChunkTerrain>();

				// Use FaceIndex to get the correct face orientation
				FaceOrientation? faceOrientation = SegmentCreator?.GetFaceOrientation(info.FaceIndex);
				int segmentsPerSide = SegmentCreator?.SegmentsPerSide ?? 1;

				StaticBody3D body;
				if (faceOrientation.HasValue)
				{
					FaceOrientation orientation = faceOrientation.Value;
					body = BuildSphericalCollisionBody(terrain.Data, ref info, ref orientation, segmentsPerSide);
				}
				else
				{
					body = BuildCollisionBody(terrain.Data, info);
				}

				if (body != null)
					buffer.AddComponent(entityId, new ChunkCollider { BodyId = body.GetInstanceId() });
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ChunkCollisionBuildSystem] Error building collider for entity {entityId}: {ex.Message}");
			}
		}
	}

	private StaticBody3D BuildCollisionBody(byte[] data, ChunkInfo info)
	{
		if (data == null || data.Length < ChunkConstants.CHUNK_DATA_SIZE)
			return null;

		List<Vector3> faces = BuildFaces(data);
		if (faces.Count == 0)
			return null;

		ConcavePolygonShape3D shape = new ConcavePolygonShape3D();
		shape.SetFaces(faces.ToArray());

		CollisionShape3D collisionShape = new CollisionShape3D
		{
			Shape = shape,
			Name = $"Collision_{info.X}_{info.Z}_F{info.FaceIndex}"
		};

		StaticBody3D body = new StaticBody3D
		{
			Name = $"ChunkBody_{info.X}_{info.Z}_F{info.FaceIndex}",
			CollisionLayer = 1,
			CollisionMask = 1,
			Position = new Vector3(
				info.X * ChunkConstants.CHUNK_WORLD_SIZE,
				0,
				info.Z * ChunkConstants.CHUNK_WORLD_SIZE)
		};

		body.AddChild(collisionShape);
		ParentNode.AddChild(body);
		return body;
	}

	/// <summary>
	/// Builds collision body with spherical (cube→sphere) projection.
	/// </summary>
	private StaticBody3D BuildSphericalCollisionBody(byte[] data, ref ChunkInfo info, ref FaceOrientation orientation, int segmentsPerSide)
	{
		if (data == null || data.Length < ChunkConstants.CHUNK_DATA_SIZE)
			return null;

		List<Vector3> faces = BuildSphericalFaces(data, ref info, ref orientation, segmentsPerSide, PlanetRadius);
		if (faces.Count == 0)
			return null;

		ConcavePolygonShape3D shape = new ConcavePolygonShape3D();
		shape.SetFaces(faces.ToArray());

		CollisionShape3D collisionShape = new CollisionShape3D
		{
			Shape = shape,
			Name = $"Collision_{info.X}_{info.Z}_F{info.FaceIndex}"
		};

		StaticBody3D body = new StaticBody3D
		{
			Name = $"ChunkBody_{info.X}_{info.Z}_F{info.FaceIndex}",
			CollisionLayer = 1,
			CollisionMask = 1,
			Position = PlanetPosition // Offset to planet position in world space
		};

		body.AddChild(collisionShape);
		ParentNode.AddChild(body);
		return body;
	}

	private static List<Vector3> BuildFaces(byte[] data)
	{
		int size = ChunkConstants.CHUNK_SIZE;
		float step = ChunkConstants.TILE_SIZE;
		float heightStep = ChunkConstants.TILE_HEIGHT;

		List<Vector3> faces = new List<Vector3>(size * size * 6);

		for (int z = 0; z < size; z++)
		{
			for (int x = 0; x < size; x++)
			{
				int hNW = GetHeight(data, x, z);
				int hNE = GetHeight(data, x + 1, z);
				int hSW = GetHeight(data, x, z + 1);
				int hSE = GetHeight(data, x + 1, z + 1);

				Vector3 nw = new Vector3(x * step, hNW * heightStep, z * step);
				Vector3 ne = new Vector3((x + 1) * step, hNE * heightStep, z * step);
				Vector3 sw = new Vector3(x * step, hSW * heightStep, (z + 1) * step);
				Vector3 se = new Vector3((x + 1) * step, hSE * heightStep, (z + 1) * step);

				AddFace(faces, nw, ne, se);
				AddFace(faces, nw, se, sw);
			}
		}

		return faces;
	}

	/// <summary>
	/// Builds collision faces with spherical (cube→sphere) projection.
	/// </summary>
	private static List<Vector3> BuildSphericalFaces(byte[] data, ref ChunkInfo info, ref FaceOrientation orientation, int segmentsPerSide, float radius)
	{
		int size = ChunkConstants.CHUNK_SIZE;
		float heightStep = ChunkConstants.TILE_HEIGHT;
		int faceResolution = CubeSphereProjection.GetFaceResolution(segmentsPerSide);

		List<Vector3> faces = new List<Vector3>(size * size * 6);

		for (int z = 0; z < size; z++)
		{
			for (int x = 0; x < size; x++)
			{
				int hNW = GetHeight(data, x, z);
				int hNE = GetHeight(data, x + 1, z);
				int hSW = GetHeight(data, x, z + 1);
				int hSE = GetHeight(data, x + 1, z + 1);

				// Compute global vertex coordinates on the face
				var (gxNW, gzNW) = CubeSphereProjection.GetGlobalVertexCoords(info.X, info.Z, x, z, segmentsPerSide);
				var (gxNE, gzNE) = CubeSphereProjection.GetGlobalVertexCoords(info.X, info.Z, x + 1, z, segmentsPerSide);
				var (gxSW, gzSW) = CubeSphereProjection.GetGlobalVertexCoords(info.X, info.Z, x, z + 1, segmentsPerSide);
				var (gxSE, gzSE) = CubeSphereProjection.GetGlobalVertexCoords(info.X, info.Z, x + 1, z + 1, segmentsPerSide);

				// Project onto sphere with height offset
				Vector3 nw = CubeSphereProjection.GetSpherePointWithHeight(gxNW, gzNW, faceResolution, orientation, radius, hNW * heightStep);
				Vector3 ne = CubeSphereProjection.GetSpherePointWithHeight(gxNE, gzNE, faceResolution, orientation, radius, hNE * heightStep);
				Vector3 sw = CubeSphereProjection.GetSpherePointWithHeight(gxSW, gzSW, faceResolution, orientation, radius, hSW * heightStep);
				Vector3 se = CubeSphereProjection.GetSpherePointWithHeight(gxSE, gzSE, faceResolution, orientation, radius, hSE * heightStep);

				AddFace(faces, nw, ne, se);
				AddFace(faces, nw, se, sw);
			}
		}

		return faces;
	}

	private static void AddFace(List<Vector3> faces, Vector3 a, Vector3 b, Vector3 c)
	{
		Vector3 n = (b - a).Cross(c - a);
		if (n.LengthSquared() < 0.000001f)
			return;

		faces.Add(a);
		faces.Add(b);
		faces.Add(c);
	}

	private static int GetHeight(byte[] data, int x, int z)
	{
		int vertexSize = ChunkConstants.CHUNK_VERTEX_SIZE;
		x = Math.Clamp(x, 0, vertexSize - 1);
		z = Math.Clamp(z, 0, vertexSize - 1);
		int idx = ChunkConstants.HEIGHTS_OFFSET + z * vertexSize + x;
		return data[idx];
	}
}
