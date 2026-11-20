param([Parameter(Mandatory)][string]$OutputDir)
$ErrorActionPreference='Stop'
$dependencyRoot=Join-Path $PSScriptRoot '..\work\v13-deps'
function Get-Package([string]$Id,[string]$Version,[string]$Name,[string]$Expected) {
    $root=Join-Path $dependencyRoot $Name
    if(Test-Path -LiteralPath (Join-Path $root $Expected)){return $root}
    New-Item -ItemType Directory -Path $dependencyRoot -Force | Out-Null
    $package=Join-Path $dependencyRoot ($Name+'.nupkg')
    $versionPath=$Id+'/'+$Version+'/'+$Id+'.'+$Version+'.nupkg'
    Invoke-WebRequest -UseBasicParsing -Uri ('https://api.nuget.org/v3-flatcontainer/'+$versionPath) -OutFile $package
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $stage=Join-Path $dependencyRoot ($Name+'-'+[guid]::NewGuid().ToString('N'))
    [IO.Compression.ZipFile]::ExtractToDirectory($package,$stage)
    if(!(Test-Path -LiteralPath (Join-Path $stage $Expected))){throw ('NuGet package contents do not match '+$Id)}
    if(!(Test-Path -LiteralPath $root)){[IO.Directory]::Move($stage,$root)}
    else {$root=$stage}
    return $root
}
$sdk=Get-Package 'microsoft.web.webview2' '1.0.4314.35' 'webview2' 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'
$net=Get-Package 'microsoft.netframework.referenceassemblies.net462' '1.0.3' 'net462' 'build\.NETFramework\v4.6.2\mscorlib.dll'
$framework=Join-Path $net 'build\.NETFramework\v4.6.2'
$target=(New-Item -ItemType Directory -Path (Join-Path $OutputDir 'tracking') -Force).FullName
$managed=Join-Path $sdk 'lib\net462'
$dependencies=@((Join-Path $managed 'Microsoft.Web.WebView2.Core.dll'),
    (Join-Path $managed 'Microsoft.Web.WebView2.WinForms.dll'),
    (Join-Path $sdk 'runtimes\win-x86\native\WebView2Loader.dll'))
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$argsList=@('/nologo','/target:library','/platform:x86','/langversion:5','/noconfig','/nostdlib+','/optimize+','/codepage:65001',
    ('/out:'+(Join-Path $target 'QslPost.TrackingView.dll')))
foreach($name in @('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms')){
    $argsList+=('/r:'+(Join-Path $framework ($name+'.dll')))
}
$argsList+=$dependencies[0..1] | ForEach-Object { '/r:'+$_ }
& $compiler @argsList (Join-Path $PSScriptRoot 'TrackingPane.cs')
if($LASTEXITCODE){throw 'Tracking viewer compilation failed.'}
Copy-Item -LiteralPath $dependencies -Destination $target -Force
Copy-Item -LiteralPath (Join-Path $sdk 'LICENSE.txt'),(Join-Path $sdk 'NOTICE.txt') -Destination $target -Force
