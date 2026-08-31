using System.Diagnostics.CodeAnalysis;
using ComputeSharp.D2D1.Interop;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YMM4LiquidGlass.Shaders;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YMM4LiquidGlass.Effects;

[SuppressMessage("Interoperability", "CA1416:プラットフォームの互換性を検証")]
internal class LiquidGlassEffectProcessor(IGraphicsDevicesAndContext devices, LiquidGlassEffect item)
    : VideoEffectProcessorBase(devices)
{
    private GaussianBlur? _blurEffect;
    private ID2D1Effect? _glassEffect;

    private double _blur = double.NaN;

    protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
    {
        var context = devices.DeviceContext;

        RegisterShader(context);

        _blurEffect = new GaussianBlur(context)
        {
            BorderMode = BorderMode.Hard,
            Optimization = GaussianBlurOptimization.Quality,
        };
        disposer.Collect(_blurEffect);

        _glassEffect = (ID2D1Effect)context.CreateEffect(D2D1PixelShaderEffect.GetEffectId<LiquidGlassShader>());
        disposer.Collect(_glassEffect);

        var blurOutput = _blurEffect.Output;
        disposer.Collect(blurOutput);

        _glassEffect.SetInput(1, blurOutput, true);

        var output = _glassEffect.Output;
        disposer.Collect(output);

        return output;
    }

    private static unsafe void RegisterShader(ID2D1DeviceContext context)
    {
        using var factory = context.Factory.QueryInterface<ID2D1Factory1>();

        try
        {
            D2D1PixelShaderEffect.RegisterForD2D1Factory1<LiquidGlassShader>((void*)factory.NativePointer, out _);
        }
        catch (Exception)
        {
        }
    }

    protected override void setInput(ID2D1Image? input)
    {
        _blurEffect?.SetInput(0, input, true);
        _glassEffect?.SetInput(0, input, true);
    }

    protected override void ClearEffectChain()
    {
        SetInput(null);
        _glassEffect?.SetInput(1, null, true);
    }

    public override unsafe DrawDescription Update(EffectDescription effectDescription)
    {
        if (_glassEffect is null || _blurEffect is null)
        {
            return effectDescription.DrawDescription;
        }

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;

        var screenHeight = (float)Math.Max(effectDescription.ScreenSize.Height, 1);

        var blurValue = item.IsFrostEnabled
            ? item.FrostBlur.GetValue(frame, length, fps) / 1000.0 * screenHeight
            : 0.0;
        if (_blur != blurValue)
        {
            _blurEffect.StandardDeviation = (float)Math.Clamp(blurValue / 3.0, 0.0, 100.0);
            _blur = blurValue;
        }

        var tint = item.TintColor;

        var shader = new LiquidGlassShader(
            center: new float2(
                (float)item.X.GetValue(frame, length, fps),
                (float)item.Y.GetValue(frame, length, fps)),
            halfSize: new float2(
                (float)(item.Width.GetValue(frame, length, fps) / 2.0),
                (float)(item.Height.GetValue(frame, length, fps) / 2.0)),
            cornerRadius: (float)item.CornerRadius.GetValue(frame, length, fps),
            cornerSmoothing: (float)item.CornerSmoothing.GetValue(frame, length, fps),
            screenHeight: screenHeight,
            bezelWidth: (float)(item.Bezel.GetValue(frame, length, fps) / 1000.0),
            thickness: (float)(item.Thickness.GetValue(frame, length, fps) / 1000.0),
            distortion: (float)(item.Distortion.GetValue(frame, length, fps) / 1000.0),
            aberration: (float)(item.Aberration.GetValue(frame, length, fps) / 1000.0),
            tint: new float3(tint.R / 255f, tint.G / 255f, tint.B / 255f),
            density: (float)(item.Density.GetValue(frame, length, fps) / 100.0),
            specular: (float)(item.Specular.GetValue(frame, length, fps) / 100.0),
            specularAngle: (float)(item.SpecularAngle.GetValue(frame, length, fps) * Math.PI / 180.0),
            specularBackLobe: (float)(item.SpecularBackLobe.GetValue(frame, length, fps) / 100.0),
            shadowIntensity: (float)(item.ShadowIntensity.GetValue(frame, length, fps) / 100.0),
            shadowSpread: (float)item.ShadowSpread.GetValue(frame, length, fps),
            shadowOffsetY: (float)item.ShadowOffsetY.GetValue(frame, length, fps),
            frost: item.IsFrostEnabled ? 1f : 0f,
            frostBlurStart: (float)(item.FrostStart.GetValue(frame, length, fps) / 100.0),
            frostBlurEdge: (float)(item.FrostEdge.GetValue(frame, length, fps) / 100.0));

        D2D1PixelShaderEffect.SetConstantBufferForD2D1Effect((void*)_glassEffect.NativePointer, in shader);

        return effectDescription.DrawDescription;
    }
}
