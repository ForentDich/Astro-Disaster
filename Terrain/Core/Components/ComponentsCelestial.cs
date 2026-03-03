using Friflo.Engine.ECS;
using Godot;

public struct CelestialIdentity : IComponent
{
    public int Id;               
    public CelestialType Type;
}

public struct CelestialGeometry : IComponent
{
    public float Radius;        
}

public struct CelestialTransform : IComponent
{
    public Vector3 Position;     
    public Quaternion Rotation;  
    public Vector3 Scale;       
}

public struct CelestialStatus : IComponent
{
    public float Gravity;       
}

public struct CelestialParent : ILinkComponent
{
    public Entity World;         
    public Entity GetIndexedValue() => World;
}

public enum CelestialType
{
    Planet,
    Moon,
    Star,
    Asteroid,
    Comet
}