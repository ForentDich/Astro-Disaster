using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System.Collections.Generic;

/// <summary>
/// Creates MultiMeshInstance3D nodes for chunks that have ChunkTrees but no ChunkTreeMesh.
/// Groups trees by (type, variation) for batched rendering.
/// Uses TreeTypeRegistry for per-type materials (texture + color tint).
/// </summary>
public class TreeRenderSystem : QuerySystem<ChunkInfo, ChunkTrees>
{
	public Node3D TreeRoot { get; set; }

	public TreeRenderSystem() => Filter
		.WithoutAllTags(Tags.Get<PendingRemoval>());

	private readonly List<(int entityId, ChunkTrees trees)> _pending = new();

	protected override void OnUpdate()
	{
		// Phase 1: collect entities that need rendering (no structural changes)
		_pending.Clear();
		foreach (var entity in Query.Entities)
		{
			if (entity.HasComponent<ChunkTreeMesh>()) continue;

			ref var trees = ref entity.GetComponent<ChunkTrees>();
			if (trees.Count == 0) continue;

			_pending.Add((entity.Id, trees));
		}

		// Phase 2: create visuals + add components (outside query loop)
		var buffer = CommandBuffer;
		for (int i = 0; i < _pending.Count; i++)
		{
			var (entityId, trees) = _pending[i];
			var ids = RenderTrees(ref trees);
			buffer.AddComponent(entityId, new ChunkTreeMesh { InstanceIds = ids });
		}
	}

	private ulong[] RenderTrees(ref ChunkTrees trees)
	{
		// Group by (TreeType, variation) so each group shares one mesh
		var groups = new Dictionary<(TreeType type, int variation), List<(Vector3 pos, int seed)>>();

		for (int i = 0; i < trees.Count; i++)
		{
			var type = trees.Types[i];
			int seed = trees.Seeds[i];
			int variation = (seed & 0x7FFFFFFF) % 20; // must match VARIATIONS_PER_TYPE
			var key = (type, variation);

			if (!groups.ContainsKey(key))
				groups[key] = new List<(Vector3, int)>();
			groups[key].Add((trees.Positions[i], seed));
		}

		var instanceIds = new List<ulong>();

		foreach (var ((type, variation), instances) in groups)
		{
			var template = TreeTemplateGenerator.Get(type, variation);
			
			// Get per-type materials from registry
			var trunkMatPerType = TreeTypeRegistry.GetTrunkMaterial(type);
			var canopyMatPerType = TreeTypeRegistry.GetCanopyMaterial(type);

			// ── Trunk MultiMesh ──
			var trunkMM = new MultiMesh
			{
				TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
				Mesh = template.TrunkMesh,
				InstanceCount = instances.Count
			};

			// ── Canopy MultiMesh ──
			var canopyMM = new MultiMesh
			{
				TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
				Mesh = template.CanopyMesh,
				InstanceCount = instances.Count
			};

			for (int i = 0; i < instances.Count; i++)
			{
				var (wpos, seed) = instances[i];

				// Scale: 0.8 – 1.2
				float scale = 0.8f + ((seed & 0xFF) / 255f) * 0.4f;
				// Random Y rotation
				float rotY = (((seed >> 8) & 0xFF) / 255f) * Mathf.Tau;

				var xform = Transform3D.Identity
					.Rotated(Vector3.Up, rotY)
					.Scaled(new Vector3(scale, scale, scale));
				xform.Origin = wpos;

				trunkMM.SetInstanceTransform(i, xform);
				canopyMM.SetInstanceTransform(i, xform);
			}

			// Trunk node
			var trunkNode = new MultiMeshInstance3D
			{
				Multimesh = trunkMM,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.On
			};
			if (trunkMatPerType != null)
				trunkNode.MaterialOverride = trunkMatPerType;
			TreeRoot.AddChild(trunkNode);
			instanceIds.Add(trunkNode.GetInstanceId());

			// Canopy node
			var canopyNode = new MultiMeshInstance3D
			{
				Multimesh = canopyMM,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.On
			};
			if (canopyMatPerType != null)
				canopyNode.MaterialOverride = canopyMatPerType;
			TreeRoot.AddChild(canopyNode);
			instanceIds.Add(canopyNode.GetInstanceId());
		}

		return instanceIds.ToArray();
	}
}
