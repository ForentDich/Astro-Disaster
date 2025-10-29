using Godot;
using System;

[Tool]
public partial class Planet : Node3D
{
	[Export] public PlanetData Data { get; set; }
	[Export] public Node3D PlayerNode { get; set; } // PlayerNode теперь здесь

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

		// Очищаем старые грани
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

		// Создаем грани для земли
		for (int i = 0; i < 6; i++)
		{
			CreateFace(directions[i], faceNames[i] + "_Land", false);
		}

		// Создаем грани для океана (если нужно)
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
		face.PlayerNode = PlayerNode; // Передаем ссылку на игрока

		AddChild(face);

		if (Engine.IsEditorHint())
		{
			face.Owner = GetTree().EditedSceneRoot;
		}
	}
	public override void _Process(double delta)
	{
		// Автоматически обновляем LOD каждый кадр (для тестирования)
		UpdateLOD();
	}

	public void UpdateLOD()
	{
		foreach (Node child in GetChildren())
		{
			if (child is PlanetFace face)
			{
				face.UpdateLOD();
			}
		}
	}
	
	public void update_lod()
	{
		UpdateLOD();
	}

	public void OnPlayerMoved(Vector3 position)
	{
		UpdateLOD();
	}
}
