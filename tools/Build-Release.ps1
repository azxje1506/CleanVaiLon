# Đóng gói một bản phát hành vào thư mục dist\:
#   CleanVaiLon-Setup-<phiên bản>.exe   bộ cài (Inno Setup)
#   CleanVaiLon-<phiên bản>-portable.zip bản giải nén là chạy
#   SHA256SUMS                           mã băm để kiểm tra tệp tải về
# Phiên bản lấy từ $AppVersion trong CleanVaiLon.ps1.
param(
    [string]$Iscc,        # đường dẫn ISCC.exe; để trống thì tự tìm
    [switch]$SkipInstaller
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Get-Content (Join-Path $root 'CleanVaiLon.ps1') -Raw -Encoding UTF8
$ver = [regex]::Match($src, "\`$AppVersion = '([^']+)'").Groups[1].Value
if (-not $ver) { throw 'Không đọc được $AppVersion trong CleanVaiLon.ps1' }
"Phiên bản: $ver"

$dist = Join-Path $root 'dist'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item $dist -ItemType Directory | Out-Null

# Bản portable
$stage = Join-Path $dist "CleanVaiLon-$ver"
New-Item $stage -ItemType Directory | Out-Null
foreach ($f in 'CleanVaiLon.ps1', 'CleanVaiLon.bat', 'CleanVaiLon.ico', 'CreateShortcut.ps1', 'LICENSE', 'README.md') {
    Copy-Item (Join-Path $root $f) $stage
}
@"
Clean VaiLon $ver – ban portable

1. Giai nen vao mot thu muc co dinh (vi du D:\Tools\CleanVaiLon).
2. Mo PowerShell trong thu muc do va chay mot lan:
       Get-ChildItem -Recurse | Unblock-File
   (bo dau "tai tu Internet" de Windows khong chan script)
3. Bam dup CleanVaiLon.bat, chon Yes khi Windows hoi quyen quan tri.
4. Tuy chon: tao loi tat co icon
       powershell -ExecutionPolicy Bypass -File .\CreateShortcut.ps1

Huong dan day du: https://github.com/azxje1506/CleanVaiLon/blob/main/docs/user-guide.md
"@ | Set-Content (Join-Path $stage 'READ FIRST.txt') -Encoding ASCII
$zip = Join-Path $dist "CleanVaiLon-$ver-portable.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Remove-Item $stage -Recurse -Force
"Đã tạo: $zip"

# Bộ cài
if (-not $SkipInstaller) {
    if (-not $Iscc) {
        $Iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") |
            Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (-not $Iscc) { throw 'Không tìm thấy ISCC.exe (Inno Setup 6). Cài bằng: winget install JRSoftware.InnoSetup' }
    & $Iscc /Q "/DMyAppVersion=$ver" (Join-Path $root 'installer\CleanVaiLon.iss')
    if ($LASTEXITCODE -ne 0) { throw "ISCC lỗi (mã $LASTEXITCODE)" }
    "Đã tạo: " + (Join-Path $dist "CleanVaiLon-Setup-$ver.exe")
}

# Mã băm
$sums = Get-ChildItem $dist -File | Where-Object Extension -in '.exe', '.zip' | Sort-Object Name | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name
}
[IO.File]::WriteAllText((Join-Path $dist 'SHA256SUMS'), (($sums -join "`n") + "`n"), (New-Object Text.UTF8Encoding($false)))
"Đã tạo: SHA256SUMS"
Get-ChildItem $dist | Select-Object Name, @{ n = 'KB'; e = { [math]::Round($_.Length / 1KB) } } | Format-Table -AutoSize | Out-String
