using Friflo.Engine.ECS.Systems;
using Friflo.Engine.ECS;
using Godot;
using System;

public class ChunkRemovalSystem : QuerySystem<ChunkInfo>
{
	private EntityStore _store;
	private int _removalCount;
	public int MaxPerFrame { get; set; } = 8;

	public ChunkRemovalSystem() => Filter.AllTags(Tags.Get<PendingRemoval>());

	protected override void OnAddStore(EntityStore store)
	{
		base.OnAddStore(store);
		_store = store;
	}

	protected override void OnUpdate()
	{
		var buffer = CommandBuffer;
		_removalCount = 0;

		foreach (var entity in Query.Entities)
		{
			if (_removalCount >= MaxPerFrame)
				break;

			try
			{
				if (entity.TryGetComponent<ChunkMesh>(out var mesh))
				{
					var meshInstance = mesh.GetMesh();
					meshInstance?.QueueFree();
				}
				
				if (entity.TryGetComponent<ChunkCollider>(out var collider))
				{
					var body = collider.GetBody();
					body?.QueueFree();
				}
				
				buffer.DeleteEntity(entity.Id);
				_removalCount++;
			}
			catch (System.Exception ex)
			{
				GD.PrintErr($"[ChunkRemovalSystem] >> Error removing chunk {entity.Id}: {ex}");
				buffer.DeleteEntity(entity.Id);
				_removalCount++;
			}
		}
	}

	
}
