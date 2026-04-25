shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_disabled;

group_uniforms runtime;
uniform sampler2DArray terrain_textures : source_color, filter_nearest_mipmap, repeat_enable;
uniform sampler2D tint_lut : source_color, filter_nearest;
uniform int surface_count = 1;
group_uniforms;

group_uniforms surface;
uniform float tile_world_size = 3.0;
uniform float tint_variation : hint_range(0.0, 0.5) = 0.08;
uniform float tint_noise_scale : hint_range(1.0, 128.0) = 24.0;
uniform float roughness_base : hint_range(0.0, 1.0) = 0.95;
group_uniforms;

varying flat float v_surface_id;
varying vec2 v_world_xz;

float hash12(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

vec4 get_tint(int idx)
{
    float u = (float(idx) + 0.5) / float(max(surface_count, 1));
    return texture(tint_lut, vec2(u, 0.5));
}

void vertex()
{
    v_surface_id = UV2.x;
    v_world_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment()
{
    int sid = clamp(int(round(v_surface_id)), 0, max(surface_count - 1, 0));

    vec2 uv = UV;
    vec4 tex = texture(terrain_textures, vec3(uv, float(sid)));
    vec4 tint = get_tint(sid);

    vec2 noiseCell = floor(v_world_xz / max(tint_noise_scale, 0.0001));
    float n = hash12(noiseCell) * 2.0 - 1.0;
    float tintMul = 1.0 + n * tint_variation * tint.a;

    ALBEDO = clamp(tex.rgb * tint.rgb * tintMul, vec3(0.0), vec3(1.0));
    ROUGHNESS = roughness_base;
}
