using Friflo.Engine.ECS.Systems;
using Friflo.Engine.ECS;
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages chunk visibility around the viewer on ALL 6 faces.
/// For each face, converts the viewer's world position to face-local UV coordinates
/// to determine which chunks should be visible on that face.
/// </summary>
public class ChunkVisibilitySystem : QuerySystem<ChunkInfo>
{
	private readonly Dictionary<(int, int, int), int> _activeChunks = new(); // (face, x, z) -> entityId
	private readonly HashSet<(int, int, int)> _visible = new();
	private readonly List<(int, int, int)> _toRemove = new();

	private readonly List<(int x, int z, int face)> _createQueue = new();
	private int _createIndex;

	private Vector3 _lastViewerPos;
	private bool _initialized;
	private EntityStore _store;


	public Node3D Viewer { get; set; }
	public int RenderDistance { get; set; } = 5;
	public int CollisionDistance { get; set; } = 1;
	public int MaxPerFrame { get; set; } = 8;
	public float PlanetRadius { get; set; } = 1000f;
	/// <summary>World position of the planet center. Used to offset viewer position for local-space calculations.</summary>
	public Vector3 PlanetPosition { get; set; } = Vector3.Zero;
	/// <summary>Optional spherical load radius in world units (0 = auto from RenderDistance).</summary>
	public float SphericalLoadRadius { get; set; } = 0f;

	public SystemSegmentCreator SegmentCreator { get; set; }


	protected override void OnAddStore(EntityStore store)
	{
		_store = store;
	}

	protected override void OnUpdate()
	{
		if (Viewer == null) return;

		// Convert viewer world position to planet-local coordinates
		Vector3 viewerPos = Viewer.GlobalPosition - PlanetPosition;
		bool playerMoved = !_initialized ||
						  _lastViewerPos.DistanceSquaredTo(viewerPos) > 1.0f;

		if (playerMoved)
		{
			_lastViewerPos = viewerPos;
			_initialized = true;

			RecalculateVisibleAndQueue(viewerPos);
		}


		var buffer = CommandBuffer;
		ProcessChunkCreation(buffer);
		RemoveOldChunks(buffer);
		UpdateChunkCollisions(buffer);
	}

	/// <summary>
	/// Gets chunk coordinates for a face using the viewer direction projected onto that face.
	/// </summary>
	private bool TryGetFaceChunkCoords(Vector3 localViewerPos, int faceIndex, out int chunkX, out int chunkZ, out FaceOrientation orientation)
	{
		chunkX = 0;
		chunkZ = 0;
		orientation = default;

		FaceOrientation? faceOrientation = SegmentCreator?.GetFaceOrientation(faceIndex);
		if (!faceOrientation.HasValue)
			return false;

		orientation = faceOrientation.Value;
		Vector2 faceUV = CubeSphereProjection.WorldToFaceUV(localViewerPos, orientation, PlanetRadius);

		int segmentsPerSide = SegmentCreator?.SegmentsPerSide ?? 1;
		var (gridX, gridZ) = CubeSphereProjection.UVToFaceGrid(faceUV, segmentsPerSide);

		chunkX = Mathf.FloorToInt(gridX);
		chunkZ = Mathf.FloorToInt(gridZ);
		return true;
	}

	private void RecalculateVisibleAndQueue(Vector3 viewerPos)
	{
		_visible.Clear();
		_createQueue.Clear();
		_createIndex = 0;

		int segmentsPerSide = SegmentCreator?.SegmentsPerSide ?? 1;
		int faceResolution = CubeSphereProjection.GetFaceResolution(segmentsPerSide);
		float loadRadius = GetLoadRadiusWorld();
		float loadRadiusSq = loadRadius > 0f ? GetPaddedLoadRadiusSq(loadRadius) : -1f;
		int gridRadius = GetGridRadius(loadRadius);
		var (minBound, maxBound) = GetChunkBounds();

		for (int faceIndex = 0; faceIndex < ConstantsCelestial.FACE_COUNT; faceIndex++)
		{
			if (!TryGetFaceChunkCoords(viewerPos, faceIndex, out int centerX, out int centerZ, out FaceOrientation orientation))
				continue;

			centerX = Math.Clamp(centerX, minBound, maxBound);
			centerZ = Math.Clamp(centerZ, minBound, maxBound);

			int minX = centerX - gridRadius;
			int maxX = centerX + gridRadius;
			int minZ = centerZ - gridRadius;
			int maxZ = centerZ + gridRadius;

			for (int x = minX; x <= maxX; x++)
			{
				for (int z = minZ; z <= maxZ; z++)
				{
					AddVisibleAndMaybeQueue(x, z, faceIndex, viewerPos, orientation, segmentsPerSide, faceResolution, loadRadiusSq);
				}
			}
		}
	}

