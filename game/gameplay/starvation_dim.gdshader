shader_type canvas_item;
render_mode unshaded;
// Smooth lower-world falloff leaves the remains readable; no texture or noise.
void fragment(){vec4 shade=COLOR;shade.a*=1.0-.4*smoothstep(.68,.94,UV.y);COLOR=shade;}
