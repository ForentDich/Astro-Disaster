// PlanetData.cs
using Godot;
using System;

[Tool]
public partial class PlanetData : Resource
{
	[ExportGroup("Basic Settings")]
	[Export] public float Radius { get; set; } = 200.0f;
	[Export] public int Resolution { get; set; } = 32;
	
	[ExportGroup("Height Settings")]
	[Export] public FastNoiseLite BaseNoise { get; set; } = new FastNoiseLite();
	[Export] public float MaxHeight { get; set; } = 50.0f;
	[Export] public float MinHeight { get; set; } = 0.0f;
	
	[ExportGroup("Height Curve")]
	[Export] public Curve HeightCurve { get; set; }
	
	[ExportGroup("Grid Settings")]
	[Export] public bool EnableGridSnap { get; set; } = true;
	[Export] public float GridStep { get; set; } = 1.0f;
	
	[ExportGroup("Ocean Settings")]
	[Export] public bool HasOcean { get; set; } = true;
	[Export(PropertyHint.Range, "0.0, 1.0")] 
	public float OceanLevel { get; set; } = 0.3f;
	
	[ExportGroup("Materials")]
	[Export] public Material TerrainMaterial { get; set; }
	[Export] public Material OceanMaterial { get; set; }

	public PlanetData()
	{
		BaseNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		BaseNoise.Frequency = 0.005f;
		BaseNoise.FractalOctaves = 4;
		

		if (HeightCurve == null)
		{
			HeightCurve = new Curve();
			HeightCurve.AddPoint(new Vector2(0, 0));
			HeightCurve.AddPoint(new Vector2(1, 1));
		}
	}
}
