using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Handles jump buffering: stores jump intent for a short window,
/// applies jump_force when the player lands within that window.
/// Mirrors: SystemsG/s_jump.gd
/// </summary>
public class GameplayJumpSystem : QuerySystem<PlayerJump, PlayerVelocity, GodotBody>
{
    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;

        foreach (var entity in Query.Entities)
        {
            ref var jump     = ref entity.GetComponent<PlayerJump>();
            ref var velocity = ref entity.GetComponent<PlayerVelocity>();
            var body = entity.GetComponent<GodotBody>().GetBody();
            if (body == null) continue;

            if (jump.BufferTimer > 0)
                jump.BufferTimer -= delta;

            if (jump.BufferTimer > 0 && body.IsOnFloor())
            {
                var vel = velocity.Velocity;

                // If linked to a planet, jump away from planet center (along UpDirection)
                if (entity.HasComponent<GravityAffected>())
                    vel += body.UpDirection * jump.JumpForce;
                else
                    vel.Y = jump.JumpForce;

                velocity.Velocity = vel;
                jump.BufferTimer = 0;
            }
        }
    }
}
