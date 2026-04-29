using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Computes the boundary between surface and space (low orbit) and
/// blends alignment strength near that boundary.
/// </summary>
public class GameplayOrbitBoundarySystem : QuerySystem<GravityAffected, GodotBody, PlayerOrbitBoundary, PlayerOrbitState>
{

    public GameplayOrbitBoundarySystem() => Filter.AllTags(Tags.Get<PlayerTag>());

    protected override void OnUpdate()
    {
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
        }
    }
}
