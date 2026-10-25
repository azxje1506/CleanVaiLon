# Đóng gói một bản phát hành vào thư mục dist\:
#   CleanVaiLon-Setup-<phiên bản>.exe   bộ cài (Inno Setup)
#   CleanVaiLon-<phiên bản>-portable.zip bản giải nén là chạy
#   SHA256SUMS                           mã băm để kiểm tra tệp tải về
# Phiên bản lấy từ <Version> trong Directory.Build.props. CleanVaiLon.exe được build bằng dotnet publish.
param(
    [string]$Iscc,        # đường dẫn ISCC.exe; để trống thì tự tìm
    [string]$Dotnet = 'dotnet',
    [switch]$SkipInstaller
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props') -Raw -Encoding UTF8
$ver = "$($props.Project.PropertyGroup.Version)".Trim()
if (-not $ver) { throw 'Không đọc được <Version> trong Directory.Build.props' }
"Phiên bản: $ver"

# CleanVaiLon.exe
$publish = Join-Path $root 'publish'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
& $Dotnet publish (Join-Path $root 'src\CleanVaiLon\CleanVaiLon.csproj') -c Release -o $publish --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish lỗi (mã $LASTEXITCODE)" }
$exe = Join-Path $publish 'CleanVaiLon.exe'
if (-not (Test-Path $exe)) { throw "Không thấy $exe" }
$exeVer = (Get-Item $exe).VersionInfo.ProductVersion
if (-not $exeVer.StartsWith($ver)) { throw "CleanVaiLon.exe có phiên bản $exeVer, không khớp $ver" }
"Đã build: $exe"

$dist = Join-Path $root 'dist'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item $dist -ItemType Directory | Out-Null

# Bản portable
$stage = Join-Path $dist "CleanVaiLon-$ver"
New-Item $stage -ItemType Directory | Out-Null
Copy-Item $exe $stage
foreach ($f in 'LICENSE', 'README.md') { Copy-Item (Join-Path $root $f) $stage }
@"
Clean VaiLon $ver – ban portable

1. Giai nen vao mot thu muc co dinh (vi du D:\Tools\CleanVaiLon).
2. Bam dup CleanVaiLon.exe, chon Yes khi Windows hoi quyen quan tri.
   Neu SmartScreen chan: More info -> Run anyway.
3. Tuy chon: tao loi tat bang chuot phai CleanVaiLon.exe > Send to > Desktop.

Can Windows 10/11 (.NET Framework 4.8 co san trong Windows).
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
