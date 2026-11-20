param([Parameter(Mandatory=$true)][int]$AppProcessId,[Parameter(Mandatory=$true)][string]$Output,[string]$Page='卡片台账')
$ErrorActionPreference='Stop'
$app=Get-Process -Id $AppProcessId
if($app.ProcessName -ne 'QslPost'){throw 'Capture target is not QslPost.'}
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class QslLiveCapture {
 [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;}
 public delegate bool EnumProc(IntPtr window,IntPtr state);
 [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr window,int command);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window,out Rect rect);
 [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent,EnumProc callback,IntPtr state);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr window,StringBuilder text,int max);
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
 public static bool Navigate(IntPtr parent,string target){IntPtr found=IntPtr.Zero;EnumChildWindows(parent,(w,s)=>{var b=new StringBuilder(256);GetWindowText(w,b,256);if(b.ToString()==target){found=w;return false;}return true;},IntPtr.Zero);if(found==IntPtr.Zero)return false;SendMessage(found,0xF5,IntPtr.Zero,IntPtr.Zero);return true;}
}
'@
$window=$app.MainWindowHandle
[QslLiveCapture]::ShowWindowAsync($window,9) | Out-Null
[QslLiveCapture]::SetForegroundWindow($window) | Out-Null
if($Page -and ![QslLiveCapture]::Navigate($window,$Page)){throw 'Requested app navigation button not found.'}
Start-Sleep -Milliseconds 400
if([QslLiveCapture]::GetForegroundWindow() -ne $window){throw 'Target window is not foreground; no desktop pixels were captured.'}
$rect=New-Object QslLiveCapture+Rect
if(![QslLiveCapture]::GetWindowRect($window,[ref]$rect)){throw 'Cannot locate target window.'}
$bitmap=New-Object Drawing.Bitmap ($rect.Right-$rect.Left),($rect.Bottom-$rect.Top)
$graphics=[Drawing.Graphics]::FromImage($bitmap)
try{$graphics.CopyFromScreen($rect.Left,$rect.Top,0,0,$bitmap.Size);$bitmap.Save([IO.Path]::GetFullPath($Output),[Drawing.Imaging.ImageFormat]::Png)}finally{$graphics.Dispose();$bitmap.Dispose()}
Write-Output ('Captured target app at '+$Output)
