using ComputeSharp;
using ComputeSharp.D2D1;

namespace YMM4LiquidGlass.Shaders;

[D2DInputCount(2)]
[D2DInputComplex(0)]
[D2DInputComplex(1)]
[D2DInputDescription(0, D2D1Filter.MinMagMipLinear)]
[D2DInputDescription(1, D2D1Filter.MinMagMipLinear)]
[D2DRequiresScenePosition]
[D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
[D2DGeneratedPixelShaderDescriptor]
internal readonly partial struct LiquidGlassShader(
    float2 center,
    float2 halfSize,
    float cornerRadius,
    float cornerSmoothing,
    float screenHeight,
    float bezelWidth,
    float thickness,
    float distortion,
    float aberration,
    float3 tint,
    float density,
    float specular,
    float specularAngle,
    float specularBackLobe,
    float shadowIntensity,
    float shadowSpread,
    float shadowOffsetY,
    float frost,
    float frostBlurStart,
    float frostBlurEdge) : ID2D1PixelShader
{
    private static float Convex(float x)
    {
        return Hlsl.Sqrt(Hlsl.Saturate(1f - Hlsl.Pow(1f - x, 2f)));
    }

    private static float HeightAt(float x)
    {
        return 1f - Convex(x);
    }

    private static float SmoothMin(float a, float b, float k)
    {
        var h = Hlsl.Saturate(0.5f + (0.5f * (b - a) / Hlsl.Max(k, 1e-6f)));
        var smooth = Hlsl.Lerp(b, a, h) - (k * h * (1f - h));

        return k <= 0f ? Hlsl.Min(a, b) : smooth;
    }

    private static float SmoothMax(float a, float b, float k)
    {
        var h = Hlsl.Saturate(0.5f + (0.5f * (a - b) / Hlsl.Max(k, 1e-6f)));
        var smooth = Hlsl.Lerp(b, a, h) + (k * h * (1f - h));

        return k <= 0f ? Hlsl.Max(a, b) : smooth;
    }

    private float SignedDistance(float2 p)
    {
        var r = Hlsl.Min(cornerRadius, Hlsl.Min(halfSize.X, halfSize.Y));
        var q = Hlsl.Abs(p) - halfSize + r;

        var sharp = Hlsl.Min(Hlsl.Max(q.X, q.Y), 0f) + Hlsl.Length(Hlsl.Max(q, 0f)) - r;

        var termA = SmoothMax(q.X, q.Y, cornerSmoothing);
        var termB = SmoothMin(termA, 0f, cornerSmoothing * 0.5f);
        var termC = new float2(
            SmoothMax(q.X, 0f, cornerSmoothing),
            SmoothMax(q.Y, 0f, cornerSmoothing));
        var smooth = termB + Hlsl.Length(termC) - r;

        return cornerSmoothing <= 0f ? sharp : smooth;
    }

    private float FieldAt(float2 p)
    {
        return Hlsl.Saturate(-SignedDistance(p) / (Hlsl.Max(bezelWidth, 1e-5f) * screenHeight));
    }

    public float4 Execute()
    {
        var position = D2D.GetScenePosition().XY;
        var p = position - center;

        var distance = SignedDistance(p);
        var coverage = Hlsl.Saturate(0.5f - distance);

        var background = D2D.SampleInputAtOffset(0, float2.Zero);
        float4 result;

        if (coverage <= 0f)
        {
            var shadowDistance = SignedDistance(p - new float2(0f, shadowOffsetY));
            var shadowFalloff = Hlsl.Saturate(1f - (shadowDistance / Hlsl.Max(shadowSpread, 1e-3f)));

            result = background * (1f - (shadowFalloff * shadowFalloff * shadowIntensity));
        }
        else
        {
            var bezel = Hlsl.Max(bezelWidth, 1e-5f);

            var x = FieldAt(p);
            var distFromEdge = x * bezel;

            var height = HeightAt(x);
            var delta = 0.002f;
            var h2 = HeightAt(Hlsl.Saturate((distFromEdge + delta) / bezel));
            var slope = (h2 - height) / delta;

            var e = Hlsl.Max(bezel * 0.08f, 1f / Hlsl.Max(screenHeight, 1f));
            var ePixels = e * screenHeight;
            var gradient = new float2(
                FieldAt(p + new float2(ePixels, 0f)) - FieldAt(p - new float2(ePixels, 0f)),
                FieldAt(p + new float2(0f, ePixels)) - FieldAt(p - new float2(0f, ePixels)));
            var gradientLength = Hlsl.Length(gradient);
            var offsetDirection = gradientLength > 1e-6f ? -gradient / gradientLength : float2.Zero;

            var gradientReference = 2f * e / bezel;
            var gradientNormalized = Hlsl.Saturate(gradientLength / Hlsl.Max(gradientReference, 1e-6f));

            var totalDisplacement = height * slope * thickness * distortion * gradientNormalized;
            totalDisplacement = Hlsl.Clamp(totalDisplacement, -0.5f, 0.5f);

            var displacement = offsetDirection * totalDisplacement * screenHeight;

            var color = new float3(
                D2D.SampleInputAtOffset(0, displacement * (1f - aberration)).R,
                D2D.SampleInputAtOffset(0, displacement).G,
                D2D.SampleInputAtOffset(0, displacement * (1f + aberration)).B);

            if (frost > 0.5f)
            {
                var blurred = D2D.SampleInputAtOffset(1, displacement).RGB;
                var mix = Hlsl.SmoothStep(frostBlurStart, frostBlurStart + Hlsl.Max(frostBlurEdge, 0.02f), x);

                color = Hlsl.Lerp(color, blurred, mix);
            }

            if (density > 0f)
            {
                var absorption = (new float3(1f, 1f, 1f) - tint) * (density * 3.5f);
                var path = 1f - height;

                color *= Hlsl.Exp(-absorption * path);
            }

            var u = 1f - x;
            var dZdx = Hlsl.Min(u / Hlsl.Sqrt(Hlsl.Max(1f - (u * u), 1e-4f)), 8f);
            var normal = Hlsl.Normalize(new float3(dZdx * offsetDirection, 1f));
            var fresnel = Hlsl.Pow(1f - Hlsl.Saturate(normal.Z), 5f);

            var lightDirection = new float2(Hlsl.Cos(specularAngle), -Hlsl.Sin(specularAngle));
            var nl = Hlsl.Dot(offsetDirection, lightDirection);
            var lobe = Hlsl.Pow(Hlsl.Saturate(nl), 2f) + (Hlsl.Pow(Hlsl.Saturate(-nl), 2f) * specularBackLobe);

            var alpha = background.A;

            color += fresnel * lobe * specular * alpha;

            result = Hlsl.Lerp(background, new float4(Hlsl.Clamp(color, 0f, alpha), alpha), coverage);
        }

        return result;
    }
}
