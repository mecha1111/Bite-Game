shader_type canvas_item;
render_mode unshaded;
uniform float left_visibility : hint_range(0.0,1.0) = 1.0;
uniform float right_visibility : hint_range(0.0,1.0) = 1.0;
uniform float feather_width : hint_range(0.05,0.4) = 0.18;
uniform float left_darkness : hint_range(0.0,1.0) = 0.90;
uniform float right_darkness : hint_range(0.0,1.0) = 0.96;
uniform float vignette_strength : hint_range(0.0,0.3) = 0.12;
void fragment() {
    vec2 uv=SCREEN_UV;
    // Broad curved feather, never a vertical cut or noisy injury texture.
    float boundary=0.5+0.025*cos(uv.y*6.283185);
    float right=smoothstep(boundary-feather_width,boundary+feather_width,uv.x);
    float lost_left=1.0-left_visibility;
    float lost_right=1.0-right_visibility;
    float darkness=mix(lost_left*left_darkness,lost_right*right_darkness,right);
    float corners=smoothstep(0.25,0.85,abs(uv.y-0.5)*2.0);
    float damaged_edge=mix(lost_left*smoothstep(0.55,1.0,1.0-uv.x),
                           lost_right*smoothstep(0.55,1.0,uv.x),right);
    darkness=1.0-(1.0-darkness)*(1.0-corners*damaged_edge*vignette_strength);
    // Even full blindness retains a very dark navy environment beneath readable senses.
    COLOR=vec4(0.003,0.012,0.025,min(darkness,0.97));
}
