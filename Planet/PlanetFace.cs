using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class PlanetFace : MeshInstance3D
{
	[Export] public Vector3 FaceNormal { get; set; } = Vector3.Up;
	[Export] public PlanetData Data { get; set; }
	[Export] public bool IsOcean { get; set; } = false;
	[Export] public Node3D PlayerNode { get; set; } // Переносим PlayerNode сюда

	private QuadtreeChunk rootChunk;
	private Dictionary<string, MeshInstance3D> activeChunks = new Dictionary<string, MeshInstance3D>();
	private Dictionary<string, bool> currentChunks = new Dictionary<string, bool>();

	public override void _Ready()
	{
		if (Data != null)
		{
			MaterialOverride = IsOcean ? Data.OceanMaterial : Data.TerrainMaterial;
			GenerateLOD();
		}
	}

	public void UpdateLOD()
	{
		GenerateLOD();
	}

	public void UpdateLOD(Vector3 focusPoint)
	{
		GenerateLOD();
	}

	private void GenerateLOD()
	{
		if (Data == null) return;

		currentChunks.Clear();

		Vector3 normal = FaceNormal.Normalized();
		Vector3 axisA = new Vector3(normal.Y, normal.Z, normal.X).Normalized();
		Vector3 axisB = normal.Cross(axisA).Normalized();

		Aabb bounds = new Aabb(new Vector3(-1, 0, -1), new Vector3(2, 0, 2));
		rootChunk = new QuadtreeChunk(bounds, 0);

		Vector3 focusPoint = PlayerNode != null ? PlayerNode.GlobalPosition : GlobalPosition;
		
		// ВАЖНО: Всегда пересчитываем дерево при обновлении LOD
		SubdivideChunk(rootChunk, normal, axisA, axisB, focusPoint);
		CollectActiveChunks(rootChunk);
		RemoveInactiveChunks();
		CreateChunkMeshes(rootChunk, normal, axisA, axisB);
	}

	private void SubdivideChunk(QuadtreeChunk chunk, Vector3 faceOrigin, Vector3 axisA, Vector3 axisB, Vector3 focusPoint)
	{
		if (chunk.Depth >= Data.MaxLOD) return;

		float halfSize = chunk.Bounds.Size.X * 0.5f;
		float quarterSize = halfSize * 0.5f;

		// Центр квадранта
		Vector2 chunkCenter2D = new Vector2(chunk.Bounds.Position.X, chunk.Bounds.Position.Z) + new Vector2(halfSize, halfSize);
		Vector3 chunkCenter3D = faceOrigin + axisA * chunkCenter2D.X + axisB * chunkCenter2D.Y;
		Vector3 pointOnSphere = chunkCenter3D.Normalized() * Data.Radius;
		float distance = pointOnSphere.DistanceTo(focusPoint);

		// ВАЖНО: Всегда создаем детей если нужно, не проверяем существование
		if (chunk.Depth < Data.LODDistances.Length && distance <= Data.LODDistances[chunk.Depth])
		{
			Vector2[] offsets = {
				new Vector2(-quarterSize, -quarterSize),
				new Vector2(quarterSize, -quarterSize),
				new Vector2(-quarterSize, quarterSize),
				new Vector2(quarterSize, quarterSize)
			};

			foreach (var offset in offsets)
			{
				Vector2 childCenter2D = chunkCenter2D + offset;
				Aabb childBounds = new Aabb(
					new Vector3(childCenter2D.X - quarterSize, 0, childCenter2D.Y - quarterSize),
					new Vector3(halfSize, 0, halfSize)
				);
				QuadtreeChunk childChunk = new QuadtreeChunk(childBounds, chunk.Depth + 1);
				chunk.Children.Add(childChunk);
				SubdivideChunk(childChunk, faceOrigin, axisA, axisB, focusPoint);
			}
		}
	}

	private void CollectActiveChunks(QuadtreeChunk chunk)
	{
		if (chunk.Children.Count == 0)
		{
			currentChunks[chunk.Identifier] = true;
		}
		else
		{
			foreach (var child in chunk.Children)
			{
				CollectActiveChunks(child);
			}
		}
	}

	private void RemoveInactiveChunks()
	{
		List<string> chunksToRemove = new List<string>();
		foreach (var chunkId in activeChunks.Keys)
		{
			if (!currentChunks.ContainsKey(chunkId))
			{
				chunksToRemove.Add(chunkId);
			}
		}

		foreach (string id in chunksToRemove)
		{
			if (activeChunks.TryGetValue(id, out MeshInstance3D meshInstance))
			{
				RemoveChild(meshInstance);
				meshInstance.QueueFree();
			}
			activeChunks.Remove(id);
		}
	}

	private void CreateChunkMeshes(QuadtreeChunk chunk, Vector3 faceOrigin, Vector3 axisA, Vector3 axisB)
	{
		if (chunk.Children.Count == 0)
		{
			if (!activeChunks.ContainsKey(chunk.Identifier))
			{
				CreateChunkMesh(chunk, faceOrigin, axisA, axisB);
			}
		}
		else
		{
			foreach (var child in chunk.Children)
			{
				CreateChunkMeshes(child, faceOrigin, axisA, axisB);
			}
		}
	}

	private void CreateChunkMesh(QuadtreeChunk chunk, Vector3 faceOrigin, Vector3 axisA, Vector3 axisB)
	{
		int resolution = Data.LODResolutions[Math.Min(chunk.Depth, Data.LODResolutions.Length - 1)];
		float chunkSize = chunk.Bounds.Size.X;
		Vector2 chunkOffset = new Vector2(chunk.Bounds.Position.X, chunk.Bounds.Position.Z);

		SurfaceTool surfaceTool = new SurfaceTool();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

		// Вершины
		for (int y = 0; y < resolution; y++)
		{
			for (int x = 0; x < resolution; x++)
			{
				Vector2 percent = new Vector2(x, y) / (resolution - 1);
				Vector2 point2D = chunkOffset + percent * chunkSize;
				Vector3 pointOnCube = faceOrigin + axisA * point2D.X + axisB * point2D.Y;
				Vector3 pointOnSphere = pointOnCube.Normalized();
				

				float height = Data.Radius;
				if (!IsOcean && Data.Noise != null)
				{
					float noiseValue = (Data.Noise.GetNoise3Dv(pointOnSphere * 100f) + 1f) * 0.5f * Data.NoiseHeight;
					height += noiseValue;
				}
				else if (IsOcean)
				{
					height += Data.NoiseHeight * Data.OceanLevel;
				}

				Vector3 vertex = pointOnSphere * height;
				Vector3 normal = pointOnSphere;

				surfaceTool.SetNormal(normal);
				surfaceTool.SetUV(percent);
				surfaceTool.AddVertex(vertex);
			}
		}

		// Индексы
		for (int y = 0; y < resolution - 1; y++)
		{
			for (int x = 0; x < resolution - 1; x++)
			{
				int i = x + y * resolution;

				surfaceTool.AddIndex(i);
				surfaceTool.AddIndex(i + resolution);
				surfaceTool.AddIndex(i + resolution + 1);

				surfaceTool.AddIndex(i);
				surfaceTool.AddIndex(i + resolution + 1);
				surfaceTool.AddIndex(i + 1);
			}
		}

		surfaceTool.GenerateNormals();
		var mesh = surfaceTool.Commit();
		
		var meshInstance = new MeshInstance3D();
		meshInstance.Mesh = mesh;
		meshInstance.MaterialOverride = IsOcean ? Data.OceanMaterial : Data.TerrainMaterial;
		
		AddChild(meshInstance);
		activeChunks[chunk.Identifier] = meshInstance;
	}
}


public class QuadtreeChunk
{
	public Aabb Bounds { get; set; }
	public int Depth { get; set; }
	public string Identifier { get; set; }
	public List<QuadtreeChunk> Children { get; set; } = new List<QuadtreeChunk>();

	public QuadtreeChunk(Aabb bounds, int depth)
	{
		Bounds = bounds;
		Depth = depth;
		Identifier = $"{bounds.Position}_{bounds.Size}_{depth}";
	}
}
