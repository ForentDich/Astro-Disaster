using Godot;

[GlobalClass]
public partial class NoiseSettings : Resource
{
    [Export] public int Seed { get; set; } = 1337;

    // ═══════════════════ Континентальность (C) ═══════════════════
    [ExportCategory("Континентальность")]
    [Export] public FastNoiseLite.NoiseTypeEnum NoiseType { get; set; } = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;

    /// <summary>Частота шума континентальности. Масштаб в мировых единицах (тайлы).</summary>
    [Export(PropertyHint.Range, "0.001,20.0,0.001")]
    public float Frequency { get; set; } = 2.0f;

    // ═══════════════════ Детали рельефа ═══════════════════
    [ExportCategory("Детали рельефа")]

    /// <summary>Частота деталей (холмы). Масштаб в мировых единицах (тайлы).</summary>
    [Export(PropertyHint.Range, "0.001,50.0,0.001")]
    public float DetailFrequency { get; set; } = 5.0f;

    /// <summary>Сила деталей: 0 = плоско, 0.15 = холмы, 0.3 = горы.</summary>
    [Export(PropertyHint.Range, "0.0,0.5,0.01")]
    public float DetailStrength { get; set; } = 0.15f;

    // ═══════════════════ Эрозия (E) ═══════════════════
    [ExportCategory("Эрозия")]

    /// <summary>Частота шума эрозии. Масштаб в мировых единицах (тайлы).</summary>
    [Export(PropertyHint.Range, "0.001,20.0,0.001")]
    public float ErosionFrequency { get; set; } = 3.0f;

    // ═══════════════════ Реки (Weirdness / PV) ═══════════════════
    [ExportCategory("Реки")]

    /// <summary>Частота шума Weirdness (реки). Масштаб в мировых единицах (тайлы).</summary>
    [Export(PropertyHint.Range, "0.001,20.0,0.001")]
    public float RiverFrequency { get; set; } = 4.0f;

    /// <summary>Ширина речной долины. Больше = шире реки (0..1).</summary>
    [Export(PropertyHint.Range, "0.0,0.3,0.01")]
    public float RiverWidth { get; set; } = 0.15f;

    /// <summary>Глубина реки (сколько ниже уровня моря). 0..1 от coastLevel.</summary>
    [Export(PropertyHint.Range, "0.0,1.0,0.05")]
    public float RiverDepth { get; set; } = 0.4f;

    /// <summary>Включить генерацию рек.</summary>
    [Export] public bool RiversEnabled { get; set; } = true;

    // ═══════════════════ Domain Warp ═══════════════════
    [ExportCategory("Domain Warp")]

    /// <summary>Сила искажения координат. 0 = ровные границы, 50+ = органичные.</summary>
    [Export(PropertyHint.Range, "0,200,1")]
    public float DomainWarpAmplitude { get; set; } = 60f;

    /// <summary>Частота warp-шума. Масштаб в мировых единицах (тайлы).</summary>
    [Export(PropertyHint.Range, "0.001,20.0,0.001")]
    public float DomainWarpFrequency { get; set; } = 2.0f;

