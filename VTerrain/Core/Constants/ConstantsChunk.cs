public static class ChunkConstants
{
	public const int CHUNK_SIZE = 32;          
	public const int SEGMENT_SIZE_IN_CHUNKS = 16;
	public const int TILE_SIZE = 3;
	public const float TILE_HEIGHT = 1.73f; // TILE_SIZE * tan(30°) ≈ 3 * 0.577
	public const int CHUNK_WORLD_SIZE = CHUNK_SIZE * TILE_SIZE; // 96
}
