/// <summary>
/// Continental zones — determined by continentalness noise (C).
/// Controls terrain SHAPE (height profile, detail amplitude).
/// Separate from biomes, which control SURFACE (textures, vegetation).
///
/// Future: biome = f(zone, temperature, humidity)
///   e.g. Zone=Coast + Temp=warm → Beach biome
///        Zone=FarInland + Temp=cold → Snowy Mountains biome
/// </summary>
public enum ContinentalZone : byte
{
    /// <summary>C below CoastStart. Flat ocean floor, no detail.</summary>
    Ocean = 0,

    /// <summary>C between CoastStart and InlandStart. Beaches, shallow water, gentle rise.</summary>
    Coast = 1,

    /// <summary>C between InlandStart and FarInlandStart. Rolling hills, plains.</summary>
    Inland = 2,

    /// <summary>C above FarInlandStart. Mountains, plateaus, max detail.</summary>
    FarInland = 3,

    /// <summary>River valley carved by PV noise. Sand/gravel bed.</summary>
    River = 4
}
