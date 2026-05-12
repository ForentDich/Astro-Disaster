using Friflo.Engine.ECS;

public struct CelestialActive : ITag { }   
public struct CelestialInactive : ITag { } 


public struct CelestialNeedsSegments : ITag { } 
public struct CelestialHasSegments : ITag { }   
public struct CelestialNeedsFaces : ITag { }    
public struct CelestialHasFaces : ITag { }      


public struct CelestialSun : ITag { }
public struct CelestialMoon : ITag { }
public struct CelestialStars : ITag { }
public struct CelestialPlanet : ITag { }
public struct CelestialPrimary : ITag { }
public struct CelestialAsteroid : ITag { }
public struct CelestialComet : ITag { }


public struct CelestialHasRings : ITag { }
public struct CelestialHasWater : ITag { }


public struct FaceCreated : ITag { }            
public struct FaceHasSegments : ITag { }     
public struct FaceNeedsSegments : ITag { }