using Friflo.Engine.ECS;
using Godot;

/// <summary>
/// Holds the result of the tile-selection raycast.
/// Updated every frame by <see cref="TileCursorSystem"/>.
/// </summary>
public struct TileCursor : IComponent
{
	/// <summary>Max horizontal distance from player to selected tile (world units).</summary>
	public float MaxReach;
	public bool IsActive;
	public Vector3I TileCoord;
	public Vector3 WorldPosition;
	public Vector3 HitNormal;
}


public struct TileCursorVisual : IComponent
{
	public ulong InstanceId;
	public MeshInstance3D GetMesh() => GodotObject.InstanceFromId(InstanceId) as MeshInstance3D;
}
