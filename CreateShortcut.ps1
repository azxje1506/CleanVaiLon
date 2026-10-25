# Tạo lối tắt "Clean VaiLon" có icon trên Desktop và trong Start Menu.
# Chạy lại script này nếu bạn chuyển thư mục công cụ đi chỗ khác.
param([switch]$NoDesktop, [switch]$NoStartMenu)

$dir = Split-Path -Parent $PSCommandPath
$script = Join-Path $dir 'CleanVaiLon.ps1'
$icon = Join-Path $dir 'CleanVaiLon.ico'
if (-not (Test-Path $script)) { Write-Error "Không thấy $script"; exit 1 }

$targets = @()
if (-not $NoDesktop) { $targets += [Environment]::GetFolderPath('Desktop') }
if (-not $NoStartMenu) { $targets += Join-Path ([Environment]::GetFolderPath('Programs')) '' }

$ws = New-Object -ComObject WScript.Shell
foreach ($t in $targets) {
    $path = Join-Path $t 'Clean VaiLon.lnk'
    $lnk = $ws.CreateShortcut($path)
    $lnk.TargetPath = "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"
    $lnk.Arguments = "-NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Minimized -File `"$script`""
    $lnk.WorkingDirectory = $dir
    if (Test-Path $icon) { $lnk.IconLocation = "$icon,0" }
    $lnk.WindowStyle = 7
    $lnk.Description = 'Clean VaiLon – kiểm tra và dọn dẹp máy Windows'
    $lnk.Save()
    "Đã tạo: $path"
}
