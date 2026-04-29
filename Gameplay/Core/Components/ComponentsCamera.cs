using Friflo.Engine.ECS;
using Godot;

public struct OrbitalCameraData : IComponent
{
    public float Distance;
    /// <summary>Target distance for smooth zoom lerp. -1 means no lerp in progress.</summary>
    public float TargetDistance;
    public float DistanceLerpSpeed;
    public Vector3 ShoulderOffset;
    public float Sensitivity;
    public float Yaw;
    public float Pitch;
    public Vector3 ReferenceForward;
    public Vector3 ReferenceUp;
}

/// <summary>
/// Link component: camera entity → player entity it follows.
/// Only one CameraFollowsPlayer per camera entity.
/// </summary>
public struct CameraFollowsPlayer : ILinkComponent
{
    public Entity Target;
    public Entity GetIndexedValue() => Target;
}
