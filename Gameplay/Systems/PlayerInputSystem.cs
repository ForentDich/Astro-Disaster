using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Reads keyboard/gamepad input and sets PlayerVelocity.Direction
/// relative to the orbital camera's yaw.
/// Handles noclip toggle (V key) and jump buffer initiation.
/// Mirrors: SystemsG/s_input_player.gd
/// </summary>
public class PlayerInputSystem : QuerySystem<PlayerVelocity>
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
    /// Returns true if found, with yaw populated.
    /// </summary>
    private bool TryCameraYaw(Entity playerEntity, out float yaw)
    {
        yaw = 0f;
        foreach (var entity in _cameraQuery.Entities)
        {
            var follow = entity.GetComponent<CameraFollowsPlayer>();
            if (follow.Target.Id != playerEntity.Id) continue;
            yaw = entity.GetComponent<OrbitalCameraData>().Yaw;
            return true;
        }
        return false;
    }

    protected override void OnUpdate()
    {
        foreach (var entity in Query.Entities)
        {
            ref var velocity = ref entity.GetComponent<PlayerVelocity>();

            var rawInput = Vector3.Zero;
            rawInput.Z = Input.GetAxis("move_forward", "move_backward");
            rawInput.X = Input.GetAxis("move_left", "move_right");

            // ── Noclip toggle ──
            bool isNoclip = false;
            if (entity.HasComponent<PlayerNoclip>())
            {
                ref var noclip = ref entity.GetComponent<PlayerNoclip>();
                if (Input.IsActionJustPressed("v"))
                {
                    noclip.IsActive = !noclip.IsActive;
                    GD.Print("Noclip: ", noclip.IsActive ? "ON" : "OFF");
                }
                isNoclip = noclip.IsActive;

                if (isNoclip)
                {
                    if (Input.IsActionPressed("jump"))        rawInput.Y =  25f;
                    else if (Input.IsActionPressed("crouch")) rawInput.Y = -25f;
                }
            }

            // ── Camera-relative direction ──
            if (rawInput.Length() > 0.1f && TryCameraYaw(entity, out float camYaw))
            {
                var cameraForward = (-Vector3.Forward).Rotated(Vector3.Up, camYaw);
                var cameraRight   = Vector3.Right.Rotated(Vector3.Up, camYaw);

                var worldDir = cameraForward * rawInput.Z
                             + cameraRight   * rawInput.X;

                if (isNoclip)
                    worldDir += Vector3.Up * rawInput.Y;

                velocity.Direction = worldDir.Normalized();
            }
            else
            {
                velocity.Direction = Vector3.Zero;
            }

            // ── Jump buffer (only outside noclip) ──
            if (Input.IsActionJustPressed("jump") && !isNoclip
                && entity.HasComponent<PlayerJump>())
            {
                ref var jump = ref entity.GetComponent<PlayerJump>();
                jump.BufferTimer = jump.BufferDuration;
            }
        }
    }
}
