public static class ChunkConstants
{
	public const int CHUNK_SIZE = 32;          
	public const int SEGMENT_SIZE_IN_CHUNKS = 16;
	public const int TILE_SIZE = 3;
	public const float TILE_HEIGHT = 1.73f;
	public const int CHUNK_WORLD_SIZE = CHUNK_SIZE * TILE_SIZE;
	public const int CHUNK_VERTEX_SIZE = CHUNK_SIZE + 1;

	/// <summary>
	/// Terrain payload layout per chunk:
	/// 1) Vertex heights: 33x33 bytes (Y quantized to 0..255)
	/// 2) Cell surfaces:  32x32 bytes (surfaceId|waterFlag)
	/// </summary>
	public const int HEIGHT_COUNT = CHUNK_VERTEX_SIZE * CHUNK_VERTEX_SIZE; // 1089
	public const int CELL_COUNT = CHUNK_SIZE * CHUNK_SIZE; // 1024

	public const int HEIGHTS_OFFSET = 0;
	public const int CELLS_OFFSET = HEIGHT_COUNT;

	/// <summary>Bit 7 of surfaceId byte: 1 = tile has water on top.</summary>
	public const byte WATER_FLAG = 0x80;

	/// <summary>Mask to extract surface index (bits 0-6) from surfaceId byte.</summary>
	public const byte SURFACE_MASK = 0x7F;

	/// <summary>Total terrain data size per chunk in bytes.</summary>
	public const int CHUNK_DATA_SIZE = HEIGHT_COUNT + CELL_COUNT; // 2113
}
