using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Applies planetary gravity using inverse-square law (KSP-style).
/// Entities with GravityAffected are pulled toward their linked planet's GravitySource.
/// The farther from the planet, the weaker the gravity: g = GM / r².
/// Skips entities in noclip mode.
/// </summary>
public class GameplayGravitySystem : QuerySystem<GravityAffected, PlayerVelocity, PlayerGravity, GodotBody>
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

            ref var affected = ref entity.GetComponent<GravityAffected>();
            ref var velocity = ref entity.GetComponent<PlayerVelocity>();
            ref var gravity  = ref entity.GetComponent<PlayerGravity>();
            var body = entity.GetComponent<GodotBody>().GetBody();
            if (body == null) continue;

            // Get planet data
            var planet = affected.Planet;
            if (!planet.HasComponent<GravitySource>()) continue;

            ref var source = ref planet.GetComponent<GravitySource>();

            var vel = velocity.Velocity;

            Vector3 toCenter = source.Center - body.GlobalPosition;
            float distSq = toCenter.LengthSquared();
            if (distSq > 0.000001f)
            {
                float dist = Mathf.Sqrt(distSq);
                Vector3 gravityDir = toCenter / dist;

                // Inverse-square law: g = GM / r²
                float g = source.GM / distSq;

                float surfaceG = source.Radius > 0.001f
                    ? source.GM / (source.Radius * source.Radius)
                    : g;
                float gravityScale = (gravity.Force > 0f && surfaceG > 0.001f)
                    ? gravity.Force / surfaceG
                    : 1f;

                vel += gravityDir * (g * gravityScale) * delta;

                // Clamp fall speed along the gravity direction
                if (gravity.MaxFallSpeed > 0f)
                {
                    float speedDown = vel.Dot(gravityDir);
                    if (speedDown > gravity.MaxFallSpeed)
                        vel -= gravityDir * (speedDown - gravity.MaxFallSpeed);
                }

                gravity.Direction = gravityDir;
            }

            velocity.Velocity = vel;
        }
    }
}
