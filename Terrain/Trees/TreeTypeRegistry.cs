using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Registry for tree type materials. Loads from tree_types.json once, caches materials.
/// </summary>
public static class TreeTypeRegistry
{
	private static Dictionary<TreeType, (ShaderMaterial trunk, Material canopy)> _materialCache = new();
	private static bool _loaded = false;

	/// <summary>
	/// Load tree types from JSON. Call once at startup.
	/// Path format: res://Terrain/Data/tree_types.json
	/// </summary>
	public static void Load(string path = "res://Terrain/Data/tree_types.json")
	{
		if (_loaded) return;

		var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr($"[TreeTypeRegistry] Failed to load {path}");
			return;
		}

		var jsonStr = file.GetAsText();
		var json = new Json();
		var error = json.Parse(jsonStr);
		
		if (error != Error.Ok)
		{
			GD.PrintErr($"[TreeTypeRegistry] Failed to parse JSON: {error}");
			return;
		}

		var rootVariant = json.Data;
		// Cast Variant to Godot Dictionary
		var root = rootVariant.AsGodotDictionary();
		if (root == null)
		{
			GD.PrintErr("[TreeTypeRegistry] Root must be object");
			return;
		}

		foreach (var typeNameKey in root.Keys)
		{
			string typeNameStr = (string)typeNameKey;
			
			if (!Enum.TryParse<TreeType>(typeNameStr, ignoreCase: true, out var treeType))
			{
				GD.Print($"[TreeTypeRegistry] Unknown tree type: {typeNameStr}");
				continue;
			}

			var typeNodeVariant = root[typeNameKey];
			var typeNode = typeNodeVariant.AsGodotDictionary();
			if (typeNode == null) continue;

			string trunkTexPath = (string)typeNode["trunk_texture"];
			string trunkColorHex = (string)typeNode["trunk_color"];
			string canopyColorHex = (string)typeNode["canopy_color"];
			float barkTileWorldSize = typeNode.ContainsKey("bark_tile_world_size")
				? (float)(double)typeNode["bark_tile_world_size"]
				: 1.25f;

			var trunkMat = CreateTrunkMaterial(
				trunkTexPath,
				ParseColor(trunkColorHex),
				barkTileWorldSize
			);
			var canopyMat = CreateCanopyMaterial(ParseColor(canopyColorHex));

			_materialCache[treeType] = (trunkMat, canopyMat);
		}

		_loaded = true;
		GD.Print($"[TreeTypeRegistry] Loaded {_materialCache.Count} tree types");
	}

	private static ShaderMaterial CreateTrunkMaterial(string texturePath, Color color, float barkTileWorldSize)
	{
		var shader = GD.Load<Shader>("res://Terrain/Shaders/trunk_textured.gdshader");
		if (shader == null)
		{
			GD.PrintErr($"[TreeTypeRegistry] Failed to load trunk shader");
			return null;
		}

		var material = new ShaderMaterial { Shader = shader };

		// Load texture — nullable, fallback to white
		var texture = GD.Load<Texture2D>(texturePath);
		if (texture != null)
		{
			material.SetShaderParameter("bark_texture", texture);
		}
		else
		{
			GD.Print($"[TreeTypeRegistry] Texture not found, using default: {texturePath}");
		}

		material.SetShaderParameter("bark_color", color);
		material.SetShaderParameter("bark_tile_world_size", barkTileWorldSize);
		return material;
	}

	private static StandardMaterial3D CreateCanopyMaterial(Color color)
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = 0.9f,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		return material;
	}

	private static Color ParseColor(string hex)
	{
		hex = hex.TrimStart('#');
		// Godot 4: использованием FromString с "0x" префиксом
		uint argb = (uint)hex.HexToInt();
		return new Color(
			((argb >> 16) & 0xFF) / 255f,
			((argb >> 8) & 0xFF) / 255f,
			(argb & 0xFF) / 255f,
			1.0f
		);
	}

	/// <summary>Get cached trunk material for tree type.</summary>
	public static ShaderMaterial GetTrunkMaterial(TreeType type)
	{
		return _materialCache.TryGetValue(type, out var mats) ? mats.trunk : null;
	}

	/// <summary>Get cached canopy material for tree type.</summary>
	public static Material GetCanopyMaterial(TreeType type)
	{
		return _materialCache.TryGetValue(type, out var mats) ? mats.canopy : null;
	}
}
