using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QslPost
{
    internal sealed class TrackingDialog : GlassForm
    {
        public TrackingDialog(Shipment shipment,string cacheDirectory,Action save)
        {
            string address=Tracker.WebUrl(shipment.Number);
            Text="17TRACK 免费查询";Width=1090;Height=790;MinimumSize=new Size(730,490);
            StartPosition=FormStartPosition.CenterParent;MinimizeBox=false;ShowInTaskbar=false;Padding=new Padding(18);
            var header=new SurfacePanel{Dock=DockStyle.Top,Height=58,BackColor=Color.Transparent};
            var title=Ui.Label(shipment.Number,15,true);title.Dock=DockStyle.Fill;header.Controls.Add(title);
            var tools=Ui.Toolbar();tools.Dock=DockStyle.Right;tools.Width=380;tools.AutoScroll=false;
            tools.Controls.Add(Ui.Button("保存进度",save,true));
            tools.Controls.Add(Ui.Button("复制单号",()=>Clipboard.SetText(shipment.Number),false));
            tools.Controls.Add(Ui.Button("在浏览器打开",()=>Process.Start(new ProcessStartInfo(address){UseShellExecute=true}),false));
            header.Controls.Add(tools);title.BringToFront();Controls.Add(header);
            var finish=Ui.Note("正在打开 17TRACK…");finish.Dock=DockStyle.Bottom;finish.Height=30;Controls.Add(finish);
            var host=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};Controls.Add(host);host.BringToFront();
            Shown+=(s,e)=>{
                try
                {
                    if(Environment.OSVersion.Version.Major<10||Convert.ToInt32(Registry.GetValue(
                       @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full","Release",0))<394802)
                        throw new NotSupportedException();
                    string plugin=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tracking","QslPost.TrackingView.dll");
                    Type type=Assembly.LoadFrom(plugin).GetType("QslPost.TrackingView.TrackingPane",true);
                    var web=(Control)Activator.CreateInstance(type);
                    web.Dock=DockStyle.Fill;host.Controls.Add(web);
                    type.GetMethod("Open").Invoke(web,new object[]{Path.Combine(cacheDirectory,"17track"),address,
                        (Action<string>)(text=>{finish.Text=text;finish.Visible=text.Length>0;})});
                }
                catch(Exception)
                {
                    finish.Text="内置查询需要 WebView2。请用浏览器打开。";
                }
            };
        }
    }
}
