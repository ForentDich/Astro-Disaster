using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Positions the Camera3D around the target entity using orbital parameters
/// (yaw, pitch, distance, shoulder offset).
/// Uses CameraFollowsPlayer link to find the target.
/// Mirrors: SystemsG/s_camera_follow.gd
/// </summary>
public class GameplayCameraFollowSystem : QuerySystem<OrbitalCameraData, GodotCamera>
{
    public GameplayCameraFollowSystem() => Filter.AllTags(Tags.Get<CameraTag>());

    protected override void OnUpdate()
    {
        foreach (var entity in Query.Entities)
        {
            if (!entity.HasComponent<CameraFollowsPlayer>()) continue;

            var follow     = entity.GetComponent<CameraFollowsPlayer>();
            ref var cam    = ref entity.GetComponent<OrbitalCameraData>();
            var cameraNode = entity.GetComponent<GodotCamera>().GetCamera();
            if (cameraNode == null) continue;

            var targetEntity = follow.Target;
            if (!targetEntity.HasComponent<GodotBody>()) continue;

            var targetBody = targetEntity.GetComponent<GodotBody>().GetBody();
            if (targetBody == null) continue;

            // Shoulder offset relative to camera yaw (not player rotation)
            var cameraBasis = Basis.Identity.Rotated(Vector3.Up, cam.Yaw);
            var worldOffset = cameraBasis * cam.ShoulderOffset;
            var center      = targetBody.GlobalPosition + worldOffset;

            // Spherical coordinates around center
            var camPos = new Vector3(
                center.X + cam.Distance * Mathf.Sin(cam.Yaw) * Mathf.Cos(cam.Pitch),
                center.Y + cam.Distance * Mathf.Sin(cam.Pitch),
                center.Z + cam.Distance * Mathf.Cos(cam.Yaw) * Mathf.Cos(cam.Pitch)
            );

            cameraNode.GlobalPosition = camPos;
            cameraNode.LookAt(center);
        }
    }
}
