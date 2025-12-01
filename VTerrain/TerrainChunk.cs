using Godot;
using System;

[GlobalClass]
public partial class TerrainChunk : Resource
{
    public const int ChunkSize = 64;
    
    [Export] private byte[] _data = new byte[ChunkSize * ChunkSize * 2];

    public void SetTile(int x, int y, TileType type, int height)
    {
        int index = (y * ChunkSize + x) * 2;
        _data[index] = (byte)height;
        _data[index + 1] = (byte)type;
    }

    public (TileType type, int height) GetTile(int x, int y)
    {
        int index = (y * ChunkSize + x) * 2;
        return ((TileType)_data[index + 1], _data[index]);
    }
}
