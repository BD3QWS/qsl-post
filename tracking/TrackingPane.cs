using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace QslPost.TrackingView
{
    public sealed class TrackingPane : UserControl
    {
        readonly WebView2 web=new WebView2{Dock=DockStyle.Fill};

        public TrackingPane() {Controls.Add(web);}

        public async void Open(string cacheDirectory,string url,Action<string> status)
        {
            try
            {
                if(!Allowed(url))throw new ArgumentException("无效的 17TRACK 地址。");
                Directory.CreateDirectory(cacheDirectory);
                var environment=await CoreWebView2Environment.CreateAsync(null,cacheDirectory);
                if(IsDisposed)return;
                await web.EnsureCoreWebView2Async(environment);
                if(IsDisposed)return;
                var engine=web.CoreWebView2;
                engine.Settings.AreHostObjectsAllowed=false;
                engine.Settings.IsWebMessageEnabled=false;
                engine.Settings.AreDevToolsEnabled=false;
                engine.Settings.AreBrowserAcceleratorKeysEnabled=false;
                engine.NavigationStarting+=(s,e)=>{if(!Allowed(e.Uri))e.Cancel=true;};
                engine.NewWindowRequested+=(s,e)=>{e.Handled=true;if(Allowed(e.Uri))engine.Navigate(e.Uri);};
                engine.DownloadStarting+=(s,e)=>e.Cancel=true;
                engine.PermissionRequested+=(s,e)=>e.State=CoreWebView2PermissionState.Deny;
                engine.NavigationCompleted+=(s,e)=>{
                    if(!IsDisposed)status(e.IsSuccess?"":"页面未加载。可在浏览器打开。");
                };
                engine.Navigate(url);
            }
            catch(Exception)
            {
                if(!IsDisposed)status("内置网页打不开。请用浏览器查询。");
            }
        }

        static bool Allowed(string value)
        {
            Uri url;
            return Uri.TryCreate(value,UriKind.Absolute,out url)
                &&url.Scheme==Uri.UriSchemeHttps&&url.IsDefaultPort&&url.UserInfo.Length==0
                &&(url.Host=="t.17track.net"||url.Host=="www.17track.net"||url.Host=="17track.net");
        }
    }
}
