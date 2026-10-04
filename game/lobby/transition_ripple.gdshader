shader_type canvas_item;
uniform float progress : hint_range(0.0, 1.0) = 1.0;
uniform float direction = 1.0;
uniform float strength : hint_range(0.0, 0.25) = 0.12;
void fragment() {
    vec2 uv = floor(UV * vec2(280.0, 158.0)) / vec2(280.0, 158.0);
    float x = direction > 0.0 ? uv.x : 1.0 - uv.x;
    float front = 1.1 - progress * 1.2;
    float arc = front + (uv.y - 0.5) * (uv.y - 0.5) * 0.25;
    float stripe = step(abs(x - arc), 0.016);
    COLOR = vec4(0.60, 0.96, 1.0, stripe * sin(progress * 3.14159) * strength);
}
