using Friflo.Engine.ECS;
using Godot;

public struct PlayerVelocity : IComponent
{
    public float Speed;
    public Vector3 Direction;
    public Vector3 Velocity;
}

public struct PlayerGravity : IComponent
{
    public float Force;
    public float MaxFallSpeed;
}

public struct PlayerJump : IComponent
{
    public float JumpForce;
    public float BufferTimer;
    public float BufferDuration;
}

public struct PlayerRotation : IComponent
{
    public float Speed;
    public Vector3 Facing;
}

public struct PlayerOrbitBoundary : IComponent
{
    public float LowOrbitHeightFactor;
    public float BlendHeightFactor;
    public float MinCornerSafeRadiusFactor;
}

public struct PlayerOrbitState : IComponent
{
    public float AlignWeight;
    public bool IsInSpace;
}
