using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Godot;

public class NoiseGenerator
{
    private FastNoiseLite _continentNoise;  // Континентальность (крупные формы)
    private FastNoiseLite _detailNoise;     // Детали рельефа (холмы, долины)
    private FastNoiseLite _erosionNoise;    // Эрозия (гладкость рельефа)
    private FastNoiseLite _riverNoise;      // Weirdness → реки через PV-складку
    private NoiseSettings _settings;
    private float _coastLevel;              // Кэш: ContinentCurve(CoastStart)

    /// <summary>
    /// Maximum allowed height difference between any two adjacent heightmap points.
    /// MAX_GRADIENT=3 → adjacent corners differ by ≤3, diagonals by ≤6.
    /// </summary>
    public const int MAX_GRADIENT = 2;

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

        // Континентальность: низкая частота, меньше октав → гладкие материки
        // + Domain Warp для органичных границ зон
        _continentNoise = new FastNoiseLite
        {
            Seed              = _settings.Seed,
            NoiseType         = _settings.NoiseType,
            Frequency         = _settings.Frequency,
            FractalType       = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves    = Mathf.Max(2, _settings.Octaves / 2),
            FractalGain       = _settings.Persistence,
            FractalLacunarity = _settings.Lacunarity,
            DomainWarpEnabled   = _settings.DomainWarpAmplitude > 0f,
            DomainWarpType      = FastNoiseLite.DomainWarpTypeEnum.SimplexReduced,
            DomainWarpAmplitude = _settings.DomainWarpAmplitude,
            DomainWarpFrequency = _settings.DomainWarpFrequency,
            DomainWarpFractalType      = FastNoiseLite.DomainWarpFractalTypeEnum.Progressive,
            DomainWarpFractalOctaves   = 3,
            DomainWarpFractalGain      = 0.5f,
            DomainWarpFractalLacunarity = 2.0f
        };

        // Детали: выше частота, полные октавы → холмы, долины
        _detailNoise = new FastNoiseLite
        {
            Seed              = _settings.Seed + 1000,
            NoiseType         = _settings.NoiseType,
            Frequency         = _settings.DetailFrequency,
            FractalType       = _settings.FractalType,
            FractalOctaves    = _settings.Octaves,
            FractalGain       = _settings.Persistence,
            FractalLacunarity = _settings.Lacunarity
        };

        // Эрозия: отдельный шум, средняя частота, гладкие формы
        // + Domain Warp (тот же стиль, но другой seed)
        _erosionNoise = new FastNoiseLite
        {
            Seed              = _settings.Seed + 2000,
            NoiseType         = _settings.NoiseType,
            Frequency         = _settings.ErosionFrequency,
            FractalType       = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves    = Mathf.Max(2, _settings.Octaves / 2),
            FractalGain       = _settings.Persistence,
            FractalLacunarity = _settings.Lacunarity,
            DomainWarpEnabled   = _settings.DomainWarpAmplitude > 0f,
            DomainWarpType      = FastNoiseLite.DomainWarpTypeEnum.SimplexReduced,
            DomainWarpAmplitude = _settings.DomainWarpAmplitude,
            DomainWarpFrequency = _settings.DomainWarpFrequency,
            DomainWarpFractalType      = FastNoiseLite.DomainWarpFractalTypeEnum.Progressive,
            DomainWarpFractalOctaves   = 3,
            DomainWarpFractalGain      = 0.5f,
            DomainWarpFractalLacunarity = 2.0f
        };

        // Weirdness (Ridges): реки через PV-складку, как в Minecraft 1.18+
        // Низкая частота, мало октав, Domain Warp для органичных русел
        _riverNoise = new FastNoiseLite
        {
            Seed              = _settings.Seed + 3000,
            NoiseType         = _settings.NoiseType,
            Frequency         = _settings.RiverFrequency,
            FractalType       = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves    = 3,
            FractalGain       = 0.5f,
            FractalLacunarity = 2.0f,
            DomainWarpEnabled   = _settings.DomainWarpAmplitude > 0f,
            DomainWarpType      = FastNoiseLite.DomainWarpTypeEnum.SimplexReduced,
            DomainWarpAmplitude = _settings.DomainWarpAmplitude * 0.7f,
            DomainWarpFrequency = _settings.DomainWarpFrequency,
            DomainWarpFractalType      = FastNoiseLite.DomainWarpFractalTypeEnum.Progressive,
            DomainWarpFractalOctaves   = 2,
            DomainWarpFractalGain      = 0.5f,
            DomainWarpFractalLacunarity = 2.0f
        };

