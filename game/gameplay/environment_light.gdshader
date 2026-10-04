shader_type canvas_item;
uniform float time_seconds = 0.0;
// Extended camera framing keeps the original artwork mapping inside its normal viewport.
uniform float camera_art_extent=1.0;
uniform float vent_strength : hint_range(0.0,0.08) = 0.0;
// One background pass. Authored current adds only a tiny nearest-sampled parallax.
uniform float current_pixels=0.0;
varying vec4 node_tint;
void vertex(){node_tint=COLOR;}
uniform float strength : hint_range(0.0, 0.08, 0.001) = 0.028;
uniform float speed : hint_range(0.05, 0.6, 0.01) = 0.22;
uniform float ray_opacity : hint_range(0.0, 0.15, 0.005) = 0.075;
uniform float ray_speed : hint_range(0.01, 0.3, 0.01) = 0.09;
uniform float ray_drift : hint_range(0.0, 0.08, 0.001) = 0.03;
uniform float ray_width_scale : hint_range(0.5, 2.0, 0.05) = 1.0;
uniform float caustic_strength : hint_range(0.0, 0.05, 0.001) = 0.014;
uniform float caustic_speed : hint_range(0.02, 0.5, 0.01) = 0.16;

float shaft(vec2 uv, float center, float width, float phase) {
    float drift = sin(time_seconds * ray_speed + phase) * ray_drift;
    float distance_to_ray = abs(uv.x - center - uv.y * 0.15 - drift);
    float profile = 1.0 - smoothstep(width * 0.2, width, distance_to_ray);
    return profile * (0.85 + 0.15 * sin(time_seconds * ray_speed * 0.7 + phase));
}
uniform float failure_strength : hint_range(0.0,1.0) = 0.0;
uniform vec3 ambient_tint = vec3(0.8,0.94,1.0);
uniform float vignette_strength : hint_range(0.0,0.4) = 0.12;
void fragment() {
    vec2 extended=(UV-vec2(.5))*camera_art_extent+vec2(.5);
    vec2 uv=vec2(1.0)-abs(mod(extended,vec2(2.0))-vec2(1.0));
    COLOR=texture(TEXTURE,clamp(uv+vec2(round(current_pixels*.5),0.0)*TEXTURE_PIXEL_SIZE,vec2(0.0),vec2(1.0)))*node_tint;
    float gray=dot(COLOR.rgb,vec3(0.2126,0.7152,0.0722));
    COLOR.rgb=mix(COLOR.rgb,vec3(gray)*vec3(0.65,0.8,1.0),failure_strength);
    COLOR.rgb *= ambient_tint;
    vec2 edge = (uv-0.5)*2.0;
    COLOR.rgb *= 1.0-vignette_strength*clamp(dot(edge,edge)*0.5,0.0,1.0);
    float light = sin(time_seconds * speed + uv.x * 3.0 + uv.y * 1.4);
    COLOR.rgb *= vec3(1.0 + light * strength * 0.65,
                      1.0 + light * strength * 0.85,
                      1.0 + light * strength);
    float rays = shaft(uv, 0.13, 0.065 * ray_width_scale, 0.0)
               + shaft(uv, 0.41, 0.095 * ray_width_scale, 2.1)
               + shaft(uv, 0.71, 0.050 * ray_width_scale, 4.3);
    float surface_fade = pow(1.0 - uv.y, 2.0);
    COLOR.rgb += vec3(0.55, 0.9, 1.0) * rays * surface_fade * ray_opacity;
    // Coarse lighting cells keep the original pixel-art edges intact.
    vec2 cells = floor(uv * vec2(418.0, 235.0)) / vec2(418.0, 235.0);
    float pattern = sin(cells.x * 37.0 + cells.y * 11.0 + time_seconds * caustic_speed)
                  * sin(cells.y * 29.0 - cells.x * 7.0 - time_seconds * caustic_speed * 0.73);
    float caustic = pow(abs(pattern), 6.0) - 0.18;
    COLOR.rgb += vec3(0.4, 0.8, 1.0) * caustic * caustic_strength;
    COLOR.rgb += vec3(1.0,0.38,0.1)*vent_strength*pow(uv.y,4.0)*(0.8+0.2*sin(time_seconds*.4));
}
