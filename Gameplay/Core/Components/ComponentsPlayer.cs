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
    public Vector3 Direction;
    public float Force;
    public float MaxFallSpeed;
}

public struct PlayerJump : IComponent
{
    public float JumpForce;
    public float BufferTimer;
    public float BufferDuration;
}

public struct PlayerNoclip : IComponent
{
    public bool IsActive;
    public float SpeedMultiplier;
    public float VerticalSpeed;
    public bool CanFlyThroughWalls;
}

public struct PlayerHealth : IComponent
{
    public float Current;
    public float Maximum;
}

public struct PlayerRotation : IComponent
{
    public float Speed;
}