    // ═══════════════════ Фрактал ═══════════════════
    [ExportCategory("Фрактал")]
    [Export] public FastNoiseLite.FractalTypeEnum FractalType { get; set; } = FastNoiseLite.FractalTypeEnum.Fbm;
    [Export(PropertyHint.Range, "1,8,1")] public int Octaves { get; set; } = 5;
    [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float Persistence { get; set; } = 0.45f;
    [Export(PropertyHint.Range, "1.0,4.0,0.1")] public float Lacunarity { get; set; } = 2.2f;

    // ═══════════════════ Зоны C ═══════════════════
    [ExportCategory("Зоны C (пороги)")]

    /// <summary>C ниже = океан, C выше = берег.</summary>
    [Export(PropertyHint.Range, "0.1,0.8,0.01")]
    public float CoastStart { get; set; } = 0.35f;

    /// <summary>C выше = суша (равнины, леса).</summary>
    [Export(PropertyHint.Range, "0.15,0.85,0.01")]
    public float InlandStart { get; set; } = 0.45f;

    /// <summary>C выше = глубина континента (горы, плато).</summary>
    [Export(PropertyHint.Range, "0.2,0.95,0.01")]
    public float FarInlandStart { get; set; } = 0.65f;

    // ═══════════════════ Terrain Curves ═══════════════════
    //
    //  Две кривые определяют весь рельеф:
    //
    //  1. ContinentCurve(C) — ПОТЕНЦИАЛЬНАЯ высота.
    //     Плавная кривая: океан (0.04) → берег (0.22) → глубина (0.80).
    //     Определяет МАКСИМУМ, который может быть достигнут.
    //
    //  2. ErosionCurve(E) — ФАКТОР реализации потенциала.
    //     Монотонно убывает: E=0 → 1.0 (всё), E=1 → 0.01 (ничего).
    //     Управляет: горы (E=0), холмы (E≈0.4), равнины (E=1).
    //
    //  Формула суши:
    //     seaLevel = ContinentCurve(CoastStart)
    //     landRise = ContinentCurve(C) - seaLevel
    //     height = seaLevel + landRise × ErosionCurve(E)
    //            + detail × DetailStrength × ErosionCurve(E) × coastMask
    //
    //  Гарантии:
    //     • При одном C: меньше E → выше (монотонность ErosionCurve)
    //     • При одном E: больше C → выше (монотонность ContinentCurve)
    //     • Горы ВСЕГДА выше холмов. Математически невозможно иначе.
    //
    [ExportCategory("Terrain Curves")]

    /// <summary>
    /// Кривая ПОТЕНЦИАЛА: Континентальность (X: 0..1) → Макс. высота (Y: 0..1).
    ///
    /// Должна быть МОНОТОННО ВОЗРАСТАЮЩЕЙ:
    ///   X=0    → Y≈0.04  (дно океана)
    ///   X=0.20 → Y≈0.04  (плоское дно)
    ///   X=0.35 → Y≈0.22  (берег = уровень моря)
    ///   X=0.55 → Y≈0.40  (суша)
    ///   X=0.75 → Y≈0.58  (глубокая суша)
    ///   X=1.00 → Y≈0.80  (потенциал пиков)
    ///
    /// Часть НИЖЕ seaLevel — океан (E не влияет).
    /// Часть ВЫШЕ seaLevel — landRise (умножается на ErosionCurve).
    /// </summary>
    [Export] public Curve ContinentCurve { get; set; }

    /// <summary>
    /// Кривая РЕАЛИЗАЦИИ: Эрозия (X: 0..1) → Фактор высоты (Y: 0..1).
    ///
    /// Должна быть МОНОТОННО УБЫВАЮЩЕЙ:
    ///   X=0   (нет эрозии)  → Y=1.00  (горы: весь потенциал)
    ///   X=0.2 (мало)        → Y≈0.72  (предгорья)
    ///   X=0.4 (умеренно)    → Y≈0.40  (холмы)
    ///   X=0.7 (много)       → Y≈0.08  (почти плоско)
    ///   X=1.0 (макс.)       → Y≈0.01  (равнина)
    ///
    /// Умножает и landRise, и detail noise.
    /// Поэтому: горы высокие И текстурные, равнины низкие И гладкие.
    /// </summary>
    [Export] public Curve ErosionCurve { get; set; }

    /// <summary>Создаёт кривые по умолчанию, если null (старые .tres).</summary>
    public void EnsureCurves()
    {
        ContinentCurve ??= CreateDefaultContinentCurve();
        ErosionCurve   ??= CreateDefaultErosionCurve();
    }

    /// <summary>
    /// C-кривая: потенциал высоты.
    /// Плоское дно → крутой подъём к берегу → плавный рост вглубь.
    /// </summary>
    public static Curve CreateDefaultContinentCurve()
    {
        var c = new Curve();
        c.AddPoint(new Vector2(0.00f, 0.04f));   // Глубокий океан
        c.AddPoint(new Vector2(0.20f, 0.04f));   // Плоское дно
        c.AddPoint(new Vector2(0.33f, 0.12f));   // Подводный склон
        c.AddPoint(new Vector2(0.35f, 0.22f));   // Берег (= уровень моря)
        c.AddPoint(new Vector2(0.45f, 0.30f));   // Прибрежная суша
        c.AddPoint(new Vector2(0.55f, 0.40f));   // Суша
        c.AddPoint(new Vector2(0.70f, 0.55f));   // Глубокая суша
        c.AddPoint(new Vector2(0.85f, 0.68f));   // Горный базис
        c.AddPoint(new Vector2(1.00f, 0.80f));   // Потенциал пиков
        return c;
    }

    /// <summary>
    /// E-кривая: фактор реализации.
    /// Крутой спад: горы(1.0) → холмы(0.40) → равнины(0.01).
    /// </summary>
    public static Curve CreateDefaultErosionCurve()
    {
        var c = new Curve();
        c.AddPoint(new Vector2(0.00f, 1.00f));   // Пики: полный потенциал
        c.AddPoint(new Vector2(0.20f, 0.72f));   // Предгорья
        c.AddPoint(new Vector2(0.40f, 0.40f));   // Холмы
        c.AddPoint(new Vector2(0.55f, 0.20f));   // Переход
        c.AddPoint(new Vector2(0.70f, 0.08f));   // Почти плоско
        c.AddPoint(new Vector2(0.85f, 0.03f));   // Очень плоско
        c.AddPoint(new Vector2(1.00f, 0.01f));   // Равнина
        return c;
    }

    public static NoiseSettings CreateDefault()
    {
        var s = new NoiseSettings();
        s.EnsureCurves();
        return s;
    }
}