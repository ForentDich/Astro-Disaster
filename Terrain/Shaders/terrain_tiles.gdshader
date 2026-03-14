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

group_uniforms blending;
uniform float blend_width : hint_range(0.05, 0.5) = 0.25;
uniform float border_noise_freq : hint_range(0.5, 10.0) = 3.0;
uniform float border_pixel_size : hint_range(0.1, 3.0) = 0.5;
uniform float border_amplitude : hint_range(0.1, 2.0) = 0.6;
group_uniforms;

varying float v_surface_id;
varying flat float v_tile_hash;    // per-tile random for rotation
varying flat vec2  v_tile_offset;  // per-tile pixel offset (snapped to 1/32)
varying vec2 v_world_xz;           // world position for smooth noise
varying flat vec4 v_neighbor_ids;  // R=north, G=east, B=south, A=west surface IDs

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
    v_neighbor_ids = COLOR;

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

    // ── Decode neighbor surface IDs ──
    int north_id = int(round(v_neighbor_ids.r * 255.0));
    int east_id  = int(round(v_neighbor_ids.g * 255.0));
    int south_id = int(round(v_neighbor_ids.b * 255.0));
    int west_id  = int(round(v_neighbor_ids.a * 255.0));

    int final_id = surf_id;

    // ── Organic border blending ──
    if (north_id != surf_id || east_id != surf_id || south_id != surf_id || west_id != surf_id) {
        // Snap to pixel grid — all computations use cell center → clean blocks
        vec2 cell_center = (floor(v_world_xz / border_pixel_size) + 0.5) * border_pixel_size;
        vec2 tile_frac = fract(cell_center / tile_world_size);
        float dist_n = tile_frac.y;
        float dist_s = 1.0 - tile_frac.y;
        float dist_w = tile_frac.x;
        float dist_e = 1.0 - tile_frac.x;

        // Find the closest edge where neighbor has HIGHER surface ID
        // (only one side blends → no double stripe)
        float best_dist = 999.0;
        int blend_id = surf_id;

        if (north_id > surf_id && dist_n < best_dist) { best_dist = dist_n; blend_id = north_id; }
        if (south_id > surf_id && dist_s < best_dist) { best_dist = dist_s; blend_id = south_id; }
        if (west_id  > surf_id && dist_w < best_dist) { best_dist = dist_w; blend_id = west_id; }
        if (east_id  > surf_id && dist_e < best_dist) { best_dist = dist_e; blend_id = east_id; }

        // Warp border with noise sampled at same cell center
        float warp = grad_noise(cell_center * border_noise_freq) * 2.0 - 1.0;
        float warped_edge = blend_width * (1.0 + warp * border_amplitude);

        if (best_dist < warped_edge) {
            final_id = blend_id;
        }
    }

    // ── Per-tile UV rotation + pixel offset ──
    int rotation = int(v_tile_hash * 4.0);
    vec2 tex_uv = rotate_uv(UV, rotation) + v_tile_offset;

    vec4 tex = texture(terrain_textures, vec3(tex_uv, float(final_id)));
    vec4 tint = get_tint(final_id);

    float detail = dot(tex.rgb, vec3(0.299, 0.587, 0.114)) * 2.0;

    // ── Smooth tint variation (noise patches) ──
    float noise_raw = (tint_noise(v_world_xz) * 2.0 - 1.0) * tint_variation;
    float tint_mul = 1.0 + noise_raw * tint.a;

    ALBEDO = clamp(tint.rgb * detail * tint_mul, vec3(0.0), vec3(1.0));
    ROUGHNESS = roughness_base;
}
