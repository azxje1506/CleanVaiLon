# Chạy thử từng phần xử lý của Clean VaiLon mà không mở giao diện.
# Chỉ đọc thông tin, không thay đổi gì trên máy. Thoát với mã 1 nếu có phần lỗi.
param([string]$Tool = (Join-Path (Split-Path -Parent $PSScriptRoot) 'CleanVaiLon.ps1'))

. $Tool -NoGui
$ErrorActionPreference = 'Continue'
$script:Failed = 0
function T($name, [scriptblock]$b) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    try { $r = & $b; '{0,-26} OK   {1,5:N1}s  {2}' -f $name, $sw.Elapsed.TotalSeconds, $r }
    catch { $script:Failed++; '{0,-26} FAIL {1}' -f $name, $_.Exception.Message }
}
$sysDrive = $env:SystemDrive

T 'Get-PerfSnapshot'      { $p = Get-PerfSnapshot; "rows=$($p.Rows.Count) commit=$($p.CommitGB)/$($p.LimitGB) GB" }
T 'Get-StartupItems'      { $s = @(Get-StartupItems @()); "items=$($s.Count)" }
T 'Get-DriveSummary'      { (Get-DriveSummary | ForEach-Object { "$($_.Drive)=$(Format-Size $_.Free)" }) -join ' ' }
T 'Get-DownloadFolders'   { @(Get-DownloadFolders).Count }
T 'Get-SteamLibraries'    { @(Get-SteamLibraries).Count }
T 'Get-RecycleRoom'       { Format-Size (Get-RecycleRoom "$sysDrive\Users") }
T 'Get-BackupStatus'      { @(Get-BackupStatus).Count }
T 'Get-BrowserExtensions' { $e = @(Get-BrowserExtensions); "n=$($e.Count) high=" + @($e | Where-Object Level -eq 'Cao').Count }
T 'Get-SecurityReport'    { @(Get-SecurityReport).Count }
T 'Get-AppUsage'          { $a = @(Get-AppUsage); "apps=$($a.Count)" }
T "Invoke-DiskScan $sysDrive"      { $d = Invoke-DiskScan @($sysDrive); "files=$($d.FileCount) dirs=$($d.TopDirs.Count)" }
T "Find-DuplicateImages $sysDrive" { $x = Find-DuplicateImages @($sysDrive); "scanned=$($x.Scanned) dups=$($x.Rows.Count)" }
T 'Runspace'              {
    $ps = [powershell]::Create()
    [void]$ps.AddScript("param(`$A0, `$A1)`n" + $WorkerCode + "`n" + '[pscustomobject]@{ R = @(Get-StartupItems $A0) }')
    [void]$ps.AddArgument([string[]]@())
    $res = $ps.Invoke()
    if (-not $res) { throw 'không có kết quả' }
    "items=$(@($res[$res.Count - 1].R).Count)"
}

if ($script:Failed) { "$($script:Failed) phần lỗi"; exit 1 } else { 'Tất cả đều OK' }
