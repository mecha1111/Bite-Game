shader_type canvas_item;
uniform float phase=0.0;
uniform float strength=1.0;
uniform vec3 catch_tint : source_color=vec3(.85,1.0,.65);
void fragment(){
    vec2 p=floor((UV-.5)*128.0/2.0)*2.0+1.0;
    float radius=48.0*pow(1.0-phase,2.0)+2.0;
    float ring=1.0-step(2.0,abs(length(p)-radius));
    float core=(1.0-step(5.0,length(p)))*smoothstep(.65,.85,phase);
    COLOR=vec4(catch_tint,max(ring,core)*strength*(1.0-phase));
}
