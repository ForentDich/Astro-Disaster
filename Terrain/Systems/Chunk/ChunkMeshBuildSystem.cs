using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Builds chunk mesh from grid-cell heights with spherical (cube→sphere) projection.
/// Uses tangent (S2) method for uniform spherical distribution.
/// Supports all 6 faces via FaceIndex from ChunkInfo.
/// </summary>
public class ChunkMeshBuildSystem : QuerySystem<ChunkInfo, ChunkTerrain>
{
	private static StandardMaterial3D _fallbackMaterial;

	private EntityStore _store;
	private int[] _selectedEntityIds;
	private int[] _selectedDistances;
	private int _selectedCount;

	public Material TerrainMaterial { get; set; }
	public int MaxPerFrame { get; set; } = 2;
	public Node ParentNode { get; set; }
	public Node3D Viewer { get; set; }

	/// <summary>Planet position in world space. Used to offset mesh instances.</summary>
	public Vector3 PlanetPosition { get; set; } = Vector3.Zero;

	/// <summary>Reference to segment creator for face resolution and orientation.</summary>
	public SystemSegmentCreator SegmentCreator { get; set; }

	/// <summary>Planet radius for spherical projection.</summary>
	public float PlanetRadius { get; set; } = 1000f;

	/// <summary>Height scale multiplier for terrain elevation.</summary>
	public float HeightScale { get; set; } = 1.73f;

	public ChunkMeshBuildSystem()
		=> Filter.AllTags(Tags.Get<ChunkDataReady>())
			.WithoutAnyTags(Tags.Get<PendingRemoval>());

	protected override void OnAddStore(EntityStore store)
	{
		base.OnAddStore(store);
		_store = store;
	}

