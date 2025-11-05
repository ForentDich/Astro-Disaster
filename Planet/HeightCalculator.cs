
using Godot;

public partial class HeightCalculator
{
    private PlanetData data;
    private bool isOcean;
    private FastNoiseLite baseNoise;

    public HeightCalculator(PlanetData data, bool isOcean)
    {
        this.data = data;
        this.isOcean = isOcean;
        this.baseNoise = data.BaseNoise;
    }

    public float CalculateHeight(Vector3 pointOnSphere)
    {
        float baseHeight = data.Radius;

        if (!isOcean && data.BaseNoise != null)
        {
            float rawNoise = baseNoise.GetNoise3Dv(pointOnSphere);
            float normalizedNoise = (rawNoise + 1f) * 0.5f;
            
            float curvedNoise = data.HeightCurve?.Sample(normalizedNoise) ?? normalizedNoise;
            float height = data.MinHeight + curvedNoise * (data.MaxHeight - data.MinHeight);
            

            if (data.EnableGridSnap && data.GridStep > 0)
            {
                height = Mathf.Round(height / data.GridStep) * data.GridStep;
            }
            
            baseHeight += height;
        }
        else if (isOcean)
        {
            baseHeight += data.MinHeight + data.OceanLevel * (data.MaxHeight - data.MinHeight);
        }

        return baseHeight;
    }

    public float GetOceanHeight()
    {
        return data.Radius + data.MinHeight + data.OceanLevel * (data.MaxHeight - data.MinHeight);
    }
}