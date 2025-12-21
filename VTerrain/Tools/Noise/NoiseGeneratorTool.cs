using System;
using Godot;

public class NoiseGenerator
{
    private FastNoiseLite _continentNoise;  // Континенты (очень низкая частота)
    private FastNoiseLite _terrainNoise;    // Основной рельеф
    private NoiseSettings _settings;
    
    public NoiseSettings Settings 
    { 
        get => _settings;
        set
        {
            _settings = value;
            ApplySettings();
        }
    }
    
    public NoiseGenerator(NoiseSettings settings = null)
    {
        Settings = settings ?? NoiseSettings.CreateDefault();
    }
    
    private void ApplySettings()
    {
        if (_settings == null) return;
        
        // 1. Шум для континентов (очень низкая частота)
        _continentNoise = new FastNoiseLite
        {
            Seed = _settings.Seed,
            NoiseType = _settings.NoiseType,
            Frequency = _settings.BaseFrequency, // ОЧЕНЬ низкая!
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = Mathf.Max(2, _settings.Octaves / 2), // Меньше октав для плавности
            FractalGain = _settings.Persistence,
            FractalLacunarity = _settings.Lacunarity
        };
        
        // 2. Шум для рельефа (детали)
        _terrainNoise = new FastNoiseLite
        {
            Seed = _settings.Seed + 1000, // Разный сид
            NoiseType = _settings.NoiseType,
            Frequency = _settings.DetailFrequency, // Высокая частота
            FractalType = _settings.FractalType,
            FractalOctaves = _settings.Octaves,
            FractalGain = _settings.Persistence,
            FractalLacunarity = _settings.Lacunarity
        };
    }
    
    public float GetNoise(float x, float y)
    {
        // 1. Получаем континенты (-1..1 → 0..1)
        float continent = (_continentNoise.GetNoise2D(x, y) + 1f) * 0.5f;
        
        // 2. Получаем детали рельефа
        float terrain = _terrainNoise.GetNoise2D(x, y) * _settings.DetailStrength;
        
        // 3. Комбинируем: континенты + детали
        float combined = continent + terrain;
        
        // 4. Нормализуем к 0..1
        combined = Mathf.Clamp((combined + 1f) * 0.5f, 0f, 1f);
        
        // 5. Применяем кривую высот (если есть)
        if (_settings.HeightCurve != null && _settings.HeightCurve.PointCount > 0)
        {
            combined = _settings.HeightCurve.Sample(combined);
        }
        
        return combined;
    }
    
    public void GenerateHeightmap(
        Span<int> output, 
        int offsetX, int offsetZ, 
        int width, int height, 
        int maxHeight,
        float heightScale) // 0..1
    {
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float worldX = offsetX + x;
                float worldZ = offsetZ + z;
                
                // Получаем комбинированный шум (уже нормализованный к 0..1)
                float noiseValue = GetNoise(worldX, worldZ);
                
                // Масштабируем (heightScale = какая часть диапазона используется)
                float scaled = noiseValue * heightScale;
                
                // Преобразуем в целую высоту
                int heightValue = Mathf.RoundToInt(scaled * maxHeight);
                output[z * width + x] = Mathf.Clamp(heightValue, 0, maxHeight);
            }
        }
    }
}