	protected override void OnUpdate()
	{
		if (ParentNode == null || MaxPerFrame <= 0)
			return;

		var commandBuffer = CommandBuffer;

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
			if (entity.Tags.Has<PendingRemoval>())
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

			if (entity.Tags.Has<PendingRemoval>())
				continue;

			try
			{
				ref var info = ref entity.GetComponent<ChunkInfo>();
				ref var terrain = ref entity.GetComponent<ChunkTerrain>();

				// Use FaceIndex to get the correct face orientation
				FaceOrientation? faceOrientation = SegmentCreator?.GetFaceOrientation(info.FaceIndex);
				int segmentsPerSide = SegmentCreator?.SegmentsPerSide ?? 1;

				Mesh mesh;
				if (faceOrientation.HasValue)
				{
					FaceOrientation orientation = faceOrientation.Value;
					mesh = BuildSphericalGridMesh(terrain.Data, ref info, ref orientation, segmentsPerSide);
				}
				else
				{
					mesh = BuildGridMesh(terrain.Data);
				}

				if (mesh == null)
					continue;

				if (entity.TryGetComponent<ChunkMesh>(out var chunkMesh))
				{
					var existing = chunkMesh.GetMesh();
					if (existing != null)
					{
					existing.Mesh = mesh;
					existing.Name = $"Chunk_{info.X}_{info.Z}_F{info.FaceIndex}";
					existing.Position = PlanetPosition; // Offset to planet position in world space
					}
					else
					{
						MeshInstance3D meshInstance = CreateMeshInstance(mesh, info);
						commandBuffer.AddComponent(entityId, new ChunkMesh { InstaceId = meshInstance.GetInstanceId() });
					}
				}
				else
				{
					MeshInstance3D meshInstance = CreateMeshInstance(mesh, info);
					commandBuffer.AddComponent(entityId, new ChunkMesh { InstaceId = meshInstance.GetInstanceId() });
				}

				if (entity.TryGetComponent<ChunkCollider>(out var collider))
				{
					collider.GetBody()?.QueueFree();
					commandBuffer.RemoveComponent<ChunkCollider>(entityId);
				}

				commandBuffer.RemoveTag<ChunkDataReady>(entityId);
				commandBuffer.RemoveTag<NeedsMeshUpdate>(entityId);
				commandBuffer.RemoveTag<ChunkError>(entityId);
				commandBuffer.AddTag<ChunkComplete>(entityId);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ChunkMeshBuildSystem] Error building chunk mesh for {entityId}: {ex.Message}");
				commandBuffer.RemoveTag<ChunkDataReady>(entityId);
				commandBuffer.AddTag<ChunkError>(entityId);
			}
		}
	}

	/// <summary>
	/// Builds a flat (non-spherical) grid mesh — fallback when no face orientation is available.
	/// </summary>
	private Mesh BuildGridMesh(byte[] data)
	{
		if (data == null || data.Length < ChunkConstants.CHUNK_DATA_SIZE)
			return null;

		int size = ChunkConstants.CHUNK_SIZE;
		float step = ChunkConstants.TILE_SIZE;
		float heightStep = ChunkConstants.TILE_HEIGHT;

		SurfaceTool st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		st.SetSmoothGroup(uint.MaxValue);

		for (int z = 0; z < size; z++)
		{
			for (int x = 0; x < size; x++)
			{
				int hNW = GetHeight(data, x, z);
				int hNE = GetHeight(data, x + 1, z);
				int hSW = GetHeight(data, x, z + 1);
				int hSE = GetHeight(data, x + 1, z + 1);

				byte surfaceId = GetSurface(data, x, z);
				Color color = GetSurfaceColor(surfaceId);

				Vector3 nw = new Vector3(x * step, hNW * heightStep, z * step);
				Vector3 ne = new Vector3((x + 1) * step, hNE * heightStep, z * step);
				Vector3 sw = new Vector3(x * step, hSW * heightStep, (z + 1) * step);
				Vector3 se = new Vector3((x + 1) * step, hSE * heightStep, (z + 1) * step);
				Vector2 uvNW = new Vector2(0f, 0f);
				Vector2 uvNE = new Vector2(1f, 0f);
				Vector2 uvSW = new Vector2(0f, 1f);
				Vector2 uvSE = new Vector2(1f, 1f);

				AddTriangle(st, nw, ne, se, uvNW, uvNE, uvSE, surfaceId, color);
				AddTriangle(st, nw, se, sw, uvNW, uvSE, uvSW, surfaceId, color);
			}
		}

		ArrayMesh mesh = st.Commit();
		if (mesh == null || mesh.GetSurfaceCount() == 0)
			return null;

		Material material = TerrainMaterial ?? GetFallbackMaterial();
		if (material != null)
			mesh.SurfaceSetMaterial(0, material);

		return mesh;
	}

	/// <summary>
	/// Builds a spherical grid mesh using cube→sphere projection with tangent correction.
	/// </summary>
	private Mesh BuildSphericalGridMesh(byte[] data, ref ChunkInfo info, ref FaceOrientation orientation, int segmentsPerSide)
	{
		if (data == null || data.Length < ChunkConstants.CHUNK_DATA_SIZE)
			return null;

		int size = ChunkConstants.CHUNK_SIZE;
		float heightStep = ChunkConstants.TILE_HEIGHT;
		int faceResolution = CubeSphereProjection.GetFaceResolution(segmentsPerSide);

		SurfaceTool st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		st.SetSmoothGroup(uint.MaxValue);

		for (int z = 0; z < size; z++)
		{
			for (int x = 0; x < size; x++)
			{
				int hNW = GetHeight(data, x, z);
				int hNE = GetHeight(data, x + 1, z);
				int hSW = GetHeight(data, x, z + 1);
				int hSE = GetHeight(data, x + 1, z + 1);

				byte surfaceId = GetSurface(data, x, z);
				Color color = GetSurfaceColor(surfaceId);

				// Compute global vertex coordinates on the face
				var (gxNW, gzNW) = CubeSphereProjection.GetGlobalVertexCoords(info.X, info.Z, x, z, segmentsPerSide);
				var (gxNE, gzNE) = CubeSphereProjection.GetGlobalVertexCoords(info.X, info.Z, x + 1, z, segmentsPerSide);
				var (gxSW, gzSW) = CubeSphereProjection.GetGlobalVertexCoords(info.X, info.Z, x, z + 1, segmentsPerSide);
				var (gxSE, gzSE) = CubeSphereProjection.GetGlobalVertexCoords(info.X, info.Z, x + 1, z + 1, segmentsPerSide);

				// Project onto sphere with height offset
				Vector3 nw = CubeSphereProjection.GetSpherePointWithHeight(gxNW, gzNW, faceResolution, orientation, PlanetRadius, hNW * heightStep);
				Vector3 ne = CubeSphereProjection.GetSpherePointWithHeight(gxNE, gzNE, faceResolution, orientation, PlanetRadius, hNE * heightStep);
				Vector3 sw = CubeSphereProjection.GetSpherePointWithHeight(gxSW, gzSW, faceResolution, orientation, PlanetRadius, hSW * heightStep);
				Vector3 se = CubeSphereProjection.GetSpherePointWithHeight(gxSE, gzSE, faceResolution, orientation, PlanetRadius, hSE * heightStep);

				Vector2 uvNW = new Vector2(0f, 0f);
				Vector2 uvNE = new Vector2(1f, 0f);
				Vector2 uvSW = new Vector2(0f, 1f);
				Vector2 uvSE = new Vector2(1f, 1f);

				AddTriangle(st, nw, ne, se, uvNW, uvNE, uvSE, surfaceId, color);
				AddTriangle(st, nw, se, sw, uvNW, uvSE, uvSW, surfaceId, color);
			}
		}

		ArrayMesh mesh = st.Commit();
		if (mesh == null || mesh.GetSurfaceCount() == 0)
			return null;

		Material material = TerrainMaterial ?? GetFallbackMaterial();
		if (material != null)
			mesh.SurfaceSetMaterial(0, material);

		return mesh;
	}

	private static void AddTriangle(
		SurfaceTool st,
		Vector3 a,
		Vector3 b,
		Vector3 c,
		Vector2 uvA,
		Vector2 uvB,
		Vector2 uvC,
		byte surfaceId,
		Color color)
	{
		Vector3 n = (c - a).Cross(b - a);
		if (n.LengthSquared() < 0.000001f)
			return;

		n = n.Normalized();
		Vector2 uv2 = new Vector2(surfaceId, 0f);

		st.SetNormal(n);
		st.SetUV(uvA);
		st.SetUV2(uv2);
		st.SetColor(color);
		st.AddVertex(a);

		st.SetNormal(n);
		st.SetUV(uvB);
		st.SetUV2(uv2);
		st.SetColor(color);
		st.AddVertex(b);

		st.SetNormal(n);
		st.SetUV(uvC);
		st.SetUV2(uv2);
		st.SetColor(color);
		st.AddVertex(c);
	}

	private static int GetHeight(byte[] data, int x, int z)
	{
		int vertexSize = ChunkConstants.CHUNK_VERTEX_SIZE;
		x = Math.Clamp(x, 0, vertexSize - 1);
		z = Math.Clamp(z, 0, vertexSize - 1);
		int idx = ChunkConstants.HEIGHTS_OFFSET + z * vertexSize + x;
		return data[idx];
	}

	private static byte GetSurface(byte[] data, int x, int z)
	{
		int size = ChunkConstants.CHUNK_SIZE;
		x = Math.Clamp(x, 0, size - 1);
		z = Math.Clamp(z, 0, size - 1);
		int idx = ChunkConstants.CELLS_OFFSET + z * size + x;
		return (byte)(data[idx] & ChunkConstants.SURFACE_MASK);
	}

	private static Color GetSurfaceColor(byte surfaceId)
	{
		if (surfaceId < SurfaceRegistry.Count)
			return SurfaceRegistry.Surfaces[surfaceId].Tint;

		return new Color(0.45f, 0.72f, 0.42f, 1f);
	}

	private static Material GetFallbackMaterial()
	{
		if (_fallbackMaterial != null)
			return _fallbackMaterial;

		_fallbackMaterial = new StandardMaterial3D
		{
			VertexColorUseAsAlbedo = true,
			Roughness = 1f,
			Metallic = 0f
		};

		return _fallbackMaterial;
	}

	private MeshInstance3D CreateMeshInstance(Mesh mesh, ChunkInfo info)
	{
		MeshInstance3D meshInstance = new MeshInstance3D
		{
			Mesh = mesh,
			Name = $"Chunk_{info.X}_{info.Z}_F{info.FaceIndex}",
			Position = PlanetPosition // Offset to planet position in world space
		};

		ParentNode.AddChild(meshInstance);
		return meshInstance;
	}
}
