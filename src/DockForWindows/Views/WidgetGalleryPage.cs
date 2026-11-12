using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

internal sealed partial class WidgetGalleryPage : ScrollViewer
{
    private readonly WidgetOptions _options;
    private readonly WidgetWeatherService _weatherService;
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource? _lifetime, _searchRequest, _weatherRequest;
    private WidgetWeatherSnapshot? _weather;
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1), _selected = DateTime.Today;
    private long _lastCalendarMinute;
    private DateTimeOffset _nextWeather = DateTimeOffset.MinValue;
    private readonly TextBlock _weatherStatus = Note("选择城市，查看实时天气与五日预报。"), _calendarStatus = Note("本应用日程 · 更改随设置保存，不读取 Windows 系统日历。 ");
    private readonly TextBox _city = new() { MaxLength = 100, MinWidth = 130 }, _eventTitle = new() { MaxLength = 120 }, _eventTime = new() { Text = "09:00", MaxLength = 5, Width = 64 };
    private readonly ComboBox _cities = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 7, 0, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly DatePicker _eventDate = new() { SelectedDate = DateTime.Today, Width = 132 };
    private readonly StackPanel _eventList = new();
    private readonly Button _searchCity;
    private readonly Button _refreshWeather;
    private readonly Expander _cityEditor;
    private readonly Border _weatherSection, _calendarSection;
    internal WidgetCarousel WeatherCarousel { get; }
    internal WidgetCarousel CalendarCarousel { get; }
    internal bool IsUpdating => _timer.IsEnabled;
    internal WidgetGalleryPage(WidgetOptions options, WidgetWeatherService? weatherService = null)
    {
        _options = options; options.Normalize(); _weatherService = weatherService ?? new();
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto; HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled; Focusable = false;
        AutomationProperties.SetAutomationId(this, "widget-gallery");
        var body = new StackPanel { Margin = new Thickness(28, 23, 28, 28) }; Content = body;
        var heading = new Grid(); heading.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); heading.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        heading.Children.Add(new TextBlock { Text = "小组件库", FontSize = 27, FontWeight = FontWeights.SemiBold });
        var badge = new Border { Background = WidgetBrushes.Get("#E3F2EE"), CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 4, 10, 4), VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "桌面灵感", FontSize = 10, Foreground = WidgetBrushes.Get("#246C65") } }; Grid.SetColumn(badge, 1); heading.Children.Add(badge); body.Children.Add(heading);
        body.Children.Add(Note("六个类别 · 多种模板与尺寸。选择外观和数据源，添加独立桌面实例。", new Thickness(0, 7, 0, 19)));
        var appearance = new WrapPanel { Margin = new Thickness(0, 0, 0, 20), Orientation = Orientation.Horizontal };
        var materials = new SettingsSegmentedControl { ItemsSource = new[] { "浅色", "深色", "玻璃" }, SelectedIndex = (int)options.Material, Width = 224, Margin = new Thickness(0, 0, 12, 8) };
        AutomationProperties.SetName(materials, "小组件材质"); AutomationProperties.SetAutomationId(materials, "widget-material"); appearance.Children.Add(materials);
        var curves = new ComboBox { ItemsSource = new[] { "柔和轮廓 · 2.5", "饱满轮廓 · 3" }, SelectedIndex = options.Exponent == 3 ? 1 : 0, Width = 154, Height = 34, Margin = new Thickness(0, 0, 0, 8) };
        AutomationProperties.SetName(curves, "超椭圆指数"); AutomationProperties.SetAutomationId(curves, "widget-exponent"); appearance.Children.Add(curves); body.Children.Add(appearance);
        WeatherCarousel = new(WidgetRegistry.Definitions.Where(d => d.Kind == WidgetKind.Weather).ToArray(), options.WeatherSelection, options.Exponent);
        CalendarCarousel = new(WidgetRegistry.Definitions.Where(d => d.Kind == WidgetKind.Calendar).ToArray(), options.CalendarSelection, options.Exponent);
        AutomationProperties.SetAutomationId(WeatherCarousel, "weather-carousel"); AutomationProperties.SetAutomationId(CalendarCarousel, "calendar-carousel");
        AutomationProperties.SetName(WeatherCarousel, "天气小组件，左右方向键切换，Home 和 End 跳到边界");
        AutomationProperties.SetName(CalendarCarousel, "日历小组件，左右方向键切换，Home 和 End 跳到边界");
        WeatherCarousel.SelectionChanged += index => options.WeatherSelection = index;
        CalendarCarousel.SelectionChanged += async index => { options.CalendarSelection = index; if(WeatherNeeded()&&_options.Latitude.HasValue&&_weatherRequest==null&&_nextWeather<=DateTimeOffset.Now)await RefreshWeatherAsync(); };
        var (weatherSurface, weatherBody) = Section("天气", "Weather", "Brightness"); _weatherSection = weatherSurface; body.Children.Add(weatherSurface);
        _searchCity = ActionButton("查找城市", async () => await SearchCitiesAsync(), "widget-city-search");
        _refreshWeather = ActionButton("刷新天气", async () => await RefreshWeatherAsync(), "widget-weather-refresh");
        var searchRow = new Grid(); searchRow.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); searchRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        AutomationProperties.SetAutomationId(_city, "widget-city-input"); AutomationProperties.SetName(_city, "城市名，例如北京或 Shanghai"); _city.ToolTip = "城市名，例如北京或 Shanghai";
        searchRow.Children.Add(_city); Grid.SetColumn(_searchCity, 1); _searchCity.Margin = new Thickness(8, 0, 0, 0); searchRow.Children.Add(_searchCity);
        var searchBody = new StackPanel { Margin = new Thickness(0, 10, 0, 6) }; searchBody.Children.Add(searchRow); searchBody.Children.Add(_cities);
        _cityEditor = new Expander { Header = options.City.Length == 0 ? "选择天气城市" : "城市 · " + options.City, Content = searchBody, IsExpanded = options.City.Length == 0, FontSize = 11 };
        AutomationProperties.SetAutomationId(_cityEditor, "widget-city-editor"); weatherBody.Children.Add(_cityEditor);
        _city.KeyDown += async (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; await SearchCitiesAsync(); } };
        _cities.SelectionChanged += async (_, _) => {
            if (_cities.SelectedItem is not WidgetLocation location) return;
            _options.City = location.Name; _options.Latitude = location.Latitude; _options.Longitude = location.Longitude;
            _weather = null; _cityEditor.Header = "城市 · " + location; _cityEditor.IsExpanded = false; _refreshWeather.IsEnabled = true; RenderWeather(); RenderCalendar(); await RefreshWeatherAsync();
        };
        weatherBody.Children.Add(TemplateHeading(WeatherCarousel)); weatherBody.Children.Add(WeatherCarousel); weatherBody.Children.Add(TemplateControls(WeatherCarousel,"weather")); AddPlacementButton(weatherBody, "weather"); weatherBody.Children.Add(BuildColorEditor(WeatherCarousel,"weather")); weatherBody.Children.Add(_weatherStatus);
        _refreshWeather.IsEnabled = options.Latitude.HasValue; _refreshWeather.HorizontalAlignment = HorizontalAlignment.Center;
        _refreshWeather.Margin = new Thickness(0, 5, 0, 0); weatherBody.Children.Add(_refreshWeather);
        var attribution = new TextBlock { FontSize = 10, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        attribution.SetResourceReference(TextBlock.ForegroundProperty, "SettingsMuted");
        var source = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("天气：Open-Meteo · 地点：GeoNames")) { NavigateUri = new Uri("https://open-meteo.com/") };
        source.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "SettingsAccent"); source.TextDecorations = null;
        source.RequestNavigate += (_, e) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); } catch (Exception ex) { _weatherStatus.Text = "打开数据来源失败：" + ex.Message; } e.Handled = true; };
        attribution.Inlines.Add(source); weatherBody.Children.Add(attribution);
        var (calendarSurface, calendarBody) = Section("日历", "Calendar", null); _calendarSection = calendarSurface; body.Children.Add(calendarSurface);
        var monthActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        monthActions.Children.Add(ActionButton("‹", () => MoveMonth(-1), "widget-month-previous"));
        monthActions.Children.Add(ActionButton("回到今天", () => SelectDate(DateTime.Today), "widget-month-today"));
        monthActions.Children.Add(ActionButton("›", () => MoveMonth(1), "widget-month-next")); calendarBody.Children.Add(monthActions);
        calendarBody.Children.Add(TemplateHeading(CalendarCarousel)); calendarBody.Children.Add(CalendarCarousel); calendarBody.Children.Add(TemplateControls(CalendarCarousel,"calendar")); AddPlacementButton(calendarBody, "calendar"); calendarBody.Children.Add(BuildColorEditor(CalendarCarousel,"calendar")); calendarBody.Children.Add(_calendarStatus);
        var eventBody = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        var dates = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) }; _eventDate.Margin = new Thickness(0, 0, 8, 4); _eventTime.Margin = new Thickness(0, 0, 8, 4); dates.Children.Add(_eventDate); dates.Children.Add(_eventTime);
        dates.Children.Add(Note("日期与时间", new Thickness(0, 7, 0, 0))); eventBody.Children.Add(dates);
        var eventInput = new Grid(); eventInput.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); eventInput.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); eventInput.Children.Add(_eventTitle);
        AutomationProperties.SetAutomationId(_eventTitle, "widget-event-title"); AutomationProperties.SetName(_eventTitle, "日程标题"); AutomationProperties.SetAutomationId(_eventTime, "widget-event-time"); AutomationProperties.SetName(_eventTime, "24小时制时间 HH:mm"); AutomationProperties.SetName(_eventDate, "日程日期");
        var add = ActionButton("添加日程", AddEvent, "widget-event-add"); add.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(add, 1); eventInput.Children.Add(add); eventBody.Children.Add(eventInput); eventBody.Children.Add(_eventList);
        var editor = new Expander { Header = "管理本应用日程", Content = eventBody, Margin = new Thickness(0, 10, 0, 0), FontSize = 11 };
        AutomationProperties.SetAutomationId(editor, "widget-calendar-editor"); calendarBody.Children.Add(editor);
        BuildAdditionalCategories(body);
        materials.SelectionChanged += (_, _) => { if (materials.SelectedIndex < 0) return; options.Material = (WidgetMaterial)materials.SelectedIndex; ApplyAppearance(); };
        curves.SelectionChanged += (_, _) => { options.Exponent = curves.SelectedIndex == 1 ? 3 : 2.5; ApplyAppearance(); };
        ApplyAppearance(); RenderWeather(); RenderCalendar(); RenderEvents();RefreshExtras(null);
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = UntilNextMinute() }; _timer.Tick += OnTick;
        Loaded += async (_, _) => { _lifetime?.Cancel(); _lifetime?.Dispose(); _lifetime = new(); StartRuntime(); _timer.Interval=UntilNextMinute(); _timer.Start(); RefreshCalendarIfNeeded(); if (_options.Latitude.HasValue && _nextWeather <= DateTimeOffset.Now && WeatherNeeded()) await RefreshWeatherAsync(); };
        Unloaded += (_, _) => { StopRuntime(); _timer.Stop(); _lifetime?.Cancel(); _lifetime?.Dispose(); _lifetime = null; _searchRequest?.Cancel(); _weatherRequest?.Cancel(); };
        ScrollChanged += async (_, _) => { RefreshCalendarIfNeeded(); if (IsLoaded && _lifetime != null && _weatherRequest == null && _nextWeather <= DateTimeOffset.Now && WeatherNeeded() && _options.Latitude.HasValue) await RefreshWeatherAsync(); };
    }
    private static TextBlock Note(string text, Thickness? margin = null)
    {
        var label = new TextBlock { Text = text, FontSize = 11, TextWrapping = TextWrapping.Wrap, LineHeight = 17, Margin = margin ?? new Thickness(0, 8, 0, 0) };
        label.SetResourceReference(TextBlock.ForegroundProperty, "SettingsMuted"); return label;
    }
    private static Button ActionButton(string title, Action action, string id)
    {
        var button = new Button { Content = title, Margin = new Thickness(4, 0, 4, 0) }; button.Click += (_, _) => action(); AutomationProperties.SetAutomationId(button, id); return button;
    }
    private static (Border Surface, StackPanel Body) Section(string title, string subtitle, string? glyph)
    {
        var body = new StackPanel(); var heading = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        FrameworkElement icon;
        if (glyph == null) { icon = WidgetIcons.Create("calendar", 22); icon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "SettingsAccent"); }
        else { var control = new ControlCenterIcon(glyph) { Width = 22, Height = 22 }; control.SetResourceReference(ControlCenterIcon.BrushProperty, "SettingsAccent"); icon = control; }
        icon.Margin = new Thickness(0, 0, 11, 0); heading.Children.Add(icon); heading.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(Note(subtitle, new Thickness(9, 6, 0, 0))); body.Children.Add(heading);
        var surface = new Border { Child = body, Padding = new Thickness(20, 20, 20, 19), Margin = new Thickness(0, 0, 0, 22) };
        surface.SetResourceReference(FrameworkElement.StyleProperty, "SettingsSurface"); return (surface, body);
    }
    private void AddPlacementButton(Panel body, string category)
    {
        var button = new Button { Content = "+ 添加到桌面", IsEnabled = true, ToolTip = "使用当前模板、尺寸、配色与数据源创建独立实例", Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(18, 7, 18, 7) };
        AutomationProperties.SetAutomationId(button, "widget-add-" + category); body.Children.Add(button);
        var status=Note("",new Thickness(0,5,0,0));body.Children.Add(status);
        button.Click+=async(_,_)=>{var carousel=Carousel(category);var host=carousel.Hosts[carousel.SelectedIndex];var instance=WidgetRegistry.CreateInstance(host.Definition.Id,_options.Material,_options);instance.Size=host.Definition.Size;instance.Source=Source(host.Definition).Copy();
            if(WidgetDesktopService.Current is not { } service){status.Text="此隔离预览未连接桌面服务；正式应用启动后可添加。";return;}
            button.IsEnabled=false;try{bool ok=await service.AddAsync(instance);status.Text=ok?"已添加到桌面 · 右键可调整设置与尺寸":"未添加，原有桌面保持不变。";}finally{button.IsEnabled=true;}};
    }
    private IEnumerable<WidgetHost> Hosts => WeatherCarousel.Hosts.Concat(CalendarCarousel.Hosts).Concat(_extraCarousels.Values.SelectMany(c=>c.Hosts));
    private void ApplyAppearance() { foreach (var host in Hosts) { host.SetExponent(_options.Exponent); host.SetMaterial(_options.Material,_options.CardColors.GetValueOrDefault(host.Definition.Id)); } foreach(var refresh in _colorEditors) refresh(); }
    private void RenderWeather() { foreach (var host in WeatherCarousel.Hosts) RenderHost(host); }
    private void RenderCalendar() { _lastCalendarMinute=DateTime.Now.Ticks/TimeSpan.TicksPerMinute; foreach (var host in CalendarCarousel.Hosts) RenderHost(host); }
    private static TimeSpan UntilNextMinute()=>TimeSpan.FromMilliseconds(60020-DateTime.Now.TimeOfDay.TotalMilliseconds%60000);
    private void RefreshCalendarIfNeeded(){if(_lastCalendarMinute!=DateTime.Now.Ticks/TimeSpan.TicksPerMinute&&OnScreen(_calendarSection))RenderCalendar();}
    private void SelectDate(DateTime date) { _selected = date.Date; _month = new(date.Year, date.Month, 1); _eventDate.SelectedDate = date.Date; RenderCalendar(); }
    private void MoveMonth(int direction)
    {
        if (direction < 0 && _month.Year == 1 && _month.Month == 1 || direction > 0 && _month.Year == 9999 && _month.Month == 12) return;
        SelectDate(_month.AddMonths(direction));
    }
    private void AddEvent()
    {
        if (_eventDate.SelectedDate is not DateTime date || !TimeOnly.TryParseExact(_eventTime.Text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) || string.IsNullOrWhiteSpace(_eventTitle.Text)) {
            _calendarStatus.Text = "请填写日期、24 小时制时间（HH:mm）和日程标题。"; return;
        }
        if (_options.Events.Count >= 200) { _calendarStatus.Text = "本应用日程已达 200 条，请先移除不需要的日程。"; return; }
        _options.Events.Add(new() { Start = date.Date.Add(time.ToTimeSpan()), Title = _eventTitle.Text.Trim() });
        _eventTitle.Clear(); SelectDate(date); RenderEvents(); _calendarStatus.Text = "已加入日程草稿；点击窗口底部保存后保留。";
    }
    private void RenderEvents()
    {
        _eventList.Children.Clear();
        foreach (var entry in _options.Events.OrderBy(e => e.Start)) {
            var row = new Grid { Margin = new Thickness(0, 8, 0, 0) }; row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            var text = Note(entry.Start.ToString("MM-dd HH:mm") + "  " + entry.Title, new Thickness(0)); text.TextTrimming = TextTrimming.CharacterEllipsis; text.TextWrapping = TextWrapping.NoWrap; text.ToolTip = entry.Title; row.Children.Add(text);
            var remove = ActionButton("移除", () => { _options.Events.Remove(entry); RenderEvents(); RenderCalendar(); _calendarStatus.Text = "移除已保留在草稿，保存后生效。"; }, "widget-event-remove-" + entry.Id);
            Grid.SetColumn(remove, 1); row.Children.Add(remove); _eventList.Children.Add(row);
        }
    }
    private bool OnScreen(FrameworkElement section)
    {
        if (!IsLoaded || !section.IsVisible || ActualHeight <= 0 || Window.GetWindow(this)?.WindowState == WindowState.Minimized) return false;
        return section.TransformToAncestor(this).TransformBounds(new Rect(section.RenderSize)).IntersectsWith(new Rect(RenderSize));
    }
    private bool WeatherNeeded()=>OnScreen(_weatherSection)||(OnScreen(_calendarSection)&&CalendarCarousel.Hosts[CalendarCarousel.SelectedIndex].Definition.Layout==WidgetLayout.CalendarWeather);
    private async void OnTick(object? sender, EventArgs e)
    {
        _timer.Interval=UntilNextMinute();RefreshCalendarIfNeeded();RefreshExtras(WidgetKind.Clock);
        if (_options.Latitude.HasValue && _weatherRequest == null && _nextWeather <= DateTimeOffset.Now && WeatherNeeded()) await RefreshWeatherAsync();
    }
    internal async Task SearchCitiesAsync()
    {
        if (_lifetime == null) return;
        _searchRequest?.Cancel(); var request = _searchRequest = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _searchCity.IsEnabled = false; _weatherStatus.Text = "正在查找城市…";
        try {
            var cities = await _weatherService.SearchAsync(_city.Text, request.Token);
            if (request.IsCancellationRequested || _searchRequest != request) return;
            _cities.ItemsSource = cities; _cities.SelectedIndex = -1; _cities.Visibility = cities.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            _weatherStatus.Text = cities.Count == 0 ? "未找到城市，请尝试完整名称或拼音。" : "请选择城市与地区；不使用设备自动定位。";
        }
        catch (OperationCanceledException) { if (!request.IsCancellationRequested) _weatherStatus.Text = "城市查询超时，请重试。"; }
        catch (Exception ex) { if (!request.IsCancellationRequested && _searchRequest == request) _weatherStatus.Text = "城市查询失败，请重试。" + ex.Message; }
        finally { if (_searchRequest == request) { _searchRequest = null; _searchCity.IsEnabled = true; } request.Dispose(); }
    }
    internal async Task RefreshWeatherAsync()
    {
        if (_lifetime == null || _options.Latitude is not double lat || _options.Longitude is not double lon) return;
        _weatherRequest?.Cancel(); var request = _weatherRequest = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _weatherStatus.Text = "正在更新 " + _options.City + " 的天气…"; _nextWeather = DateTimeOffset.Now.AddMinutes(5);
        try {
            var weather = await _weatherService.ReadAsync(lat, lon, request.Token);
            if (request.IsCancellationRequested || _weatherRequest != request) return;
            _weather = weather; _nextWeather = DateTimeOffset.Now.AddMinutes(15); RenderWeather(); RenderCalendar();
            _weatherStatus.Text = $"{_options.City} · {WidgetWeatherService.Description(weather.Code)} · 当地模型时间 {weather.ModelTime:MM-dd HH:mm} · 获取于 {weather.RetrievedAt:HH:mm}";
        }
        catch (OperationCanceledException) { if (!request.IsCancellationRequested) _weatherStatus.Text = "天气请求超时，请重新选择城市重试。"; }
        catch (Exception ex) {
            if (!request.IsCancellationRequested && _weatherRequest == request)
                _weatherStatus.Text = (_weather == null ? "天气暂不可用，请重试。" : $"更新失败，保留 { _weather.RetrievedAt:HH:mm} 获取的数据。") + ex.Message;
        }
        finally { if (_weatherRequest == request) _weatherRequest = null; request.Dispose(); }
    }
}
