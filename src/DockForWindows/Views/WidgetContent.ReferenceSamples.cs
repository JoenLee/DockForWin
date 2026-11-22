using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

// Original WPF layouts. Reference proportions and source review: reference-design-20261119/02-设计基准.md.
internal static class WidgetSampleTokens
{
    internal const double Inset = 16, SmallGap = 4, Gap = 8, SectionGap = 12;
    internal const double Title = 16, Label = 12, Caption = 10, WeatherIcon = 48;
    internal static bool Applies(WidgetLayout layout) => layout is WidgetLayout.ClockDot or WidgetLayout.ClockDotWide
        or WidgetLayout.WeatherDot or WidgetLayout.MusicVinyl or WidgetLayout.MusicVinylWide;
}

internal static partial class WidgetContent
{
    private static FrameworkElement ReferenceSample(WidgetDefinition d, WidgetRenderData data) => d.Kind switch {
        WidgetKind.Clock => ReferenceClock(d, data), WidgetKind.Weather => ReferenceWeather(d, data), _ => ReferenceMusic(d, data)
    };
    private static Grid SampleBody(WidgetRenderData data) => new() { Margin = new Thickness(WidgetSampleTokens.Inset), ClipToBounds = true, ToolTip = data.Error };
    private static FrameworkElement SampleDots(string text, double height, string ink = "WidgetInk")
    {
        var glyphs = new WidgetDotNumber(text, pixels: true); glyphs.SetResourceReference(WidgetDotNumber.InkProperty, ink);
        AutomationProperties.SetName(glyphs, text);
        return new Viewbox { Child = glyphs, Height = height, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center };
    }
    private static Border SamplePlate(FrameworkElement child, double radius, string fill = "WidgetTrack")
    {
        var plate = new Border { Child = child, CornerRadius = new CornerRadius(radius) };
        plate.SetResourceReference(Border.BackgroundProperty, fill); return plate;
    }
    private static FrameworkElement ReferenceClock(WidgetDefinition d, WidgetRenderData data)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(data.Source.TimeZone);
        var now = data.Now ?? TimeZoneInfo.ConvertTime(DateTimeOffset.Now, zone).DateTime;
        var root = SampleBody(data); bool square = d.Size.Columns == d.Size.Rows;
        double height = d.Size.Height - 32, width = d.Size.Width - 32;
        double scale = square ? Math.Min(height / 148, width / 148) : 1;
        double detailWidth = square ? 48 * scale : 72;
        root.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new() { Width = new GridLength(12 * scale) });
        root.ColumnDefinitions.Add(new() { Width = new GridLength(detailWidth) });
        var numbers = new Grid();
        if (square) {
            Rows(numbers, 1, 1); double digitHeight = 48 * scale;
            var hours = SampleDots(now.ToString("HH", CultureInfo.InvariantCulture), digitHeight);
            var minutes = SampleDots(now.ToString("mm", CultureInfo.InvariantCulture), digitHeight, "WidgetAccent");
            hours.VerticalAlignment = VerticalAlignment.Bottom; hours.Margin = new Thickness(0, 0, 0, 6 * scale);
            minutes.VerticalAlignment = VerticalAlignment.Top; minutes.Margin = new Thickness(0, 6 * scale, 0, 0);
            At(numbers, hours); At(numbers, minutes, row: 1);
        } else {
            var time = SampleDots(now.ToString("HH:mm", CultureInfo.InvariantCulture), Math.Min(66, (width - detailWidth - 36) * 7 / 25));
            time.VerticalAlignment = VerticalAlignment.Center; numbers.Children.Add(time);
        }
        At(root, SamplePlate(numbers, square ? 44 * scale : height / 2));
        var detail = new Grid(); Rows(detail, -1, 1, -1);
        var day = T(now.ToString("ddd", UiText.Culture).ToUpperInvariant(), 10 * scale, "WidgetMuted"); day.HorizontalAlignment = HorizontalAlignment.Center;
        At(detail, day);
        var date = T(now.Day.ToString("00", CultureInfo.InvariantCulture), 19 * scale, "WidgetAccent", true); date.HorizontalAlignment = HorizontalAlignment.Center;
        var dateCircle = SamplePlate(date, detailWidth / 2); dateCircle.Width = dateCircle.Height = detailWidth;
        At(detail, dateCircle, row: 1);
        var month = T(now.ToString("MM / dd", CultureInfo.InvariantCulture), 9 * scale, "WidgetMuted"); month.HorizontalAlignment = HorizontalAlignment.Center;
        At(detail, month, row: 2); At(root, detail, 2);
        root.ToolTip = now.ToString("yyyy-MM-dd HH:mm", UiText.Culture) + " · " + zone.DisplayName;
        return root;
    }
    private static FrameworkElement ReferenceWeather(WidgetDefinition d, WidgetRenderData data)
    {
        var root = SampleBody(data); root.RowDefinitions.Add(new() { Height = new GridLength(20) });
        root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new() { Height = new GridLength(38) });
        bool wide = d.Size.Columns >= 4;
        if (wide) { Columns(root, 1.3, .15, 1); }
        var city = T(string.IsNullOrWhiteSpace(data.Source.City) ? L("当前位置", "Current location") : data.Source.City, 12, "WidgetMuted");
        city.ToolTip = data.Source.City; At(root, city);
        var main = new Grid(); double iconSize = 48;
        main.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        main.ColumnDefinitions.Add(new() { Width = new GridLength(8) });
        main.ColumnDefinitions.Add(new() { Width = new GridLength(iconSize) });
        var weather = data.Weather; string temperature = weather == null ? "--" : Math.Round(weather.Temperature).ToString(CultureInfo.InvariantCulture);
        double mainWidth = wide ? (d.Size.Width - 32) * 1.3 / 2.45 : d.Size.Width - 32;
        double available = mainWidth - iconSize - 8 - 14;
        double hero = Math.Min(d.Size.Columns == 2 ? 48 : 66, available * 7 / Math.Max(1, temperature.Length * 6 - 1));
        var reading = new Grid { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left,
            Width = hero * Math.Max(1, temperature.Length * 6 - 1) / 7 + 14 };
        reading.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); reading.ColumnDefinitions.Add(new() { Width = new GridLength(14) });
        var digits = SampleDots(temperature, hero); digits.HorizontalAlignment = HorizontalAlignment.Left; At(reading, digits);
        var unit = T("°", 19, "WidgetAccent"); unit.VerticalAlignment = VerticalAlignment.Top; At(reading, unit, 1); At(main, reading);
        var icon = new WidgetSampleWeatherIcon(weather?.Code, weather?.IsDay ?? true) { Width = iconSize, Height = iconSize, VerticalAlignment = VerticalAlignment.Center };
        At(main, icon, 2); At(root, main, row: 1);
        var footer = new Grid(); Rows(footer, 1, 1);
        string condition = weather == null ? L("天气未接入", "No weather data") : WidgetWeatherService.Description(weather.Code);
        var summary = T(condition, wide ? 16 : 12); summary.ToolTip = condition; At(footer, summary);
        string range = weather == null || weather.Days.Count == 0 ? data.Error ?? L("选择城市以获取实况", "Choose a city")
            : FormattableString.Invariant($"{L("最低", "L")} {weather.Days[0].Low:0}°   {L("最高", "H")} {weather.Days[0].High:0}°");
        At(footer, T(range, wide ? 11 : 10, "WidgetMuted"), row: 1);
        if (wide) {
            footer.Height = 58; footer.VerticalAlignment = VerticalAlignment.Center; At(root, footer, 2, 1); Grid.SetRowSpan(footer, 2);
            At(root, T(weather == null ? "—" : L("更新 ", "Updated ") + weather.RetrievedAt.LocalDateTime.ToString("HH:mm", CultureInfo.InvariantCulture), 10, "WidgetMuted"), row: 2);
        } else At(root, footer, row: 2);
        return root;
    }
    private static FrameworkElement ReferenceMusic(WidgetDefinition d, WidgetRenderData data)
    {
        var media = data.Media; var root = SampleBody(data); bool square = d.Size.Columns == 2;
        string title = string.IsNullOrWhiteSpace(media?.Title) ? L("尚未播放", "Nothing playing") : media.Title;
        string artist = string.IsNullOrWhiteSpace(media?.Artist) ? L("等待系统媒体会话", "Waiting for media") : media.Artist;
        var label = T(title, square ? 12 : 16, bold: true); label.ToolTip = title;
        var subtitle = T(artist, square ? 10 : 11, "WidgetMuted"); subtitle.ToolTip = artist;
        label.Margin = new Thickness(0, 0, 0, 4);
        var meta = Stack(label, subtitle); meta.VerticalAlignment = VerticalAlignment.Top;
        var controls = Transport(data, compact: true); controls.Margin = new Thickness(0);
        controls.HorizontalAlignment = square ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        foreach (Button button in ((StackPanel)controls).Children) {
            bool primary = AutomationProperties.GetAutomationId(button) == "widget-music-toggle";
            button.Width = button.Height = primary ? 36 : 32;
            button.Margin = new Thickness(0, 0, square ? 4 : 8, 0);
            if (primary) {
                var icon = (Grid)button.Content;
                button.Content = null;
                foreach (Shape shape in icon.Children) shape.SetResourceReference(Shape.FillProperty, "WidgetBackground");
                var circle = SamplePlate(icon, 18, "WidgetAccent"); circle.Width = circle.Height = 36;
                button.Content = circle; button.Padding = new Thickness(0);
            }
        }
        var last = ((StackPanel)controls).Children[((StackPanel)controls).Children.Count - 1] as FrameworkElement;
        if (last != null) last.Margin = new Thickness(0);
        if (square) {
            root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new() { Height = new GridLength(35) }); root.RowDefinitions.Add(new() { Height = new GridLength(36) });
            At(root, Sleeve(data, 72)); At(root, meta, row: 1); At(root, controls, row: 2);
        } else {
            root.ColumnDefinitions.Add(new() { Width = new GridLength(148) }); root.ColumnDefinitions.Add(new() { Width = new GridLength(12) });
            root.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            At(root, Sleeve(data, 148));
            var info = new Grid(); info.RowDefinitions.Add(new() { Height = new GridLength(42) });
            info.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); info.RowDefinitions.Add(new() { Height = new GridLength(38) });
            At(info, meta); At(info, controls, row: 1); At(info, MusicTimeline(data, reference: true), row: 2); At(root, info, 2);
        }
        return root;
    }
    private static FrameworkElement Sleeve(WidgetRenderData data,double size)=>new WidgetRecordPlayer(data,size,sleeve:true);
}

