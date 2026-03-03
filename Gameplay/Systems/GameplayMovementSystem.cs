using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Applies velocity to CharacterBody3D via MoveAndSlide.
/// Handles noclip flight mode with wall-clipping and speed multipliers.
/// Mirrors: SystemsG/s_movement.gd
/// </summary>
public class GameplayMovementSystem : QuerySystem<PlayerVelocity, GodotBody>
{
    protected override void OnUpdate()
    {
        foreach (var entity in Query.Entities)
        {
            ref var velocity = ref entity.GetComponent<PlayerVelocity>();
            var body = entity.GetComponent<GodotBody>().GetBody();
            if (body == null) continue;

            bool isNoclip = false;
            float speedMul = 1f;
            float vertSpeed = 10f;
            bool flyThroughWalls = false;

            if (entity.HasComponent<PlayerNoclip>())
            {
                ref var noclip = ref entity.GetComponent<PlayerNoclip>();
                isNoclip        = noclip.IsActive;
                speedMul        = noclip.SpeedMultiplier;
                vertSpeed       = noclip.VerticalSpeed;
                flyThroughWalls = noclip.CanFlyThroughWalls;
            }

            var vel = velocity.Velocity;

            if (isNoclip)
            {
                float currentSpeed = velocity.Speed * speedMul;
                vel.X = velocity.Direction.X * currentSpeed;
                vel.Z = velocity.Direction.Z * currentSpeed;
                vel.Y = velocity.Direction.Y * vertSpeed;

                if (Mathf.Abs(velocity.Direction.Y) < 0.1f)
                    vel.Y = 0f;

                body.CollisionMask = flyThroughWalls ? 0u : 1u;
            }
            else
            {
                body.CollisionMask = 1;
                vel.X = velocity.Direction.X * velocity.Speed;
                vel.Z = velocity.Direction.Z * velocity.Speed;
                // vel.Y is handled by GravitySystem / JumpSystem
            }

            velocity.Velocity = vel;
            body.Velocity = velocity.Velocity;
            body.MoveAndSlide();
            velocity.Velocity = body.Velocity;

            // Sprint boost in noclip (applies to NEXT frame, matches original behavior)
            if (isNoclip && Input.IsActionPressed("shift"))
            {
                velocity.Velocity *= 2f;
            }
        }
    }
}
