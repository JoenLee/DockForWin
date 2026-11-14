// The control-center plate in one pass: the refraction and chromatic split of LiquidGlass.hlsl, WPF's Gaussian
// frost, the plate outline and the control-center lighting, composited as the multi-pass chain did:
//   frost  = Gaussian blur of (refracted backdrop x outline coverage)        [image effect, source clip, BlurEffect]
//   glass  = frost x glass opacity x outline coverage                         [host opacity, outline clip]
//   light  = control-center lighting x glass opacity x outline coverage       [lighting overlay and its clip]
//   result = light + glass x (1 - light alpha)
// The blur is WPF's own kernel (sigma = floor(radius in pixels) / 3, measured), evaluated on the refracted image
// with every neighbour refracted at its own depth instead of on a separate surface, so a plate costs one effect
// instead of three. The control center uses it while that kernel fits five taps (sigma <= 1 pixel); wider frost keeps the
// multi-pass chain.
sampler2D Background : register(s0);
float2 SourceSize : register(c0);
float2 PlateOrigin : register(c1);
float2 PlateSize : register(c2);
float2 Shape : register(c3);       // radius percent, refraction strength
float2 Material : register(c4);    // physical pixels per DIP, unused
float2 Texture : register(c5);     // grain (unused), distortion
float2 Lighting : register(c7);    // strength, narrow feather width in DIP
float2 Tint : register(c10);       // tint opacity, glass opacity
float2 ControlProfile : register(c11); // unused, outline exponent
float4 Frost : register(c12);      // Gaussian weights at 0, 1 and 2 pixels (normalised over five taps); w > 0 when frosted

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float2 pixel = uv * SourceSize;
    float2 p = pixel - PlateOrigin - PlateSize * .5;
    float fraction = saturate(Shape.x / 100);
    float n = 3 - pow(fraction, 8);
    if (ControlProfile.y >= 2) n = ControlProfile.y;
    float radius = min(PlateSize.x, PlateSize.y) * .5 * fraction;
    float2 q = abs(p) - (PlateSize * .5 - radius);
    float2 normal;
    float distance;
    if (q.x > 0 && q.y > 0)
    {
        float norm = pow(pow(abs(q.x), n) + pow(abs(q.y), n), 1 / n);
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
    float coverage = saturate(.5 - distance);
    // Nothing of the plate reaches pixels a frost radius beyond its outline.
    if (distance > 4) return 0;

    // Lens, as LiquidGlass.hlsl.
    float expandedRadius = min(radius * 1.5, min(PlateSize.x, PlateSize.y) * .5);
    float2 lensQ = abs(p) - PlateSize * .5 + expandedRadius;
    float2 gradient = max(lensQ, 0);
    float gradientLength = length(gradient);
    float2 straight = lensQ.x >= lensQ.y ? float2(1, 0) : float2(0, 1);
    float2 lensNormal = ((lensQ.x >= 0 || lensQ.y >= 0) && gradientLength > .00001)
        ? gradient / max(gradientLength, .00001) : straight;
    lensNormal *= float2(p.x < 0 ? -1 : 1, p.y < 0 ? -1 : 1);
    float distortion = saturate(Texture.y) / .65;
    float band = max(.001, min(24 * Material.x * (.5 + .5 * distortion), min(PlateSize.x, PlateSize.y) * .5));
    float depth = max(0, -distance);
    float t = saturate(1 - depth / band);
    float strength = 32 * Material.x * max(0, Shape.y) * distortion;
    float displacement = -(1 - sqrt(max(0, 1 - t * t))) * strength;
    float2 lensUv = uv + displacement * lensNormal / SourceSize;
    float displacementDip = abs(displacement) / max(Material.x, .001);
    float refracting = saturate(displacementDip / 8);
    float curvature = saturate(2 * abs(lensNormal.x * lensNormal.y));
    float splitDip = min(.75, displacementDip * .035) * refracting * refracting * (.65 + .35 * curvature);
    float2 splitUv = splitDip * Material.x * lensNormal / SourceSize;

    float4 frost;
    if (Frost.w > 0)
    {
        // Gaussian of the refracted, outline-clipped image around this pixel, each neighbour refracted on its own.
        // The displacement depends only on the depth below the outline, so a neighbour o pixels away sits at depth
        // (depth - normal . o) and is displaced exactly as LiquidGlass.hlsl displaces that depth, along the same
        // lens normal; this keeps the fold the lens makes near the rim, where the image changes fastest.
        frost = 0;
        [loop] for (int i = -2; i <= 2; i++)
        {
            float wi = abs(i) == 0 ? Frost.x : abs(i) == 1 ? Frost.y : Frost.z;
            [loop] for (int j = -2; j <= 2; j++)
            {
                float wj = abs(j) == 0 ? Frost.x : abs(j) == 1 ? Frost.y : Frost.z;
                float2 o = float2(i, j);
                float along = dot(normal, o);
                float cover = saturate(.5 - (distance + along));
                float tk = saturate(1 - max(0, depth - along) / band);
                float shift = -(1 - sqrt(max(0, 1 - tk * tk))) * strength;
                float2 at = uv + (o + shift * lensNormal) / SourceSize;
                float shiftDip = abs(shift) / max(Material.x, .001), bend = saturate(shiftDip / 8);
                float2 split = min(.75, shiftDip * .035) * bend * bend * (.65 + .35 * curvature) * Material.x * lensNormal / SourceSize;
                float weight = wi * wj * cover;
                frost.r += weight * tex2D(Background, saturate(at + split)).r;
                frost.g += weight * tex2D(Background, saturate(at)).g;
                frost.b += weight * tex2D(Background, saturate(at - split)).b;
                frost.a += weight;
            }
        }
    }
    else
    {
        frost = tex2D(Background, saturate(lensUv));
        frost.r = tex2D(Background, saturate(lensUv + splitUv)).r;
        frost.b = tex2D(Background, saturate(lensUv - splitUv)).b;
        frost = float4(frost.rgb, 1) * coverage;
    }

    // Control-center lighting, as LiquidGlass.hlsl with CONTROL_CENTER_LIGHTING.
    float width = max(.5, Lighting.y) * Material.x;
    float feather = smoothstep(0, width * .25, depth) * (1 - smoothstep(width * .25, width, depth));
    float core = smoothstep(0, width * .18, depth) * (1 - smoothstep(width * .18, width * .42, depth));
    float white = ((normal.y < 0 ? .16 : .13) * feather + (normal.y < 0 ? .20 : .18) * core) * Lighting.x * abs(normal.y);
    float underLip = smoothstep(.8 * Material.x, 1.4 * Material.x, depth) * (1 - smoothstep(1.4 * Material.x, 2.8 * Material.x, depth));
    white += .09 * Lighting.x * abs(normal.x) * underLip;
    float opacity = saturate(white + Tint.x * (1 - white));

    float4 light = float4(white, white, white, opacity) * Tint.y * coverage;
    float4 glass = frost * Tint.y * coverage;
    return light + glass * (1 - light.a);
}
