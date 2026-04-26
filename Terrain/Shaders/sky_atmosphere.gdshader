shader_type sky;

uniform vec3 sun_direction = vec3(0.3, 0.4, -0.7);
uniform vec3 sun_color : source_color = vec3(1.0, 0.95, 0.85);
uniform float sun_angular_radius : hint_range(0.002, 0.05) = 0.01;
uniform float sun_glow : hint_range(0.0, 1.0) = 0.1;

// Настройки звезд
uniform float star_density : hint_range(100.0, 2000.0) = 800.0; 
uniform float star_threshold : hint_range(0.0, 1.0) = 0.995; 

float hash(vec3 p) {
    p = fract(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
}

void sky() {
    vec3 dir = EYEDIR;
    vec3 sun_dir = normalize(sun_direction);
    
    vec3 color = vec3(0.0);

    // Расчет звезд через простую математику без range_lerp
    vec3 star_grid = dir * star_density;
    vec3 grid_id = floor(star_grid);
    float n = hash(grid_id);
    
    if (n > star_threshold) {
        // Вычисляем яркость: чем ближе n к 1.0, тем ярче звезда
        float star_brightness = (n - star_threshold) / (1.0 - star_threshold);
        color += vec3(pow(star_brightness, 5.0)); 
    }

    // Солнце
    float sun_dot = dot(dir, sun_dir);
    float sun_angle = acos(clamp(sun_dot, -1.0, 1.0));
    float disc = 1.0 - smoothstep(sun_angular_radius * 0.9, sun_angular_radius, sun_angle);
    color += sun_color * disc * 5.0;

    // Свечение солнца
    float glow = pow(max(sun_dot, 0.0), 1024.0) * sun_glow;
    color += sun_color * glow;

    COLOR = color;
}
