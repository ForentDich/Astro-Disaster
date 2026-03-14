using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Generates and caches tree meshes (trunk+branches + canopy) per TreeType.
/// Creates N variations per type; seed selects which variation to use.
/// </summary>
public static class TreeTemplateGenerator
{
	public struct TreeTemplate
	{
		public Mesh TrunkMesh;
		public Mesh CanopyMesh;
	}

	private const int VARIATIONS_PER_TYPE = 20;
	private static readonly Dictionary<TreeType, TreeTemplate[]> _cache = new();

	/// <summary>Get a tree mesh variation. Seed selects which of N cached variants.</summary>
	public static TreeTemplate Get(TreeType type, int seed)
	{
		if (!_cache.TryGetValue(type, out var variants))
		{
			var p = TreeParams.ForType(type);
			variants = new TreeTemplate[VARIATIONS_PER_TYPE];
			for (int i = 0; i < VARIATIONS_PER_TYPE; i++)
				variants[i] = Generate(p, type.GetHashCode() * 31 + i * 7919);
			_cache[type] = variants;
		}
		return variants[((seed & 0x7FFFFFFF) % VARIATIONS_PER_TYPE)];
	}

	public static void ClearCache() => _cache.Clear();

	// ─── Core generation ───────────────────────────────────────────

	private static TreeTemplate Generate(TreeParams p, int seed)
	{
		var rng = new SeededRandom(seed);

		float trunkH    = rng.Range(p.TrunkHeightMin, p.TrunkHeightMax);
		float trunkR    = rng.Range(p.TrunkRadiusMin, p.TrunkRadiusMax);
		float trunkTopR = trunkR * p.TrunkTaper;

		// ── Trunk mesh ──
		var trunkSt = new SurfaceTool();
		trunkSt.Begin(Mesh.PrimitiveType.Triangles);
		trunkSt.SetSmoothGroup(uint.MaxValue);
		AddCylinder(trunkSt, Vector3.Zero, Vector3.Up * trunkH, trunkR, trunkTopR, p.TrunkSides);

		// ── Canopy mesh ──
		var canopySt = new SurfaceTool();
		canopySt.Begin(Mesh.PrimitiveType.Triangles);
		canopySt.SetSmoothGroup(uint.MaxValue);

		// Main canopy at top of trunk
		float canopyR = rng.Range(p.CanopyRadiusMin, p.CanopyRadiusMax);
		float canopyRotY = rng.Range(0f, MathF.Tau);
		AddBox(canopySt, new Vector3(0, trunkH + canopyR * 0.3f, 0),
			new Vector3(canopyR, canopyR * 0.75f, canopyR), canopyRotY);

		// Branches
		int branchCount = rng.RangeInt(p.BranchCountMin, p.BranchCountMax);
		float angleStep = MathF.Tau / branchCount;
		float baseAngle = rng.Range(0f, MathF.Tau);

		for (int i = 0; i < branchCount; i++)
		{
			float startT = rng.Range(p.BranchStartMin, p.BranchStartMax);
			var branchStart = new Vector3(0, trunkH * startT, 0);

			float yaw   = baseAngle + i * angleStep + rng.Range(-0.3f, 0.3f);
			float pitch = Mathf.DegToRad(rng.Range(p.BranchAngleMin, p.BranchAngleMax));

			var dir = new Vector3(
				MathF.Cos(yaw) * MathF.Sin(pitch),
				MathF.Cos(pitch),
				MathF.Sin(yaw) * MathF.Sin(pitch)
			).Normalized();

			float branchLen   = trunkH * rng.Range(p.BranchLengthMin, p.BranchLengthMax);
			float branchThick = trunkR * rng.Range(p.BranchThicknessMin, p.BranchThicknessMax);
			var branchEnd = branchStart + dir * branchLen;

			AddCylinder(trunkSt, branchStart, branchEnd, branchThick, branchThick * 0.4f, p.BranchSides);

			// Canopy blob at end of branch — oriented along branch direction
			float bcR = branchLen * p.BranchCanopyScale;
			if (bcR > 0.3f)
				AddBoxOriented(canopySt, branchEnd, new Vector3(bcR, bcR * 0.75f, bcR), dir);
		}

		trunkSt.GenerateNormals();
		canopySt.GenerateNormals();

		return new TreeTemplate
		{
			TrunkMesh  = trunkSt.Commit(),
			CanopyMesh = canopySt.Commit()
		};
	}

	// ─── Cylinder primitive ────────────────────────────────────────