// Shapes are generated from elementary geometry on our own 19×19 grid, rather than copied pixel arrays or a font.
internal sealed class WidgetSampleWeatherIcon : FrameworkElement
{
    private readonly int? code;
    private readonly bool day;
    private static readonly DependencyProperty InkProperty = DependencyProperty.Register("Ink", typeof(Brush), typeof(WidgetSampleWeatherIcon), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty AccentProperty = DependencyProperty.Register("Accent", typeof(Brush), typeof(WidgetSampleWeatherIcon), new FrameworkPropertyMetadata(Brushes.Teal, FrameworkPropertyMetadataOptions.AffectsRender));
    internal WidgetSampleWeatherIcon(int? weatherCode, bool isDay) {
        code = weatherCode; day = isDay;
        SetResourceReference(InkProperty, "WidgetInk"); SetResourceReference(AccentProperty, "WidgetAccent");
        AutomationProperties.SetName(this, code is {} c ? WidgetWeatherService.Description(c) : "—");
    }
    protected override void OnRender(DrawingContext dc)
    {
        var ink = (Brush)GetValue(InkProperty);
        var accent = (Brush)GetValue(AccentProperty);
        bool fog = code is 45 or 48, snow = code is 71 or 73 or 75 or 77 or 85 or 86, storm = code >= 95;
        bool rain = !snow && (code is >= 51 and <= 67 or >= 80 and <= 82), clear = code is 0 or 1;
        double pitch = Math.Min(ActualWidth, ActualHeight) / 19, dot = pitch * .68;
        for (int y = 0; y < 19; y++) for (int x = 0; x < 19; x++) {
            double sx = x - (clear ? 9 : 12), sy = y - (clear ? 9 : 5), radial = Math.Sqrt(sx * sx + sy * sy);
            bool sun = day && code is (0 or 1 or 2) && (Math.Abs(radial - 3.3) < .65 || radial is > 5 and < 6.3 && (Math.Abs(sx) < .5 || Math.Abs(sy) < .5 || Math.Abs(Math.Abs(sx) - Math.Abs(sy)) < .5));
            bool moon = !day && clear && radial < 5 && Math.Pow(sx - 2.2, 2) + Math.Pow(sy + 1.8, 2) > 21;
            bool Cloud(int cx, int cy) => Math.Pow((cx - 8) / 5.5, 2) + Math.Pow((cy - 10) / 3d, 2) < 1
                || Math.Pow(cx - 7, 2) + Math.Pow(cy - 8, 2) < 10 || Math.Pow(cx - 12, 2) + Math.Pow(cy - 9, 2) < 10;
            bool cloud = !clear && !fog && code != null && Cloud(x, y) && (!Cloud(x - 1, y) || !Cloud(x + 1, y) || !Cloud(x, y - 1) || !Cloud(x, y + 1));
            if (!clear && Cloud(x, y)) sun = false;
            bool precipitation = rain && y is 14 or 15 or 16 && (x + y) % 4 == 0
                || snow && y is 15 or 17 && x is 5 or 9 or 13 || storm && y >= 13 && y <= 17 && x == 11 - (y - 13) / 2;
            bool mist = fog && y is 6 or 9 or 12 && x >= 3 + (y % 2) && x <= 16 - (y % 3);
            if (sun || moon || cloud || precipitation || mist)
                dc.DrawRectangle(sun || precipitation ? accent : ink, null, new Rect((x + .5) * pitch - dot / 2, (y + .5) * pitch - dot / 2, dot, dot));
        }
    }
}