	/// <summary>
	/// Returns the chunk bounds for a single face.
	/// Total chunks per side = SegmentsPerSide * ConstantsSegment.SIDE.
	/// Half-size in chunks = (SegmentsPerSide * ConstantsSegment.SIDE) / 2.
	/// </summary>
	private (int min, int max) GetChunkBounds()
	{
		if (SegmentCreator == null)
			return (int.MinValue, int.MaxValue);

		int segsPerSide = SegmentCreator.SegmentsPerSide;
		int chunksPerSide = segsPerSide * ConstantsSegment.SIDE;
		int half = chunksPerSide / 2;
		return (-half, half - 1);
	}

	private bool IsWithinChunkBounds(int chunkX, int chunkZ)
	{
		var (min, max) = GetChunkBounds();
		return chunkX >= min && chunkX <= max &&
			   chunkZ >= min && chunkZ <= max;
	}

	private void AddVisibleAndMaybeQueue(
		int x,
		int z,
		int faceIndex,
		Vector3 viewerLocalPos,
		FaceOrientation orientation,
		int segmentsPerSide,
		int faceResolution,
		float loadRadiusSq)
	{
		if (!IsWithinChunkBounds(x, z))
			return;

		if (loadRadiusSq > 0f)
		{
			Vector3 chunkCenter = GetChunkCenterLocal(x, z, orientation, segmentsPerSide, faceResolution);
			if (chunkCenter.DistanceSquaredTo(viewerLocalPos) > loadRadiusSq)
				return;
		}

		var key = (x, z, faceIndex);
		_visible.Add(key);

		if (!_activeChunks.ContainsKey(key))
			_createQueue.Add((x, z, faceIndex));
	}

	private float GetLoadRadiusWorld()
	{
		if (SphericalLoadRadius > 0f)
			return SphericalLoadRadius;

		return RenderDistance * ChunkConstants.CHUNK_WORLD_SIZE;
	}

	private static float GetPaddedLoadRadiusSq(float loadRadius)
	{
		float pad = ChunkConstants.CHUNK_WORLD_SIZE * 0.75f;
		float padded = loadRadius + pad;
		return padded * padded;
	}

	private static int GetGridRadius(float loadRadius)
	{
		if (loadRadius <= 0f)
			return 0;

		float chunks = loadRadius / ChunkConstants.CHUNK_WORLD_SIZE;
		return Mathf.Max(1, Mathf.CeilToInt(chunks) + 1);
	}

	private Vector3 GetChunkCenterLocal(int chunkX, int chunkZ, FaceOrientation orientation, int segmentsPerSide, int faceResolution)
	{
		int localX = ChunkConstants.CHUNK_SIZE / 2;
		int localZ = ChunkConstants.CHUNK_SIZE / 2;
		var (globalX, globalZ) = CubeSphereProjection.GetGlobalVertexCoords(chunkX, chunkZ, localX, localZ, segmentsPerSide);
		return CubeSphereProjection.GetSpherePoint(globalX, globalZ, faceResolution, orientation, PlanetRadius);
	}

	private void ProcessChunkCreation(CommandBuffer buffer)
	{
		if (_createIndex >= _createQueue.Count) return;

		int createdThisFrame = 0;

		while (_createIndex < _createQueue.Count && createdThisFrame < MaxPerFrame)
		{
			var (x, z, faceIndex) = _createQueue[_createIndex++];
			var key = (x, z, faceIndex);

			if (!_visible.Contains(key))
				continue;

			if (!_activeChunks.ContainsKey(key))
			{
				CreateChunk(x, z, faceIndex, buffer);
				createdThisFrame++;
			}
		}

		if (_createIndex >= _createQueue.Count)
		{
			_createQueue.Clear();
			_createIndex = 0;
		}
	}

