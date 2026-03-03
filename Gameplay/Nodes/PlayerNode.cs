using Friflo.Engine.ECS;
using Godot;

/// <summary>
/// Attach this script to a CharacterBody3D in the scene.
/// On _Ready it registers itself with GameplaySession and creates a Friflo entity.
/// All gameplay parameters are configured in the Godot Inspector.
/// </summary>
public partial class PlayerNode : CharacterBody3D
{
    [ExportGroup("Movement")]
    [Export] public float Speed         { get; set; } = 30.0f;
    [Export] public float GravityForce  { get; set; } = 9.8f;
    [Export] public float MaxFallSpeed  { get; set; } = 20.0f;

    [ExportGroup("Jump")]
    [Export] public float JumpForce         { get; set; } = 5.0f;
    [Export] public float JumpBufferDuration { get; set; } = 0.12f;

    [ExportGroup("Noclip")]
    [Export] public float NoclipSpeedMultiplier { get; set; } = 5.0f;
    [Export] public float NoclipVerticalSpeed   { get; set; } = 10.0f;

    [ExportGroup("Rotation")]
    [Export] public float RotationSpeed { get; set; } = 12.0f;

    /// <summary>Friflo entity representing this player.</summary>
    public Entity Entity { get; private set; }

    public override void _Ready()
    {
        // GameplaySession may not be ready yet if it's a sibling,
        // so defer one frame to be safe.
        CallDeferred(MethodName.Register);
    }

    private void Register()
    {
        var session = GameplaySession.Instance;
        if (session == null)
        {
            GD.PrintErr("[PlayerNode] GameplaySession.Instance is null! " +
                        "Make sure GameplaySession is higher in the scene tree.");
            return;
        }

        Entity = session.RegisterPlayer(
            this,
            Speed, GravityForce, MaxFallSpeed,
            JumpForce, JumpBufferDuration,
            NoclipSpeedMultiplier, NoclipVerticalSpeed,
            RotationSpeed
        );
    }

    public override void _ExitTree()
    {
        if (!Entity.IsNull && Entity.Store != null)
            Entity.DeleteEntity();
    }
}
