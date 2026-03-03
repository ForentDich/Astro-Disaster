shader_type sky;

// ── Sun ──
uniform vec3 sun_direction = vec3(0.3, 0.4, -0.7);
uniform vec3 sun_color : source_color = vec3(1.0, 0.95, 0.85);
uniform float sun_angular_radius : hint_range(0.002, 0.05) = 0.015; // radians, ~0.86°
uniform float sun_glow : hint_range(0.0, 1.0) = 0.3;

// ── Sky gradient ──
uniform vec3 sky_top : source_color = vec3(0.18, 0.34, 0.78);
uniform vec3 sky_horizon : source_color = vec3(0.55, 0.75, 0.95);
uniform vec3 ground_color : source_color = vec3(0.25, 0.22, 0.2);

// ── Atmosphere ──
uniform float horizon_sharpness : hint_range(1.0, 20.0) = 4.0;
uniform vec3 sunset_color : source_color = vec3(1.0, 0.45, 0.15);
uniform float sunset_strength : hint_range(0.0, 2.0) = 1.0;

void sky() {
    vec3 dir = EYEDIR;
    vec3 sun_dir = normalize(sun_direction);

    // ── Sky gradient ──
    float y = dir.y;
    // Upper hemisphere: blend top → horizon
    float horizon_mix = pow(1.0 - max(y, 0.0), horizon_sharpness);
    vec3 sky = mix(sky_top, sky_horizon, horizon_mix);

    // Lower hemisphere: blend to ground
    if (y < 0.0) {
        float ground_mix = clamp(-y * 10.0, 0.0, 1.0);
        sky = mix(sky_horizon, ground_color, ground_mix);
    }

    // ── Sunset/sunrise glow ──
    float sun_height = sun_dir.y;
    // Sunset is active when sun is near horizon (y ≈ 0)
    float sunset_factor = smoothstep(0.3, 0.0, abs(sun_height)) * sunset_strength;
    // Glow concentrated near horizon and toward sun direction
    vec3 sun_flat = normalize(vec3(sun_dir.x, 0.0, sun_dir.z));
    vec3 dir_flat = normalize(vec3(dir.x, 0.0, dir.z));
    float sun_horizon_dot = max(dot(dir_flat, sun_flat), 0.0);
    float horizon_glow = pow(1.0 - abs(y), 4.0);
    sky = mix(sky, sunset_color, sunset_factor * pow(sun_horizon_dot, 3.0) * horizon_glow);

    // ── Sun disc (angular distance — uniform size everywhere on screen) ──
    float sun_dot = dot(dir, sun_dir);
    float sun_angle = acos(clamp(sun_dot, -1.0, 1.0));
    float disc = 1.0 - smoothstep(sun_angular_radius * 0.85, sun_angular_radius, sun_angle);
    sky += sun_color * disc * 2.0;

    // ── Sun glow (halo) ──
    float glow = pow(max(sun_dot, 0.0), 128.0) * sun_glow;
    sky += sun_color * glow;

    // ── Atmospheric scattering (sky brightens near horizon) ──
    float scatter = pow(horizon_mix, 2.0) * 0.15;
    sky += sun_color * scatter * max(sun_height, 0.0);

    COLOR = sky;
}
