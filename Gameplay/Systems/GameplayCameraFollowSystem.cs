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

    private static Vector3 GetStableTangent(Vector3 up, Vector3 preferred)
    {
        Vector3 tangent = preferred - up * preferred.Dot(up);
        if (tangent.LengthSquared() < 0.0001f)
        {
            Vector3 axis = Mathf.Abs(up.Dot(Vector3.Up)) < 0.9f
                ? Vector3.Up
                : Vector3.Right;
            tangent = axis - up * axis.Dot(up);
        }
        return tangent.Normalized();
    }

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

            Vector3 up = targetBody.UpDirection;
            if (up.LengthSquared() < 0.001f)
                up = Vector3.Up;

            Vector3 referenceForward = cam.ReferenceForward;
            Vector3 referenceUp = cam.ReferenceUp;

            if (referenceForward.LengthSquared() < 0.001f || referenceUp.LengthSquared() < 0.001f)
            {
                referenceForward = GetStableTangent(up, Vector3.Forward);
                referenceUp = up;
            }
            else
            {
                Vector3 axis = referenceUp.Cross(up);
                float axisLen = axis.Length();
                if (axisLen > 0.0001f)
                {
                    axis /= axisLen;
                    float dot = Mathf.Clamp(referenceUp.Dot(up), -1f, 1f);
                    float angle = Mathf.Acos(dot);
                    referenceForward = referenceForward.Rotated(axis, angle);
                }

                referenceForward = GetStableTangent(up, referenceForward);
                referenceUp = up;
            }

            cam.ReferenceForward = referenceForward;
            cam.ReferenceUp = referenceUp;

            Vector3 forward = referenceForward.Rotated(up, cam.Yaw);
            Vector3 right = up.Cross(forward).Normalized();
            forward = right.Cross(up).Normalized();

            var yawBasis = new Basis(right, up, -forward);
            var worldOffset = yawBasis * cam.ShoulderOffset;
            var center = targetBody.GlobalPosition + worldOffset;

            Vector3 camDir = forward.Rotated(right, cam.Pitch).Normalized();
            var camPos = center - camDir * cam.Distance;

            cameraNode.GlobalPosition = camPos;
            cameraNode.LookAt(center, up);
        }
    }
}
