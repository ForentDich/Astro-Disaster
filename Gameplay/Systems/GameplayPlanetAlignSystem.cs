using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Aligns the player's up vector away from the planet center.
/// Uses GravityAffected link to find the planet's GravitySource.
/// </summary>
public class GameplayPlanetAlignSystem : QuerySystem<GravityAffected, GodotBody>
{
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

            // Build target quaternion: Y = up, forward from input if available
            Vector3 currentForward = -body.GlobalTransform.Basis.Z;
            Vector3 desiredForward = currentForward;

            if (entity.HasComponent<PlayerVelocity>())
            {
                var inputDir = entity.GetComponent<PlayerVelocity>().Direction;
                Vector3 tangentInput = inputDir - up * inputDir.Dot(up);
                if (tangentInput.LengthSquared() > 0.001f)
                    desiredForward = tangentInput.Normalized();
            }

            Vector3 targetZ = desiredForward - up * desiredForward.Dot(up);
            if (targetZ.LengthSquared() < 0.001f)
                targetZ = currentForward - up * currentForward.Dot(up);
            if (targetZ.LengthSquared() < 0.001f)
                targetZ = body.GlobalTransform.Basis.X - up * body.GlobalTransform.Basis.X.Dot(up);
            targetZ = targetZ.Normalized();

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