	private void CreateChunk(int x, int z, int faceIndex, CommandBuffer buffer)
	{
		int entityId = buffer.CreateEntity();

		var (segX, segZ) = SegmentFile.ChunkToSegment(x, z);

		buffer.AddComponent(entityId, new ChunkInfo
		{
			X = x,
			Z = z,
			LOD = CalculateLOD(x, z, faceIndex),
			SegmentX = segX,
			SegmentY = segZ,
			FaceIndex = faceIndex
		});

		buffer.AddTag<ChunkPending>(entityId);

		_activeChunks[(x, z, faceIndex)] = entityId;

		if (ShouldHaveCollision(x, z, faceIndex))
		{
			buffer.AddTag<NeedsCollision>(entityId);
		}
	}

	private byte CalculateLOD(int chunkX, int chunkZ, int faceIndex)
	{
		float distanceSq = GetChunkDistanceSq(chunkX, chunkZ, faceIndex);
		float distance = Mathf.Sqrt(distanceSq) / ChunkConstants.CHUNK_WORLD_SIZE;

		if (distance <= 2f) return 0;
		if (distance <= 4f) return 1;
		if (distance <= 6f) return 2;
		return 3;
	}

	private void RemoveOldChunks(CommandBuffer buffer)
	{
		_toRemove.Clear();

		foreach (var kvp in _activeChunks)
		{
			if (!_visible.Contains(kvp.Key))
			{
				_toRemove.Add(kvp.Key);
			}
		}

		foreach (var pos in _toRemove)
		{
			if (_activeChunks.TryGetValue(pos, out int entityId))
			{
				buffer.AddTag<PendingRemoval>(entityId);
				_activeChunks.Remove(pos);
			}
		}
	}

	private void UpdateChunkCollisions(CommandBuffer buffer)
	{
		foreach (var kvp in _activeChunks)
		{
			var (x, z, faceIndex) = kvp.Key;
			int entityId = kvp.Value;

			bool shouldCollide = ShouldHaveCollision(x, z, faceIndex);

			if (!_store.TryGetEntityById(entityId, out var entity) || entity.IsNull)
				continue;

			bool hasCollision = entity.Tags.Has<NeedsCollision>();

			if (shouldCollide && !hasCollision)
			{
				buffer.AddTag<NeedsCollision>(entityId);
			}
			else if (!shouldCollide && hasCollision)
			{
				buffer.RemoveTag<NeedsCollision>(entityId);
			}
		}
	}

	private bool ShouldHaveCollision(int chunkX, int chunkZ, int faceIndex)
	{
		float radius = CollisionDistance * ChunkConstants.CHUNK_WORLD_SIZE;
		float distanceSq = GetChunkDistanceSq(chunkX, chunkZ, faceIndex);
		return distanceSq <= radius * radius;
	}

	private float GetChunkDistanceSq(int chunkX, int chunkZ, int faceIndex)
	{
		FaceOrientation? faceOrientation = SegmentCreator?.GetFaceOrientation(faceIndex);
		int segmentsPerSide = SegmentCreator?.SegmentsPerSide ?? 1;
		if (faceOrientation.HasValue)
		{
			Vector3 center = CubeSphereProjection.GetChunkCenterOnSphere(
				chunkX,
				chunkZ,
				segmentsPerSide,
				faceOrientation.Value,
				PlanetRadius);
			return center.DistanceSquaredTo(_lastViewerPos);
		}

		float half = ChunkConstants.CHUNK_WORLD_SIZE * 0.5f;
		Vector3 fallbackCenter = new Vector3(
			chunkX * ChunkConstants.CHUNK_WORLD_SIZE + half,
			0f,
			chunkZ * ChunkConstants.CHUNK_WORLD_SIZE + half);
		return fallbackCenter.DistanceSquaredTo(_lastViewerPos);
	}
}
