using Friflo.Engine.ECS;
using Godot;

/// <summary>
/// Godot node that creates the tile cursor visual (wireframe MeshInstance3D)
/// and registers a cursor entity in the gameplay ECS store.
///
/// Add as a child of the main scene. On _Ready it creates:
///   - A MeshInstance3D with BoxMesh + wireframe shader
///   - A Friflo entity with TileCursor + TileCursorVisual components
///
/// The ECS <see cref="TileCursorSystem"/> handles the raycast each frame
/// and moves the visual to the selected tile.
/// </summary>
public partial class TileCursorNode : Node3D
{
	/// <summary>Max horizontal distance from player to tile (world units). 0 = unlimited.</summary>
	[Export] public float MaxReach { get; set; } = 30f;
	[Export] public Color LineColor { get; set; } = new Color(1f, 1f, 0f, 0.9f);

	public Entity Entity { get; private set; }

	public override void _Ready()
	{
		CallDeferred(MethodName.Register);
	}

	private void Register()
	{
		var session = GameplaySession.Instance;
		if (session == null)
		{
			GD.PrintErr("[TileCursorNode] GameplaySession.Instance is null!");
			return;
		}
		
		var meshInstance = new MeshInstance3D();

		float ts = ChunkConstants.TILE_SIZE;
		float th = ChunkConstants.TILE_HEIGHT;
		float pad = 0.06f;

		meshInstance.Mesh = BuildEdgeMesh(ts + pad, th + pad, ts + pad);

		var mat = new StandardMaterial3D();
		mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		mat.AlbedoColor = LineColor;
		mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		mat.NoDepthTest = false; 
		meshInstance.MaterialOverride = mat;
		meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		meshInstance.Visible = false;

		GetTree().Root.CallDeferred(Node.MethodName.AddChild, meshInstance);

		Entity = session.Store.CreateEntity(
			new TileCursor { MaxReach = MaxReach },
			new TileCursorVisual { InstanceId = meshInstance.GetInstanceId() }
		);

		GD.Print($"[TileCursorNode] Cursor entity {Entity.Id} registered");
	}

	/// <summary>Builds 12 edges of a box using Lines primitive — clean wireframe, no diagonals.</summary>
	private static ArrayMesh BuildEdgeMesh(float sx, float sy, float sz)
	{
		float hx = sx * 0.5f, hy = sy * 0.5f, hz = sz * 0.5f;

		Vector3[] v =
		{
			new(-hx, -hy, -hz), new( hx, -hy, -hz), new( hx, -hy,  hz), new(-hx, -hy,  hz),
			new(-hx,  hy, -hz), new( hx,  hy, -hz), new( hx,  hy,  hz), new(-hx,  hy,  hz)
		};

		int[] edges = {
			0,1, 1,2, 2,3, 3,0,
			4,5, 5,6, 6,7, 7,4,
			0,4, 1,5, 2,6, 3,7
		};

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Lines);
		foreach (int i in edges)
			st.AddVertex(v[i]);
		return st.Commit();
	}

	public override void _ExitTree()
	{
		if (!Entity.IsNull && Entity.Store != null)
		{
			if (Entity.TryGetComponent<TileCursorVisual>(out var visual))
			{
				var mesh = visual.GetMesh();
				mesh?.QueueFree();
			}
			Entity.DeleteEntity();
		}
	}
}
