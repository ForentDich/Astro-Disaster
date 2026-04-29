using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Reads keyboard/gamepad input and sets PlayerVelocity.Direction
/// relative to the orbital camera's yaw.
/// Mirrors: SystemsG/s_input_player.gd
/// </summary>
public class PlayerInputSystem : QuerySystem<PlayerVelocity, GodotBody>
{
    /// <summary>Query to find the camera linked to the player.</summary>
    private ArchetypeQuery<CameraFollowsPlayer, OrbitalCameraData> _cameraQuery;

    public PlayerInputSystem() => Filter.AllTags(Tags.Get<PlayerTag>());

    protected override void OnAddStore(EntityStore store)
    {
        _cameraQuery = store.Query<CameraFollowsPlayer, OrbitalCameraData>();
    }

    /// <summary>
    /// Finds the OrbitalCameraData for a camera that follows the given player entity.
    /// Returns true if found, with yaw and reference forward populated.
    /// </summary>
    private bool TryCameraData(Entity playerEntity, out float yaw, out Vector3 referenceForward)
    {
        yaw = 0f;
        referenceForward = Vector3.Zero;
        foreach (var entity in _cameraQuery.Entities)
        {
            var follow = entity.GetComponent<CameraFollowsPlayer>();
            if (follow.Target.Id != playerEntity.Id) continue;
            var cam = entity.GetComponent<OrbitalCameraData>();
            yaw = cam.Yaw;
            referenceForward = cam.ReferenceForward;
            return true;
        }
        return false;
    }

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
            ref var velocity = ref entity.GetComponent<PlayerVelocity>();
            var body = entity.GetComponent<GodotBody>().GetBody();
            if (body == null) continue;

            Vector3 rawInput = Vector3.Zero;
            rawInput.Z = Input.GetAxis("move_forward", "move_backward");
            rawInput.X = Input.GetAxis("move_left", "move_right");

            // ── Camera-relative direction ──
            if (rawInput.Length() > 0.1f && TryCameraData(entity, out float camYaw, out Vector3 referenceForward))
            {
                Vector3 up = body.UpDirection;
                if (up.LengthSquared() < 0.001f) up = Vector3.Up;

                Vector3 baseForward = referenceForward.LengthSquared() < 0.001f
                    ? GetStableTangent(up, Vector3.Forward)
                    : GetStableTangent(up, referenceForward);

                Vector3 cameraForward = baseForward.Rotated(up, camYaw);
                cameraForward = -cameraForward;
                Vector3 cameraRight = up.Cross(cameraForward).Normalized();
                cameraForward = cameraRight.Cross(up).Normalized();
                Vector3 worldDir = (cameraForward * rawInput.Z) + (cameraRight * rawInput.X);

                velocity.Direction = worldDir.Normalized();
            }
            else
            {
                velocity.Direction = Vector3.Zero;
            }


            if (Input.IsActionJustPressed("jump") && entity.HasComponent<PlayerJump>())
            {
                ref var jump = ref entity.GetComponent<PlayerJump>();
                jump.BufferTimer = jump.BufferDuration;
            }
        }
    }
}
