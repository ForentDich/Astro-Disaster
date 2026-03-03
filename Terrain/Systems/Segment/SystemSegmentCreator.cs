using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Creates and manages segment entities around the player on the active face.
///
/// Pipeline position: after CelestialCreator (faces exist), before ChunkVisibility.
///
/// Responsibilities:
///   1. Pick the active face (first face tagged FaceCreated).
///   2. Track the player's segment-grid position.
///   3. Create segment entities (+ empty .seg files) within LoadRadius.
///   4. Mark far-away segments for unloading beyond UnloadRadius.
/// </summary>
public class SystemSegmentCreator : BaseSystem
{
    // ── Config (set from GameSession) ──
    public Node3D Viewer { get; set; }
    public int LoadRadius { get; set; } = ConstantsSegment.LOAD_RADIUS;
    public int UnloadRadius { get; set; } = ConstantsSegment.UNLOAD_RADIUS;

    // ── Internal state ──
    private EntityStore _store;
    private Entity _activeFace;
    private string _faceStoragePath;

    /// <summary>Path to the active face's storage folder. Used by chunk systems to find .seg files.</summary>
    public string FaceStoragePath => _faceStoragePath;

    private readonly Dictionary<(int, int), int> _activeSegments = new();
    private readonly HashSet<(int, int)> _visible = new();
    private readonly List<(int, int)> _toRemove = new();

    private (int x, int z) _lastSegPos;
    private bool _initialized;

    // ────────────────────── Lifecycle ──────────────────────

    protected override void OnAddStore(EntityStore store)
    {
        _store = store;
    }

    protected override void OnUpdateGroup()
    {
        if (Viewer == null) return;

        // Step 1 — resolve active face (once)
        if (_activeFace.IsNull)
        {
            if (!TryResolveActiveFace())
                return;
        }

        // Step 2 — create / unload segments around viewer
        UpdateSegmentsAroundViewer();
    }

    // ────────────────────── Face resolution ──────────────────────

    private bool TryResolveActiveFace()
    {
        // Pick the face closest to the viewer.
        // For a flat-terrain prototype any FaceCreated face works.
        var query = _store.Query().AllTags(Tags.Get<FaceCreated>());

        float bestDist = float.MaxValue;
        Entity bestFace = default;

        foreach (var entity in query.Entities)
        {
            ref var pos = ref entity.GetComponent<FacePosition>();
            float dist = Viewer.GlobalPosition.DistanceSquaredTo(pos.WorldPosition);

            if (dist < bestDist)
            {
                bestDist = dist;
                bestFace = entity;
            }
        }

        if (bestFace.IsNull)
            return false;

        _activeFace = bestFace;
        _faceStoragePath = bestFace.GetComponent<FaceStorage>().SavePath;

        // Transition tag
        if (bestFace.Tags.Has<FaceNeedsSegments>())
        {
            bestFace.RemoveTag<FaceNeedsSegments>();
            bestFace.AddTag<FaceHasSegments>();
        }

        GD.Print($"[SegmentCreator] Active face: {bestFace.GetComponent<FaceName>().Value}");
        return true;
    }

    // ────────────────────── Segment grid management ──────────────────────

    private void UpdateSegmentsAroundViewer()
    {
        var (segX, segZ) = SegmentFile.WorldToSegment(Viewer.GlobalPosition);

        // Nothing changed — skip
        if (_initialized && segX == _lastSegPos.x && segZ == _lastSegPos.z)
            return;

        _lastSegPos = (segX, segZ);
        _initialized = true;

        // 1. Build visible set
        _visible.Clear();
        for (int dx = -LoadRadius; dx <= LoadRadius; dx++)
            for (int dz = -LoadRadius; dz <= LoadRadius; dz++)
                _visible.Add((segX + dx, segZ + dz));

        // 2. Create missing segments
        foreach (var pos in _visible)
        {
            if (!_activeSegments.ContainsKey(pos))
                CreateSegment(pos.Item1, pos.Item2);
        }

        // 3. Unload distant segments
        _toRemove.Clear();
        foreach (var kvp in _activeSegments)
        {
            int dist = Math.Max(
                Math.Abs(kvp.Key.Item1 - segX),
                Math.Abs(kvp.Key.Item2 - segZ));

            if (dist > UnloadRadius)
                _toRemove.Add(kvp.Key);
        }

        foreach (var pos in _toRemove)
            UnloadSegment(pos);
    }

    // ────────────────────── Create ──────────────────────

    private void CreateSegment(int segX, int segZ)
    {
        try
        {
            ref var faceId = ref _activeFace.GetComponent<FaceIdentity>();

            string fileName = $"seg_{segX}_{segZ}{ConstantsSegment.FILE_EXTENSION}";
            string filePath = Path.Combine(_faceStoragePath, fileName);

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

            seg.AddComponent(new SegmentParentFace { Face = _activeFace });

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
            _activeSegments[(segX, segZ)] = seg.Id;

            if (fileExists)
                GD.Print($"[SegmentCreator] Segment ({segX},{segZ}) loaded (existing)");
            else
                GD.Print($"[SegmentCreator] Segment ({segX},{segZ}) created (new) → {fileName}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SegmentCreator] Error ({segX},{segZ}): {ex.Message}");
        }
    }

    // ────────────────────── Unload ──────────────────────

    private void UnloadSegment((int, int) pos)
    {
        if (!_activeSegments.TryGetValue(pos, out int id))
            return;

        if (_store.TryGetEntityById(id, out var entity) && !entity.IsNull)
        {
            if (entity.Tags.Has<SegmentDataDirty>())
                entity.AddTag<SegmentNeedsSave>();

            entity.RemoveTag<SegmentActive>();
            entity.AddTag<SegmentInactive>();
        }

        _activeSegments.Remove(pos);
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
        _activeSegments.Clear();
        _initialized = false;
    }
}
