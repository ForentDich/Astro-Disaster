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
        float dt = Tick.deltaTime;

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

                if (entity.HasComponent<GravityAffected>())
                {
                    Vector3 up = body.UpDirection;
                    Vector3 inputDir = velocity.Direction;

                    Vector3 tangentInput = inputDir - up * inputDir.Dot(up);
                    Vector3 desiredTangent = Vector3.Zero;
                    if (tangentInput.LengthSquared() > 0.001f)
                        desiredTangent = tangentInput.Normalized() * velocity.Speed;

                    float verticalSpeed = vel.Dot(up);
                    Vector3 verticalVel = up * verticalSpeed;
                    Vector3 tangentVel = vel - verticalVel;

                    bool onFloor = body.IsOnFloor();
                    float groundAccel = velocity.Speed * 8f;
                    float airAccel = velocity.Speed * 2.5f;
                    float groundFriction = velocity.Speed * 10f;

                    if (desiredTangent.LengthSquared() > 0.001f)
                    {
                        float accel = onFloor ? groundAccel : airAccel;
                        tangentVel = tangentVel.MoveToward(desiredTangent, accel * dt);
                    }
                    else if (onFloor)
                    {
                        tangentVel = tangentVel.MoveToward(Vector3.Zero, groundFriction * dt);
                    }

                    vel = tangentVel + verticalVel;
                }
                else
                {
                    vel.X = velocity.Direction.X * velocity.Speed;
                    vel.Z = velocity.Direction.Z * velocity.Speed;
                    // vel.Y is handled by GravitySystem / JumpSystem
                }
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
