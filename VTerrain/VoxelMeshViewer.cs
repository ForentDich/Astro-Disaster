using Godot;
using System;

[Tool]
public partial class VoxelMeshViewer : Node3D
{
	[Export] public BlockShape Shape 
	{ 
		get => _shape;
		set
		{
			_shape = value;
			UpdateMesh();
		}
	}
	
	[Export] public Direction Direction 
	{ 
		get => _direction;
		set
		{
			_direction = value;
			UpdateMesh();
		}
	}
	
	private BlockShape _shape = BlockShape.Cube;
	private Direction _direction = Direction.North;
	private MeshInstance3D _meshInstance;

	public override void _Ready()
	{
		VoxelMeshes.Initialize();
		
		_meshInstance = new MeshInstance3D();
		AddChild(_meshInstance);
		
		UpdateMesh();
	}
	
	private void UpdateMesh()
	{
		if (_meshInstance != null)
		{
			_meshInstance.Mesh = VoxelMeshes.GetMesh(_shape, _direction);
		}
	}
	
	public override void _ExitTree()
	{
		VoxelMeshes.Dispose();
	}
}
