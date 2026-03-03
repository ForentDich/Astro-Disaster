using Friflo.Engine.ECS;
using Godot;

/// <summary>
/// Stores a reference to a CharacterBody3D node via its Godot InstanceId.
/// Same pattern used by ChunkMesh / ChunkCollider in the terrain pipeline.
/// </summary>
public struct GodotBody : IComponent
{
    public ulong InstanceId;
    public CharacterBody3D GetBody() => GodotObject.InstanceFromId(InstanceId) as CharacterBody3D;
}

/// <summary>
/// Stores a reference to a Camera3D node via its Godot InstanceId.
/// </summary>
public struct GodotCamera : IComponent
{
    public ulong InstanceId;
    public Camera3D GetCamera() => GodotObject.InstanceFromId(InstanceId) as Camera3D;
}

/// <summary>
/// Singleton component for accumulated input state.
/// Written by GameplaySession (Node) each frame, read by ECS systems.
/// </summary>
public struct InputState : IComponent
{
    public float MouseDeltaX;
    public float MouseDeltaY;
    public float ScrollDelta;
}
