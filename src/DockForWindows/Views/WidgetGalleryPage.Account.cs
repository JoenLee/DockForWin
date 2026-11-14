using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using DockForWindows.Services;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal sealed partial class WidgetGalleryPage
{
    private FrameworkElement MicrosoftAccountEditor(string category,Action<bool> change)
    {
        var body=new StackPanel{Margin=new Thickness(0,8,0,0)};
        var use=new CheckBox{Content=category=="notes"?"OneNote":"Microsoft To Do",Margin=new Thickness(0,4,0,4)};
        System.Windows.Automation.AutomationProperties.SetAutomationId(use,"widget-microsoft-source-"+category);
        use.Checked+=(_,_)=>change(true);use.Unchecked+=(_,_)=>change(false);body.Children.Add(use);
        use.Loaded+=(_,_)=>use.IsChecked=Source(Carousel(category).Hosts[Carousel(category).SelectedIndex].Definition).MicrosoftAccount;
        Carousel(category).SelectionChanged+=_=>use.IsChecked=Source(Carousel(category).Hosts[Carousel(category).SelectedIndex].Definition).MicrosoftAccount;
        var configure=ActionButton(UiText.IsEnglish?"Microsoft account…":"Microsoft 账户…",()=>OpenMicrosoftAccount(),"widget-microsoft-account-"+category);body.Children.Add(configure);return body;
    }
    private void OpenMicrosoftAccount()
    {
        if(_runtime==null)return;var service=_runtime.Microsoft;
        var dialog=new Window{Title=UiText.IsEnglish?"Microsoft account":"Microsoft 账户",Width=460,Height=440,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=Window.GetWindow(this)};
        var body=new StackPanel{Margin=new Thickness(22)};dialog.Content=body;
        body.Children.Add(new TextBlock{Text=UiText.IsEnglish?"Connect Microsoft To Do and OneNote":"连接 Microsoft To Do 和 OneNote",FontSize=18,FontWeight=FontWeights.SemiBold});
        body.Children.Add(new TextBlock{Text=UiText.IsEnglish?"Application (client) ID":"应用程序（客户端）ID",Margin=new Thickness(0,18,0,6)});
        var client=new TextBox{Text=service.ClientId,MinHeight=30};body.Children.Add(client);
        var status=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,BorderThickness=new Thickness(0),Background=System.Windows.Media.Brushes.Transparent,MinHeight=110,Margin=new Thickness(0,12,0,8),
            Text=service.Connected?(UiText.IsEnglish?"Signed in. Data refreshes every minute.":"已登录，数据每分钟更新。"):(UiText.IsEnglish?"Register a public client app, enable device code flow, then enter its client ID. No client secret is needed.":"注册公共客户端应用并启用设备代码登录，然后填写客户端 ID。无需客户端密钥。")};body.Children.Add(status);
        var link=new Button{Content=UiText.IsEnglish?"Open Microsoft sign-in":"打开 Microsoft 登录",Visibility=Visibility.Collapsed,MinHeight=30};body.Children.Add(link);
        using var cancellation=new CancellationTokenSource();bool busy=false;string verification="";
        link.Click+=(_,_)=>{if(verification.Length>0)try{Process.Start(new ProcessStartInfo(verification){UseShellExecute=true});}catch(Exception ex){status.Text=ex.Message;}};
        var login=new Button{Content=UiText.IsEnglish?"Sign in":"登录",MinHeight=30,Margin=new Thickness(0,10,0,0)};body.Children.Add(login);
        var logout=new Button{Content=UiText.IsEnglish?"Sign out":"退出账户",MinHeight=30,Margin=new Thickness(0,8,0,0)};body.Children.Add(logout);
        login.Click+=async(_,_)=>{
            if(busy)return;busy=true;login.IsEnabled=logout.IsEnabled=false;status.Text=UiText.IsEnglish?"Requesting sign-in code…":"正在申请登录代码…";
            try{await service.SignInAsync(client.Text,(code,url)=>{verification=url;link.Visibility=Visibility.Visible;status.Text=UiText.IsEnglish?$"Open {url}\nEnter code: {code}\nWaiting for authorization…":$"打开 {url}\n输入代码：{code}\n等待账户授权…";},cancellation.Token);
                if(!cancellation.IsCancellationRequested){status.Text=UiText.IsEnglish?"Signed in. Enable Microsoft To Do / OneNote in the data source and save settings.":"登录成功。请在数据源中勾选 Microsoft To Do / OneNote，并保存设置。";link.Visibility=Visibility.Collapsed;service.Refresh();}
            }catch(OperationCanceledException){}catch(Exception ex){if(!cancellation.IsCancellationRequested)status.Text=ex.Message;}
            finally{busy=false;if(!cancellation.IsCancellationRequested)login.IsEnabled=logout.IsEnabled=true;}
        };
        logout.Click+=(_,_)=>{service.SignOut();status.Text=UiText.IsEnglish?"Signed out. Cached cloud data cleared.":"已退出，云端数据缓存已清除。";link.Visibility=Visibility.Collapsed;};
        dialog.Closed+=(_,_)=>cancellation.Cancel();dialog.ShowDialog();
    }
}
