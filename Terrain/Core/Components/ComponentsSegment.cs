using Friflo.Engine.ECS;
using Godot;

public struct SegmentIdentity : IComponent
{
    public int FaceIndex;           
    public Vector2I GridPosition;   
    public int SegmentId; 
}

public struct SegmentWorldPosition : IComponent
{
    public Vector3 Center;          
    public float Size;              
}

public struct SegmentHeightmap : IComponent
{
    public short[] Heights;         
    public bool IsCompressed;       
    public byte LOD;                
}

public struct SegmentBiomeMap : IComponent
{
    public byte[] Biomes;          
}

public struct SegmentResourceMap : IComponent
{
    public byte[] Resources;       
}

public struct SegmentTerrainStats : IComponent
{
    public short MinHeight;
    public short MaxHeight;
    public short AverageHeight;
    public bool HasWater;
    public bool HasCaves;
}

public struct SegmentPerformance : IComponent
{
    public float DistanceToViewer;  
    public float Priority;          
    public int LastFrameVisible;    
}

public struct SegmentParentFace : ILinkComponent
{
    public Entity Face;
    public Entity GetIndexedValue() => Face;
}

public struct SegmentParentCelestial : ILinkComponent
{
    public Entity Celestial;
    public Entity GetIndexedValue() => Celestial;
}
