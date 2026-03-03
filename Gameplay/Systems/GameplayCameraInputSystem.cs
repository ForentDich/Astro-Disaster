using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

/// <summary>
/// Reads accumulated mouse motion / scroll from InputState singleton
/// and updates OrbitalCameraData (yaw, pitch, distance lerp).
/// Mirrors: SystemsG/s_input_camera.gd
/// </summary>
public class GameplayCameraInputSystem : QuerySystem<OrbitalCameraData>
{
    private ArchetypeQuery<InputState> _inputQuery;

    public GameplayCameraInputSystem() => Filter.AllTags(Tags.Get<CameraTag>());

    protected override void OnAddStore(EntityStore store)
    {
        _inputQuery = store.Query<InputState>().AllTags(Tags.Get<InputSingleton>());
    }

    protected override void OnUpdate()
    {
        // ── Read input singleton ──
        float mouseDX = 0, mouseDY = 0, scroll = 0;
        _inputQuery.ForEachEntity((ref InputState state, Entity e) =>
        {
            mouseDX = state.MouseDeltaX;
            mouseDY = state.MouseDeltaY;
            scroll  = state.ScrollDelta;
        });

        float delta = Tick.deltaTime;

        foreach (var entity in Query.Entities)
        {
            ref var cam = ref entity.GetComponent<OrbitalCameraData>();

            // ── Mouse rotation ──
            if (mouseDX != 0 || mouseDY != 0)
            {
                cam.Yaw   += -mouseDX * cam.Sensitivity;
                cam.Pitch +=  mouseDY * cam.Sensitivity;
                cam.Pitch  = Mathf.Clamp(cam.Pitch, -Mathf.Pi / 4f, Mathf.Pi / 3f);
            }

            // ── Scroll zoom ──
            if (scroll != 0)
            {
                if (cam.TargetDistance < 0)
                    cam.TargetDistance = cam.Distance;
                cam.TargetDistance = Mathf.Clamp(cam.TargetDistance + scroll, 1.5f, 8.0f);
            }

            // ── Distance lerp ──
            if (cam.TargetDistance >= 0)
            {
                cam.Distance = Mathf.Lerp(cam.Distance, cam.TargetDistance,
                                          cam.DistanceLerpSpeed * delta);

                if (Mathf.Abs(cam.Distance - cam.TargetDistance) < 0.05f)
                {
                    cam.Distance       = cam.TargetDistance;
                    cam.TargetDistance  = -1f;
                }
            }
        }
    }
}
