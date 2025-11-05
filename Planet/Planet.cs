using Godot;
using System;

[Tool]
public partial class Planet : Node3D
{
	[Export] public PlanetData Data { get; set; }

	private bool _isReady = false;

	public override void _Ready()
	{
		_isReady = true;
		
		if (Data == null)
		{
			Data = new PlanetData();
		}

		if (Engine.IsEditorHint() || GetChildCount() == 0)
		{
			GeneratePlanet();
		}
	}

	public void GeneratePlanet()
	{
		if (!_isReady || Data == null) return;

		foreach (Node child in GetChildren())
		{
			if (child is PlanetFace)
				child.QueueFree();
		}

		Vector3[] directions = { 
			Vector3.Up, Vector3.Down, Vector3.Left, 
			Vector3.Right, Vector3.Forward, Vector3.Back 
		};

		string[] faceNames = { "Top", "Bottom", "Left", "Right", "Front", "Back" };


		for (int i = 0; i < 6; i++)
		{
			CreateFace(directions[i], faceNames[i] + "_Land", false);
		}


		if (Data.HasOcean)
		{
			for (int i = 0; i < 6; i++)
			{
				CreateFace(directions[i], faceNames[i] + "_Ocean", true);
			}
		}
	}

	private void CreateFace(Vector3 direction, string name, bool isOcean)
	{
		var face = new PlanetFace();
		face.Name = name;
		face.FaceNormal = direction;
		face.Data = Data;
		face.IsOcean = isOcean;

		AddChild(face);

		if (Engine.IsEditorHint())
		{
			face.Owner = GetTree().EditedSceneRoot;
		}
	}

	public void UpdateAllFaces()
	{
		foreach (Node child in GetChildren())
		{
			if (child is PlanetFace face)
			{
				face.UpdateData(Data);
			}
		}
	}

	public float GetHeightAtGlobalPoint(Vector3 globalPoint)
	{
		Vector3 localPoint = ToLocal(globalPoint);
		Vector3 pointOnSphere = localPoint.Normalized();

		foreach (Node child in GetChildren())
		{
			if (child is PlanetFace face && !face.IsOcean)
			{
				return face.GetHeightAtPoint(pointOnSphere);
			}
		}
		
		return Data?.Radius ?? 0f;
	}
}
