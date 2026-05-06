shader_type spatial;
// Возвращаем освещение и непрозрачный пайплайн
render_mode cull_back, depth_draw_opaque, diffuse_burley, specular_disabled;

uniform vec3 player_pos = vec3(0.0, 0.0, 0.0);
uniform float discard_radius = 250.0;
uniform vec4 proxy_tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);

varying vec3 v_world_pos;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	if (distance(v_world_pos, player_pos) < discard_radius) {
		discard;
	}
	// Сфера теперь всегда непрозрачная. 
	// Чанки просто ложатся поверх неё, потому что они выше на 2 метра.
	ALBEDO = COLOR.rgb * proxy_tint.rgb;
	ALPHA = 1.0;
}
