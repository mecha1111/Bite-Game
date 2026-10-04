shader_type canvas_item;
render_mode unshaded;
uniform sampler2D interior_mask : filter_nearest;
uniform vec2 body_x_range=vec2(0.171875,0.86962890625);
uniform float fill_ratio : hint_range(0.0,1.0)=0.5;
uniform float overfill_strength : hint_range(0.0,1.0)=0.0;
uniform float time_seconds=0.0;
uniform float recovery_pulse=0.0;
uniform float recovery_phase=1.0;
uniform float loss_pulse=0.0;
uniform float low_strength=0.0;
uniform float critical_strength=0.0;
varying vec4 node_tint;
void vertex(){node_tint=COLOR;}
void fragment(){
    vec4 art=texture(TEXTURE,UV);
    float mask=texture(interior_mask,UV).a;
    float x=clamp((UV.x-body_x_range.x)/(body_x_range.y-body_x_range.x),0.0,1.0);
    float filled=fill_ratio>=1.0?1.0:1.0-step(fill_ratio,x);
    vec3 tint=mix(vec3(.30,.79,.87),vec3(.43,.61,.56),low_strength);
    float ripple=sin(x*32.0+UV.y*18.0-time_seconds*1.1)*.022;
    float shimmer=pow(.5+.5*sin(x*20.0-time_seconds*.7),4.0)*.035;
    tint*=.97+ripple+shimmer;
    tint*=1.0-critical_strength*(.055+.055*sin(time_seconds*1.4));
    tint=mix(tint,vec3(.78,1.0,1.0),overfill_strength*.30+recovery_pulse*.25);
    float sweep=1.0-smoothstep(.0,.07,abs(x-recovery_phase));
    tint+=vec3(.12,.18,.18)*sweep*recovery_pulse;
    tint=mix(tint,vec3(.48,.48,.36),loss_pulse*.15);
    vec3 interior=mix(vec3(.018,.052,.081),tint,filled);
    COLOR=vec4(mix(interior,art.rgb,art.a),max(art.a,mask))*node_tint;
}
