using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Aligns the player's up vector away from the planet center.
/// Uses GravityAffected link to find the planet's GravitySource.
/// </summary>
public class GameplayPlanetAlignSystem : QuerySystem<GravityAffected, GodotBody>
{
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
        float dt = Tick.deltaTime;

        foreach (var entity in Query.Entities)
        {
            ref var affected = ref entity.GetComponent<GravityAffected>();
            var body = entity.GetComponent<GodotBody>().GetBody();
            if (body == null) continue;

            var planet = affected.Planet;
            if (!planet.HasComponent<GravitySource>()) continue;

            ref var source = ref planet.GetComponent<GravitySource>();

            // Direction from planet center → player
            Vector3 fromCenter = body.GlobalPosition - source.Center;
            if (fromCenter.LengthSquared() < 0.001f) continue;

            Vector3 up = fromCenter.Normalized();

            // Set UpDirection for MoveAndSlide so it knows what "floor" is
            body.UpDirection = up;

            float alignWeight = 1f;
            if (entity.HasComponent<PlayerOrbitState>())
                alignWeight = entity.GetComponent<PlayerOrbitState>().AlignWeight;
            if (alignWeight <= 0.001f)
                continue;

            // Build target quaternion: Y = up, forward from last input when idle
            Vector3 currentForward = -body.GlobalTransform.Basis.Z;
            Vector3 desiredForward = currentForward;

            if (entity.HasComponent<PlayerRotation>())
            {
                ref var rotation = ref entity.GetComponent<PlayerRotation>();
                Vector3 facing = rotation.Facing;
                if (facing.LengthSquared() < 0.001f)
                    facing = currentForward;

                if (entity.HasComponent<PlayerVelocity>())
                {
                    var inputDir = entity.GetComponent<PlayerVelocity>().Direction;
                    if (inputDir.LengthSquared() > 0.001f)
                        facing = inputDir;
                }

                rotation.Facing = GetStableTangent(up, facing);
                desiredForward = rotation.Facing;
            }
            else if (entity.HasComponent<PlayerVelocity>())
            {
                var inputDir = entity.GetComponent<PlayerVelocity>().Direction;
                if (inputDir.LengthSquared() > 0.001f)
                    desiredForward = inputDir;

                desiredForward = GetStableTangent(up, desiredForward);
            }
            else
            {
                desiredForward = GetStableTangent(up, desiredForward);
            }

            Vector3 targetZ = desiredForward;

            Vector3 targetX = up.Cross(targetZ).Normalized();
            targetZ = targetX.Cross(up).Normalized();

            Quaternion targetQ = new Basis(targetX, up, -targetZ).GetRotationQuaternion().Normalized();
            Quaternion currentQ = body.GlobalTransform.Basis.GetRotationQuaternion().Normalized();

            float rotSpeed = 10f;
            if (entity.HasComponent<PlayerRotation>())
                rotSpeed = entity.GetComponent<PlayerRotation>().Speed;

            // Smoothly interpolate
            float t = (1.0f - Mathf.Exp(-rotSpeed * dt)) * alignWeight;
            body.GlobalTransform = new Transform3D(
                new Basis(currentQ.Slerp(targetQ, t)),
                body.GlobalPosition
            );
        }
    }
}
