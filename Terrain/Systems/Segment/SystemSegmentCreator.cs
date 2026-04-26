using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Creates and manages segment entities around the player on ALL 6 faces.
///
/// Pipeline position: after CelestialCreator (faces exist), before ChunkVisibility.
///
/// Responsibilities:
///   1. Track all 6 faces from the ECS store.
///   2. For each face, track the player's segment-grid position.
///   3. Create segment entities (+ empty .seg files) within LoadRadius on each face.
///   4. Mark far-away segments for unloading beyond UnloadRadius.
/// </summary>
public class SystemSegmentCreator : BaseSystem
{
    // ── Config (set from GameSession) ──
    public Node3D Viewer { get; set; }
    public int LoadRadius { get; set; } = ConstantsSegment.LOAD_RADIUS;
    public int UnloadRadius { get; set; } = ConstantsSegment.UNLOAD_RADIUS;
    public float PlanetRadius { get; set; } = 1000f;

    // ── Internal state ──
    private EntityStore _store;
    private int _segmentsPerSide = 1;

    /// <summary>Face data per face index.</summary>
    private readonly Dictionary<int, FaceData> _faceData = new();

    /// <summary>Path to the active face's storage folder. Used by chunk systems to find .seg files.</summary>
    public string FaceStoragePath
    {
        get
        {
            // For backward compatibility — return the first face's path
            foreach (var kvp in _faceData)
                return kvp.Value.StoragePath;
            return null;
        }
    }

    /// <summary>Number of segments along one side of the active face.</summary>
    public int SegmentsPerSide => _segmentsPerSide;

    private class FaceData
    {
        public Entity Face;
        public string StoragePath;
        public FaceOrientation Orientation;
        public readonly Dictionary<(int, int), int> ActiveSegments = new();
        public readonly HashSet<(int, int)> Visible = new();
        public readonly List<(int, int)> ToRemove = new();
        public (int x, int z) LastSegPos;
        public bool Initialized;
    }

    // ────────────────────── Lifecycle ──────────────────────

    protected override void OnAddStore(EntityStore store)
    {
        _store = store;
    }

    protected override void OnUpdateGroup()
    {
        if (Viewer == null) return;

        // Step 1 — resolve all faces (once)
        if (_faceData.Count == 0)
        {
            TryResolveAllFaces();
            if (_faceData.Count == 0)
                return;
        }

        // Step 2 — create / unload segments around viewer on each face
        UpdateSegmentsAroundViewer();
    }

    // ────────────────────── Face resolution ──────────────────────

    private void TryResolveAllFaces()
    {
        var query = _store.Query().AllTags(Tags.Get<FaceCreated>());

        int count = 0;
        foreach (var entity in query.Entities)
        {
            ref var faceId = ref entity.GetComponent<FaceIdentity>();
            int index = faceId.Index;

            if (_faceData.ContainsKey(index))
                continue;

            var data = new FaceData
            {
                Face = entity,
                StoragePath = entity.GetComponent<FaceStorage>().SavePath,
                Orientation = entity.GetComponent<FaceOrientation>()
            };

            _faceData[index] = data;

            count++;
            GD.Print($"[SegmentCreator] Registered face {index}: {entity.GetComponent<FaceName>().Value}");
        }

        if (count > 0)
        {
            // Get segments per side from the first face entity
            foreach (var firstEntity in query.Entities)
            {
                _segmentsPerSide = firstEntity.GetComponent<FaceIdentity>().SegmentsPerSide;
                break;
            }
            GD.Print($"[SegmentCreator] Registered {count} faces (segments per side: {_segmentsPerSide})");
        }
    }

    /// <summary>
    /// Returns the FaceData for a given face index, or null if not found.
    /// </summary>
    public FaceOrientation? GetFaceOrientation(int faceIndex)
    {
        if (_faceData.TryGetValue(faceIndex, out var data))
            return data.Orientation;
        return null;
    }

    /// <summary>
    /// Returns the storage path for a given face index, or null if not found.
    /// </summary>
    public string GetFaceStoragePath(int faceIndex)
    {
        if (_faceData.TryGetValue(faceIndex, out var data))
            return data.StoragePath;
        return null;
    }

    // ────────────────────── Segment grid management ──────────────────────

    /// <summary>
    /// Returns the half-size of the segment grid for this face.
    /// For SegmentsPerSide=1 → half=0 (only center segment at 0,0).
    /// For SegmentsPerSide=3 → half=1 (segments -1..1).
    /// For SegmentsPerSide=5 → half=2 (segments -2..2).
    /// </summary>
    private int SegmentGridHalf => (_segmentsPerSide - 1) / 2;

    /// <summary>Checks if segment coordinates are within the face bounds.</summary>
    private bool IsWithinFaceBounds(int segX, int segZ)
    {
        int half = SegmentGridHalf;
        return segX >= -half && segX <= half &&
               segZ >= -half && segZ <= half;
    }

