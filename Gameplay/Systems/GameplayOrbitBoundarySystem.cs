using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Computes the boundary between surface and space (low orbit) and
/// blends alignment strength near that boundary.
/// </summary>
public class GameplayOrbitBoundarySystem : QuerySystem<GravityAffected, GodotBody, PlayerOrbitBoundary, PlayerOrbitState>
{
    private bool _f4WasDown;

    public GameplayOrbitBoundarySystem() => Filter.AllTags(Tags.Get<PlayerTag>());

    protected override void OnUpdate()
    {
        bool f4Down = Input.IsKeyPressed(Key.F4);
        bool f4Pressed = f4Down && !_f4WasDown;
        _f4WasDown = f4Down;

        bool printed = false;

        foreach (var entity in Query.Entities)
        {
            ref var affected = ref entity.GetComponent<GravityAffected>();
            ref var boundary = ref entity.GetComponent<PlayerOrbitBoundary>();
            ref var state = ref entity.GetComponent<PlayerOrbitState>();
            var body = entity.GetComponent<GodotBody>().GetBody();
            if (body == null) continue;

            var planet = affected.Planet;
            if (!planet.HasComponent<GravitySource>()) continue;
            ref var source = ref planet.GetComponent<GravitySource>();

            float radius = source.Radius;
            if (radius <= 0.001f) continue;

            float dist = (body.GlobalPosition - source.Center).Length();
            float lowOrbitHeight = radius * boundary.LowOrbitHeightFactor;
            float boundaryRadius = radius + lowOrbitHeight;

            float minCornerSafe = radius * boundary.MinCornerSafeRadiusFactor;
            if (boundaryRadius < minCornerSafe)
                boundaryRadius = minCornerSafe;

            float blendHeight = radius * boundary.BlendHeightFactor;
            if (blendHeight < 0.001f)
                blendHeight = 0.001f;

            float blendStart = boundaryRadius - blendHeight;
            float blendEnd = boundaryRadius + blendHeight;

            float alignWeight = 1f;
            if (dist >= blendEnd)
            {
                alignWeight = 0f;
            }
            else if (dist > blendStart)
            {
                alignWeight = 1f - (dist - blendStart) / (blendEnd - blendStart);
            }

            state.AlignWeight = alignWeight;
            state.IsInSpace = dist >= boundaryRadius;
            state.DistanceFromCenter = dist;
            state.Altitude = Mathf.Max(0f, dist - radius);
            state.BoundaryRadius = boundaryRadius;

            if (f4Pressed && !printed)
            {
                GD.Print(
                    $"[Orbit] Alt={state.Altitude:0.0} Dist={dist:0.0} Boundary={boundaryRadius:0.0} " +
                    $"Align={alignWeight:0.00} Space={(state.IsInSpace ? "yes" : "no")}");
                printed = true;
            }
        }
    }
}
