using Friflo.Engine.ECS;
using Godot;

/// <summary>
/// Attach this script to a Camera3D in the scene.
/// On _Ready it registers itself with GameplaySession, creating a Friflo entity
/// linked to the player entity via <see cref="CameraFollowsPlayer"/>.
///
/// Set <see cref="PlayerNodePath"/> in the Inspector to point at the PlayerNode.
/// </summary>
public partial class OrbitalCameraNode : Camera3D
{
    [ExportGroup("Target")]
    /// <summary>Path to the PlayerNode this camera follows.</summary>
    [Export] public NodePath PlayerNodePath { get; set; }

    [ExportGroup("Orbital")]
    [Export] public float Distance        { get; set; } = 3.0f;
    [Export] public float Sensitivity     { get; set; } = 0.002f;
    [Export] public Vector3 ShoulderOffset { get; set; } = new(0, 1.5f, 0);

    /// <summary>Friflo entity representing this camera.</summary>
    public Entity Entity { get; private set; }

    public override void _Ready()
    {
        CallDeferred(MethodName.Register);
    }

    private void Register()
    {
        var session = GameplaySession.Instance;
        if (session == null)
        {
            return;
        }

        if (PlayerNodePath == null || PlayerNodePath.IsEmpty)
        {
            return;
        }

        var playerNode = GetNode<PlayerNode>(PlayerNodePath);
        if (playerNode == null)
        {
            return;
        }

        if (playerNode.Entity.IsNull)
        {
            return;
        }

        Entity = session.RegisterCamera(
            this,
            playerNode.Entity,
            Distance, Sensitivity, ShoulderOffset
        );
    }

    public override void _ExitTree()
    {
        if (!Entity.IsNull && Entity.Store != null)
            Entity.DeleteEntity();
    }
}