    private void UpdateSegmentsAroundViewer()
    {
        Vector3 viewerPos = Viewer.GlobalPosition;
        int half = SegmentGridHalf;

        foreach (var kvp in _faceData)
        {
            int faceIndex = kvp.Key;
            var data = kvp.Value;

            // Convert world position to face-local UV coordinates
            Vector2 faceUV = CubeSphereProjection.WorldToFaceUV(viewerPos, data.Orientation, PlanetRadius);
            var (gridX, gridZ) = CubeSphereProjection.UVToFaceGrid(faceUV, _segmentsPerSide);

            // Convert to segment coordinates
            int segX = Mathf.FloorToInt(gridX / ConstantsSegment.SIDE);
            int segZ = Mathf.FloorToInt(gridZ / ConstantsSegment.SIDE);

            // Clamp viewer segment position to face bounds
            segX = Math.Clamp(segX, -half, half);
            segZ = Math.Clamp(segZ, -half, half);

            UpdateSegmentsForFace(data, faceIndex, segX, segZ);
        }
    }

    private void UpdateSegmentsForFace(FaceData data, int faceIndex, int viewerSegX, int viewerSegZ)
    {
        // Nothing changed — skip
        if (data.Initialized && viewerSegX == data.LastSegPos.x && viewerSegZ == data.LastSegPos.z)
            return;

        data.LastSegPos = (viewerSegX, viewerSegZ);
        data.Initialized = true;

        // 1. Build visible set (clamped to face bounds)
        data.Visible.Clear();
        for (int dx = -LoadRadius; dx <= LoadRadius; dx++)
        {
            for (int dz = -LoadRadius; dz <= LoadRadius; dz++)
            {
                int sx = viewerSegX + dx;
                int sz = viewerSegZ + dz;
                if (IsWithinFaceBounds(sx, sz))
                    data.Visible.Add((sx, sz));
            }
        }

        // 2. Create missing segments
        foreach (var pos in data.Visible)
        {
            if (!data.ActiveSegments.ContainsKey(pos))
                CreateSegment(data, faceIndex, pos.Item1, pos.Item2);
        }

        // 3. Unload distant segments
        data.ToRemove.Clear();
        foreach (var kvp in data.ActiveSegments)
        {
            int dist = Math.Max(
                Math.Abs(kvp.Key.Item1 - viewerSegX),
                Math.Abs(kvp.Key.Item2 - viewerSegZ));

            if (dist > UnloadRadius)
                data.ToRemove.Add(kvp.Key);
        }

        foreach (var pos in data.ToRemove)
            UnloadSegment(data, pos);
    }

    // ────────────────────── Create ──────────────────────

    private void CreateSegment(FaceData data, int faceIndex, int segX, int segZ)
    {
        try
        {
            ref var faceId = ref data.Face.GetComponent<FaceIdentity>();

            string fileName = $"seg_{segX}_{segZ}{ConstantsSegment.FILE_EXTENSION}";
            string filePath = Path.Combine(data.StoragePath, fileName);

            float worldX = segX * ConstantsSegment.WORLD_SIZE;
            float worldZ = segZ * ConstantsSegment.WORLD_SIZE;
            float half   = ConstantsSegment.WORLD_SIZE * 0.5f;

            Entity seg = _store.CreateEntity();

            seg.AddComponent(new SegmentIdentity
            {
                FaceIndex    = faceId.Index,
                GridPosition = new Vector2I(segX, segZ),
                SegmentId    = HashId(faceId.Index, segX, segZ)
            });

            seg.AddComponent(new SegmentWorldPosition
            {
                Center = new Vector3(worldX + half, 0, worldZ + half),
                Size   = ConstantsSegment.WORLD_SIZE
            });

            seg.AddComponent(new SegmentStorage
            {
                FilePath     = filePath,
                LastModified = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                FileSize     = 0
            });

            seg.AddComponent(new SegmentParentFace { Face = data.Face });

            // Decide: load existing data or generate fresh
            string absPath = ProjectSettings.GlobalizePath(filePath);
            bool fileExists = File.Exists(absPath);

            if (fileExists)
            {
                seg.AddTag<SegmentNeedsLoad>();
            }
            else
            {
                SegmentFile.CreateEmpty(filePath);
                seg.AddTag<SegmentNeedsGenerate>();
            }

            seg.AddTag<SegmentActive>();
            data.ActiveSegments[(segX, segZ)] = seg.Id;

            if (fileExists)
                GD.Print($"[SegmentCreator] Face {faceIndex} segment ({segX},{segZ}) loaded (existing)");
            else
                GD.Print($"[SegmentCreator] Face {faceIndex} segment ({segX},{segZ}) created (new) → {fileName}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SegmentCreator] Error face {faceIndex} ({segX},{segZ}): {ex.Message}");
        }
    }

    // ────────────────────── Unload ──────────────────────

    private void UnloadSegment(FaceData data, (int, int) pos)
    {
        if (!data.ActiveSegments.TryGetValue(pos, out int id))
            return;

        if (_store.TryGetEntityById(id, out var entity) && !entity.IsNull)
        {
            if (entity.Tags.Has<SegmentDataDirty>())
                entity.AddTag<SegmentNeedsSave>();

            entity.RemoveTag<SegmentActive>();
            entity.AddTag<SegmentInactive>();
        }

        data.ActiveSegments.Remove(pos);
        GD.Print($"[SegmentCreator] Unloaded segment {pos}");
    }

    // ────────────────────── Helpers ──────────────────────

    private static int HashId(int face, int segX, int segZ)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + face;
            h = h * 31 + segX;
            h = h * 31 + segZ;
            return h & 0x7FFFFFFF;
        }
    }

    /// <summary>
    /// Clears all internal tracking so the next update re-discovers segments
    /// around the viewer. Call after deleting segment/chunk entities.
    /// </summary>
    public void Reset()
    {
        _faceData.Clear();
    }
}
