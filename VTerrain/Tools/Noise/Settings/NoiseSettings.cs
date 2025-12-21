using Godot;

[GlobalClass]
public partial class NoiseSettings : Resource
{
    [Export] public int Seed { get; set; } = 1337;
    
    [ExportCategory("Base Noise (Continents)")]
    [Export] public FastNoiseLite.NoiseTypeEnum NoiseType { get; set; } = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;
    [Export] public float BaseFrequency { get; set; } = 0.001f; // ОЧЕНЬ низкая частота для континентов
    
    [ExportCategory("Detail Noise (Terrain)")]
    [Export] public float DetailFrequency { get; set; } = 0.02f; // Высокая частота для деталей
    [Export(PropertyHint.Range, "0.0,1.0")] 
    public float DetailStrength { get; set; } = 0.3f; // Насколько сильно влияют детали
    
    [ExportCategory("Fractal")]
    [Export] public FastNoiseLite.FractalTypeEnum FractalType { get; set; } = FastNoiseLite.FractalTypeEnum.Fbm;
    [Export(PropertyHint.Range, "1,8,1")] public int Octaves { get; set; } = 4;
    [Export(PropertyHint.Range, "0.0,1.0")] public float Persistence { get; set; } = 0.5f;
    [Export] public float Lacunarity { get; set; } = 2.0f;
    
    [ExportCategory("Height Curve")]
    [Export] public Curve HeightCurve { get; set; }
    
    public static NoiseSettings CreateDefault()
    {
        var settings = new NoiseSettings();
        // Создаём простую кривую по умолчанию
        settings.HeightCurve = new Curve();
        settings.HeightCurve.AddPoint(new Vector2(0, 0));
        settings.HeightCurve.AddPoint(new Vector2(1, 1));
        return settings;
    }
}