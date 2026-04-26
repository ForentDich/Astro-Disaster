using Friflo.Engine.ECS.Systems;
using Friflo.Engine.ECS;
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages chunk visibility around the viewer on ALL 6 faces.
/// Uses raw XZ world coordinates for chunk positioning on each face.
/// </summary>
public class ChunkVisibilitySystem : QuerySystem<ChunkInfo>
{
	private readonly Dictionary<(int, int, int), int> _activeChunks = new(); // (face, x, z) -> entityId
	private readonly HashSet<(int, int, int)> _visible = new();
	private readonly List<(int, int, int)> _toRemove = new();

	private readonly List<(int x, int z, int face)> _createQueue = new();
	private int _createIndex;

	private Vector3 _lastViewerPos;
	private (int x, int z) _lastChunkPos;
	private bool _initialized;
	private EntityStore _store;
	
	public Node3D Viewer { get; set; }
	public int RenderDistance { get; set; } = 5;
	public int CollisionDistance { get; set; } = 1;
	public int MaxPerFrame { get; set; } = 8;

	public SystemSegmentCreator SegmentCreator { get; set; }
	
	protected override void OnAddStore(EntityStore store)
	{
		_store = store;
	}
	
	protected override void OnUpdate()
	{
		if (Viewer == null) return;

		// Use raw XZ world coordinates for chunk positioning
		(int currentX, int currentZ) = GetChunkCoords();

		int prevX = _lastChunkPos.x;
		int prevZ = _lastChunkPos.z;
		
		bool playerMoved = !_initialized || 
						  currentX != _lastChunkPos.x || 
						  currentZ != _lastChunkPos.z ||
						  _lastViewerPos.DistanceSquaredTo(Viewer.GlobalPosition) > 1.0f;
		
		if (playerMoved)
		{
			int dx = _initialized ? (currentX - prevX) : 0;
			int dz = _initialized ? (currentZ - prevZ) : 0;

			_lastViewerPos = Viewer.GlobalPosition;
			_lastChunkPos = (currentX, currentZ);
			_initialized = true;

			if (_visible.Count != 0 && Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1 && (dx != 0 || dz != 0))
			{
				ShiftVisibleAndQueue(prevX, prevZ, currentX, currentZ, dx, dz);
			}
			else
			{
				RecalculateVisibleAndQueue(currentX, currentZ);
			}
		}
		
		var buffer = CommandBuffer;
		ProcessChunkCreation(buffer);
		RemoveOldChunks(buffer);
		UpdateChunkCollisions(buffer);
	}

	/// <summary>
	/// Gets chunk coordinates from raw XZ world position.
	/// </summary>
	private (int x, int z) GetChunkCoords()
	{
		return (
			Mathf.FloorToInt(Viewer.GlobalPosition.X / ChunkConstants.CHUNK_WORLD_SIZE),
			Mathf.FloorToInt(Viewer.GlobalPosition.Z / ChunkConstants.CHUNK_WORLD_SIZE)
		);
	}
	
	private void RecalculateVisibleAndQueue(int centerX, int centerZ)
	{
		_visible.Clear();
		_createQueue.Clear();
		_createIndex = 0;

		for (int faceIndex = 0; faceIndex < ConstantsCelestial.FACE_COUNT; faceIndex++)
		{
			for (int r = 0; r <= RenderDistance; r++)
			{
				if (r == 0)
				{
					AddVisibleAndMaybeQueue(centerX, centerZ, faceIndex);
					continue;
				}

				int minX = centerX - r;
				int maxX = centerX + r;
				int minZ = centerZ - r;
				int maxZ = centerZ + r;

				for (int x = minX; x <= maxX; x++)
				{
					AddVisibleAndMaybeQueue(x, minZ, faceIndex);
					AddVisibleAndMaybeQueue(x, maxZ, faceIndex);
				}

				for (int z = minZ + 1; z <= maxZ - 1; z++)
				{
					AddVisibleAndMaybeQueue(minX, z, faceIndex);
					AddVisibleAndMaybeQueue(maxX, z, faceIndex);
				}
			}
		}
	}

	private void ShiftVisibleAndQueue(int oldCenterX, int oldCenterZ, int newCenterX, int newCenterZ, int dx, int dz)
	{
		int r = RenderDistance;

		for (int faceIndex = 0; faceIndex < ConstantsCelestial.FACE_COUNT; faceIndex++)
		{
			if (dx != 0)
			{
				int sign = Math.Sign(dx);
				int addX = newCenterX + sign * r;
				for (int z = newCenterZ - r; z <= newCenterZ + r; z++)
					AddVisibleAndMaybeQueue(addX, z, faceIndex);

				int removeX = oldCenterX - sign * r;
				for (int z = oldCenterZ - r; z <= oldCenterZ + r; z++)
					_visible.Remove((removeX, z, faceIndex));
			}

			if (dz != 0)
			{
				int sign = Math.Sign(dz);
				int addZ = newCenterZ + sign * r;
				for (int x = newCenterX - r; x <= newCenterX + r; x++)
					AddVisibleAndMaybeQueue(x, addZ, faceIndex);

				int removeZ = oldCenterZ - sign * r;
				for (int x = oldCenterX - r; x <= oldCenterX + r; x++)
					_visible.Remove((x, removeZ, faceIndex));
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

	private void AddVisibleAndMaybeQueue(int x, int z, int faceIndex)
	{
		if (!IsWithinChunkBounds(x, z))
			return;

		var key = (x, z, faceIndex);
		_visible.Add(key);

		if (!_activeChunks.ContainsKey(key))
			_createQueue.Add((x, z, faceIndex));
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
			LOD = CalculateLOD(x, z),
			SegmentX = segX,
			SegmentY = segZ,
			FaceIndex = faceIndex
		});
		
		buffer.AddTag<ChunkNeedsLoad>(entityId);
		
		_activeChunks[(x, z, faceIndex)] = entityId;
		
		if (ShouldHaveCollision(x, z))
		{
			buffer.AddTag<NeedsCollision>(entityId);
		}
	}
	
	private byte CalculateLOD(int chunkX, int chunkZ)
	{
		int dx = Math.Abs(chunkX - _lastChunkPos.x);
		int dz = Math.Abs(chunkZ - _lastChunkPos.z);
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
			
			bool shouldCollide = ShouldHaveCollision(x, z);
			
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
	
	private bool ShouldHaveCollision(int chunkX, int chunkZ)
	{
		return Math.Abs(chunkX - _lastChunkPos.x) <= CollisionDistance && 
			   Math.Abs(chunkZ - _lastChunkPos.z) <= CollisionDistance;
	}
}
