using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YMM4LiquidGlass.Effects;

[VideoEffect("Liquid Glass", [VideoEffectCategories.Filtering], ["liquid", "glass", "リキッドグラス", "ガラス", "屈折"], IsAviUtlSupported = false)]
public class LiquidGlassEffect : VideoEffectBase
{
    public override string Label => $"Liquid Glass {Width.GetValue(0, 1, 30):F0}x{Height.GetValue(0, 1, 30):F0}";

    [Display(GroupName = "パネル", Name = "X", Description = "パネル中心のX座標（画面中央が0）", Order = 100)]
    [AnimationSlider("F0", "px", -960, 960)]
    public Animation X { get; } = new Animation(0, -99999, 99999);

    [Display(GroupName = "パネル", Name = "Y", Description = "パネル中心のY座標（画面中央が0）", Order = 101)]
    [AnimationSlider("F0", "px", -540, 540)]
    public Animation Y { get; } = new Animation(0, -99999, 99999);

    [Display(GroupName = "パネル", Name = "幅", Description = "パネルの幅", Order = 102)]
    [AnimationSlider("F0", "px", 0, 1920)]
    public Animation Width { get; } = new Animation(600, 0, 99999);

    [Display(GroupName = "パネル", Name = "高さ", Description = "パネルの高さ", Order = 103)]
    [AnimationSlider("F0", "px", 0, 1080)]
    public Animation Height { get; } = new Animation(200, 0, 99999);

    [Display(GroupName = "パネル", Name = "角丸", Description = "角の丸みの半径", Order = 104)]
    [AnimationSlider("F0", "px", 0, 250)]
    public Animation CornerRadius { get; } = new Animation(48, 0, 99999);

    [Display(GroupName = "パネル", Name = "角の平滑化", Description = "角丸をさらに滑らかにつなぐ量", Order = 105)]
    [AnimationSlider("F1", "", 0, 20)]
    public Animation CornerSmoothing { get; } = new Animation(0, 0, 1000);

    [Display(GroupName = "屈折", Name = "ベゼル幅", Description = "ガラスが立ち上がる縁の帯の幅（画面の高さの千分率）", Order = 200)]
    [AnimationSlider("F1", "", 0, 100)]
    public Animation Bezel { get; } = new Animation(70, 0, 1000);

    [Display(GroupName = "屈折", Name = "厚み", Description = "ガラスの厚み（画面の高さの千分率）", Order = 201)]
    [AnimationSlider("F1", "", 0, 50)]
    public Animation Thickness { get; } = new Animation(20, 0, 1000);

    [Display(GroupName = "屈折", Name = "歪み", Description = "変位量の倍率", Order = 202)]
    [AnimationSlider("F1", "", 0, 500)]
    public Animation Distortion { get; } = new Animation(350, -5000, 5000);

    [Display(GroupName = "屈折", Name = "色収差", Description = "縁でR/G/Bの変位量をずらす量", Order = 203)]
    [AnimationSlider("F1", "", 0, 100)]
    public Animation Aberration { get; } = new Animation(100, 0, 1000);

    [Display(GroupName = "ガラス", Name = "色", Description = "ガラスの色。この色の補色が吸収される", Order = 300)]
    [ColorPicker]
    public Color TintColor
    {
        get;
        set => Set(ref field, value);
    } = Colors.White;

    [Display(GroupName = "ガラス", Name = "濃度", Description = "色の濃さ。0で無色透明", Order = 301)]
    [AnimationSlider("F1", "", 0, 100)]
    public Animation Density { get; } = new Animation(0, 0, 100);

    [Display(GroupName = "ハイライト", Name = "角度", Description = "光源の向き（0度=右 / 90度=上）", Order = 400)]
    [AnimationSlider("F1", "°", -180, 180)]
    public Animation SpecularAngle { get; } = new Animation(123, -99999, 99999);

    [Display(GroupName = "ハイライト", Name = "強さ", Description = "縁の反射の強さ", Order = 401)]
    [AnimationSlider("F1", "", 0, 200)]
    public Animation Specular { get; } = new Animation(100, 0, 1000);

    [Display(GroupName = "ハイライト", Name = "回り込み", Description = "光源の反対側の縁に回り込む光の割合", Order = 402)]
    [AnimationSlider("F1", "", 0, 100)]
    public Animation SpecularBackLobe { get; } = new Animation(50, 0, 200);

    [Display(GroupName = "影", Name = "強さ", Description = "パネルの外側に落とす影の濃さ", Order = 600)]
    [AnimationSlider("F1", "", 0, 100)]
    public Animation ShadowIntensity { get; } = new Animation(0, 0, 100);

    [Display(GroupName = "影", Name = "広がり", Description = "影のぼけ幅", Order = 601)]
    [AnimationSlider("F0", "px", 0, 80)]
    public Animation ShadowSpread { get; } = new Animation(18, 0, 99999);

    [Display(GroupName = "影", Name = "Yオフセット", Description = "影を下方向にずらす量", Order = 602)]
    [AnimationSlider("F0", "px", -40, 40)]
    public Animation ShadowOffsetY { get; } = new Animation(6, -99999, 99999);

    [Display(GroupName = "すりガラス", Name = "有効", Description = "内側を曇らせる。縁の屈折は鮮明なまま残る", Order = 500)]
    [ToggleSlider]
    public bool IsFrostEnabled
    {
        get;
        set => Set(ref field, value);
    }

    [Display(GroupName = "すりガラス", Name = "ぼかし量", Description = "曇りの強さ（画面の高さの千分率）", Order = 501)]
    [AnimationSlider("F1", "", 0, 100)]
    public Animation FrostBlur { get; } = new Animation(30, 0, 1000);

    [Display(GroupName = "すりガラス", Name = "ぼかし開始", Description = "曇り始める位置（0=輪郭 / 100=ベゼル内端）", Order = 502)]
    [AnimationSlider("F1", "", 0, 100)]
    public Animation FrostStart { get; } = new Animation(0, 0, 100);

    [Display(GroupName = "すりガラス", Name = "ぼかし遷移", Description = "曇りの遷移幅（0=くっきり / 100=ベゼル全体）", Order = 503)]
    [AnimationSlider("F1", "", 0, 100)]
    public Animation FrostEdge { get; } = new Animation(50, 0, 100);

    protected override IEnumerable<IAnimatable> GetAnimatables() =>
    [
        X, Y, Width, Height, CornerRadius, CornerSmoothing,
        Bezel, Thickness, Distortion, Aberration,
        Density,
        SpecularAngle, Specular, SpecularBackLobe,
        ShadowIntensity, ShadowSpread, ShadowOffsetY,
        FrostBlur, FrostStart, FrostEdge,
    ];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) =>
        new LiquidGlassEffectProcessor(devices, this);

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];
}
