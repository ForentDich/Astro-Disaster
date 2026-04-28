using Friflo.Engine.ECS;
using Godot;

/// <summary>
/// Component for a celestial body that exerts gravity.
/// GM = gravitational parameter (G * Mass). 
/// Surface gravity = GM / Radius².
/// Attached to the planet entity in the Terrain ECS.
/// </summary>
public struct GravitySource : IComponent
{
    /// <summary>Center of the planet in world space.</summary>
    public Vector3 Center;
    /// <summary>Gravitational parameter GM = G * Mass. Determines gravity strength.</summary>
    public float GM;
    /// <summary>Planet radius. Used to compute surface gravity for reference.</summary>
    public float Radius;
}

/// <summary>
/// Link from a gameplay entity (player) to a celestial body that pulls it.
/// The farther the entity is from the planet, the weaker the gravity (inverse square law).
/// </summary>
public struct GravityAffected : ILinkComponent
{
    /// <summary>The celestial entity (from Terrain ECS) that exerts gravity.</summary>
    public Entity Planet;
    public Entity GetIndexedValue() => Planet;
}
