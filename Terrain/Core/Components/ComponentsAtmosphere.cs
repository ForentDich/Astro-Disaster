using Friflo.Engine.ECS;
using Godot;

public struct AtmosphereSettings : IComponent
{
    public float AtmosphereHeight;
    public float RayleighScaleHeight;
    public float MieScaleHeight;
    public Vector3 RayleighScattering;
    public Vector3 MieScattering;
    public Vector3 MieExtinction;
    public Vector3 OzoneAbsorption;
    public float MiePhaseG;
    public Vector3 GroundAlbedo;
    public float SunIntensity;
}

public struct AtmosphereLuts : IComponent
{
    public Texture2D Transmittance;
    public Texture2D MultiScattering;
    public Texture2D SkyView;
}

public struct AtmosphereMesh : IComponent
{
    public ulong InstanceId;
    public MeshInstance3D GetMesh() => GodotObject.InstanceFromId(InstanceId) as MeshInstance3D;
}

public struct AtmosphereRuntime : IComponent
{
    public float LastViewHeight;
    public Vector3 LastSunDirection;
}
