shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix;

uniform vec4 line_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float line_thickness : hint_range(0.0, 0.5) = 0.05;

void fragment() {
    vec2 uv = abs(UV - 0.5);
    float edge = max(uv.x, uv.y);

    if (edge > (0.5 - line_thickness)) {
        ALBEDO = line_color.rgb;
        ALPHA = line_color.a;
    } else {
        discard;
    }
}
