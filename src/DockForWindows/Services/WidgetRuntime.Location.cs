using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using DockForWindows.Models;

namespace DockForWindows.Services;

internal sealed partial class WidgetRuntime
{
    private WidgetLocation? _location;
    private string? _locationError;
    private Task? _locating;
    private DateTime _nextLocation;
    internal async Task RefreshWeatherAsync(WidgetSource source)
    {
        if(source.UseCurrentLocation)await LocateAsync();if(_disposed)return;
        var resolved=ResolvedSource(source);
        if(resolved.Latitude is double lat&&resolved.Longitude is double lon){WidgetWeatherService.Invalidate(lat,lon);string key=Key(resolved);if(_requests.Add(key))await ReadWeather(key,lat,lon);}
    }
    internal Task LocateAsync(){if(_locating is {IsCompleted:false})return _locating;return _locating=LocateCoreAsync();}
    private async Task LocateCoreAsync()
    {
        try{
            var access=await Geolocator.RequestAccessAsync().AsTask(_lifetime.Token);
            if(access!=GeolocationAccessStatus.Allowed)throw new UnauthorizedAccessException(LocalizedText.Get("定位权限未开启，请在 Windows 隐私设置中允许位置访问，或手动选择城市。"));
            var locator=new Geolocator{DesiredAccuracy=PositionAccuracy.Default};
            var position=await locator.GetGeopositionAsync(TimeSpan.FromMinutes(5),TimeSpan.FromSeconds(20)).AsTask(_lifetime.Token);
            var coordinate=position.Coordinate.Point.Position;
            if(!_disposed){_location=new WidgetLocation(LocalizedText.Get("当前位置"),"",coordinate.Latitude,coordinate.Longitude);_locationError=null;_nextLocation=DateTime.Now.AddMinutes(15);}
        }catch(OperationCanceledException){}catch(Exception ex){if(!_disposed){_locationError=LocalizedText.Get("定位失败：")+ex.Message;_nextLocation=DateTime.Now.AddMinutes(5);}}
        finally{_locating=null;if(!_disposed)Changed?.Invoke(WidgetKind.Weather);}
    }
    internal WidgetSource ResolvedSource(WidgetSource source)
    {
        if(!source.UseCurrentLocation)return source;
        if(_nextLocation<=DateTime.Now&&(_locating==null||_locating.IsCompleted))_ =LocateAsync();
        var result=source.Copy();result.City=LocalizedText.Get("当前位置");result.Latitude=_location?.Latitude;result.Longitude=_location?.Longitude;return result;
    }
}
