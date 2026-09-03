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

// ── Orbit ──

public struct OrbitData : IComponent
{
    /// <summary>Orbit radius (distance from star center).</summary>
    public float Distance;
    /// <summary>Angular speed (radians per tick).</summary>
    public float Speed;
    /// <summary>Starting angle in radians.</summary>
    public float InitialAngle;
    /// <summary>Current angle in radians (updated each frame).</summary>
    public float CurrentAngle;
    /// <summary>Axial tilt in degrees (for seasons).</summary>
    public float AxialTilt;
}

// ── Surface ──

public struct SurfaceData : IComponent
{
    public int Seed;
    public float RotationSpeed;
}

// ── Planet proxy ──

public struct PlanetProxySettings : IComponent
{
    public bool Enabled;
    public int ResolutionDiv;
    public float ProxySink;
    public float ProxyDiscardRadius;
}

public struct PlanetProxyMesh : IComponent
{
    public ulong InstanceId;
    public MeshInstance3D GetMesh() => GodotObject.InstanceFromId(InstanceId) as MeshInstance3D;
}

