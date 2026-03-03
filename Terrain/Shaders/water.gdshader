shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_disabled, diffuse_burley, specular_schlick_ggx;

// ── Colour ──
uniform vec4 water_color : source_color = vec4(0.05, 0.22, 0.38, 1.0);
uniform float beer_factor : hint_range(0.05, 2.0) = 0.45;

// ── Waves ──
uniform vec2  wave_direction  = vec2(1.0, 0.0);  // toward shore
uniform float wave_speed      : hint_range(0.0, 5.0)   = 1.5;
uniform float wave_height     : hint_range(0.0, 5.0)   = 0.3;
uniform float wave_scale      : hint_range(0.02, 0.5)  = 0.08;

// ── Surface ──
uniform float roughness_val : hint_range(0.0, 1.0) = 0.25;
uniform float specular_val  : hint_range(0.0, 1.0) = 0.5;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;

// ── Procedural value noise (no textures needed) ──
float _hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f); // smoothstep interpolation
	float a = _hash(i);
	float b = _hash(i + vec2(1.0, 0.0));
	float c = _hash(i + vec2(0.0, 1.0));
	float d = _hash(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

// 2 octaves of scrolling noise → organic, non-repeating displacement
float wave_noise(vec2 world_xz, float time) {
	vec2 scroll = normalize(wave_direction) * time * wave_speed;
	vec2 p = world_xz * wave_scale + scroll;

	float n  = value_noise(p)         * 1.0;   // large rolls
	      n += value_noise(p * 2.3 + 50.0) * 0.5;   // smaller detail
	return (n / 1.5) * 2.0 - 1.0; // normalize to -1..1
}

void vertex() {
	vec3 wv = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	VERTEX.y += wave_noise(wv.xz, TIME) * wave_height;
}

void fragment() {
	// Flat shading → each triangle is one facet
	vec3 world_v = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	NORMAL = -normalize(cross(dFdx(world_v), dFdy(world_v)));

	// Beer's law depth opacity
	float scene_depth = textureLod(depth_tex, SCREEN_UV, 0.0).r;
	vec4 ndc = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, scene_depth, 1.0);
	ndc.xyz /= ndc.w;
	float depth_diff = max(VERTEX.z - ndc.z, 0.0);
	float opacity = clamp(1.0 - exp(-depth_diff * beer_factor), 0.0, 0.98);

	ALBEDO    = water_color.rgb;
	ALPHA     = opacity;
	ROUGHNESS = roughness_val;
	METALLIC  = 0.0;
	SPECULAR  = specular_val;
}
