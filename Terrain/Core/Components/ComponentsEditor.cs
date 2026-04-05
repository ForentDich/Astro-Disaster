using Friflo.Engine.ECS;

public struct TerrainEditRequest : IComponent
{
    public int PointX;
    public int PointY;
    public int PointZ;
    public int DeltaHeight;
}