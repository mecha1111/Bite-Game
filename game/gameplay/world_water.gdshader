shader_type canvas_item;
render_mode unshaded;
uniform sampler2D world_screen : hint_screen_texture, repeat_disable, filter_nearest;
uniform float time_seconds = 0.0;
uniform vec3 world_tint : source_color = vec3(0.796,0.937,1.0);
uniform float shark_tint_strength : hint_range(0.0,0.6) = 0.24;
uniform float white_reduction : hint_range(0.0,0.1) = 0.035;
uniform float caustic_strength : hint_range(0.0,0.2) = 0.01;
uniform float ray_opacity : hint_range(0.0,0.08) = 0.025;
uniform float vignette_strength : hint_range(0.0,0.4) = 0.12;
uniform float failure_strength : hint_range(0.0,1.0) = 0.0;
uniform float refraction_strength = 0.0011;
uniform float refraction_speed = 0.18;
uniform float caustic_speed = 0.3;
uniform float caustic_scale = 1.0;
uniform float fog_strength = 0.045;
uniform float saturation = 0.92;
uniform vec2 presentation_impulse_pixels=vec2(0.0);
uniform float transition_pressure=0.0;
uniform float pressure_strength=0.0;
uniform float presentation_dim=0.0;
uniform float presentation_dull=0.0;
uniform float presentation_strength=0.0;
uniform vec3 presentation_tint : source_color=vec3(.45,.95,1.0);
uniform float stage_camera_zoom=1.0;
uniform vec2 stage_camera_offset=vec2(0.0);
uniform vec2 stage_camera_pivot=vec2(.5,.65);
uniform float preserve_black=0.0;
uniform vec3 stage_color=vec3(0.0);
uniform float stage_dim=0.0;
uniform float stage_vignette=0.0;
uniform float stage_caustic=0.0;
uniform float stage_light=0.0;
void fragment() {
    vec2 drift=vec2(sin(SCREEN_UV.y*4.0+time_seconds*refraction_speed)+.2*sin(SCREEN_UV.y*3.0-time_seconds*refraction_speed*.7),
        sin(SCREEN_UV.x*3.0-time_seconds*refraction_speed*.8));
    vec2 sampled=(SCREEN_UV-stage_camera_pivot)/stage_camera_zoom+stage_camera_pivot+stage_camera_offset*SCREEN_PIXEL_SIZE;
    sampled+=presentation_impulse_pixels*SCREEN_PIXEL_SIZE+drift*(refraction_strength+pressure_strength+transition_pressure);
    sampled=(floor(sampled/SCREEN_PIXEL_SIZE)+.5)*SCREEN_PIXEL_SIZE;
    vec3 world=texture(world_screen,clamp(sampled,SCREEN_PIXEL_SIZE*.5,vec2(1.0)-SCREEN_PIXEL_SIZE*.5)).rgb;
    // Smooth the already-rendered environment edge for camera overscan. Never
    // stretch a single bright wave pixel into a line, or bypass the EX shader blend.
    if(any(lessThan(sampled,SCREEN_PIXEL_SIZE*.5))||any(greaterThan(sampled,vec2(1.0)-SCREEN_PIXEL_SIZE*.5))) {
        vec2 edge_uv=clamp(sampled,SCREEN_PIXEL_SIZE*.5,vec2(1.0)-SCREEN_PIXEL_SIZE*.5);
        vec3 edge_world=vec3(1.0);
        for(int tap=-3;tap<=3;tap++) {
            vec2 edge_tap=clamp(edge_uv+vec2(0.0,float(tap)*16.0)*SCREEN_PIXEL_SIZE,SCREEN_PIXEL_SIZE*.5,vec2(1.0)-SCREEN_PIXEL_SIZE*.5);
            edge_world=min(edge_world,texture(world_screen,edge_tap).rgb);
        }
        world=edge_world;
    }
    vec2 uv=floor(SCREEN_UV/SCREEN_PIXEL_SIZE/2.0)*SCREEN_PIXEL_SIZE*2.0;
    float luma=dot(world,vec3(.2126,.7152,.0722));
    // Whole-pixel refraction keeps the screen nearest-sampled; bright waves retain contrast.
    float protection=smoothstep(.72,.92,luma);
    float integration=shark_tint_strength*(1.0-protection*.7);
    world=mix(vec3(luma),world,saturation);
    world*=mix(vec3(1.0),world_tint,integration);
    float depth=clamp(.25+uv.y*.4+length(uv-.5)*.5,0.0,1.0);
    world=mix(world,vec3(.06,.19,.26)*world_tint,fog_strength*depth*(1.0-protection*.8));
    world.r*=1.0-fog_strength*depth;
    world*=1.0-white_reduction*smoothstep(.6,1.0,luma);
    // Broad signed light breathing; no absolute-value spot peaks or high-frequency cells.
    float caustic=.5*(sin((uv.x*3.0+uv.y*1.8)*caustic_scale+time_seconds*caustic_speed)
                     +sin(uv.y*2.5-time_seconds*caustic_speed*.73));
    world*=1.0+caustic*(caustic_strength+stage_caustic)*(1.0-protection*.65);
    float bands=.5+.5*sin((uv.x-uv.y*.2)*4.0-time_seconds*.07);
    world+=vec3(.3,.65,.8)*bands*bands*pow(1.0-uv.y,2.0)*(ray_opacity+stage_light);
    vec2 edge=(uv-.5)*2.0;
    world*=1.0-(vignette_strength+stage_vignette)*.25*clamp(dot(edge,edge)*.5,0.0,1.0);
    float gray=dot(world,vec3(.2126,.7152,.0722));
    world=mix(world,vec3(gray)*vec3(.65,.8,1.0),failure_strength);
    world=mix(world,vec3(gray)*vec3(.75,.85,1.0),presentation_dull*(1.0-protection*.8));
    world*=1.0-(presentation_dim+stage_dim)*(1.0-protection*.8);
    world+=presentation_tint*presentation_strength*(1.0-protection*.8);
    world+=stage_color*(1.0-protection*.85);
    world*=mix(1.0,smoothstep(.001,.035,luma),preserve_black);
    COLOR=vec4(world,1.0);
}
