using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace DockForWindows.Views;

public sealed class LiquidGlassEffect : ShaderEffect
{
    private static readonly PixelShader Shader = new() { UriSource = new Uri("pack://application:,,,/DockForWindows;component/Shaders/LiquidGlass.ps") };
    private static PixelShader? _controlCenterLighting, _fused;
    // The control center's plate in one pass (Shaders/LiquidGlass.Fused.hlsl) instead of this refraction pass
    // followed by a separate frost and lighting pass.
    internal void SetFused(bool fused)
    {
        var shader=fused?(_fused??=new PixelShader{UriSource=new Uri("pack://application:,,,/DockForWindows;component/Shaders/LiquidGlass.Fused.ps")}):Shader;
        if(PixelShader!=shader)PixelShader=shader;
    }
    internal void SetControlCenterLighting(bool enabled)
    {
        var shader=enabled?(_controlCenterLighting??=new PixelShader{UriSource=new Uri("pack://application:,,,/DockForWindows;component/Shaders/LiquidGlass.ControlCenterLighting.ps")}):Shader;
        if(PixelShader!=shader)PixelShader=shader;
    }
    public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty(nameof(Input), typeof(LiquidGlassEffect), 0, SamplingMode.Bilinear);
    public static readonly DependencyProperty ClearInputProperty = RegisterPixelShaderSamplerProperty(nameof(ClearInput), typeof(LiquidGlassEffect), 1, SamplingMode.Bilinear);
    public static readonly DependencyProperty SourceSizeProperty = Constant(nameof(SourceSize), new Point(1, 1), 0);
    public static readonly DependencyProperty PlateOriginProperty = Constant(nameof(PlateOrigin), new Point(), 1);
    public static readonly DependencyProperty PlateSizeProperty = Constant(nameof(PlateSize), new Point(1, 1), 2);
    public static readonly DependencyProperty ShapeProperty = Constant(nameof(Shape), new Point(85, .7), 3);
    public static readonly DependencyProperty MaterialProperty = Constant(nameof(Material), new Point(1, .1), 4);
    public static readonly DependencyProperty TextureProperty = Constant(nameof(Texture), new Point(.45, 0), 5);
    public static readonly DependencyProperty WorldOriginProperty = Constant(nameof(WorldOrigin), new Point(), 6);
    public static readonly DependencyProperty LightingProperty = Constant(nameof(Lighting), new Point(.55, 2.5), 7);
    public static readonly DependencyProperty SidesProperty = Constant(nameof(Sides), new Point(.55, 0), 8);
    public static readonly DependencyProperty LightingOnlyProperty = DependencyProperty.Register(nameof(LightingOnly), typeof(double), typeof(LiquidGlassEffect), new UIPropertyMetadata(0d, PixelShaderConstantCallback(9)));
    public static readonly DependencyProperty TintProperty = Constant(nameof(Tint), new Point(.06, 0), 10);
    public static readonly DependencyProperty ControlProfileProperty = Constant(nameof(ControlProfile), new Point(), 11);
    // Fused pass only: Gaussian frost weights at 0, 1 and 2 pixels, and w > 0 when frosted.
    public static readonly DependencyProperty FrostProperty = DependencyProperty.Register(nameof(Frost), typeof(Point4D), typeof(LiquidGlassEffect), new UIPropertyMetadata(new Point4D(), PixelShaderConstantCallback(12)));
    private static DependencyProperty Constant(string name, Point value, int register) => DependencyProperty.Register(name, typeof(Point), typeof(LiquidGlassEffect), new UIPropertyMetadata(value, PixelShaderConstantCallback(register)));
    public Brush? Input { get => (Brush?)GetValue(InputProperty); set { if (Input != value) SetValue(InputProperty, value); } }
    public Brush? ClearInput { get => (Brush?)GetValue(ClearInputProperty); set { if (ClearInput != value) SetValue(ClearInputProperty, value); } }
    public Point SourceSize { get => (Point)GetValue(SourceSizeProperty); set { if (SourceSize != value) SetValue(SourceSizeProperty, value); } }
    public Point PlateOrigin { get => (Point)GetValue(PlateOriginProperty); set { if (PlateOrigin != value) SetValue(PlateOriginProperty, value); } }
    public Point PlateSize { get => (Point)GetValue(PlateSizeProperty); set { if (PlateSize != value) SetValue(PlateSizeProperty, value); } }
    public Point Shape { get => (Point)GetValue(ShapeProperty); set { if (Shape != value) SetValue(ShapeProperty, value); } }
    public Point Material { get => (Point)GetValue(MaterialProperty); set { if (Material != value) SetValue(MaterialProperty, value); } }
    public Point Texture { get => (Point)GetValue(TextureProperty); set { if (Texture != value) SetValue(TextureProperty, value); } }
    public Point WorldOrigin { get => (Point)GetValue(WorldOriginProperty); set { if (WorldOrigin != value) SetValue(WorldOriginProperty, value); } }
    public Point Lighting { get => (Point)GetValue(LightingProperty); set { if (Lighting != value) SetValue(LightingProperty, value); } }
    public Point Sides { get => (Point)GetValue(SidesProperty); set { if (Sides != value) SetValue(SidesProperty, value); } }
    public double LightingOnly { get => (double)GetValue(LightingOnlyProperty); set { if (LightingOnly != value) SetValue(LightingOnlyProperty, value); } }
    public Point Tint { get => (Point)GetValue(TintProperty); set { if (Tint != value) SetValue(TintProperty, value); } }
    public Point ControlProfile { get => (Point)GetValue(ControlProfileProperty); set { if (ControlProfile != value) SetValue(ControlProfileProperty, value); } }
    public Point4D Frost { get => (Point4D)GetValue(FrostProperty); set { if (Frost != value) SetValue(FrostProperty, value); } }
    public LiquidGlassEffect()
    {
        PixelShader = Shader;
        foreach (var property in new[] { InputProperty, ClearInputProperty, SourceSizeProperty, PlateOriginProperty, PlateSizeProperty, ShapeProperty, MaterialProperty, TextureProperty, WorldOriginProperty, LightingProperty, SidesProperty, LightingOnlyProperty, TintProperty, ControlProfileProperty, FrostProperty }) UpdateShaderValue(property);
    }
}
