using Godot;
using System;

[Tool]
public partial class PlanetData : Resource
{
	[ExportGroup("Basic Settings")]
	[Export] public float Radius { get; set; } = 200.0f;
	
	[ExportGroup("Noise Settings")]
	[Export] public FastNoiseLite Noise { get; set; } = new FastNoiseLite();
	[Export] public float NoiseHeight { get; set; } = 10.0f;
	
	[ExportGroup("Ocean Settings")]
	[Export] public bool HasOcean { get; set; } = true;
	[Export(PropertyHint.Range, "0.0, 1.0")] 
	public float OceanLevel { get; set; } = 0.3f;
	
	[ExportGroup("LOD Settings")]
	[Export] public int MaxLOD { get; set; } = 4;
	[Export] public float[] LODDistances { get; set; } = new float[] { 100f, 50f, 25f, 12f };
	[Export] public int[] LODResolutions { get; set; } = new int[] { 8, 12, 16, 24, 32 };
	
	[ExportGroup("Materials")]
	[Export] public Material TerrainMaterial { get; set; }
	[Export] public Material OceanMaterial { get; set; }
	
}
