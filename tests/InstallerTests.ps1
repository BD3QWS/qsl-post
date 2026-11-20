param(
    [string]$Setup,
    [string]$TestRoot=(Join-Path $PSScriptRoot ('..\work\installer-check-'+[guid]::NewGuid().ToString('N').Substring(0,8))),
    [string]$Makensis='E:\Codex\2026-11-12\ui-win-11-win-11-windows\work\installer\nsis-3.13\makensis.exe'
)
$ErrorActionPreference='Stop'
$workspace=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$testPath=[IO.Path]::GetFullPath($TestRoot)
if(!$testPath.StartsWith($workspace+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
    (Test-Path -LiteralPath $testPath)){throw 'Choose a new test directory inside this workspace.'}
if(!$Setup){$Setup=Join-Path $workspace 'outputs\1.4.4\QSLPost-Setup-1.4.4.exe'}
$published=Get-Item -LiteralPath $Setup
$payload=Join-Path $workspace 'build\release'
$releaseApp=Join-Path $payload 'QslPost.exe'
$releaseVersion=(Get-Item -LiteralPath $releaseApp).VersionInfo.ProductVersion
$testKey='QslPost.InstallTest.'+[guid]::NewGuid().ToString('N')
$regPath='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\'+$testKey
$installPath=Join-Path $testPath 'installed'
$profilePath=Join-Path $testPath 'profile'
$defaultData=Join-Path $profilePath 'Data'
$customData=Join-Path $testPath 'custom-data'
$customCache=Join-Path $testPath 'custom-cache'
$manualBackup=Join-Path $testPath 'manual-backup.xml'
$exe=Join-Path $installPath 'QslPost.exe'
$uninstall=Join-Path $installPath 'Uninstall.exe'
$shortcut=Join-Path $testPath 'QSL 邮务台.lnk'
$menu=Join-Path $testPath 'StartMenu\QSL 邮务台.lnk'
$fixture=Join-Path $testPath 'setup-fixture.exe'
$checks=[Collections.Generic.List[string]]::new()

function Check-Qsl([bool]$ok,[string]$message){
    if(!$ok){throw $message}
    $checks.Add($message)
    Write-Output ('PASS '+$message)
}
function Run-QslSetup([string]$program){
    $process=Start-Process -FilePath $program -ArgumentList '/S' -WindowStyle Hidden -Wait -PassThru
    if($process.ExitCode -ne 0){throw ('Installer returned '+$process.ExitCode+' for '+$program)}
}
function Seed-QslProfile([switch]$Custom){
    $arguments=@('seed',$profilePath,$manualBackup)
    if($Custom){$arguments+=@($customData,$customCache)}
    & $probe @arguments
    if($LASTEXITCODE){throw 'Could not seed the test profile.'}
}

Check-Qsl ($published.VersionInfo.ProductVersion -eq $releaseVersion) 'published installer and application versions match'
New-Item -ItemType Directory -Path $testPath | Out-Null
$icon=Join-Path $workspace 'src\QslPost.ico'
& $Makensis /V2 /WX /INPUTCHARSET UTF8 ("/DPAYLOAD="+$payload) ("/DOUTPUT="+$fixture) ("/DICON="+$icon) `
    ("/DTEST_ROOT="+$testPath) ("/DTEST_ID="+$testKey) (Join-Path $workspace 'installer\QslPost.nsi')
if($LASTEXITCODE){throw 'Isolated installer build failed.'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$probe=Join-Path $testPath 'UninstallDataProbe.exe'
Copy-Item -LiteralPath $releaseApp -Destination $testPath
& $compiler /nologo /platform:x86 /codepage:65001 ('/out:'+$probe) ('/r:'+$releaseApp) (Join-Path $PSScriptRoot 'UninstallDataProbe.cs')
if($LASTEXITCODE){throw 'Installer probe compilation failed.'}

try {
    Run-QslSetup $fixture
    Check-Qsl ((Get-FileHash -LiteralPath $exe).Hash -eq (Get-FileHash -LiteralPath $releaseApp).Hash) 'isolated installation matches the release application'
    Check-Qsl ((Get-ItemProperty -LiteralPath $regPath).InstallLocation -eq $installPath) 'uninstall registration points to the isolated directory'
    $shell=New-Object -ComObject WScript.Shell
    Check-Qsl ((Test-Path -LiteralPath $menu) -and (Test-Path -LiteralPath $shortcut) -and
        $shell.CreateShortcut($shortcut).TargetPath -eq $exe) 'normal shortcuts are created inside the test directory'
    Seed-QslProfile
    $defaultDatabase=Join-Path $defaultData 'qsl-post.xml'
    $defaultHash=(Get-FileHash -LiteralPath $defaultDatabase).Hash
    Run-QslSetup $fixture
    Check-Qsl ((Get-FileHash -LiteralPath $defaultDatabase).Hash -eq $defaultHash) 'installing over an existing version keeps the ledger'

    Seed-QslProfile -Custom
    $customDatabase=Join-Path $customData 'qsl-post.xml'
    $customHash=(Get-FileHash -LiteralPath $customDatabase).Hash
    $manualHash=(Get-FileHash -LiteralPath $manualBackup).Hash
    $fileToKeep=Join-Path $customData 'unrelated.txt'
    $cacheFileToKeep=Join-Path $customCache 'unrelated.txt'
    Set-Content -LiteralPath $fileToKeep -Value 'Unrelated document.'
    Set-Content -LiteralPath $cacheFileToKeep -Value 'Unrelated document.'
    Set-Content -LiteralPath (Join-Path $customData '17track-token.dat') -Value 'Legacy test token.'
    $demoPath=Join-Path $profilePath 'Demo'
    New-Item -ItemType Directory -Path $demoPath | Out-Null
    Copy-Item -LiteralPath $customDatabase -Destination (Join-Path $demoPath 'qsl-post.xml')

    $cleaner=Join-Path $payload 'QslPost.DataCleanup.exe'
    $cleanupArgument='"'+$profilePath+'"'
    $lease=[IO.File]::Open((Join-Path $profilePath 'locations.lock'),'Open','ReadWrite','None')
    try {
        $child=Start-Process -FilePath $cleaner -ArgumentList $cleanupArgument -WindowStyle Hidden -Wait -PassThru
        Check-Qsl ($child.ExitCode -ne 0 -and (Get-FileHash -LiteralPath $customDatabase).Hash -eq $customHash) 'cleanup refuses a running profile before deleting records'
    } finally {$lease.Dispose()}

    $linkedFolder=Join-Path $testPath 'linked-folder'
    $demoCache=Join-Path $profilePath 'Demo.cache'
    New-Item -ItemType Directory -Path $linkedFolder,$demoCache | Out-Null
    $protectedFile=Join-Path $linkedFolder 'keep.txt'
    Set-Content -LiteralPath $protectedFile -Value 'File behind a directory junction.'
    $junction=Join-Path $demoCache '17track'
    New-Item -ItemType Junction -Path $junction -Target $linkedFolder | Out-Null
    try {
        $child=Start-Process -FilePath $cleaner -ArgumentList $cleanupArgument -WindowStyle Hidden -Wait -PassThru
        Check-Qsl ($child.ExitCode -ne 0 -and (Test-Path -LiteralPath $protectedFile) -and
            (Get-FileHash -LiteralPath $customDatabase).Hash -eq $customHash) 'cleanup stops before deleting records when a configured cache contains a junction'
    } finally {[IO.Directory]::Delete($junction)}

    Run-QslSetup $uninstall
    Check-Qsl (!(Test-Path -LiteralPath $exe) -and !(Test-Path -LiteralPath $regPath) -and
        !(Test-Path -LiteralPath $shortcut) -and !(Test-Path -LiteralPath $menu)) 'uninstall removes the program, registration and shortcuts'
    Check-Qsl (!(Test-Path -LiteralPath $profilePath) -and !(Test-Path -LiteralPath $customDatabase) -and
        !(Test-Path -LiteralPath ($customDatabase+'.bak')) -and !(Test-Path -LiteralPath (Join-Path $customData 'backups')) -and
        !(Test-Path -LiteralPath (Join-Path $customData '17track-token.dat')) -and
        !(Test-Path -LiteralPath (Join-Path $customCache '17track'))) 'uninstall clears default, custom and demo records, automatic backups, settings and cache'
    Check-Qsl ((Test-Path -LiteralPath $fileToKeep) -and (Test-Path -LiteralPath $cacheFileToKeep) -and
        (Get-FileHash -LiteralPath $manualBackup).Hash -eq $manualHash) 'uninstall preserves unrelated files and an independently saved backup'

    Run-QslSetup $fixture
    & $probe 'empty' $profilePath
    Check-Qsl ($LASTEXITCODE -eq 0) 'reinstall after uninstall opens an empty workspace with default settings'
    Run-QslSetup $uninstall
    Check-Qsl (!(Test-Path -LiteralPath $profilePath)) 'uninstall also clears a default-only profile'
} finally {
    if(Test-Path -LiteralPath $uninstall){Run-QslSetup $uninstall}
}

$checks | Set-Content -LiteralPath (Join-Path $testPath 'installer-results.txt') -Encoding utf8
Write-Output ($checks.Count.ToString()+' installer checks passed. '+$testPath)
