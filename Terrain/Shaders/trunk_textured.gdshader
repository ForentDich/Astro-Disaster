shader_type spatial;
render_mode cull_back;

uniform sampler2D bark_texture : source_color, filter_nearest_mipmap, repeat_enable, hint_default_white;
uniform vec4 bark_color : source_color = vec4(0.55, 0.35, 0.17, 1.0);
uniform float bark_tile_world_size : hint_range(0.1, 8.0) = 1.25;
uniform float reflectivity : hint_range(0.0, 1.0) = 0.03;

varying vec3 world_pos;
varying vec3 world_nrm;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_nrm = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

vec3 sample_triplanar(vec3 p, vec3 n) {
	const float triplanar_sharpness = 10.0;
	vec3 an = abs(n);
	vec3 w = pow(an, vec3(triplanar_sharpness));
	w /= max(w.x + w.y + w.z, 1e-5);

	float inv_size = 1.0 / max(bark_tile_world_size, 1e-4);
	vec2 uv_x = p.zy * inv_size;
	vec2 uv_y = p.xz * inv_size;
	vec2 uv_z = p.xy * inv_size;

	vec3 tx = texture(bark_texture, uv_x).rgb;
	vec3 ty = texture(bark_texture, uv_y).rgb;
	vec3 tz = texture(bark_texture, uv_z).rgb;

	return tx * w.x + ty * w.y + tz * w.z;
}

void fragment() {
	vec3 tex = sample_triplanar(world_pos, world_nrm);
	float bark_mask = dot(tex, vec3(0.299, 0.587, 0.114));
	vec3 color = bark_color.rgb * bark_mask;
	
	ALBEDO = color;
	ROUGHNESS = 0.8;
	METALLIC = 0.0;
	SPECULAR = reflectivity;
}
