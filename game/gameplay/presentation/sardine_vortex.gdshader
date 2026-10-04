shader_type canvas_item;
uniform float strength=0.0;
uniform float age=0.0;
void fragment(){
    vec2 p=(UV-.5)*vec2(2.0,2.5);
    float r=length(p),a=atan(p.y,p.x);
    float spiral=pow(max(0.0,cos(a*3.0+r*18.0-age*2.4)),18.0);
    float envelope=smoothstep(.05,.18,r)*(1.0-smoothstep(.6,1.0,r));
    COLOR=vec4(.22,.66,.78,step(.35,spiral)*step(.2,envelope)*strength);
}
