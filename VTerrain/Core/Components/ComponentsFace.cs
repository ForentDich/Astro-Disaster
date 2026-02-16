using Friflo.Engine.ECS;
using Godot;

public struct FaceIdentity : IComponent
{
    public int Index;           
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