using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.Buffers;
using System.Collections.Generic;

public class ChunkMeshBuildSystem : QuerySystem<ChunkInfo, ChunkTerrain>
{
	public Material TerrainMaterial { get; set; }
	public Material WaterMaterial { get; set; }
	/// <summary>Sea level in tile height units (baseHeight). Tiles below get water.</summary>
	public int SeaLevelTile { get; set; }
	public int MaxPerFrame { get; set; } = 2;
	public Node ParentNode { get; set; }
	public Node3D Viewer { get; set; }

	private EntityStore _store;

	/// <summary>All chunks that have terrain data — used for neighbor lookups.</summary>
	private ArchetypeQuery<ChunkInfo, ChunkTerrain> _allTerrainQuery;
	private readonly Dictionary<(int, int), int> _chunkLookup = new();

	private int[] _selectedEntityIds;
	private int[] _selectedDistances;
	private int _selectedCount;

	private readonly ArrayPool<Vector3> _vertexPool = ArrayPool<Vector3>.Shared;
	private readonly ArrayPool<Vector3> _normalPool = ArrayPool<Vector3>.Shared;

	public ChunkMeshBuildSystem() => Filter.AllTags(Tags.Get<NeedsMeshUpdate>());

	protected override void OnAddStore(EntityStore store)
	{
		base.OnAddStore(store);
		_store = store;
		_allTerrainQuery = store.Query<ChunkInfo, ChunkTerrain>();
	}

