shader_type canvas_item;
uniform float intensity=0.0;
uniform float phase=0.0;
uniform bool from_right=false;
void fragment(){
    float x=from_right?1.0-UV.x:UV.x;
    float band=1.0-smoothstep(.08,.16,abs(x-(.05+phase*.65)));
    float height=pow(max(0.0,1.0-abs(UV.y-.5)*2.0),2.0);
    COLOR=vec4(.55,.94,1.0,intensity*band*height*(1.0-x));
}
