using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Applies gravity to entities with PlayerGravity + PlayerVelocity + GodotBody.
/// Skips entities in noclip mode.
/// </summary>
public class GameplayGravitySystem : QuerySystem<PlayerGravity, PlayerVelocity, GodotBody>
{
    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;

        foreach (var entity in Query.Entities)
        {
            // Skip gravity in noclip mode
            if (entity.HasComponent<PlayerNoclip>())
            {
                ref var noclip = ref entity.GetComponent<PlayerNoclip>();
                if (noclip.IsActive) continue;
            }

            ref var gravity  = ref entity.GetComponent<PlayerGravity>();
            ref var velocity = ref entity.GetComponent<PlayerVelocity>();
            var body = entity.GetComponent<GodotBody>().GetBody();
            if (body == null) continue;

            if (!body.IsOnFloor())
            {
                var vel = velocity.Velocity;
                vel.Y -= gravity.Force * delta;
                vel.Y  = Mathf.Max(vel.Y, -gravity.MaxFallSpeed);
                velocity.Velocity = vel;
            }
        }
    }
}
