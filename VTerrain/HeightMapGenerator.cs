using Godot;

public static class HeightMapGenerator
{
	public static int[,] Generate(int width, int height, int maxHeight,
		float noiseScale, int octaves, float persistence, float lacunarity, Vector2 noiseOffset,
		FastNoiseLite sharedNoise = null)
	{
		var noise = sharedNoise ?? new FastNoiseLite();
		var heightmap = new int[width, height];

		for (int x = 0; x < width; x++)
		{
			for (int y = 0; y < height; y++)
			{
				heightmap[x, y] = SampleHeight(x, y, noiseScale, octaves, persistence, lacunarity, noiseOffset, maxHeight, noise);
			}
		}

		return heightmap;
	}

	public static int SampleHeight(float x, float y,
		float noiseScale, int octaves, float persistence, float lacunarity, Vector2 noiseOffset,
		int maxHeight, FastNoiseLite noise)
	{
		float raw = SampleRawNoise(noise, x, y, noiseScale, octaves, persistence, lacunarity, noiseOffset);
		float normalized = (raw + 1f) * 0.5f;
		int height = (int)(normalized * (maxHeight + 1));
		return Mathf.Clamp(height, 0, maxHeight);
	}

	private static float SampleRawNoise(FastNoiseLite noise, float x, float y,
		float noiseScale, int octaves, float persistence, float lacunarity, Vector2 noiseOffset)
	{
		float value = 0f;
		float amplitude = 1f;
		float frequency = 1f;
		float maxValue = 0f;

		for (int i = 0; i < octaves; i++)
		{
			float sampleX = (x + noiseOffset.X) * noiseScale * frequency;
			float sampleY = (y + noiseOffset.Y) * noiseScale * frequency;

			value += noise.GetNoise2D(sampleX, sampleY) * amplitude;
			maxValue += amplitude;

			amplitude *= persistence;
			frequency *= lacunarity;
		}

		return maxValue == 0f ? 0f : value / maxValue;
	}
}
