using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;

/// <summary>
/// Casts a ray from the camera center each frame to find the tile
/// under the crosshair. Updates <see cref="TileCursor"/> component
/// and moves the wireframe <see cref="TileCursorVisual"/> to match.
///
/// Requires: a camera entity with <see cref="GodotCamera"/> component,
///           and a cursor entity with <see cref="TileCursor"/> + <see cref="TileCursorVisual"/>.
/// </summary>
public class TileCursorSystem : QuerySystem<TileCursor, TileCursorVisual>
{
    private ArchetypeQuery<GodotCamera> _cameraQuery;
    private ArchetypeQuery<GodotBody> _playerQuery;

    protected override void OnAddStore(EntityStore store)
    {
        base.OnAddStore(store);
        _cameraQuery = store.Query<GodotCamera>();
        _playerQuery = store.Query<GodotBody>().AllTags(Tags.Get<PlayerTag>());
    }

    /// <summary>Find the first active Camera3D in the ECS.</summary>
    private Camera3D FindCamera()
    {
        foreach (var entity in _cameraQuery.Entities)
        {
            var cam = entity.GetComponent<GodotCamera>().GetCamera();
            if (cam != null && cam.Current) return cam;
        }
        return null;
    }

    /// <summary>Get player world position (XZ only).</summary>
    private bool TryGetPlayerPos(out Vector3 pos)
    {
        pos = Vector3.Zero;
        foreach (var entity in _playerQuery.Entities)
        {
            var body = entity.GetComponent<GodotBody>().GetBody();
            if (body != null) { pos = body.GlobalPosition; return true; }
        }
        return false;
    }

    protected override void OnUpdate()
    {
        var camera = FindCamera();
        if (camera == null) return;

        if (!TryGetPlayerPos(out var playerPos)) return;

        // Raycast from screen center
        var viewport = camera.GetViewport();
        var screenCenter = viewport.GetVisibleRect().Size / 2f;

        var rayOrigin = camera.ProjectRayOrigin(screenCenter);
        var rayDir    = camera.ProjectRayNormal(screenCenter);

        foreach (var entity in Query.Entities)
        {
            ref var cursor = ref entity.GetComponent<TileCursor>();
            var visual = entity.GetComponent<TileCursorVisual>().GetMesh();

            var rayEnd = rayOrigin + rayDir * camera.Far;

            var spaceState = camera.GetWorld3D().DirectSpaceState;
            var rayQuery = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd);
            rayQuery.CollideWithAreas = false;
            var result = spaceState.IntersectRay(rayQuery);

            if (result.Count == 0)
            {
                cursor.IsActive = false;
                if (visual != null) visual.Visible = false;
                continue;
            }

            var hitPos    = (Vector3)result["position"];
            var hitNormal = (Vector3)result["normal"];

            // Snap to nearest corner (vertex) instead of tile center
            float ts = ChunkConstants.TILE_SIZE;
            float th = ChunkConstants.TILE_HEIGHT;

            int pointX = (int)MathF.Round(hitPos.X / ts);
            int pointY = (int)MathF.Round(hitPos.Y / th);
            int pointZ = (int)MathF.Round(hitPos.Z / ts);

            float cornerX = pointX * ts;
            float cornerY = pointY * th;
            float cornerZ = pointZ * ts;

            // Check horizontal distance from player
            float dx = cornerX - playerPos.X;
            float dz = cornerZ - playerPos.Z;
            if (cursor.MaxReach > 0f && (dx * dx + dz * dz) > cursor.MaxReach * cursor.MaxReach)
            {
                cursor.IsActive = false;
                if (visual != null) visual.Visible = false;
                continue;
            }

            cursor.IsActive      = true;
            cursor.TileCoord     = new Vector3I(pointX, pointY, pointZ);
            cursor.WorldPosition = new Vector3(cornerX, cornerY, cornerZ);
            cursor.HitNormal     = hitNormal;

            if (visual != null)
            {
                visual.GlobalPosition = cursor.WorldPosition;
                visual.Visible = true;
            }

            // Обработка кликов для редактирования!
            if (Input.IsActionJustPressed("edit_raise")) // ЛКМ
            {
                TerrainEditorSystem.RequestEdit(pointX, pointY, pointZ, 1);
            }
            else if (Input.IsActionJustPressed("edit_lower")) // ПКМ
            {
                TerrainEditorSystem.RequestEdit(pointX, pointY, pointZ, -1);
            }
        }
    }
}
