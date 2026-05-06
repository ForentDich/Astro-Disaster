public static class ConstantsCelestial
{
	public const int FACE_COUNT = 6;           
	
	/// <summary>
	/// Atmosphere height above the planet surface.
	/// </summary>
	public const float ATMOSPHERE_HEIGHT = 100.0f;
	
	public const int SEA_LEVEL = 64;
	public const int MAX_HEIGHT = 256;
	public const int MIN_HEIGHT = 0;

	/// <summary>
	/// Star radius in world units (40 km).
	/// </summary>
	public const float STAR_RADIUS = 40000f;

	/// <summary>
	/// Computes the planet radius from face geometry.
	/// Radius = half the face size in world units.
	/// Face size = SegmentsPerSide * SIDE * CHUNK_SIZE * TILE_SIZE.
	/// </summary>
	public static float ComputeRadius(int segmentsPerSide)
	{
		float halfFace = segmentsPerSide 
			* ConstantsSegment.SIDE 
			* ChunkConstants.CHUNK_SIZE 
			* ChunkConstants.TILE_SIZE 
			* 0.5f;
		return halfFace;
	}
}
