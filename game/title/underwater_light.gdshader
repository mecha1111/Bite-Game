shader_type canvas_item;
// Background-only, nearest pixel-snapped water optics. Menu remains unaffected.
uniform float strength : hint_range(0.0, 0.08, 0.001) = 0.028;
uniform float speed : hint_range(0.05, 0.6, 0.01) = 0.22;
uniform float ray_opacity : hint_range(0.0, 0.15, 0.005) = 0.075;
uniform float ray_speed : hint_range(0.01, 0.3, 0.01) = 0.09;
uniform float ray_drift : hint_range(0.0, 0.08, 0.001) = 0.03;
uniform float ray_width_scale : hint_range(0.5, 2.0, 0.05) = 1.0;
uniform float caustic_strength : hint_range(0.0, 0.15, 0.001) = 0.014;
uniform float caustic_speed : hint_range(0.02, 0.5, 0.01) = 0.16;

float shaft(vec2 uv, float center, float width, float phase) {
    float drift = sin(TIME * ray_speed + phase) * ray_drift;
    float distance_to_ray = abs(uv.x - center - uv.y * 0.15 - drift);
    float profile = 1.0 - smoothstep(width * 0.2, width, distance_to_ray);
    return profile * (0.85 + 0.15 * sin(TIME * ray_speed * 0.7 + phase));
}
uniform float brightness : hint_range(0.5,1.0) = 1.0;
uniform float saturation : hint_range(0.5,1.0) = 1.0;
uniform float highlight_softening : hint_range(0.0,0.2) = 0.0;
uniform float ui_focus_strength : hint_range(0.0,0.3) = 0.0;
uniform float vignette_strength : hint_range(0.0,0.3) = 0.1;
uniform float lower_depth_strength : hint_range(0.0,0.2) = 0.06;
uniform vec3 depth_tint = vec3(1.0);
uniform float refraction_strength = 0.0007;
uniform float refraction_speed = 0.08;
varying vec4 vertex_tint;
void vertex(){vertex_tint=COLOR;}
void fragment() {
    vec2 drift=vec2(sin(UV.y*4.0+TIME*refraction_speed),sin(UV.x*3.0-TIME*refraction_speed*.8))*refraction_strength;
    vec2 sample_uv=(floor((UV+drift)/TEXTURE_PIXEL_SIZE)+.5)*TEXTURE_PIXEL_SIZE;
    COLOR=texture(TEXTURE,clamp(sample_uv,TEXTURE_PIXEL_SIZE*.5,vec2(1.0)-TEXTURE_PIXEL_SIZE*.5))*vertex_tint;
    float luma = dot(COLOR.rgb,vec3(0.2126,0.7152,0.0722));
    COLOR.rgb = mix(vec3(luma),COLOR.rgb,saturation);
    COLOR.rgb *= brightness * depth_tint;
    COLOR.rgb -= highlight_softening * pow(COLOR.rgb,vec3(3.0));
    vec2 focus_uv=(UV-vec2(0.5,0.56))/vec2(0.23,0.46);
    COLOR.rgb *= 1.0-ui_focus_strength*exp(-dot(focus_uv,focus_uv)*1.5);
    vec2 edge=(UV-0.5)*2.0;
    COLOR.rgb *= 1.0-vignette_strength*clamp(dot(edge,edge)*0.5,0.0,1.0)-lower_depth_strength*UV.y*UV.y;
    float light = sin(TIME * speed + UV.x * 3.0 + UV.y * 1.4);
    COLOR.rgb *= vec3(1.0 + light * strength * 0.65,
                      1.0 + light * strength * 0.85,
                      1.0 + light * strength);
    float rays = shaft(UV, 0.13, 0.065 * ray_width_scale, 0.0)
               + shaft(UV, 0.41, 0.095 * ray_width_scale, 2.1)
               + shaft(UV, 0.71, 0.050 * ray_width_scale, 4.3);
    float surface_fade = pow(1.0 - UV.y, 2.0);
    COLOR.rgb += vec3(0.55, 0.9, 1.0) * rays * surface_fade * ray_opacity;
    // Slow broad luminance modulation replaces identifiable dotted caustics.
    float water_light=.5*(sin(UV.x*3.0+UV.y*1.8+TIME*caustic_speed)
                         +sin(UV.y*2.5-TIME*caustic_speed*.73));
    COLOR.rgb*=1.0+water_light*caustic_strength;
}
