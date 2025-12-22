using Friflo.Engine.ECS;

public struct CelestialActive : ITag { }
public struct CelestialInactive : ITag { }
public struct CelestialNeedsSetup : ITag { }

public struct CelestialSun : ITag { }
public struct CelestialMoon : ITag { }
public struct CelestialStars : ITag { }
public struct CelestialPlanet : ITag { }