        // Кривые: создать по умолчанию если null (старые ресурсы)
        _settings.EnsureCurves();
        // Кэш уровня моря из C-кривой
        _coastLevel = _settings.ContinentCurve.Sample(_settings.CoastStart);
    }

    /// <summary>
    /// Raw continentalness value at (x, y): 0..1.
    /// Low = ocean, high = deep inland. Domain warp is applied internally.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetContinentalness(float x, float y)
    {
        return (_continentNoise.GetNoise2D(x, y) + 1f) * 0.5f;
    }

    /// <summary>
    /// Raw erosion value at (x, y): 0..1.
    /// High = heavily eroded (flat), low = un-eroded (rough).
    /// Domain warp is applied internally.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetErosion(float x, float y)
    {
        return (_erosionNoise.GetNoise2D(x, y) + 1f) * 0.5f;
    }

    /// <summary>
    /// Peaks & Valleys value from Weirdness noise (Minecraft 1.18+ style).
    /// Formula: PV = 1 − |3|W| − 2|
    /// Returns −1 (valleys/rivers) to +1 (peaks).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetRiverPV(float x, float y)
    {
        float W = _riverNoise.GetNoise2D(x, y); // −1..1
        return 1f - Mathf.Abs(3f * Mathf.Abs(W) - 2f);
    }

    /// <summary>
    /// Final height at (x, y): 0..1.
    ///
    /// Формула:
    ///   potential = ContinentCurve(C)         — потенциальная высота
    ///   seaLevel = ContinentCurve(CoastStart) — уровень моря (кэш)
    ///   landRise = potential - seaLevel        — подъём над морем
    ///   factor   = ErosionCurve(E)             — сколько потенциала реализовано
    ///
    ///   Океан (C < Coast):  height = potential  (E не влияет)
    ///   Суша  (C ≥ Coast):  height = seaLevel + landRise × factor
    ///                              + detail × DetailStrength × factor × coastMask
    ///
    ///   Реки (PV в зоне Valleys на суше): height проваливается ниже seaLevel
    ///
    /// Результат:
    ///   C↑ E↓ → Горы | C↑ E↑ → Плато | C→ E↓ → Холмы | C↓ → Океан
    ///   PV↓ на суше → Реки/озёра
    /// </summary>
    public float GetNoise(float x, float y)
    {
        float C = GetContinentalness(x, y);
        float E = GetErosion(x, y);
        return GetNoiseFromCE(C, E, x, y);
    }

    /// <summary>
    /// seaLevel + landRise × ErosionCurve(E) + detail × factor × coastMask.
    /// + River carving via PV valleys (Minecraft-style ridges folded).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float GetNoiseFromCE(float C, float E, float x, float y)
    {
        float potential = _settings.ContinentCurve.Sample(C);

        float baseHeight;
        if (C < _settings.CoastStart)
        {
            // Океан: E не влияет, только форма дна из C-кривой
            baseHeight = potential;
        }
        else
        {
            // Суша: потенциал × фактор эрозии
            float landRise = potential - _coastLevel;
            float factor = _settings.ErosionCurve.Sample(E);
            baseHeight = _coastLevel + landRise * factor;
        }

        // Detail: микро-текстура, масштабируется фактором эрозии
        // Горы → текстурные гребни, равнины → гладкие
        float coastMask = Smoothstep(_settings.CoastStart, _settings.InlandStart, C);
        float erosionFactor = _settings.ErosionCurve.Sample(E);
        float detail = _detailNoise.GetNoise2D(x, y)
                     * _settings.DetailStrength * erosionFactor * coastMask;

        float result = baseHeight + detail;

        // ═══════ Реки: PV-складка (Minecraft-style) ═══════
        //
        // Ключевой принцип: реки текут по НИЗИНАМ, не через горы.
        // Вместо хака по erosion, маскируем по фактической высоте terrain
        // над уровнем моря. Чем выше terrain — тем слабее река.
        //
        // Это физически корректно: вода не может подняться выше
        // уровня моря, реки всегда стекают вниз.
        float riverExtendC = _settings.CoastStart - 0.08f;

        if (C >= _settings.CoastStart)
        {
            // Суша: не проваливаться ниже уровня моря (базовое правило)
            result = Mathf.Max(result, _coastLevel);
        }

        if (_settings.RiversEnabled && C >= riverExtendC)
        {
            float PV = GetRiverPV(x, y); // −1..+1
            float valleyThreshold = -1f + _settings.RiverWidth;
            float bankZone = valleyThreshold + _settings.RiverWidth * 0.5f;

            if (PV < bankZone)
            {
                // Маска по высоте: реки возможны только вблизи уровня моря.
                // aboveSea ≈ 0.02 (равнины) → mask ≈ 1.0 → полная река
                // aboveSea ≈ 0.10 (холмы)   → mask ≈ 0.0 → нет реки
                // aboveSea ≈ 0.30 (горы)     → mask = 0.0 → чистые горы
                float aboveSea = result - _coastLevel;
                float maxRiverableHeight = 0.07f;
                float heightMask = 1f - Smoothstep(0f, maxRiverableHeight, aboveSea);

                if (heightMask > 0.01f)
                {
                    float preRiverResult = result;
                    float riverBottom = _coastLevel * (1f - _settings.RiverDepth);

                    if (PV < valleyThreshold)
                    {
                        // Внутри русла: t=1 в центре (PV=−1), t=0 на краю
                        float t = (valleyThreshold - PV) / (valleyThreshold + 1f);
                        t = t * t; // квадратичный профиль

                        if (C >= _settings.CoastStart)
                        {
                            float riverHeight = Mathf.Lerp(_coastLevel, riverBottom, t);
                            result = Mathf.Lerp(preRiverResult, riverHeight, heightMask);
                        }
                        else
                        {
                            // Побережье: река сливается с океаном
                            float coastBlend = Smoothstep(riverExtendC, _settings.CoastStart, C);
                            float riverHeight = Mathf.Lerp(_coastLevel, riverBottom, t);
                            result = Mathf.Lerp(result, riverHeight, coastBlend);
                        }
                    }
                    else
                    {
                        // Банки (PV между valleyThreshold и bankZone)
                        float blend = Smoothstep(bankZone, valleyThreshold, PV) * heightMask;
                        result = Mathf.Lerp(preRiverResult, _coastLevel, blend);
                    }
                }
            }
        }

        return Mathf.Clamp(result, 0f, 1f);
    }

    /// <summary>Hermite smoothstep: 0 below edge0, 1 above edge1, smooth in between.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Smoothstep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Determines the continental zone from a raw C value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ContinentalZone GetZone(float C)
    {
        if (C < _settings.CoastStart)      return ContinentalZone.Ocean;
        if (C < _settings.InlandStart)      return ContinentalZone.Coast;
        if (C < _settings.FarInlandStart)   return ContinentalZone.Inland;
        return ContinentalZone.FarInland;
    }

    /// <summary>
    /// Zone with river detection: if PV is in valley range on land → River zone.
    /// Suppresses rivers in high terrain (mountains, hills) — rivers only on lowlands.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ContinentalZone GetZoneWithRiver(float C, float E, float x, float y)
    {
        if (C < _settings.CoastStart)      return ContinentalZone.Ocean;
        if (C < _settings.InlandStart)      return ContinentalZone.Coast;

        if (_settings.RiversEnabled)
        {
            // Only mark as river if terrain is low enough for rivers to form
            float height = GetNoiseFromCE(C, E, x, y);
            float aboveSea = height - _coastLevel;
            float maxRiverableHeight = 0.07f;

            if (aboveSea < maxRiverableHeight)
            {
                float PV = GetRiverPV(x, y);
                float valleyThreshold = -1f + _settings.RiverWidth;
                float bankZone = valleyThreshold + _settings.RiverWidth * 0.5f;
                if (PV < bankZone)
                    return ContinentalZone.River;
            }
        }

        if (C < _settings.FarInlandStart)   return ContinentalZone.Inland;
        return ContinentalZone.FarInland;
    }

    public void GenerateHeightmap(
        Span<int> output, 
        int offsetX, int offsetZ, 
        int width, int height, 
        int maxHeight,
        float heightScale,
        int step = 1)
    {
        GenerateHeightmap(output, Span<byte>.Empty, Span<byte>.Empty, offsetX, offsetZ, width, height, maxHeight, heightScale, step);
    }

    /// <summary>
    /// Generates heightmap + continental zone per point.
    /// Zone is a separate concept from biome:
    ///   Zone  = terrain shape  (from C noise)
    ///   Biome = surface type   (from zone + temperature + humidity — later)
    /// </summary>
    public void GenerateHeightmap(
        Span<int>  output,
        Span<byte> zoneOutput,
        Span<byte> erosionOutput,
        int offsetX, int offsetZ,
        int width, int height,
        int maxHeight,
        float heightScale,
        int step = 1)
    {
        bool writeZone    = zoneOutput.Length    >= output.Length;
        bool writeErosion = erosionOutput.Length >= output.Length;

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float worldX = offsetX + x * step;
                float worldZ = offsetZ + z * step;

                float C = GetContinentalness(worldX, worldZ);
                float E = GetErosion(worldX, worldZ);
                float noiseValue = GetNoiseFromCE(C, E, worldX, worldZ);
                int   heightValue = Mathf.RoundToInt(noiseValue * heightScale * maxHeight);

                int idx = z * width + x;
                output[idx] = heightValue; // Убрали скалярный Clamp для SIMD-прохода
                
                if (writeZone)    zoneOutput[idx]    = (byte)GetZoneWithRiver(C, E, worldX, worldZ);
                if (writeErosion) erosionOutput[idx] = (byte)(Mathf.Clamp(E, 0f, 1f) * 255f);
            }
        }

        // 1. Аппаратный SIMD-проход для Clamp базовой высоты
        SIMDClamp(output, 0, maxHeight);

        // 2. Градиентный Clamp (с частичным разворачиванием)
        ClampGradient(output, width, height, MAX_GRADIENT);
    }

    /// <summary>
    /// Аппаратно-ускоренный зажим массива (обрабатывает 4 или 8 интов за инструкцию)
    /// </summary>
    private static void SIMDClamp(Span<int> map, int min, int max)
    {
        int vectorSize = Vector<int>.Count;
        int i = 0;

        var minVec = new Vector<int>(min);
        var maxVec = new Vector<int>(max);

        // Векторизованный проход
        for (; i <= map.Length - vectorSize; i += vectorSize)
        {
            var vec = new Vector<int>(map.Slice(i, vectorSize));
            vec = Vector.Max(minVec, vec);
            vec = Vector.Min(maxVec, vec);
            vec.CopyTo(map.Slice(i, vectorSize));
        }

        // Хвост
        for (; i < map.Length; i++)
        {
            map[i] = Math.Clamp(map[i], min, max);
        }
    }

    /// <summary>
    /// Iterative gradient clamping: ensures no two adjacent (4-connected) heightmap
    /// points differ by more than maxDelta.
    /// </summary>
    private static void ClampGradient(Span<int> map, int w, int h, int maxDelta)
    {
        int stride = w;
        bool changed = true;
        int maxPasses = 20;

        while (changed && maxPasses-- > 0)
        {
            changed = false;

            // Forward pass: left→right, top→bottom
            for (int z = 0; z < h; z++)
            {
                int zStride = z * stride;
                
                // Горизонтальный проход (зависимый, нельзя векторизовать/разворачивать безопасно)
                for (int x = 0; x < w; x++)
                {
                    int idx = zStride + x;
                    int val = map[idx];

                    if (x + 1 < w)
                    {
                        int ri = idx + 1;
                        if (map[ri] > val + maxDelta) { map[ri] = val + maxDelta; changed = true; }
                        else if (map[ri] < val - maxDelta) { map[ri] = val - maxDelta; changed = true; }
                    }
                }

                // Вертикальный проход (независимый по X, разворачиваем цикл на 4)
                if (z + 1 < h)
                {
                    int x = 0;
                    for (; x <= w - 4; x += 4)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            int idx = zStride + (x + i);
                            int bi = idx + stride;
                            int val = map[idx];
                            if (map[bi] > val + maxDelta) { map[bi] = val + maxDelta; changed = true; }
                            else if (map[bi] < val - maxDelta) { map[bi] = val - maxDelta; changed = true; }
                        }
                    }
                    for (; x < w; x++)
                    {
                        int idx = zStride + x;
                        int val = map[idx];
                        int bi = idx + stride;
                        if (map[bi] > val + maxDelta) { map[bi] = val + maxDelta; changed = true; }
                        else if (map[bi] < val - maxDelta) { map[bi] = val - maxDelta; changed = true; }
                    }
                }
            }

            // Backward pass: right→left, bottom→top
            for (int z = h - 1; z >= 0; z--)
            {
                int zStride = z * stride;
                
                for (int x = w - 1; x >= 0; x--)
                {
                    int idx = zStride + x;
                    int val = map[idx];

                    if (x > 0)
                    {
                        int li = idx - 1;
                        if (map[li] > val + maxDelta) { map[li] = val + maxDelta; changed = true; }
                        else if (map[li] < val - maxDelta) { map[li] = val - maxDelta; changed = true; }
                    }
                }

                if (z > 0)
                {
                    int x = w - 1;
                    for (; x >= 3; x -= 4)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            int idx = zStride + (x - i);
                            int ti = idx - stride;
                            int val = map[idx];
                            if (map[ti] > val + maxDelta) { map[ti] = val + maxDelta; changed = true; }
                            else if (map[ti] < val - maxDelta) { map[ti] = val - maxDelta; changed = true; }
                        }
                    }
                    for (; x >= 0; x--)
                    {
                        int idx = zStride + x;
                        int val = map[idx];
                        int ti = idx - stride;
                        if (map[ti] > val + maxDelta) { map[ti] = val + maxDelta; changed = true; }
                        else if (map[ti] < val - maxDelta) { map[ti] = val - maxDelta; changed = true; }
                    }
                }
            }
        }
    }
}