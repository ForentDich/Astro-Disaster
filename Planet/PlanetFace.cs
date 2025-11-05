using Godot;
using System;

[Tool]
public partial class PlanetFace : MeshInstance3D
{
	[Export] public Vector3 FaceNormal { get; set; } = Vector3.Up;
	[Export] public PlanetData Data { get; set; }
	[Export] public bool IsOcean { get; set; } = false;

	private SphereMeshGenerator meshGenerator;
	private HeightCalculator heightCalculator;

	public override void _Ready()
	{
		Initialize();
	}

	public void Initialize()
	{
		if (Data == null) return;

		heightCalculator = new HeightCalculator(Data, IsOcean);
		meshGenerator = new SphereMeshGenerator(Data, FaceNormal, IsOcean, Data.Resolution);

		var mesh = meshGenerator.GenerateMesh();
		Mesh = mesh;

		MaterialOverride = IsOcean ? Data.OceanMaterial : Data.TerrainMaterial;
	}

	public void UpdateData(PlanetData newData)
	{
		Data = newData;
		Initialize();
	}

	public void SetResolution(int resolution)
	{
		if (meshGenerator != null)
		{
			meshGenerator.SetResolution(resolution);
			var mesh = meshGenerator.GenerateMesh();
			Mesh = mesh;
		}
	}

	public float GetHeightAtPoint(Vector3 pointOnSphere)
	{
		if (heightCalculator != null)
			return heightCalculator.CalculateHeight(pointOnSphere);
		
		return Data?.Radius ?? 0f;
	}
}
