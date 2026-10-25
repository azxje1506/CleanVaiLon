# Kiểm tra bộ luật "xoá được?" trên các đường dẫn mẫu. Không đụng tới tệp thật.
# Thoát với mã 1 nếu có đường dẫn bị xếp sai mức.
param([string]$Tool = (Join-Path (Split-Path -Parent $PSScriptRoot) 'CleanVaiLon.ps1'))

. $Tool -NoGui
$sd = $env:SystemDrive
$cases = @(
    @("$sd\", 'system'), @("$sd\pagefile.sys", 'system'), @('D:\pagefile.sys', 'system'), @("$sd\System Volume Information", 'system'),
    @("$env:WINDIR", 'system'), @("$env:WINDIR\System32\drivers", 'system'), @("$env:WINDIR\WinSxS", 'system'),
    @("$env:WINDIR\Installer", 'keep'), @("$env:WINDIR\Temp", 'safe'), @("$env:WINDIR\SoftwareDistribution\Download", 'safe'),
    @("$sd\Users", 'system'), @("$env:USERPROFILE", 'system'), @("$env:USERPROFILE\AppData", 'system'),
    @("$env:TEMP", 'safe'), @("$env:TEMP\abc.tmp", 'safe'), @("$env:LOCALAPPDATA\NVIDIA\DXCache", 'safe'),
    @("$env:LOCALAPPDATA\pip\cache", 'safe'), @("$env:LOCALAPPDATA\npm-cache", 'safe'), @("$env:LOCALAPPDATA\CrashDumps", 'safe'),
    @("$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Cache", 'safe'),
    @("$env:LOCALAPPDATA\Google\Chrome\User Data\Default\Code Cache", 'safe'),
    @('D:\Steam\steamapps\workshop\downloads', 'safe'), @('D:\Steam\steamapps\workshop\content\431960', 'review'),
    @('D:\Steam\steamapps\common\SomeGame', 'keep'), @('D:\Games\Riot Games\VALORANT', 'keep'),
    @('D:\Games\SomeGame [FitGirl Repack]', 'review'), @('D:\Games\SomeGame [FitGirl Repack]\fg-01.bin', 'review'),
    @("$env:USERPROFILE\Documents\My Games\SomeGame\Saves", 'keep'), @("$env:USERPROFILE\Documents", 'keep'),
    @("$env:USERPROFILE\Downloads\project.zip", 'review'), @("$env:USERPROFILE\Downloads\notes.txt", 'review'),
    @("$env:ProgramFiles\Adobe", 'keep'), @("${env:ProgramFiles(x86)}\Steam", 'keep'), @("$env:ProgramData\NVIDIA Corporation", 'keep'),
    @("$env:APPDATA\Figma", 'keep'), @('D:\Projects\client\banner.psd', 'keep'), @('D:\Projects\web\node_modules', 'review'),
    @('D:\Projects\web\.git', 'keep'), @('D:\Windows.iso', 'review'), @('D:\Tools\setup_x64.exe', 'review'), @('D:\Tools\App\app.exe', 'keep'),
    @('D:\Video\clip.crdownload', 'safe'), @('D:\random\data.xyz', 'review')
)
$bad = 0
foreach ($c in $cases) {
    $s = Get-DeleteSafety $c[0]
    $ok = $s.Level -eq $c[1]
    if (-not $ok) { $bad++ }
    '{0} {1,-7} {2}{3}' -f $(if ($ok) { 'ok ' } else { 'SAI' }), $s.Level, $c[0], $(if (-not $ok) { "   (mong đợi $($c[1]))" })
}
$blocked = (Move-ToRecycleBin "$env:WINDIR\System32\drivers" 'Item').Result
"Thử xoá mục hệ thống: $blocked"
if ($blocked -ne 'Bị chặn') { $bad++ }
"$bad / $($cases.Count + 1) sai"
if ($bad) { exit 1 }
