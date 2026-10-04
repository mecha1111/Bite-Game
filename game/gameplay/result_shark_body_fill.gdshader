shader_type canvas_item;
uniform sampler2D interior_mask : filter_nearest;
uniform float fill_ratio : hint_range(0,1) = 0.88;
uniform float overfill_strength : hint_range(0,1) = 0.0;
uniform vec3 fill_tint : source_color = vec3(0.32,0.86,0.94);
uniform vec3 empty_tint : source_color = vec3(0.025,0.09,0.15);
uniform vec2 body_x_range = vec2(0.16816,0.76029);
varying vec4 node_tint;
void vertex(){node_tint=COLOR;}
void fragment(){
    vec4 source=texture(TEXTURE,UV);
    float mask=texture(interior_mask,UV).a;
    float progress=clamp((UV.x-body_x_range.x)/(body_x_range.y-body_x_range.x),0.0,1.0);
    float filled=1.0-step(fill_ratio,progress);
    vec3 full=mix(fill_tint,vec3(0.76,1.0,1.0),overfill_strength*0.28);
    vec3 interior=mix(empty_tint,full,filled);
    // Preserve the provided frame/eye/fins; only its enclosed transparent body is filled.
    COLOR=vec4(mix(interior,source.rgb,source.a),max(source.a,mask))*node_tint;
}
