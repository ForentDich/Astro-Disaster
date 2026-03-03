using Friflo.Engine.ECS;
using Godot;

public struct ChunkInfo : IComponent {
	public int X;
	public int Z;
	public byte LOD;

	public int SegmentX;
	public int SegmentY;
	public int FaceIndex;
}

public struct ChunkTerrain : IComponent
{
	/// <summary>
	/// Tile data: CHUNK_SIZE×CHUNK_SIZE tiles × BYTES_PER_TILE.
	/// Boundary walls are built using neighbor chunk data at mesh-build time.
	/// Persisted as-is to .seg files.
	/// </summary>
	public byte[] Data;
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
