using Godot;
using System;
using Friflo.Engine.ECS;

public partial class VoxelWorld : Node
{
	public override void _Ready()
	{
		var store = new EntityStore();
		GD.Print("Init");

		var player = store.CreateEntity();

		player.AddComponent(new Position{ x = 0, y = 0, z = 0});
		player.AddComponent(new Health{ value = 100 });
		player.AddComponent(new PlayerTag{ });

		var zombie = store.CreateEntity(new Position{ x = 0, y = 0, z = 0}, new Health{ value = 100 });
		GD.Print($"{player.Id} и {zombie.Id}");
		

		
	}
}

public struct Position : IComponent { public float x, y, z; }
public struct Health : IComponent { public int value; }
public struct PlayerTag : IComponent { }
