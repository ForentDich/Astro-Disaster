using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Smoothly rotates entities to face their movement direction.
/// Mirrors: SystemsG/s_entity_rotation.gd
/// </summary>
public class GameplayEntityRotationSystem : QuerySystem<PlayerVelocity, GodotBody>
{
    protected override void OnUpdate()
    {
        float dt = Tick.deltaTime;

        foreach (var entity in Query.Entities)
        {
            // Skip if linked to a planet — GameplayPlanetAlignSystem handles rotation
            if (entity.HasComponent<GravityAffected>()) continue;

            ref var velocity = ref entity.GetComponent<PlayerVelocity>();
            var body = entity.GetComponent<GodotBody>().GetBody();
            if (body == null) continue;

            float rotSpeed = 10f;
            if (entity.HasComponent<PlayerRotation>())
                rotSpeed = entity.GetComponent<PlayerRotation>().Speed;

            var horizontalDir = new Vector2(velocity.Direction.X, velocity.Direction.Z);
            if (horizontalDir.Length() > 0.1f)
            {
                float targetYaw = Mathf.Atan2(horizontalDir.X, horizontalDir.Y);
                var rot = body.Rotation;
                rot.Y = Mathf.LerpAngle(rot.Y, targetYaw, 1f - Mathf.Exp(-rotSpeed * dt));
                body.Rotation = rot;
            }
        }
    }
}
