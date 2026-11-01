shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform vec4 base_color : source_color = vec4(0.03, 0.06, 0.16, 1.0);
uniform vec4 crest_color : source_color = vec4(0.0, 0.9, 1.0, 1.0);
uniform vec4 lip_energy_color : source_color = vec4(1.0, 0.15, 0.75, 1.0);
uniform vec4 grid_color : source_color = vec4(0.25, 0.65, 1.0, 0.5);
uniform float grid_divisions_x = 16.0;
uniform float grid_divisions_y = 30.0;
uniform float wave_scroll_speed = 0.6;
uniform float pulse_speed = 3.0;

void fragment() {
    // Scrolling UV along energy wave
    vec2 scrolled_uv = UV;
    scrolled_uv.y -= TIME * wave_scroll_speed;

    // Grid lines
    vec2 grid_fract = abs(fract(scrolled_uv * vec2(grid_divisions_x, grid_divisions_y)) - 0.5);
    vec2 grid_line = smoothstep(0.42, 0.48, grid_fract);
    float is_grid = max(grid_line.x, grid_line.y);

    // Height gradient (UV.x: 0 = trough, 1 = lip)
    float height_factor = smoothstep(0.2, 0.95, UV.x);
    float lip_factor = smoothstep(0.85, 1.0, UV.x);

    // Dynamic pulsating neon energy wave
    float pulse = 0.5 + 0.5 * sin(scrolled_uv.y * 12.0 + TIME * pulse_speed + UV.x * 6.0);

    // Base color blend
    vec3 col = mix(base_color.rgb, crest_color.rgb, height_factor * 0.7);
    col = mix(col, lip_energy_color.rgb, lip_factor * (0.8 + 0.2 * pulse));

    // Glowing grid lines
    col += grid_color.rgb * is_grid * (1.2 + height_factor);

    // Fresnel rim light
    float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
    col += crest_color.rgb * fresnel * 0.8;

    ALBEDO = col * 0.5;
    EMISSION = col * (0.8 + 0.5 * lip_factor + 0.3 * pulse);
    ROUGHNESS = 0.3;
    METALLIC = 0.2;
}
