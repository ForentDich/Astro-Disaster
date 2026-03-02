shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_disabled;

// ── Runtime (set by TerrainTextureLoader — don't touch) ──
group_uniforms runtime;
uniform sampler2DArray terrain_textures : source_color, filter_nearest_mipmap, repeat_enable;
uniform sampler2D tint_lut : filter_nearest;
uniform int surface_count = 5;
group_uniforms;

group_uniforms surface;
uniform float tile_world_size = 3.0;
uniform float tint_variation : hint_range(0.0, 0.5) = 0.08;
uniform float tint_scale : hint_range(1.0, 100.0) = 20.0;
uniform float roughness_base : hint_range(0.0, 1.0) = 0.9;
group_uniforms;

varying float v_surface_id;
varying flat float v_tile_hash;    // per-tile random for rotation
varying flat vec2  v_tile_offset;  // per-tile pixel offset (snapped to 1/32)
varying vec2 v_world_xz;           // world position for smooth noise

// Fast hash from tile grid position
float tile_hash(vec2 grid) {
    return fract(sin(dot(grid, vec2(127.1, 311.7))) * 43758.5453);
}
float tile_hash2(vec2 grid) {
    return fract(sin(dot(grid, vec2(269.5, 183.3))) * 43758.5453);
}

// ── Gradient noise (no grid artifacts) ──
vec2 grad_hash(vec2 p) {
    // Random gradient vector per grid point
    p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
    return -1.0 + 2.0 * fract(sin(p) * 43758.5453);
}
float grad_noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    // Quintic interpolation (smoother than cubic, no second-derivative discontinuities)
    vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    return mix(
        mix(dot(grad_hash(i),                 f),
            dot(grad_hash(i + vec2(1.0, 0.0)), f - vec2(1.0, 0.0)), u.x),
        mix(dot(grad_hash(i + vec2(0.0, 1.0)), f - vec2(0.0, 1.0)),
            dot(grad_hash(i + vec2(1.0, 1.0)), f - vec2(1.0, 1.0)), u.x),
        u.y) * 0.5 + 0.5;  // remap -1..1 → 0..1
}
float tint_noise(vec2 world_pos) {
    // 3 octaves with domain rotation to kill any remaining grid alignment
    vec2 p = world_pos / tint_scale;
    float n  = grad_noise(p) * 0.55;
    p = p * 2.3 + vec2(50.0, -30.0);  // different offset per octave
    n       += grad_noise(p) * 0.30;
    p = p * 2.3 + vec2(-80.0, 40.0);
    n       += grad_noise(p) * 0.15;
    return n;  // 0..1 range
}

void vertex() {
    v_surface_id = UV2.x;

    // World position — all per-tile effects must use this, not local VERTEX
    vec3 world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    v_world_xz = world_pos.xz;

    // Tile grid in WORLD space — globally unique per tile
    vec2 tile_grid = floor(world_pos.xz / tile_world_size + 0.001);
    v_tile_hash = tile_hash(tile_grid);
    // Pixel-snapped UV offset: floor(hash*32)/32 gives whole-pixel shift on a 32×32 texture
    v_tile_offset = floor(vec2(tile_hash2(tile_grid), tile_hash2(tile_grid + 71.0)) * 32.0) / 32.0;
}

vec4 get_tint(int idx) {
    float u = (float(idx) + 0.5) / float(surface_count);
    return texture(tint_lut, vec2(u, 0.5));
}

// Rotate UV in 90° increments: 0,1,2,3 → 0°,90°,180°,270°
vec2 rotate_uv(vec2 uv, int rot) {
    vec2 c = uv - 0.5;  // center
    if (rot == 1) c = vec2(-c.y, c.x);        // 90°
    else if (rot == 2) c = vec2(-c.x, -c.y);  // 180°
    else if (rot == 3) c = vec2(c.y, -c.x);   // 270°
    return c + 0.5;
}

void fragment() {
    int surf_id = int(round(v_surface_id));

    // ── Per-tile UV rotation + pixel offset ──
    int rotation = int(v_tile_hash * 4.0);  // 0..3
    vec2 tex_uv = rotate_uv(UV, rotation) + v_tile_offset;

    vec4 tex = texture(terrain_textures, vec3(tex_uv, float(surf_id)));
    vec4 tint = get_tint(surf_id);

    // Convert texture to grayscale so tint fully controls the color.
    // Texture provides shape/detail only, tint provides color and brightness.
    // ×2.0 normalizes typical textures (avg luminance ~0.5) to ~1.0,
    // so tint [1,1,1] = white, [0.5,0.5,0.5] = gray, etc.
    float detail = dot(tex.rgb, vec3(0.299, 0.587, 0.114)) * 2.0;

    // ── Smooth tint variation (noise patches) ──
    float noise_raw = (tint_noise(v_world_xz) * 2.0 - 1.0) * tint_variation;
    float tint_mul = 1.0 + noise_raw * tint.a;

    ALBEDO = clamp(tint.rgb * detail * tint_mul, vec3(0.0), vec3(1.0));
    ROUGHNESS = roughness_base;
}
