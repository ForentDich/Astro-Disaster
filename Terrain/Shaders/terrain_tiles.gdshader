shader_type spatial;
render_mode depth_draw_opaque, cull_back, diffuse_burley, specular_disabled;

group_uniforms runtime;
uniform sampler2DArray terrain_textures : source_color, filter_nearest_mipmap, repeat_enable;
uniform sampler2D tint_lut : source_color, filter_nearest;
uniform int surface_count = 1;
group_uniforms;

varying flat float v_surface_id;

vec4 get_tint(int idx)
{
    float u = (float(idx) + 0.5) / float(max(surface_count, 1));
    return texture(tint_lut, vec2(u, 0.5));
}

void vertex()
{
    v_surface_id = UV2.x;
}

void fragment()
{
    int sid = clamp(int(round(v_surface_id)), 0, max(surface_count - 1, 0));

    vec2 uv = UV;
    vec4 tex = texture(terrain_textures, vec3(uv, float(sid)));
    vec4 tint = get_tint(sid);

    // Простое смешивание текстуры и цвета без шума и вариаций
    ALBEDO = clamp(tex.rgb * tint.rgb, vec3(0.0), vec3(1.0));
    ALPHA = 1.0;
    ROUGHNESS = 1.0; // Значение по умолчанию (матовое)
}
