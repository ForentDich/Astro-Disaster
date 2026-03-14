using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Generates tree positions for chunks that just finished mesh build.
/// Runs after ChunkMeshBuildSystem (uses ChunkComplete tag).
/// Uses stored terrain data (surface, height, tileType) to decide where trees go.
/// </summary>
public class TreeSpawnSystem : QuerySystem<ChunkInfo, ChunkTerrain>
{
	public int SeaLevelTile { get; set; }

	/// <summary>Surface ID → tree type mapping. Populated from biomes.json.</summary>
	public TreeType[] SurfaceTreeMap { get; set; }

	/// <summary>Surface ID → tree density mapping.</summary>
	public float[] SurfaceDensityMap { get; set; }

	public TreeSpawnSystem() => Filter
		.AllTags(Tags.Get<ChunkComplete>())
		.WithoutAllTags(Tags.Get<PendingRemoval>());

	protected override void OnUpdate()
	{
		if (SurfaceTreeMap == null || SurfaceDensityMap == null) return;

		var buffer = CommandBuffer;

		foreach (var entity in Query.Entities)
		{
			// Skip if already has trees
			if (entity.HasComponent<ChunkTrees>()) continue;

			ref var info = ref entity.GetComponent<ChunkInfo>();
			ref var terrain = ref entity.GetComponent<ChunkTerrain>();
			SpawnTrees(buffer, entity.Id, ref info, ref terrain);
		}
	}

	private void SpawnTrees(CommandBuffer buffer, int entityId,
		ref ChunkInfo info, ref ChunkTerrain terrain)
	{
		var data = terrain.Data;
		if (data == null) return;

		int size   = ChunkConstants.CHUNK_SIZE;
		int stride = ChunkConstants.BYTES_PER_TILE;
		float ts   = ChunkConstants.TILE_SIZE;
		float th   = ChunkConstants.TILE_HEIGHT;
		float worldX = info.X * ChunkConstants.CHUNK_WORLD_SIZE;
		float worldZ = info.Z * ChunkConstants.CHUNK_WORLD_SIZE;

		var positions = new List<Vector3>();
		var types     = new List<TreeType>();
		var seeds     = new List<int>();

		for (int z = 0; z < size; z++)
		{
			for (int x = 0; x < size; x++)
			{
				int idx = (z * size + x) * stride;
				int baseHeight = data[idx];
				var tileType   = (TileType)data[idx + 1];
				byte surfRaw   = data[idx + 2];
				bool hasWater  = (surfRaw & ChunkConstants.WATER_FLAG) != 0;
				int surfaceId  = surfRaw & ChunkConstants.SURFACE_MASK;

				// Only flat tiles, above sea level, no water
				if (tileType != TileType.Flat) continue;
				if (baseHeight < SeaLevelTile) continue;
				if (hasWater) continue;

				// Lookup tree type for this surface
				if (surfaceId >= SurfaceTreeMap.Length) continue;
				TreeType treeType = SurfaceTreeMap[surfaceId];
				if (treeType == TreeType.None) continue;

				float density = surfaceId < SurfaceDensityMap.Length
					? SurfaceDensityMap[surfaceId] : 0f;
				if (density <= 0f) continue;

				// Deterministic hash per tile
				int tileHash = HashTile(info.X, info.Z, x, z);
				float roll = (tileHash & 0xFFFF) / 65535f;
				if (roll > density) continue;

				// World position: tile center + slight random jitter
				float wx = worldX + x * ts + ts * 0.5f;
				float wz = worldZ + z * ts + ts * 0.5f;
				float wy = baseHeight * th;

				float ox = ((tileHash >> 16) & 0xFF) / 255f - 0.5f;
				float oz = ((tileHash >> 8)  & 0xFF) / 255f - 0.5f;
				wx += ox * ts * 0.6f;
				wz += oz * ts * 0.6f;

				positions.Add(new Vector3(wx, wy, wz));
				types.Add(treeType);
				seeds.Add(tileHash);
			}
		}

		if (positions.Count > 0)
		{
			buffer.AddComponent(entityId, new ChunkTrees
			{
				Positions = positions.ToArray(),
				Types     = types.ToArray(),
				Seeds     = seeds.ToArray(),
				Count     = positions.Count
			});
		}
	}

	private static int HashTile(int cx, int cz, int tx, int tz)
	{
		int h = cx * 374761393 + cz * 668265263 + tx * 1274126177 + tz * 1162735585;
		h = (h ^ (h >> 13)) * 1103515245;
		h ^= h >> 16;
		return h;
	}
}
