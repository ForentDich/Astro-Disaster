shader_type spatial;
render_mode cull_back, depth_draw_always, diffuse_burley, specular_disabled;

uniform vec3 player_pos = vec3(0.0, 0.0, 0.0);
uniform float discard_radius = 250.0;

// Текстурный массив и палитра — те же, что в чанках
uniform sampler2DArray terrain_textures : source_color, filter_nearest_mipmap, repeat_enable;
uniform sampler2D tint_lut : source_color, filter_nearest;
uniform int surface_count = 1;

varying vec3 v_world_pos;
varying flat float v_surface_id;

vec4 get_tint(int idx)
{
    float u = (float(idx) + 0.5) / float(max(surface_count, 1));
    return texture(tint_lut, vec2(u, 0.5));
}

void vertex() {
    v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    v_surface_id = UV2.x;
}

void fragment() {
    //if (distance(v_world_pos, player_pos) < discard_radius) {
    //    discard;
    //}

    // 1. Сначала считаем sid (ID поверхности)
    int sid = clamp(int(round(v_surface_id)), 0, max(surface_count - 1, 0));
    vec4 tint = get_tint(sid);

    // 2. Теперь настраиваем трипланарное смешивание
    vec3 blending = abs(NORMAL);
    blending = pow(blending, vec3(8.0)); // Резкий переход, чтобы не было "наложения" сеток
    blending /= (blending.x + blending.y + blending.z);

    float s = 3.0; // Тот же масштаб, что на чанках

    // 3. Теперь sid известен, и ошибки не будет
    vec3 tex_x = texture(terrain_textures, vec3(v_world_pos.zy / s, float(sid))).rgb;
    vec3 tex_y = texture(terrain_textures, vec3(v_world_pos.xz / s, float(sid))).rgb;
    vec3 tex_z = texture(terrain_textures, vec3(v_world_pos.xy / s, float(sid))).rgb;

    vec3 mixed_tex = tex_x * blending.x + tex_y * blending.y + tex_z * blending.z;

    ALBEDO = mixed_tex * tint.rgb;
    ROUGHNESS = 1.0;
}
