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

	/// <summary>Per-face last chunk center positions.</summary>
	private readonly Dictionary<int, (int x, int z)> _lastFaceChunkPos = new();

	public Node3D Viewer { get; set; }
	public int RenderDistance { get; set; } = 5;
	public int CollisionDistance { get; set; } = 1;
	public int MaxPerFrame { get; set; } = 8;
	public float PlanetRadius { get; set; } = 1000f;

	public SystemSegmentCreator SegmentCreator { get; set; }

	protected override void OnAddStore(EntityStore store)
	{
		_store = store;
	}

	protected override void OnUpdate()
	{
		if (Viewer == null) return;

		Vector3 viewerPos = Viewer.GlobalPosition;
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
	/// Gets chunk coordinates for a specific face by converting world position
	/// to face-local UV coordinates.
	/// </summary>
	private (int x, int z) GetFaceChunkCoords(Vector3 worldPos, int faceIndex)
	{
		FaceOrientation? orientation = SegmentCreator?.GetFaceOrientation(faceIndex);
		if (orientation == null)
			return (0, 0);

		FaceOrientation ori = orientation.Value;

		// Convert world position to face-local UV coordinates
		Vector2 faceUV = CubeSphereProjection.WorldToFaceUV(worldPos, ori, PlanetRadius);

		// Convert UV to face grid coordinates (in chunk units)
		int segmentsPerSide = SegmentCreator?.SegmentsPerSide ?? 1;
		var (gridX, gridZ) = CubeSphereProjection.UVToFaceGrid(faceUV, segmentsPerSide);

		// Convert to chunk coordinates
		int chunkX = Mathf.FloorToInt(gridX);
		int chunkZ = Mathf.FloorToInt(gridZ);

		return (chunkX, chunkZ);
	}

	private void RecalculateVisibleAndQueue(Vector3 viewerPos)
	{
		_visible.Clear();
		_createQueue.Clear();
		_createIndex = 0;

		Vector3 viewerDir = viewerPos.Normalized();
		int segmentsPerSide = SegmentCreator?.SegmentsPerSide ?? 1;
		int faceResolution = CubeSphereProjection.GetFaceResolution(segmentsPerSide);
		float maxArc = RenderDistance * ChunkConstants.CHUNK_WORLD_SIZE * Mathf.Sqrt(2f);
		float safeRadius = Math.Max(PlanetRadius, 0.001f);
		float maxAngle = maxArc / safeRadius;
		float minDot = Mathf.Cos(Mathf.Min(maxAngle, Mathf.Pi));
		var (minBound, maxBound) = GetChunkBounds();

		for (int faceIndex = 0; faceIndex < ConstantsCelestial.FACE_COUNT; faceIndex++)
		{
			FaceOrientation? faceOrientation = SegmentCreator?.GetFaceOrientation(faceIndex);
			if (!faceOrientation.HasValue)
				continue;

			FaceOrientation orientation = faceOrientation.Value;
			if (viewerDir.Dot(orientation.Normal) <= 0f)
				continue;

			var (centerX, centerZ) = GetFaceChunkCoords(viewerPos, faceIndex);
			centerX = Math.Clamp(centerX, minBound, maxBound);
			centerZ = Math.Clamp(centerZ, minBound, maxBound);
			_lastFaceChunkPos[faceIndex] = (centerX, centerZ);

			for (int r = 0; r <= RenderDistance; r++)
			{
				if (r == 0)
				{
					AddVisibleAndMaybeQueue(centerX, centerZ, faceIndex, viewerDir, minDot, orientation, segmentsPerSide, faceResolution);
					continue;
				}

				int minX = centerX - r;
				int maxX = centerX + r;
				int minZ = centerZ - r;
				int maxZ = centerZ + r;

				for (int x = minX; x <= maxX; x++)
				{
					AddVisibleAndMaybeQueue(x, minZ, faceIndex, viewerDir, minDot, orientation, segmentsPerSide, faceResolution);
					AddVisibleAndMaybeQueue(x, maxZ, faceIndex, viewerDir, minDot, orientation, segmentsPerSide, faceResolution);
				}

				for (int z = minZ + 1; z <= maxZ - 1; z++)
				{
					AddVisibleAndMaybeQueue(minX, z, faceIndex, viewerDir, minDot, orientation, segmentsPerSide, faceResolution);
					AddVisibleAndMaybeQueue(maxX, z, faceIndex, viewerDir, minDot, orientation, segmentsPerSide, faceResolution);
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
		Vector3 viewerDir,
		float minDot,
		FaceOrientation orientation,
		int segmentsPerSide,
		int faceResolution)
	{
		if (!IsWithinChunkBounds(x, z))
			return;

		if (!IsChunkWithinDistance(x, z, viewerDir, minDot, orientation, segmentsPerSide, faceResolution))
			return;

		var key = (x, z, faceIndex);
		_visible.Add(key);

		if (!_activeChunks.ContainsKey(key))
			_createQueue.Add((x, z, faceIndex));
	}

	private bool IsChunkWithinDistance(
		int chunkX,
		int chunkZ,
		Vector3 viewerDir,
		float minDot,
		FaceOrientation orientation,
		int segmentsPerSide,
		int faceResolution)
	{
		int local = ChunkConstants.CHUNK_SIZE / 2;
		var (globalX, globalZ) = CubeSphereProjection.GetGlobalVertexCoords(
			chunkX,
			chunkZ,
			local,
			local,
			segmentsPerSide);

		Vector3 center = CubeSphereProjection.GetSpherePoint(
			globalX,
			globalZ,
			faceResolution,
			orientation,
			PlanetRadius);
		Vector3 chunkDir = center.Normalized();

		return viewerDir.Dot(chunkDir) >= minDot;
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

		buffer.AddTag<ChunkNeedsLoad>(entityId);

		_activeChunks[(x, z, faceIndex)] = entityId;

		if (ShouldHaveCollision(x, z, faceIndex))
		{
			buffer.AddTag<NeedsCollision>(entityId);
		}
	}

	private byte CalculateLOD(int chunkX, int chunkZ, int faceIndex)
	{
		if (!_lastFaceChunkPos.TryGetValue(faceIndex, out var center))
			return 0;

		int dx = Math.Abs(chunkX - center.x);
		int dz = Math.Abs(chunkZ - center.z);
		int distance = Math.Max(dx, dz);

		if (distance <= 2) return 0;
		if (distance <= 4) return 1;
		if (distance <= 6) return 2;
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
		if (!_lastFaceChunkPos.TryGetValue(faceIndex, out var center))
			return false;

		return Math.Abs(chunkX - center.x) <= CollisionDistance &&
			   Math.Abs(chunkZ - center.z) <= CollisionDistance;
	}
}
