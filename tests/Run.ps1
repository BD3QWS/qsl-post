param([string]$Root=(Join-Path $PSScriptRoot '..\work\tests'),[string]$FrameworkRefs='E:\Codex\2026-11-12\ui-win-11-win-11-windows\work\dependencies\net40\build\.NETFramework\v4.0')
$ErrorActionPreference='Stop'
$project=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
& (Join-Path $project 'Build.ps1') -FrameworkRefs $FrameworkRefs
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$refs=@('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms','System.Xml')
$argsList=@('/nologo','/target:exe','/platform:x86','/langversion:5','/subsystemversion:4.00','/noconfig','/nostdlib+','/codepage:65001',('/out:'+(Join-Path $project 'build\DomainTests.exe')),('/r:'+(Join-Path $project 'build\QslPost.exe')))
foreach($name in $refs){$argsList+=('/r:'+(Join-Path $FrameworkRefs ($name+'.dll')))}
& $compiler @argsList (Join-Path $PSScriptRoot 'DomainTests.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed.'}
& (Join-Path $project 'build\DomainTests.exe') $Root
if($LASTEXITCODE -ne 0){throw 'Domain tests failed.'}
