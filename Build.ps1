param([string]$OutputDir=(Join-Path $PSScriptRoot 'build'),[string]$FrameworkRefs='E:\Codex\2026-11-12\ui-win-11-win-11-windows\work\dependencies\net40\build\.NETFramework\v4.0',[string]$GuideExample=(Join-Path $PSScriptRoot 'docs\名址示例.xml'))
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if(!(Test-Path -LiteralPath (Join-Path $FrameworkRefs 'mscorlib.dll'))){throw 'Pass -FrameworkRefs pointing to Microsoft .NET Framework 4.0 reference assemblies.'}
$output=(New-Item -ItemType Directory -Path $OutputDir -Force).FullName
$refs=@('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms','System.Xml','System.Security','System.Web.Extensions')
$argsList=@('/nologo','/target:winexe','/platform:x86','/langversion:5','/subsystemversion:4.00','/noconfig','/nostdlib+','/optimize+','/codepage:65001',('/win32manifest:'+(Join-Path $PSScriptRoot 'src\app.manifest')),('/out:'+(Join-Path $output 'QslPost.exe')))
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'src\QslPost.ico')){$argsList+=('/win32icon:'+(Join-Path $PSScriptRoot 'src\QslPost.ico'))}
$argsList+=('/resource:'+(Join-Path $PSScriptRoot 'src\UpuForms.tsv')+',QslPost.UpuForms.tsv')
$argsList+=('/resource:'+(Join-Path $PSScriptRoot 'src\PostalLanguage.tsv')+',QslPost.PostalLanguage.tsv')
$argsList+=('/resource:'+(Join-Path $PSScriptRoot 'docs\使用说明.txt')+',QslPost.UserGuide.txt')
if($GuideExample){$argsList+=('/resource:'+(Resolve-Path -LiteralPath $GuideExample).Path+',QslPost.ManualExample.xml')}
$argsList+=('/resource:'+(Join-Path $PSScriptRoot 'src\FormSlots.tsv')+',QslPost.FormSlots.tsv')
$art=(Join-Path $output 'FormArt.resources')
$writer=New-Object System.Resources.ResourceWriter($art)
try {
    foreach($plate in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src\FormArt') -Filter '*.png') {
        $writer.AddResource($plate.BaseName,[IO.File]::ReadAllBytes($plate.FullName))
    }
} finally {$writer.Dispose()}
$argsList+=('/resource:'+$art+',QslPost.FormArt.resources')
foreach($name in $refs){$argsList+=('/r:'+(Join-Path $FrameworkRefs ($name+'.dll')))}
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
& $compiler @argsList @sources
if($LASTEXITCODE -ne 0){throw 'Compilation failed.'}
$cleanupArgs=@('/nologo','/target:winexe','/platform:x86','/langversion:5','/subsystemversion:4.00','/noconfig','/nostdlib+','/optimize+','/codepage:65001',
    ('/win32manifest:'+(Join-Path $PSScriptRoot 'src\app.manifest')),('/out:'+(Join-Path $output 'QslPost.DataCleanup.exe')))
foreach($name in @('mscorlib','System','System.Core','System.Xml')){$cleanupArgs+=('/r:'+(Join-Path $FrameworkRefs ($name+'.dll')))}
& $compiler @cleanupArgs (Join-Path $PSScriptRoot 'src\WorkspacePaths.cs') (Join-Path $PSScriptRoot 'installer\ClearUserData.cs')
if($LASTEXITCODE -ne 0){throw 'Data cleanup compilation failed.'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\App.config') -Destination (Join-Path $output 'QslPost.exe.config') -Force
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'docs\使用说明.txt')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\使用说明.txt') -Destination $output -Force}
& (Join-Path $PSScriptRoot 'tracking\BuildTracking.ps1') -OutputDir $output
Write-Output (Join-Path $output 'QslPost.exe')
