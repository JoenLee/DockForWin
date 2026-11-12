// Compact lens/expanded-radius gradient and chromatic split adapted from
// iishen717/GlassDesk, commit 737b8dc1737e029b292c4348f7d2254b290da971,
// src/GlassDesk.UI/Effects/TransitionGlass.hlsl and AndroidLiquidGlassRenderer.cs.
// Original lens: Kyant0/AndroidLiquidGlass, Copyright 2025 Kyant, Apache-2.0.
// See docs/third-party/glassdesk/NOTICE.md for provenance and local changes.
// The existing outline, lighting pass, Gaussian post-filter and live capture
// remain owned by DockForWindows. This pass changes background optics only.
sampler2D Background : register(s0);
sampler2D ClearBackground : register(s1);
float2 SourceSize : register(c0);
float2 PlateOrigin : register(c1);
float2 PlateSize : register(c2);
float2 Shape : register(c3); // radius percent, refraction strength
float2 Material : register(c4); // physical pixels per DIP, soft frosting
float2 Texture : register(c5);
float2 WorldOrigin : register(c6);
float2 Lighting : register(c7); // strength, narrow feather width in DIP
float2 Sides : register(c8);
#ifdef CONTROL_CENTER_LIGHTING
static const float LightingOnly = 1;
#else
float LightingOnly : register(c9);
#endif
float2 Tint : register(c10);
float2 ControlProfile : register(c11); // settings controls: padded input flag, outline exponent

float4 main(float2 uv : TEXCOORD) : COLOR
{
    if (ControlProfile.x > .5) uv = (PlateOrigin + uv * PlateSize) / SourceSize;
    float2 pixel = uv * SourceSize;
    float2 p = pixel - PlateOrigin - PlateSize * .5;
    float fraction = saturate(Shape.x / 100);
    float n = 3 - pow(fraction, 8);
    if (ControlProfile.y >= 2) n = ControlProfile.y;
    else if (ControlProfile.x > .5) n = 3;
    float radius = min(PlateSize.x, PlateSize.y) * .5 * fraction;
    float2 q = abs(p) - (PlateSize * .5 - radius);
    float2 normal;
    float distance;
    if (q.x > 0 && q.y > 0)
    {
        float norm = pow(pow(q.x, n) + pow(q.y, n), 1 / n);
        normal = pow(max(q / max(norm, .001), .0001), n - 1);
        float len = max(length(normal), .001);
        normal /= len;
        distance = (norm - radius) / len;
    }
    else if (q.x > q.y) { normal = float2(1, 0); distance = q.x - radius; }
    else { normal = float2(0, 1); distance = q.y - radius; }
    if (ControlProfile.y == 1.5 && q.x > 0 && q.y > 0)
    {
        float a=.055,s=tan(a),r=radius*s/(3*(1-cos(a))+s*(1-sin(a)));
        float join=radius-r+r*sin(a),b=r*(1-cos(a)),k=b/max(join*join*join,.00001);
        if(min(q.x,q.y)<join){float t=min(q.x,q.y),slope=3*k*t*t;
            float2 g=q.x<q.y?float2(slope,1):float2(1,slope);float len=length(g);
            normal=g/len;distance=(max(q.x,q.y)-radius+k*t*t*t)/len;
        }else{float2 g=q-(radius-r);normal=normalize(g);distance=length(g)-r;}
    }
    normal *= sign(p);
    if (LightingOnly > .5)
    {
        float depth = max(0, -distance);
        float width = max(.5, Lighting.y) * Material.x;
        float feather = smoothstep(0, width * .25, depth) * (1 - smoothstep(width * .25, width, depth));
        float core = smoothstep(0, width * .18, depth) * (1 - smoothstep(width * .18, width * .42, depth));
        #ifndef CONTROL_CENTER_LIGHTING
        float sideWidth = 1.4 * Material.x;
        float sideFeather = smoothstep(0, sideWidth * .25, depth) * (1 - smoothstep(sideWidth * .25, sideWidth, depth));
        #endif
        float white = ((normal.y < 0 ? .16 : .13) * feather + (normal.y < 0 ? .20 : .18) * core) * Lighting.x * abs(normal.y);
        float underLip = smoothstep(.8 * Material.x, 1.4 * Material.x, depth) * (1 - smoothstep(1.4 * Material.x, 2.8 * Material.x, depth));
        white += .09 * Lighting.x * abs(normal.x) * underLip;
        #ifdef CONTROL_CENTER_LIGHTING
        float black = 0;
        #else
        float black = .34 * Sides.x * abs(normal.x) * sideFeather;
        #endif
        float opacity = saturate(white + black + Tint.x * (1 - white - black));
        return float4(white, white, white, opacity);
    }
    // GlassDesk's compact lens uses an expanded-radius gradient so the bend
    // turns smoothly around corners. Keep the existing exact outline distance
    // above (continuous exponent 3 -> 2) rather than importing its 2.6 outline.
    float expandedRadius = min(radius * 1.5, min(PlateSize.x, PlateSize.y) * .5);
    float2 lensQ = abs(p) - PlateSize * .5 + expandedRadius;
    float2 gradient = max(lensQ, 0);
    float gradientLength = length(gradient);
    float2 straight = lensQ.x >= lensQ.y ? float2(1, 0) : float2(0, 1);
    float2 lensNormal = ((lensQ.x >= 0 || lensQ.y >= 0) && gradientLength > .00001)
        ? gradient / max(gradientLength, .00001) : straight;
    lensNormal *= float2(p.x < 0 ? -1 : 1, p.y < 0 ? -1 : 1);

    // Preserve both existing controls. Default distortion (.65) reproduces
    // GlassDesk's 24 DIP band / 32 DIP displacement at unit refraction strength.
    // Distortion also changes the shoulder depth; zero disables the lens.
    float distortion = saturate(Texture.y) / .65;
    float band = max(.001, min(24 * Material.x * (.5 + .5 * distortion), min(PlateSize.x, PlateSize.y) * .5));
    float depth = max(0, -distance);
    float t = saturate(1 - depth / band);
    float displacement = -(1 - sqrt(max(0, 1 - t * t))) * 32 * Material.x * max(0, Shape.y) * distortion;
    float2 lensUv = uv + displacement * lensNormal / SourceSize;
    float4 color = tex2D(Background, saturate(lensUv));

    // Upstream's subpixel chromatic split belongs to transmitted light only.
    // No tint/saturation/highlight is coupled to the refraction control.
    float displacementDip = abs(displacement) / max(Material.x, .001);
    float refracting = saturate(displacementDip / 8);
    float curvature = saturate(2 * abs(lensNormal.x * lensNormal.y));
    float splitDip = min(.75, displacementDip * .035) * refracting * refracting * (.65 + .35 * curvature);
    float2 splitUv = splitDip * Material.x * lensNormal / SourceSize;
    color.r = tex2D(Background, saturate(lensUv + splitUv)).r;
    color.b = tex2D(Background, saturate(lensUv - splitUv)).b;
    return color;
}
