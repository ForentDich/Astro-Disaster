using Friflo.Engine.ECS;

public struct WorldData : IComponent
{
    public int WorldId;
    public string Name;
    public int Seed;
    public string SavePath;
    public ulong CreatedAt;
    public int Version;
}