using Friflo.Engine.ECS;
using Godot;

public struct ChunkInfo : IComponent {
	public int X;
	public int Z;
	public byte LOD;
}

public struct ChunkTerrain : IComponent
{
	public byte[] Data; // [height, tileType] * CHUNK_SIZE * CHUNK_SIZEы
}
public struct ChunkMesh: IComponent
{
	public ulong InstaceId;
	public MeshInstance3D GetMesh() => GodotObject.InstanceFromId(InstaceId) as MeshInstance3D;
}

public struct ChunkCollider : IComponent
{
	public ulong BodyId;
	public StaticBody3D GetBody() => GodotObject.InstanceFromId(BodyId) as StaticBody3D;
}
