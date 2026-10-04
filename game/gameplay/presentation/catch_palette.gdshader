shader_type canvas_item;
render_mode unshaded;
varying vec4 tint;
void vertex(){tint=COLOR;}
void fragment(){vec4 source=texture(TEXTURE,UV);COLOR=vec4(mix(tint.rgb*.7,tint.rgb,dot(source.rgb,vec3(.333))),source.a*tint.a);}
