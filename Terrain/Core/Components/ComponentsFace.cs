using Friflo.Engine.ECS;
using Godot;

public struct FaceIdentity : IComponent
{
    public int Index;           
    /// <summary>Number of segments along one side of this face (1, 3, 5...). Always odd so there's a center segment.</summary>
    public int SegmentsPerSide;
}

public struct FaceName : IComponent
{
    public string Value;         
}

public struct FacePosition : IComponent
{
    public Vector3 WorldPosition; 
}

public struct FaceOrientation : IComponent
{
    public Vector3 Normal;      
    public Vector3 Up;          
    public Vector3 Right;       
}

public struct FaceStorage : IComponent
{
    public string SavePath;      
}

public struct FaceParent : ILinkComponent
{
    public Entity Celestial;     
    public Entity GetIndexedValue() => Celestial;
}