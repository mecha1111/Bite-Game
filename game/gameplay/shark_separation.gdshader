shader_type canvas_item;
uniform float rim_strength : hint_range(0.0,0.4) = 0.18;
uniform float shadow_strength : hint_range(0.0,0.5) = 0.24;
varying vec4 sprite_modulate;
void vertex() {sprite_modulate=COLOR;}
void fragment() {
    vec4 art=texture(TEXTURE,UV);
    float rim=texture(TEXTURE,UV+vec2(0.0,TEXTURE_PIXEL_SIZE.y)).a;
    float shadow=texture(TEXTURE,UV-vec2(0.0,TEXTURE_PIXEL_SIZE.y*2.0)).a;
    float edge=max(rim*rim_strength,shadow*shadow_strength)*(1.0-art.a);
    vec3 backing=rim>shadow?vec3(0.45,0.87,1.0):vec3(0.0,0.06,0.17);
    COLOR=vec4(mix(backing,art.rgb,art.a),max(art.a,edge))*sprite_modulate;
}
