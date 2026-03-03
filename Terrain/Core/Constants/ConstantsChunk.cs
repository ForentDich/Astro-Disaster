public static class ChunkConstants
{
	public const int CHUNK_SIZE = 32;          
	public const int SEGMENT_SIZE_IN_CHUNKS = 16;
	public const int TILE_SIZE = 3;
	public const float TILE_HEIGHT = 1.73f; // TILE_SIZE * tan(30°) ≈ 3 * 0.577
	public const int CHUNK_WORLD_SIZE = CHUNK_SIZE * TILE_SIZE; // 96

	/// <summary>Bytes per tile in terrain data: [baseHeight, tileType, surfaceId|waterFlag]</summary>
	public const int BYTES_PER_TILE = 3;

	/// <summary>Bit 7 of surfaceId byte: 1 = tile has water on top.</summary>
	public const byte WATER_FLAG = 0x80;

	/// <summary>Mask to extract surface index (bits 0-6) from surfaceId byte.</summary>
	public const byte SURFACE_MASK = 0x7F;

	/// <summary>Total terrain data size per chunk in bytes</summary>
	public const int CHUNK_DATA_SIZE = CHUNK_SIZE * CHUNK_SIZE * BYTES_PER_TILE; // 3072
}
