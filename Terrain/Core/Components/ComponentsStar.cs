using Friflo.Engine.ECS;
using Godot;

/// <summary>
/// Star-specific data.
/// Attached to the star entity created by SystemStarCreator.
/// </summary>
public struct StarData : IComponent
{
    /// <summary>Star radius in world units (e.g. 40000).</summary>
    public float Radius;

    /// <summary>Surface temperature in Kelvin (visual placeholder).</summary>
    public float Temperature;

    /// <summary>Luminosity relative to the sun (visual placeholder).</summary>
    public float Luminosity;
}
