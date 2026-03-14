/// <summary>
/// Range-based parameters for procedural tree generation.
/// All concrete values are derived from a seed at generation time.
/// </summary>
public struct TreeParams
{
	// ── Trunk ──
	public float TrunkHeightMin, TrunkHeightMax;
	public float TrunkRadiusMin, TrunkRadiusMax;
	public float TrunkTaper; // top radius = bottom × taper

	// ── Branches ──
	public int   BranchCountMin, BranchCountMax;
	public float BranchLengthMin, BranchLengthMax;     // fraction of trunk height
	public float BranchThicknessMin, BranchThicknessMax; // fraction of trunk radius
	public float BranchAngleMin, BranchAngleMax;       // degrees from vertical
	public float BranchStartMin, BranchStartMax;       // fraction of trunk height

	// ── Canopy ──
	public float CanopyRadiusMin, CanopyRadiusMax;
	public float BranchCanopyScale; // branch canopy = branch_length × this

	// ── Mesh detail ──
	public int TrunkSides;
	public int BranchSides;
	public int CanopySubdivisions;

	public static TreeParams Oak => new()
	{
		TrunkHeightMin = 8.5f, TrunkHeightMax = 12.5f,
		TrunkRadiusMin = 0.6f, TrunkRadiusMax = 1.2f,
		TrunkTaper = 0.35f,

		BranchCountMin = 2, BranchCountMax = 4,
		BranchLengthMin = 0.35f, BranchLengthMax = 0.65f,
		BranchThicknessMin = 0.25f, BranchThicknessMax = 0.55f,
		BranchAngleMin = 30f, BranchAngleMax = 65f,
		BranchStartMin = 0.5f, BranchStartMax = 0.75f,

		CanopyRadiusMin = 2.2f, CanopyRadiusMax = 4.5f,
		BranchCanopyScale = 0.45f,

		TrunkSides = 4,
		BranchSides = 4,
		CanopySubdivisions = 1,
	};

	public static TreeParams Spruce => new()
	{
		TrunkHeightMin = 5.5f, TrunkHeightMax = 8.5f,
		TrunkRadiusMin = 0.2f, TrunkRadiusMax = 0.35f,
		TrunkTaper = 0.3f,

		BranchCountMin = 4, BranchCountMax = 7,
		BranchLengthMin = 0.15f, BranchLengthMax = 0.45f,
		BranchThicknessMin = 0.15f, BranchThicknessMax = 0.3f,
		BranchAngleMin = 60f, BranchAngleMax = 80f,
		BranchStartMin = 0.25f, BranchStartMax = 0.85f,

		CanopyRadiusMin = 0.8f, CanopyRadiusMax = 1.5f,
		BranchCanopyScale = 0.7f,

		TrunkSides = 4,
		BranchSides = 4,
		CanopySubdivisions = 1,
	};

	public static TreeParams ForType(TreeType type) => type switch
	{
		TreeType.Oak    => Oak,
		TreeType.Spruce => Spruce,
		_               => Oak,
	};
}