	protected override void OnUpdate()
	{
		var buffer = CommandBuffer;

		if (MaxPerFrame <= 0)
			return;

		// Build coordinate → entityId lookup for neighbor queries
		_chunkLookup.Clear();
		foreach (var e in _allTerrainQuery.Entities)
		{
			ref var ci = ref e.GetComponent<ChunkInfo>();
			_chunkLookup[(ci.X, ci.Z)] = e.Id;
		}

		(int centerX, int centerZ) = NearestChunkSelectionTool.GetViewerChunkCoords(Viewer, ChunkConstants.CHUNK_WORLD_SIZE);

		NearestChunkSelectionTool.EnsureCapacity(ref _selectedEntityIds, ref _selectedDistances, MaxPerFrame);
		_selectedCount = 0;

		foreach (var entity in Query.Entities)
		{
			if (entity.Tags.Has<PendingRemoval>())
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

			if (entity.Tags.Has<PendingRemoval>())
				continue;

			bool success = false;
			Exception error = null;
			
			try
			{
				ref var info = ref entity.GetComponent<ChunkInfo>();
				ref var terrain = ref entity.GetComponent<ChunkTerrain>();

				// Look up right / bottom neighbor data for boundary walls
				byte[] rightData = GetNeighborData(info.X + 1, info.Z);
				byte[] bottomData = GetNeighborData(info.X, info.Z + 1);

				Mesh mesh = BuildMeshFromData(terrain.Data, rightData, bottomData);

				if (entity.TryGetComponent<ChunkMesh>(out var chunkMesh))
				{
					var existing = chunkMesh.GetMesh();
					if (existing != null)
					{
						existing.Mesh = mesh;
						existing.MaterialOverride = null;
						existing.Name = $"Chunk_{info.X}_{info.Z}";
						existing.Position = new Vector3(info.X * ChunkConstants.CHUNK_WORLD_SIZE, 0, info.Z * ChunkConstants.CHUNK_WORLD_SIZE);
					}
					else
					{
						var meshInstance = CreateMeshInstance(mesh, info);
						buffer.AddComponent(entityId, new ChunkMesh { InstaceId = meshInstance.GetInstanceId() });
					}
				}
				else
				{
					var meshInstance = CreateMeshInstance(mesh, info);
					buffer.AddComponent(entityId, new ChunkMesh { InstaceId = meshInstance.GetInstanceId() });
				}

				success = true;
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ChunkMeshBuildSystem] >> Error building mesh: {ex}");
				error = ex;
			}

			if (success)
			{
				buffer.RemoveTag<ChunkDataReady>(entityId);
				buffer.RemoveTag<NeedsMeshUpdate>(entityId);
				buffer.AddTag<ChunkComplete>(entityId);
			}
			else if (error != null)
			{
				buffer.RemoveTag<ChunkDataReady>(entityId);
				buffer.RemoveTag<NeedsMeshUpdate>(entityId);
				buffer.AddTag<ChunkError>(entityId);
			}
		}
	}

	private byte[] GetNeighborData(int cx, int cz)
	{
		if (_chunkLookup.TryGetValue((cx, cz), out int nId) &&
			_store.TryGetEntityById(nId, out var nEnt) && !nEnt.IsNull &&
			nEnt.TryGetComponent<ChunkTerrain>(out var nTerrain))
		{
			return nTerrain.Data;
		}
		return null;
	}

	private Mesh BuildMeshFromData(byte[] terrainData, byte[] rightNeighborData, byte[] bottomNeighborData)
	{
		ReadOnlySpan<byte> dataSpan = terrainData;
		
		int size = ChunkConstants.CHUNK_SIZE;
		int stride = ChunkConstants.BYTES_PER_TILE;
		int tileCount = size * size;
		int totalVertices = tileCount * 6;
		
		Vector3[] verticesArray = _vertexPool.Rent(totalVertices);
		Vector3[] normalsArray = _normalPool.Rent(totalVertices);
		
		try
		{
			Span<Vector3> vertices = verticesArray.AsSpan(0, totalVertices);
			Span<Vector3> normals = normalsArray.AsSpan(0, totalVertices);

			// Parallel arrays for UV/UV2 (same size as vertices)
			Span<Vector2> uvs  = stackalloc Vector2[totalVertices];
			Span<Vector2> uv2s = stackalloc Vector2[totalVertices];
			
			int vertexIndex = 0;
			
			for (int z = 0; z < size; z++)
			{
				for (int x = 0; x < size; x++)
				{
					int offset = (z * size + x) * stride;
					int baseHeight = dataSpan[offset];
					TileType tileType = (TileType)dataSpan[offset + 1];
					byte surfaceId = (byte)(dataSpan[offset + 2] & ChunkConstants.SURFACE_MASK);
					
					ReadOnlySpan<Vector3> tileVertices = TileMeshes.GetVertices(tileType).AsSpan();
					ReadOnlySpan<Vector3> tileNormals = TileMeshes.GetNormals(tileType).AsSpan();
					ReadOnlySpan<Vector2> tileUVs = TileMeshes.GetUVs(tileType).AsSpan();
					
					int ts = ChunkConstants.TILE_SIZE;
					float th = ChunkConstants.TILE_HEIGHT;
					Vector3 tileOffset = new Vector3(x * ts, baseHeight * th, z * ts);

					Vector2 surfaceVec = new Vector2(surfaceId, 0);
					
					for (int v = 0; v < tileVertices.Length; v++)
					{
						vertices[vertexIndex + v] = tileVertices[v] + tileOffset;
						normals[vertexIndex + v] = tileNormals[v];
						uvs[vertexIndex + v] = tileUVs[v];
						uv2s[vertexIndex + v] = surfaceVec;
					}
					
					vertexIndex += tileVertices.Length;
				}
			}
			
			// ── Surface 0: terrain ──
			SurfaceTool surfaceTool = new SurfaceTool();
			surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
			surfaceTool.SetSmoothGroup(uint.MaxValue);
			
			for (int i = 0; i < vertexIndex; i++)
			{
				surfaceTool.SetNormal(normals[i]);
				surfaceTool.SetUV(uvs[i]);
				surfaceTool.SetUV2(uv2s[i]);
				surfaceTool.AddVertex(vertices[i]);
			}

			// Fill vertical gaps between tiles
			ReadOnlySpan<byte> rightSpan = rightNeighborData != null
				? new ReadOnlySpan<byte>(rightNeighborData)
				: ReadOnlySpan<byte>.Empty;
			ReadOnlySpan<byte> bottomSpan = bottomNeighborData != null
				? new ReadOnlySpan<byte>(bottomNeighborData)
				: ReadOnlySpan<byte>.Empty;

			WallAutoMapper.GenerateWalls(surfaceTool, dataSpan, size, rightSpan, bottomSpan);

			ArrayMesh arrayMesh = surfaceTool.Commit();
			if (TerrainMaterial != null)
				arrayMesh.SurfaceSetMaterial(0, TerrainMaterial);

			// ── Surface 1: water tiles where waterFlag is set ──
			if (SeaLevelTile > 0 && WaterMaterial != null)
			{
				ReadOnlySpan<Vector3> waterVerts = TileMeshes.GetVertices(TileType.Flat).AsSpan();
				ReadOnlySpan<Vector3> waterNorms = TileMeshes.GetNormals(TileType.Flat).AsSpan();
				ReadOnlySpan<Vector2> waterUVs   = TileMeshes.GetUVs(TileType.Flat).AsSpan();

				int wts = ChunkConstants.TILE_SIZE;
				float wth = ChunkConstants.TILE_HEIGHT;
				float waterY = SeaLevelTile * wth - wth * 0.5f;
				bool hasWater = false;

				SurfaceTool waterST = new SurfaceTool();
				waterST.Begin(Mesh.PrimitiveType.Triangles);
				waterST.SetSmoothGroup(uint.MaxValue);

				for (int z = 0; z < size; z++)
				{
					for (int x = 0; x < size; x++)
					{
						int off = (z * size + x) * stride;
						byte surfByte = dataSpan[off + 2];

						if ((surfByte & ChunkConstants.WATER_FLAG) != 0)
						{
							Vector3 waterOffset = new Vector3(x * wts, waterY, z * wts);

							for (int v = 0; v < waterVerts.Length; v++)
							{
								waterST.SetNormal(waterNorms[v]);
								waterST.SetUV(waterUVs[v]);
								waterST.AddVertex(waterVerts[v] + waterOffset);
							}
							hasWater = true;
						}
					}
				}

				if (hasWater)
				{
					waterST.Commit(arrayMesh); // adds as surface 1
					arrayMesh.SurfaceSetMaterial(1, WaterMaterial);
				}
			}

			return arrayMesh;
		}
		finally
		{
			_vertexPool.Return(verticesArray);
			_normalPool.Return(normalsArray);
		}
	}

	private MeshInstance3D CreateMeshInstance(Mesh mesh, ChunkInfo chunkInfo)
	{
		int worldSize = ChunkConstants.CHUNK_WORLD_SIZE;
		var meshInstance = new MeshInstance3D
		{
			Mesh = mesh,
			Name = $"Chunk_{chunkInfo.X}_{chunkInfo.Z}"
		};

		meshInstance.Position = new Vector3(chunkInfo.X * worldSize, 0, chunkInfo.Z * worldSize);

		ParentNode?.AddChild(meshInstance);
		return meshInstance;
	}
}
