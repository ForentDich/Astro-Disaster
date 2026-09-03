shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, blend_mix, fog_disabled;

const int MAX_PLANETS = 8;

uniform int planet_count = 0;
uniform vec4 planet_center_radius[MAX_PLANETS]; // xyz center, w bottom radius
uniform vec4 planet_top_hr_hm_g[MAX_PLANETS];   // x top radius, y hr, z hm, w mie_g
uniform vec4 planet_rayleigh_intensity[MAX_PLANETS]; // rgb k_rayleigh, a sun_intensity
uniform vec4 planet_mie[MAX_PLANETS]; // rgb k_mie, a unused

uniform sampler2D depth_texture : hint_depth_texture, filter_nearest;
uniform vec3 sun_direction = vec3(0.0, 1.0, 0.0);
uniform float alpha_boost = 2.0;
uniform float space_boost = 3.0;
uniform float rim_power = 4.0;
uniform float rim_min = 0.12;

vec2 ray_sphere_intersect(vec3 ro, vec3 rd, float radius) {
    float b = dot(ro, rd);
    float c = dot(ro, ro) - radius * radius;
    float h = b * b - c;
    if (h < 0.0) return vec2(-1.0, -1.0);
    float s = sqrt(h);
    return vec2(-b - s, -b + s);
}

float get_density(vec3 pos, float scale_height, float bottom_radius) {
    float height = length(pos) - bottom_radius;
    return exp(-height / max(scale_height, 0.001));
}

void vertex() {
    POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

void fragment() {
    vec3 ro = CAMERA_POSITION_WORLD;

    vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, 1.0);
    vec4 view_pos = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
    view_pos.xyz /= max(1e-4, view_pos.w);
    vec3 world_pos = (INV_VIEW_MATRIX * vec4(view_pos.xyz, 1.0)).xyz;
    vec3 rd = normalize(world_pos - ro);

    vec3 sun_dir = normalize(sun_direction);
    vec3 color = vec3(0.0);
    float alpha = 0.0;

    float scene_dist = -1.0;
    float scene_depth = textureLod(depth_texture, SCREEN_UV, 0.0).r;
    if (scene_depth > 0.0001) {
#if CURRENT_RENDERER == RENDERER_COMPATIBILITY
        vec3 ndc_depth = vec3(SCREEN_UV, scene_depth) * 2.0 - 1.0;
#else
        vec3 ndc_depth = vec3(SCREEN_UV * 2.0 - 1.0, scene_depth);
#endif
        vec4 view_depth = INV_PROJECTION_MATRIX * vec4(ndc_depth, 1.0);
        view_depth.xyz /= max(1e-4, view_depth.w);
        vec3 world_depth = (INV_VIEW_MATRIX * vec4(view_depth.xyz, 1.0)).xyz;
        scene_dist = dot(world_depth - ro, rd);
    }

    for (int i = 0; i < MAX_PLANETS; i++) {
        if (i >= planet_count) break;

        vec3 center = planet_center_radius[i].xyz;
        float bottom_radius = planet_center_radius[i].w;
        float top_radius = planet_top_hr_hm_g[i].x;
        float hr = planet_top_hr_hm_g[i].y;
        float hm = planet_top_hr_hm_g[i].z;
        float mie_g = planet_top_hr_hm_g[i].w;

        vec3 k_rayleigh = planet_rayleigh_intensity[i].xyz;
        float sun_intensity = planet_rayleigh_intensity[i].w;
        vec3 k_mie = planet_mie[i].xyz;

        vec3 ro_local = ro - center;
        float view_height = length(ro_local);
        float view_cos = clamp(abs(dot(normalize(ro_local), rd)), 0.0, 1.0);
        float rim = pow(1.0 - view_cos, rim_power);
        vec2 atmo_hit = ray_sphere_intersect(ro_local, rd, top_radius);
        if (atmo_hit.y < 0.0) continue;

        float t_start = max(0.0, atmo_hit.x);
        float t_end = atmo_hit.y;

        vec2 planet_hit = ray_sphere_intersect(ro_local, rd, bottom_radius);
        if (planet_hit.x > 0.0) {
            t_end = min(t_end, planet_hit.x);
        }

        if (scene_dist > 0.0) {
            t_end = min(t_end, scene_dist);
        }

        if (t_start >= t_end) continue;

        const int SAMPLES = 16;
        float step_length = (t_end - t_start) / float(SAMPLES);
        vec3 sample_pos = ro_local + rd * (t_start + step_length * 0.5);

        float optical_depth_r = 0.0;
        float optical_depth_m = 0.0;
        vec3 scatter_rayleigh = vec3(0.0);
        vec3 scatter_mie = vec3(0.0);

        for (int s = 0; s < SAMPLES; s++) {
            float density_r = get_density(sample_pos, hr, bottom_radius) * step_length;
            float density_m = get_density(sample_pos, hm, bottom_radius) * step_length;

            optical_depth_r += density_r;
            optical_depth_m += density_m;

            vec3 transmittance = exp(-(optical_depth_r * k_rayleigh + optical_depth_m * k_mie));

            scatter_rayleigh += density_r * transmittance;
            scatter_mie += density_m * transmittance;

            sample_pos += rd * step_length;
        }

        float cos_theta = dot(rd, sun_dir);
        float phase_r = 3.0 / (16.0 * 3.14159) * (1.0 + cos_theta * cos_theta);

        float g2 = mie_g * mie_g;
        float phase_m = 1.5 * ((1.0 - g2) / (2.0 + g2)) * (1.0 + cos_theta * cos_theta)
            / pow(1.0 + g2 - 2.0 * mie_g * cos_theta, 1.5);

        vec3 final_color = (scatter_rayleigh * k_rayleigh * phase_r + scatter_mie * k_mie * phase_m)
            * sun_intensity;

        float total_density = (optical_depth_r * length(k_rayleigh) + optical_depth_m * length(k_mie));
        float alpha_out = 1.0 - exp(-total_density * 4.0);
        float thickness = clamp((t_end - t_start) / max(top_radius - bottom_radius, 0.001), 0.0, 1.0);
        alpha_out *= thickness;
        alpha_out *= mix(rim_min, 1.0, rim);
        float distance_boost = smoothstep(top_radius, top_radius * 4.0, view_height);
        alpha_out *= alpha_boost * mix(1.0, space_boost, distance_boost);

        final_color = vec3(1.0) - exp(-final_color * 1.5);

        color += final_color;
        alpha += alpha_out;
    }

    float final_alpha = clamp(alpha, 0.0, 1.0);

    ALBEDO = color;
    ALPHA = final_alpha;
}
