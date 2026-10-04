shader_type canvas_item;
uniform float bound_radius = 140.0;
uniform float radius = 0.0;
uniform float thickness = 4.0;
uniform float pixel_size = 2.0;
uniform float core_flash=0.0;
uniform float opacity = 0.9;
uniform float current_strength=0.0;
uniform vec3 ring_color : source_color = vec3(0.82,0.98,1.0);
uniform vec3 edge_color : source_color = vec3(0.01,0.10,0.22);
varying vec4 node_tint;
void vertex(){node_tint=COLOR;}
void fragment(){
    // A radial distance field quantized to a fine pixel grid, not polygon/Line2D segments.
    vec2 p=(UV-vec2(0.5))*2.0*bound_radius;
    p=(floor(p/pixel_size)+vec2(0.5))*pixel_size;
    // Smooth current curvature, no random jitter and no clock/target displacement.
    p.y+=current_strength*.22*p.x*p.x/max(1.0,bound_radius);
    float turn=current_strength*.22*(1.0-clamp(length(p)/max(1.0,bound_radius),0.0,1.0));
    p=mat2(vec2(cos(turn),sin(turn)),vec2(-sin(turn),cos(turn)))*p;
    float d=length(p);
    float distance_to_ring=abs(d-radius);
    float ring=1.0-step(thickness*0.5,distance_to_ring);
    float edge=1.0-step(thickness*0.5+pixel_size,distance_to_ring);
    float core=(1.0-step(3.0,d))*core_flash;
    COLOR=vec4(mix(edge_color,ring_color,max(ring,core)),max(edge*opacity,core))*node_tint;
}