	private static void AddCylinder(SurfaceTool st, Vector3 from, Vector3 to,
		float radiusBottom, float radiusTop, int sides)
	{
		var dir = to - from;
		if (dir.LengthSquared() < 0.001f) return;
		dir = dir.Normalized();

		var perp = MathF.Abs(dir.Y) < 0.99f
			? dir.Cross(Vector3.Up).Normalized()
			: dir.Cross(Vector3.Right).Normalized();
		var perp2 = dir.Cross(perp).Normalized();

		Span<Vector3> bot = stackalloc Vector3[sides];
		Span<Vector3> top = stackalloc Vector3[sides];

		for (int i = 0; i < sides; i++)
		{
			float angle = i * MathF.Tau / sides;
			float cos = MathF.Cos(angle);
			float sin = MathF.Sin(angle);
			bot[i] = from + (perp * cos + perp2 * sin) * radiusBottom;
			top[i] = to   + (perp * cos + perp2 * sin) * radiusTop;
		}

		for (int i = 0; i < sides; i++)
		{
			int next = (i + 1) % sides;
			st.AddVertex(bot[i]);
			st.AddVertex(top[i]);
			st.AddVertex(top[next]);

			st.AddVertex(bot[i]);
			st.AddVertex(top[next]);
			st.AddVertex(bot[next]);
		}
	}

	// ─── Box primitive ─────────────────────────────────────────────

	private static void AddBox(SurfaceTool st, Vector3 center, Vector3 half, float rotY = 0f)
	{
		float cos = MathF.Cos(rotY);
		float sin = MathF.Sin(rotY);

		// Local offset → rotated around Y → world position
		Vector3 R(float lx, float ly, float lz)
		{
			return center + new Vector3(lx * cos - lz * sin, ly, lx * sin + lz * cos);
		}

		// 8 corners (rotated around Y)
		var v0 = R(-half.X, -half.Y, -half.Z);
		var v1 = R( half.X, -half.Y, -half.Z);
		var v2 = R( half.X,  half.Y, -half.Z);
		var v3 = R(-half.X,  half.Y, -half.Z);
		var v4 = R(-half.X, -half.Y,  half.Z);
		var v5 = R( half.X, -half.Y,  half.Z);
		var v6 = R( half.X,  half.Y,  half.Z);
		var v7 = R(-half.X,  half.Y,  half.Z);

		// 6 faces, 2 tris each — CCW winding (normals point outward)
		// Front (+Z)
		Quad(st, v7, v6, v5, v4);
		// Back (-Z)
		Quad(st, v2, v3, v0, v1);
		// Right (+X)
		Quad(st, v6, v2, v1, v5);
		// Left (-X)
		Quad(st, v3, v7, v4, v0);
		// Top (+Y)
		Quad(st, v2, v6, v7, v3);
		// Bottom (-Y)
		Quad(st, v4, v5, v1, v0);
	}

	/// <summary>Box oriented along a direction vector (Y-axis of the box → dir).</summary>
	private static void AddBoxOriented(SurfaceTool st, Vector3 center, Vector3 half, Vector3 dir)
	{
		// Build orthonormal basis: up=dir, right+forward perpendicular
		var up = dir.Normalized();
		var right = MathF.Abs(up.Y) < 0.99f
			? up.Cross(Vector3.Up).Normalized()
			: up.Cross(Vector3.Right).Normalized();
		var forward = right.Cross(up).Normalized();

		Vector3 R(float lx, float ly, float lz)
			=> center + right * lx + up * ly + forward * lz;

		var v0 = R(-half.X, -half.Y, -half.Z);
		var v1 = R( half.X, -half.Y, -half.Z);
		var v2 = R( half.X,  half.Y, -half.Z);
		var v3 = R(-half.X,  half.Y, -half.Z);
		var v4 = R(-half.X, -half.Y,  half.Z);
		var v5 = R( half.X, -half.Y,  half.Z);
		var v6 = R( half.X,  half.Y,  half.Z);
		var v7 = R(-half.X,  half.Y,  half.Z);

		Quad(st, v7, v6, v5, v4); // Front
		Quad(st, v2, v3, v0, v1); // Back
		Quad(st, v6, v2, v1, v5); // Right
		Quad(st, v3, v7, v4, v0); // Left
		Quad(st, v2, v6, v7, v3); // Top
		Quad(st, v4, v5, v1, v0); // Bottom
	}

	private static void Quad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
	{
		st.AddVertex(a); st.AddVertex(b); st.AddVertex(c);
		st.AddVertex(a); st.AddVertex(c); st.AddVertex(d);
	}

	// ─── Deterministic RNG ─────────────────────────────────────────

	private struct SeededRandom
	{
		private uint _state;
		public SeededRandom(int seed) => _state = (uint)(seed ^ 0x5DEECE66D);

		private uint Next()
		{
			_state ^= _state << 13;
			_state ^= _state >> 17;
			_state ^= _state << 5;
			return _state;
		}

		public float Range(float min, float max)
			=> min + (Next() & 0xFFFF) / 65535f * (max - min);

		public int RangeInt(int min, int max)
			=> min + (int)(Next() % (uint)(max - min + 1));
	}
}
