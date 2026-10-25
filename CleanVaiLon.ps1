# Clean VaiLon – công cụ kiểm tra và dọn dẹp máy Windows.
# Không tự xoá gì: mọi thao tác thay đổi đều hỏi xác nhận, và chỉ chuyển vào Thùng rác.
param(
    [switch]$NoGui,         # chỉ nạp các hàm, không mở giao diện (dùng cho tests\)
    [switch]$NoElevate,     # không tự xin quyền quản trị
    [switch]$Relaunched,    # đã được mở lại với quyền quản trị
    [switch]$StartMinimized # mở sẵn ở dạng thu nhỏ (khi tự chạy lúc đăng nhập)
)

$ScriptPath = $PSCommandPath
$ScriptDir  = Split-Path -Parent $ScriptPath
$IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $NoGui -and -not $NoElevate -and -not $IsAdmin) {
    try {
        Start-Process powershell.exe -Verb RunAs -WindowStyle Minimized -ArgumentList "-NoProfile -ExecutionPolicy Bypass -STA -File `"$ScriptPath`" -Relaunched"
    } catch {
        Add-Type -AssemblyName System.Windows.Forms
        [void][Windows.Forms.MessageBox]::Show('Công cụ cần quyền quản trị để đọc đầy đủ thông tin. Bạn hãy bấm Yes khi Windows hỏi.', 'Clean VaiLon')
    }
    exit
}

# ---------------------------------------------------------------------------
# Phần xử lý. Đoạn mã này được nạp cả vào giao diện lẫn vào các luồng chạy nền.
# ---------------------------------------------------------------------------
$WorkerCode = @'
$ErrorActionPreference = 'SilentlyContinue'

if (-not ('MTC.Walker' -as [type])) {
Add-Type -TypeDefinition @"
using System; using System.IO; using System.Collections.Generic;
namespace MTC {
  public class FileHit { public string Path; public long Size; public DateTime Modified; }
  public class DirHit { public string Path; public long Size; public int Depth; }
  public class Walker {
    public List<FileHit> Files = new List<FileHit>();
    public List<DirHit> Dirs = new List<DirHit>();
    public long MinFile = 1048576;
    public int MaxDepth = 4;
    public HashSet<string> Ext = null;
    public string[] Skip = new string[0];
    public long Walk(DirectoryInfo d, int depth) {
      string low = d.FullName.ToLowerInvariant();
      foreach (var s in Skip) if (low.EndsWith(s)) return 0;
      long total = 0; FileSystemInfo[] items;
      try { items = d.GetFileSystemInfos(); } catch { return 0; }
      foreach (var it in items) {
        try {
          if ((it.Attributes & FileAttributes.ReparsePoint) != 0) continue;
          if ((it.Attributes & FileAttributes.Directory) != 0) { total += Walk((DirectoryInfo)it, depth + 1); continue; }
          var fi = (FileInfo)it; long len = fi.Length; total += len;
          if (len < MinFile) continue;
          if (Ext != null && !Ext.Contains(fi.Extension.ToLowerInvariant())) continue;
          Files.Add(new FileHit { Path = fi.FullName, Size = len, Modified = fi.LastWriteTime });
        } catch { }
      }
      if (depth <= MaxDepth) Dirs.Add(new DirHit { Path = d.FullName, Size = total, Depth = depth });
      return total;
    }
  }
}
"@
}

function Format-Size($b) {
    $b = [double]$b
    if ($b -ge 1GB) { return '{0:N1} GB' -f ($b / 1GB) }
    if ($b -ge 1MB) { return '{0:N0} MB' -f ($b / 1MB) }
    return '{0:N0} KB' -f ($b / 1KB)
}

function Get-PathSize([string]$path) {
    if (Test-Path -LiteralPath $path -PathType Leaf) { return (Get-Item -LiteralPath $path -Force).Length }
    if (-not (Test-Path -LiteralPath $path)) { return 0 }
    $w = New-Object MTC.Walker
    $w.MinFile = [long]::MaxValue
    $w.MaxDepth = -1
    return $w.Walk((New-Object IO.DirectoryInfo $path), 0)
}

# ----- Hiểu biết sẵn có về các chương trình phổ biến -----
$ProcessKB = @{
    'msedge' = 'Trình duyệt Microsoft Edge. Mỗi tab và tiện ích là một tiến trình riêng.'
    'msedgewebview2' = 'Thành phần hiển thị web mà nhiều ứng dụng dùng chung.'
    'chrome' = 'Trình duyệt Google Chrome.'
    'firefox' = 'Trình duyệt Firefox.'
    'memory compression' = 'Windows nén bớt RAM để tiết kiệm chỗ. Không phải virus. Số lớn nghĩa là máy đang thiếu RAM.'
    'svchost' = 'Tiến trình chủ chứa nhiều dịch vụ của Windows.'
    'explorer' = 'Giao diện Windows: thanh tác vụ, màn hình nền, File Explorer.'
    'dwm' = 'Vẽ cửa sổ và hiệu ứng của Windows.'
    'msmpeng' = 'Windows Defender đang quét virus.'
    'system' = 'Nhân Windows và driver phần cứng.'
    'figma' = 'Ứng dụng thiết kế Figma.'
    'claude' = 'Ứng dụng Claude.'
    'discord' = 'Ứng dụng chat Discord.'
    'steam' = 'Trình mở game Steam.'
    'steamwebhelper' = 'Phần hiển thị giao diện của Steam.'
    'signalrgb' = 'Phần mềm điều khiển đèn LED RGB.'
    'wallpaper32' = 'Wallpaper Engine – hình nền động.'
    'wallpaper64' = 'Wallpaper Engine – hình nền động.'
    'lghub' = 'Logitech G HUB – cài đặt chuột và bàn phím Logitech.'
    'lghub_agent' = 'Logitech G HUB chạy nền.'
    'nvcontainer' = 'Dịch vụ của driver NVIDIA.'
    'nvidia overlay' = 'Lớp phủ trong game của NVIDIA (quay màn hình, hiện FPS).'
    'searchhost' = 'Ô tìm kiếm của Windows.'
    'searchindexer' = 'Windows lập chỉ mục để tìm file nhanh.'
    'code' = 'Trình soạn code Visual Studio Code.'
    'notion' = 'Ứng dụng ghi chú Notion.'
    'telegram' = 'Ứng dụng chat Telegram.'
    'zalo' = 'Ứng dụng chat Zalo.'
    'spotify' = 'Ứng dụng nghe nhạc Spotify.'
    'obs64' = 'OBS Studio – quay màn hình, phát trực tiếp.'
    'parsecd' = 'Parsec – điều khiển máy và chơi game từ xa.'
    'everything' = 'Everything – tìm file tức thì.'
    'idman' = 'Internet Download Manager – trình tải file.'
    'ms-teams' = 'Microsoft Teams.'
    'teams' = 'Microsoft Teams.'
    'onedrive' = 'OneDrive – đồng bộ file lên mây.'
    'powershell' = 'Cửa sổ lệnh PowerShell (có thể là chính công cụ này).'
    'audiodg' = 'Xử lý âm thanh của Windows.'
    'wmiprvse' = 'Dịch vụ cung cấp thông tin hệ thống của Windows.'
    'runtimebroker' = 'Quản lý quyền cho ứng dụng của Windows.'
    'vmmem' = 'Máy ảo (WSL hoặc Hyper-V) đang chạy.'
    'vmmemwsl' = 'WSL (Linux trong Windows) đang chạy.'
    'node' = 'Node.js – chạy chương trình JavaScript.'
    'python' = 'Chương trình Python đang chạy.'
    'javaw' = 'Chương trình Java (thường là Minecraft).'
    'java' = 'Chương trình Java.'
    'riotclientservices' = 'Riot Client – trình mở game của Riot.'
    'valorant-win64-shipping' = 'Game VALORANT.'
    'league of legends' = 'Game Liên Minh Huyền Thoại.'
    'sguard64' = 'Chống gian lận của game Tencent / Garena.'
    'wiztree64' = 'WizTree – xem ổ đĩa đầy vì đâu.'
    'textinputhost' = 'Bàn phím ảo và bộ gõ của Windows.'
    'startmenuexperiencehost' = 'Menu Start của Windows.'
    'phoneexperiencehost' = 'Phone Link – kết nối điện thoại.'
    'flow.launcher' = 'Flow Launcher – thanh tìm kiếm nhanh.'
}

$StartupKB = @(
    @{ P = 'SecurityHealth';                         T = 'Nên giữ';    D = 'Biểu tượng Windows Security.' }
    @{ P = 'Realtek|RtkAud|Nahimic';                 T = 'Nên giữ';    D = 'Âm thanh của máy.' }
    @{ P = 'EVKey|UniKey|OpenKey';                   T = 'Nên giữ';    D = 'Bộ gõ tiếng Việt.' }
    @{ P = 'LGHUB|Logitech|logi_';                   T = 'Nên giữ';    D = 'Cài đặt chuột, bàn phím Logitech.' }
    @{ P = 'NVIDIA|NvContainer|NVDisplay';           T = 'Nên giữ';    D = 'Driver card đồ hoạ NVIDIA.' }
    @{ P = 'Everything';                             T = 'Nên giữ';    D = 'Tìm file tức thì, rất nhẹ.' }
    @{ P = 'Flow\.Launcher|PowerToys';               T = 'Nên giữ';    D = 'Tiện ích tìm kiếm nhanh.' }
    @{ P = 'Vanguard|vgtray|\bvgc\b';                T = 'Nên giữ';    D = 'Chống gian lận của Riot. Giữ nếu chơi VALORANT/LMHT.' }
    @{ P = 'OneDrive';                               T = 'Nên giữ';    D = 'Đồng bộ và sao lưu file lên mây.' }
    @{ P = 'GoogleUpdater|Google Update|edgeupdate'; T = 'Nên giữ';    D = 'Tự cập nhật trình duyệt (vá lỗi bảo mật).' }
    @{ P = 'Spooler';                                T = 'Nên giữ';    D = 'Dịch vụ in. Giữ nếu có máy in.' }
    @{ P = 'WSearch';                                T = 'Nên giữ';    D = 'Tìm kiếm của Windows.' }
    @{ P = 'WSLService';                             T = 'Nên giữ';    D = 'WSL – Linux trong Windows.' }
    @{ P = 'jhi_service|Intel|RstMw|WMIRegistration'; T = 'Nên giữ';   D = 'Dịch vụ của chipset Intel.' }
    @{ P = 'Claude|cowork';                          T = 'Nên giữ';    D = 'Thành phần của ứng dụng Claude.' }
    @{ P = 'ClickToRun|OfficeSvc';                   T = 'Nên giữ';    D = 'Chạy và cập nhật Microsoft Office.' }
    @{ P = 'GameInput';                              T = 'Nên giữ';    D = 'Thư viện tay cầm và bàn phím cho game của Microsoft.' }
    @{ P = 'SignalRgb';                              T = 'Có thể tắt'; D = 'Đèn LED RGB. Tốn nhiều RAM.' }
    @{ P = 'Wallpaper';                              T = 'Có thể tắt'; D = 'Hình nền động. Tốn RAM và card đồ hoạ.' }
    @{ P = 'IDMan|Internet Download';                T = 'Có thể tắt'; D = 'Trình tải file. Mở tay khi cần.' }
    @{ P = 'Figma.?Agent';                           T = 'Có thể tắt'; D = 'Chỉ cần khi dùng Figma trên trình duyệt.' }
    @{ P = 'Glary|CCleaner|SystemCare|IObit|Driver Booster|Wise Care'; T = 'Có thể tắt'; D = 'Phần mềm "dọn dẹp". Windows tự làm được.' }
    @{ P = 'Afterburner|RTSS|RivaTuner|EncoderServer'; T = 'Có thể tắt'; D = 'Ép xung, hiện FPS. Chỉ cần khi chơi game.' }
    @{ P = 'Magician|MigrationService';              T = 'Có thể tắt'; D = 'Quản lý ổ SSD Samsung. Không cần chạy thường trực.' }
    @{ P = 'UpdateChecker|Updater';                  T = 'Có thể tắt'; D = 'Chỉ kiểm tra bản mới.' }
    @{ P = 'TeamViewer|UltraView|AnyDesk|Parsec|pservice'; T = 'Có thể tắt'; D = 'Điều khiển máy từ xa. Không dùng thì tắt cho an toàn.' }
    @{ P = 'Teams|Skype|Zoom';                       T = 'Có thể tắt'; D = 'Họp trực tuyến. Mở tay khi cần.' }
    @{ P = 'MicrosoftEdgeAutoLaunch';                T = 'Có thể tắt'; D = 'Edge chạy sẵn ngầm.' }
    @{ P = 'uTorrent|BitTorrent|qBittorrent|\but\b'; T = 'Có thể tắt'; D = 'Tải torrent.' }
    @{ P = 'EpicGames|EADM|EA Desktop|EALauncher|Ubisoft|Battle\.net|GOG|Garena|gxx|Riot Client|Steam'; T = 'Có thể tắt'; D = 'Trình mở game. Mở tay khi chơi.' }
    @{ P = 'Adobe|AGMService|CCXProcess|Creative Cloud'; T = 'Có thể tắt'; D = 'Dịch vụ nền của Adobe.' }
    @{ P = 'Autodesk|AdskLicensing|FlexNet|FNPLicensing'; T = 'Có thể tắt'; D = 'Dịch vụ bản quyền. Chỉ cần khi dùng phần mềm đó.' }
    @{ P = 'Zalo|Telegram|Messenger|Discord|Slack|Notion|Spotify'; T = 'Có thể tắt'; D = 'Ứng dụng tự mở khi bật máy.' }
    @{ P = 'Cloudflare|WARP|VPN';                    T = 'Có thể tắt'; D = 'VPN. Bật tay khi cần.' }
    @{ P = 'Xbox';                                   T = 'Có thể tắt'; D = 'Dịch vụ Xbox.' }
    @{ P = 'Apple|iTunes|AMDS';                      T = 'Có thể tắt'; D = 'Kết nối iPhone với máy.' }
    @{ P = '\bASUS|Armoury|AcPower|\bROG\b';         T = 'Có thể tắt'; D = 'Phần mềm phụ của ASUS.' }
)

function Get-Advice([string]$name, [string]$cmd) {
    $text = "$name $cmd"
    if ($text -match '(?i)\b(wscript|cscript|mshta)\b|\.vbs\b|powershell[^|]*-w(indowstyle)?\s+h|\bnode(\.exe)?\b') {
        return @('Cần xác nhận', 'Chạy ngầm bằng script. Chỉ giữ nếu bạn tự cài và biết nó là gì.')
    }
    foreach ($k in $StartupKB) { if ($text -match "(?i)$($k.P)") { return @($k.T, $k.D) } }
    if ($cmd -match '(?i)\\DriverStore\\') { return @('Nên giữ', 'Thành phần của driver phần cứng.') }
    return @('Chưa rõ', 'Chưa có trong danh sách hiểu biết. Tra tên trên mạng trước khi tắt.')
}

# ----- Tab Hiệu năng -----
function Get-PerfSnapshot {
    $os = Get-CimInstance Win32_OperatingSystem
    $totalMB = [double]$os.TotalVisibleMemorySize / 1KB
    $cores = [Environment]::ProcessorCount
    $before = @{}
    foreach ($p in Get-Process) { if ($null -ne $p.CPU) { $before[$p.Id] = $p.CPU } }
    Start-Sleep -Seconds 3
    $rows = foreach ($p in Get-Process) {
        $cpu = 0
        if ($before.ContainsKey($p.Id) -and $null -ne $p.CPU) { $cpu = ($p.CPU - $before[$p.Id]) / 3 / $cores * 100 }
        [pscustomobject]@{ Name = $p.ProcessName; Cpu = $cpu; Mem = $p.WorkingSet64; Path = $p.Path; Desc = $p.Description; Company = $p.Company }
    }
    $groups = foreach ($g in ($rows | Group-Object Name)) {
        $mem = ($g.Group | Measure-Object Mem -Sum).Sum
        $cpu = ($g.Group | Measure-Object Cpu -Sum).Sum
        $first = $g.Group | Where-Object Path | Select-Object -First 1
        if (-not $first) { $first = $g.Group[0] }
        $kb = $ProcessKB[$g.Name.ToLower()]
        $explain = if ($kb) { $kb } elseif ($first.Desc -or $first.Company) { "$($first.Desc) ($($first.Company))".Trim() } else { 'Chưa rõ – xem đường dẫn bên dưới.' }
        [pscustomobject]@{
            Name = $g.Name; Count = $g.Count; CpuPct = [math]::Round($cpu, 1)
            MemMB = [math]::Round($mem / 1MB); MemPct = [math]::Round($mem / 1MB / $totalMB * 100, 1)
            Explain = $explain; Path = $first.Path; Company = $first.Company
        }
    }
    $mem = Get-CimInstance Win32_PerfFormattedData_PerfOS_Memory
    $cpuLoad = (Get-CimInstance Win32_Processor | Measure-Object LoadPercentage -Average).Average
    [pscustomobject]@{
        Rows = @($groups | Sort-Object MemMB -Descending)
        TotalMB = [math]::Round($totalMB); FreeMB = [math]::Round([double]$os.FreePhysicalMemory / 1KB)
        CommitGB = [math]::Round([double]$mem.CommittedBytes / 1GB, 1); LimitGB = [math]::Round([double]$mem.CommitLimit / 1GB, 1)
        CpuLoad = $cpuLoad
    }
}

# ----- Tab Khởi động -----
function Get-StartupItems([string[]]$ToolServices) {
    $items = New-Object System.Collections.ArrayList
    $runKeys = @(
        @{ Run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'; Ap = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'; Scope = 'Tài khoản này' }
        @{ Run = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run'; Ap = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'; Scope = 'Mọi tài khoản' }
        @{ Run = 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run'; Ap = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32'; Scope = 'Mọi tài khoản (32-bit)' }
    )
    foreach ($k in $runKeys) {
        $key = Get-Item $k.Run
        if (-not $key) { continue }
        $ap = Get-Item $k.Ap
        foreach ($n in $key.GetValueNames()) {
            if (-not $n) { continue }
            $cmd = [string]$key.GetValue($n)
            $b = $null; if ($ap) { $b = $ap.GetValue($n) }
            $on = -not (($b -is [byte[]]) -and ($b[0] -band 1))
            $adv = Get-Advice $n $cmd
            [void]$items.Add([pscustomobject]@{ Kind = 'Chạy khi đăng nhập'; Name = $n; Scope = $k.Scope; Enabled = $on; Advice = $adv[0]; Note = $adv[1]; Command = $cmd; ApKey = $k.Ap; Id = $n })
        }
    }
    $folders = @(
        @{ P = [Environment]::GetFolderPath('Startup'); Ap = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder'; Scope = 'Tài khoản này' }
        @{ P = [Environment]::GetFolderPath('CommonStartup'); Ap = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder'; Scope = 'Mọi tài khoản' }
    )
    foreach ($f in $folders) {
        if (-not (Test-Path $f.P)) { continue }
        $ap = Get-Item $f.Ap
        foreach ($file in Get-ChildItem $f.P -File -Force | Where-Object Name -ne 'desktop.ini') {
            $b = $null; if ($ap) { $b = $ap.GetValue($file.Name) }
            $on = -not (($b -is [byte[]]) -and ($b[0] -band 1))
            $adv = Get-Advice $file.BaseName $file.FullName
            [void]$items.Add([pscustomobject]@{ Kind = 'Thư mục Startup'; Name = $file.BaseName; Scope = $f.Scope; Enabled = $on; Advice = $adv[0]; Note = $adv[1]; Command = $file.FullName; ApKey = $f.Ap; Id = $file.Name })
        }
    }
    foreach ($t in Get-ScheduledTask | Where-Object { $_.TaskPath -notlike '\Microsoft\*' }) {
        $trig = $t.Triggers | Where-Object { $_.CimClass.CimClassName -in 'MSFT_TaskLogonTrigger', 'MSFT_TaskBootTrigger' }
        if (-not $trig) { continue }
        $exe = ($t.Actions | ForEach-Object { "$($_.Execute) $($_.Arguments)".Trim() }) -join ' ; '
        $adv = Get-Advice $t.TaskName $exe
        [void]$items.Add([pscustomobject]@{ Kind = 'Tác vụ tự chạy'; Name = $t.TaskName; Scope = $t.TaskPath; Enabled = ($t.State -ne 'Disabled'); Advice = $adv[0]; Note = $adv[1]; Command = $exe; ApKey = $null; Id = "$($t.TaskPath)|$($t.TaskName)" })
    }
    foreach ($s in Get-CimInstance Win32_Service) {
        $path = [string]$s.PathName
        $isAuto = $s.StartMode -eq 'Auto'
        $byTool = $ToolServices -contains $s.Name
        if (-not $isAuto -and -not $byTool) { continue }
        if ($path -match '(?i)\\Windows\\(System32|SysWOW64)\\[^\\]+\.exe' -or $path -match '(?i)Windows Defender|\\Microsoft\\|Microsoft Office|\\WindowsApps\\Microsoft') { continue }
        $adv = Get-Advice "$($s.Name) $($s.DisplayName)" $path
        [void]$items.Add([pscustomobject]@{ Kind = 'Dịch vụ nền'; Name = $s.DisplayName; Scope = "$($s.State) / $($s.StartMode)"; Enabled = $isAuto; Advice = $adv[0]; Note = $adv[1]; Command = $path; ApKey = $null; Id = $s.Name })
    }
    return $items
}

function Set-StartupItem($it, [bool]$enable) {
    switch ($it.Kind) {
        { $_ -in 'Chạy khi đăng nhập', 'Thư mục Startup' } {
            if (-not (Test-Path $it.ApKey)) { New-Item $it.ApKey -Force | Out-Null }
            $first = 3; if ($enable) { $first = 2 }
            $bytes = [byte[]](@($first, 0, 0, 0) + [BitConverter]::GetBytes([DateTime]::Now.ToFileTime()))
            Set-ItemProperty -Path $it.ApKey -Name $it.Id -Value $bytes -Type Binary -ErrorAction Stop
        }
        'Tác vụ tự chạy' {
            $tp, $tn = $it.Id -split '\|', 2
            if ($enable) { Enable-ScheduledTask -TaskPath $tp -TaskName $tn -ErrorAction Stop | Out-Null }
            else { Disable-ScheduledTask -TaskPath $tp -TaskName $tn -ErrorAction Stop | Out-Null }
        }
        'Dịch vụ nền' {
            $mode = 'Manual'; if ($enable) { $mode = 'Automatic' }
            Set-Service -Name $it.Id -StartupType $mode -ErrorAction Stop
        }
    }
}

# ----- Tab Ổ đĩa -----
function Get-DriveSummary {
    Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3 OR DriveType=2' | ForEach-Object {
        [pscustomobject]@{ Drive = $_.DeviceID; Label = $_.VolumeName; Size = [double]$_.Size; Free = [double]$_.FreeSpace }
    }
}

function Get-DownloadFolders {
    $list = @([Environment]::GetFolderPath('UserProfile') + '\Downloads')
    $v = (Get-ItemProperty 'HKCU:\Software\DownloadManager').LocalPathW
    if ($v -is [byte[]]) { $list += [Text.Encoding]::Unicode.GetString($v).TrimEnd([char]0) }
    $list | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique
}

function Invoke-DiskScan([string[]]$Drives) {
    $files = New-Object System.Collections.Generic.List[object]
    $dirs  = New-Object System.Collections.Generic.List[object]
    foreach ($d in $Drives) {
        $w = New-Object MTC.Walker
        $w.MinFile = 1MB
        $w.MaxDepth = 3
        [void]$w.Walk((New-Object IO.DirectoryInfo "$d\"), 0)
        foreach ($f in $w.Files) { $files.Add($f) }
        foreach ($x in $w.Dirs) { $dirs.Add($x) }
    }

    $topFiles = $files | Sort-Object Size -Descending | Select-Object -First 20 | ForEach-Object {
        [pscustomobject]@{ Path = $_.Path; Size = $_.Size; Modified = $_.Modified; Note = ''; Mode = 'Item' }
    }

    # Thư mục lớn nhất: bỏ thư mục cha nếu gần hết dung lượng nằm trong một thư mục con.
    $childMax = @{}
    foreach ($x in $dirs) {
        if ($x.Depth -lt 2) { continue }
        $parent = Split-Path $x.Path -Parent
        if (-not $childMax.ContainsKey($parent) -or $childMax[$parent] -lt $x.Size) { $childMax[$parent] = $x.Size }
    }
    $topDirs = $dirs | Where-Object { $_.Depth -ge 1 -and $_.Depth -le 3 } | Where-Object {
        -not $childMax.ContainsKey($_.Path) -or $childMax[$_.Path] -lt 0.9 * $_.Size
    } | Sort-Object Size -Descending | Select-Object -First 20 | ForEach-Object {
        [pscustomobject]@{ Path = $_.Path; Size = $_.Size; Modified = $null; Note = ''; Mode = 'Item' }
    }

    $instExt = '(?i)\.(exe|msi|iso|img|rar|zip|7z|bin|cab)$'
    $instName = '(?i)setup|install|repack|fitgirl|dodi|linkneverdie|\.part\d+\.rar$|fg-\d+\.bin$|\.iso$|offline'
    $instDir = '(?i)\\(Downloads|IDM Download)\\'
    $exclude = '(?i)\\(Windows|Program Files|Program Files \(x86\)|ProgramData|AppData)\\|\\steamapps\\|\\Riot Games\\|\\node_modules\\|\\\$Recycle'
    $installers = $files | Where-Object {
        $_.Size -ge 50MB -and $_.Path -match $instExt -and $_.Path -notmatch $exclude -and
        ((Split-Path $_.Path -Leaf) -match $instName -or $_.Path -match $instDir -or (Split-Path (Split-Path $_.Path -Parent) -Leaf) -match $instName)
    } | Sort-Object Size -Descending | ForEach-Object {
        [pscustomobject]@{ Path = $_.Path; Size = $_.Size; Modified = $_.Modified; Note = 'Bộ cài / file nén. Kiểm tra phần mềm đã cài xong chưa.'; Mode = 'Item' }
    }

    $cut = (Get-Date).AddDays(-90)
    $old = foreach ($dl in Get-DownloadFolders) {
        Get-ChildItem -LiteralPath $dl -File -Recurse -Force | Where-Object { $_.LastWriteTime -lt $cut -and $_.Name -ne 'desktop.ini' } | ForEach-Object {
            [pscustomobject]@{ Path = $_.FullName; Size = $_.Length; Modified = $_.LastWriteTime; Note = "Trong $dl, hơn 90 ngày"; Mode = 'Item' }
        }
    }

    $local = [Environment]::GetFolderPath('LocalApplicationData')
    $caches = @(
        @{ P = "$local\NVIDIA\DXCache"; N = 'Bộ nhớ đệm đồ hoạ NVIDIA. Tự tạo lại, game có thể hơi giật vài lần đầu.' }
        @{ P = "$env:USERPROFILE\AppData\LocalLow\NVIDIA\DXCache"; N = 'Bộ nhớ đệm đồ hoạ NVIDIA.' }
        @{ P = "$local\D3DSCache"; N = 'Bộ nhớ đệm DirectX. Tự tạo lại.' }
        @{ P = "$local\pip\cache"; N = 'Bộ nhớ đệm của Python pip. Tự tải lại khi cần.' }
        @{ P = "$local\npm-cache"; N = 'Bộ nhớ đệm của npm. Tự tải lại khi cần.' }
        @{ P = "$local\CrashDumps"; N = 'File ghi lỗi khi ứng dụng bị treo.' }
        @{ P = $env:TEMP; N = 'File tạm của tài khoản. File đang dùng sẽ được bỏ qua.' }
        @{ P = "$env:WINDIR\Temp"; N = 'File tạm của Windows. File đang dùng sẽ được bỏ qua.' }
    )
    foreach ($lib in Get-SteamLibraries) { $caches += @{ P = "$lib\steamapps\workshop\downloads"; N = 'Phần tải dở / thừa của Steam Workshop. Chỉ xoá khi Steam đang tắt.' } }
    $cacheRows = foreach ($c in $caches) {
        if (-not (Test-Path -LiteralPath $c.P)) { continue }
        $s = Get-PathSize $c.P
        if ($s -lt 50MB) { continue }
        [pscustomobject]@{ Path = $c.P; Size = $s; Modified = $null; Note = $c.N; Mode = 'Contents' }
    }
    foreach ($d in $Drives) {
        $rb = "$d\`$Recycle.Bin"
        $s = Get-PathSize $rb
        if ($s -gt 100MB) { $cacheRows = @($cacheRows) + [pscustomobject]@{ Path = $rb; Size = $s; Modified = $null; Note = 'Thùng rác. Bạn tự dọn: chuột phải Thùng rác > Empty Recycle Bin.'; Mode = 'None' } }
    }

    [pscustomobject]@{
        TopFiles = @($topFiles); TopDirs = @($topDirs); Installers = @($installers)
        OldDownloads = @($old | Sort-Object Size -Descending); Caches = @($cacheRows | Sort-Object Size -Descending)
        FileCount = $files.Count
    }
}

function Get-SteamLibraries {
    $roots = @()
    $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam').SteamPath
    if ($steam) {
        $steam = $steam -replace '/', '\'
        $roots += $steam
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s*"([^"]+)"')) { $roots += ($m.Groups[1].Value -replace '\\\\', '\') }
        }
    }
    $roots | Where-Object { $_ -and (Test-Path $_) } | ForEach-Object { (Get-Item $_).FullName.TrimEnd('\') } | Sort-Object -Unique
}

function Find-DuplicateImages([string[]]$Drives) {
    $w = New-Object MTC.Walker
    $w.MinFile = 50000
    $w.MaxDepth = -1
    $w.Ext = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($x in '.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp', '.heic', '.tif', '.tiff', '.psd', '.cr2', '.nef', '.arw', '.raw') { [void]$w.Ext.Add($x) }
    $w.Skip = [string[]]@('\windows', '\program files', '\program files (x86)', '\programdata', '\appdata', '\$recycle.bin', '\steamapps', '\steamlibrary', '\node_modules', '\.git', '\system volume information', '\riot games', '\hoyoplay')
    foreach ($d in $Drives) { [void]$w.Walk((New-Object IO.DirectoryInfo "$d\"), 0) }
    $md5 = [Security.Cryptography.MD5]::Create()
    $rows = New-Object System.Collections.ArrayList
    foreach ($g in ($w.Files | Group-Object Size | Where-Object Count -gt 1)) {
        $byHash = $g.Group | Group-Object {
            try { $s = [IO.File]::OpenRead($_.Path); try { [BitConverter]::ToString($md5.ComputeHash($s)) } finally { $s.Close() } } catch { $_.Path }
        } | Where-Object Count -gt 1
        foreach ($h in $byHash) {
            $sorted = $h.Group | Sort-Object Modified
            $keep = $sorted[0]
            foreach ($dup in ($sorted | Select-Object -Skip 1)) {
                $note = "Giống hệt: $($keep.Path)"
                if ($dup.Path.Substring(0, 2) -ne $keep.Path.Substring(0, 2)) { $note = "Ở ổ khác – có thể là bản sao lưu. Giống hệt: $($keep.Path)" }
                [void]$rows.Add([pscustomobject]@{ Path = $dup.Path; Size = $dup.Size; Modified = $dup.Modified; Note = $note; Mode = 'Item' })
            }
        }
    }
    [pscustomobject]@{ Rows = @($rows | Sort-Object Size -Descending); Scanned = $w.Files.Count }
}

function Get-RecycleRoom([string]$path) {
    $root = [IO.Path]::GetPathRoot($path)
    $vol = Get-CimInstance Win32_Volume | Where-Object { $_.DriveLetter -and ($_.DriveLetter + '\') -ieq $root } | Select-Object -First 1
    if (-not $vol) { return 0 }
    $guid = ([regex]::Match($vol.DeviceID, '\{[^}]+\}')).Value
    $k = Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\BitBucket\Volume\$guid"
    if ($k -and $k.NukeOnDelete -eq 1) { return 0 }
    $cap = [double]$vol.Capacity * 0.05
    if ($k -and $k.MaxCapacity) { $cap = [double]$k.MaxCapacity * 1MB }
    $used = Get-PathSize (Join-Path $root '$Recycle.Bin')
    return $cap - $used
}

# ----- Đánh giá mức an toàn khi xoá -----
# Mỗi luật: R = mẫu đường dẫn (đã thay các thư mục quen thuộc bằng %win%, %user%…), L = mức, T = lý do.
# Luật đứng trước được ưu tiên. Mức: safe = an toàn, review = cân nhắc, keep = không nên xoá, system = cấm xoá.
$SafetyLabels = @{ safe = 'An toàn'; review = 'Cân nhắc'; keep = 'Không nên xoá'; system = 'Cấm xoá' }
$SafetyRules = @(
    # Hệ thống – tuyệt đối không xoá
    @{ R = '^[a-z]:\\?$'; L = 'system'; T = 'Gốc của cả ổ đĩa.' }
    @{ R = '^[a-z]:\\(pagefile|hiberfil|swapfile)\.sys$'; L = 'system'; T = 'File bộ nhớ ảo / ngủ đông của Windows. Muốn thu nhỏ thì chỉnh trong cài đặt Windows, không xoá tay.' }
    @{ R = '^[a-z]:\\(system volume information|recovery|boot|efi|\$windows\.~bt|\$windows\.~ws|\$winreagent|\$sysreset)(\\|$)'; L = 'system'; T = 'Thư mục hệ thống ẩn, dùng để khởi động và khôi phục Windows.' }
    @{ R = '^[a-z]:\\(bootmgr|bootnxt|dumpstack\.log(\.tmp)?)$'; L = 'system'; T = 'File khởi động của Windows.' }
    @{ R = '^%user%$|^%user%\\appdata$|^%local%$|^%roaming%$|^[a-z]:\\users$'; L = 'system'; T = 'Thư mục gốc của tài khoản người dùng.' }
    # Bên trong Windows: vài chỗ dọn được, còn lại không đụng vào
    @{ R = '^%win%\\temp(\\|$)'; L = 'safe'; T = 'File tạm của Windows. File đang dùng sẽ tự được bỏ qua.' }
    @{ R = '^%win%\\softwaredistribution\\download(\\|$)'; L = 'safe'; T = 'Bản tải về của Windows Update đã cài xong. Windows tự tải lại khi cần.' }
    @{ R = '^%win%\\(logs|minidump|livekernelreports)(\\|$)|^%win%\\memory\.dmp$'; L = 'safe'; T = 'Nhật ký và file ghi lỗi của Windows.' }
    @{ R = '^%win%\\installer(\\|$)'; L = 'keep'; T = 'Windows cần các file này để gỡ và cập nhật phần mềm. Xoá nhầm thì phần mềm không gỡ / cập nhật được nữa.' }
    @{ R = '^%win%(\\|$)'; L = 'system'; T = 'Thư mục của Windows. Xoá có thể làm máy không khởi động được.' }
    # Bộ nhớ đệm và file tạm – tự tạo lại
    @{ R = '^%temp%(\\|$)'; L = 'safe'; T = 'File tạm của tài khoản. File đang dùng sẽ tự được bỏ qua.' }
    @{ R = '\\(dxcache|glcache|d3dscache|shadercache|gpucache|grshadercache|code cache|cache_data|cache2|inetcache|webcache)(\\|$)'; L = 'safe'; T = 'Bộ nhớ đệm, chương trình sẽ tự tạo lại (lần đầu mở có thể hơi chậm).' }
    @{ R = '^(%local%|%roaming%|%locallow%)\\.*\\cache(\\|$)'; L = 'safe'; T = 'Bộ nhớ đệm của ứng dụng / trình duyệt, tự tạo lại.' }
    @{ R = '^%local%\\crashdumps(\\|$)|\\crashpad\\(reports|completed)(\\|$)'; L = 'safe'; T = 'File ghi lỗi khi ứng dụng bị treo.' }
    @{ R = '\\(pip\\cache|npm-cache|yarn\\cache|\.gradle\\caches|nuget\\v3-cache|go-build|\.cache\\huggingface)(\\|$)'; L = 'safe'; T = 'Bộ nhớ đệm của công cụ lập trình, tự tải lại khi cần.' }
    @{ R = '\\steamapps\\(workshop\\downloads|downloading|temp)(\\|$)'; L = 'safe'; T = 'Phần tải dở / thừa của Steam. Chỉ xoá khi Steam đang tắt.' }
    @{ R = '\\\$recycle\.bin(\\|$)'; L = 'review'; T = 'Thùng rác. Dọn bằng chuột phải Thùng rác > Empty Recycle Bin.' }
    # Dữ liệu không nên xoá tay
    @{ R = '\\\.git(\\|$)'; L = 'keep'; T = 'Lịch sử mã nguồn của dự án. Xoá là mất toàn bộ lịch sử.' }
    @{ R = '\\(saves?|savegames?|saved games|my games|savedata)(\\|$)'; L = 'keep'; T = 'Dữ liệu lưu game. Xoá là mất tiến trình chơi.' }
    @{ R = '\\steamapps\\common\\[^\\]+'; L = 'keep'; T = 'Game đã cài qua Steam. Gỡ trong Steam (hoặc tab ứng dụng) để không hỏng thư viện game.' }
    @{ R = '\\steamapps\\workshop\\content(\\|$)'; L = 'review'; T = 'Nội dung Workshop đang dùng (hình nền, mod). Huỷ đăng ký trong Steam thay vì xoá tay.' }
    @{ R = '\\(riot games|epic games|ubisoft game launcher|battle\.net|ea games|ea desktop|gog galaxy|hoyoplay|garena)(\\|$)'; L = 'keep'; T = 'Game hoặc trình mở game. Gỡ bằng chính launcher đó.' }
    @{ R = '\\node_modules(\\|$)'; L = 'review'; T = 'Thư viện của dự án lập trình. Xoá được, cài lại bằng npm install.' }
    @{ R = '(fitgirl|dodi|repack|linkneverdie)'; L = 'review'; T = 'Bộ cài game / phần mềm dạng repack. Xoá được nếu đã cài xong.' }
    @{ R = '^%pf%(\\|$)|^%pf86%(\\|$)'; L = 'keep'; T = 'Phần mềm đã cài. Gỡ trong tab ứng dụng để không để lại lỗi.' }
    @{ R = '^%pd%(\\|$)'; L = 'keep'; T = 'Dữ liệu chung của phần mềm đã cài.' }
    @{ R = '^%user%\\(documents|desktop|pictures|videos|music|onedrive[^\\]*)(\\|$)'; L = 'keep'; T = 'Dữ liệu cá nhân. Chỉ xoá khi chắc chắn không cần.' }
    @{ R = '^%roaming%(\\|$)|^%local%(\\|$)|^%locallow%(\\|$)'; L = 'keep'; T = 'Dữ liệu và cài đặt của ứng dụng. Chỉ xoá khi đã gỡ ứng dụng đó.' }
    # Theo loại tệp
    @{ R = '\.(iso|img|rar|zip|7z|cab|msi|dmg)$|\\[^\\]*(setup|install|\.part\d+)[^\\]*$'; L = 'review'; T = 'Bộ cài / file nén. Xoá được nếu đã cài hoặc giải nén xong.' }
    @{ R = '^%user%\\downloads(\\|$)'; L = 'review'; T = 'File đã tải về. Xem lại còn cần không trước khi xoá.' }
    @{ R = '\.(tmp|temp|bak|old|log|etl|dmp|chk|crdownload|part)$'; L = 'safe'; T = 'File tạm, nhật ký hoặc bản tải dở.' }
    @{ R = '\.(exe|dll|sys|ocx|drv|msp)$'; L = 'keep'; T = 'File chương trình. Xoá có thể làm phần mềm hỏng – hãy gỡ phần mềm đó.' }
    @{ R = '\.(psd|ai|fig|xd|sketch|blend|aep|prproj|c4d|docx?|xlsx?|pptx?|pdf|jpe?g|png|heic|raw|cr2|nef|arw|mp4|mov|mkv)$'; L = 'keep'; T = 'Có thể là dữ liệu cá nhân hoặc dự án. Chỉ xoá khi chắc chắn.' }
)
function ConvertTo-SafetyPath([string]$path) {
    $p = $path.ToLower().TrimEnd('\')
    if ($p -match '^[a-z]:$') { $p += '\' }
    $map = [ordered]@{
        '%temp%' = $env:TEMP; '%locallow%' = "$env:USERPROFILE\AppData\LocalLow"; '%local%' = $env:LOCALAPPDATA
        '%roaming%' = $env:APPDATA; '%user%' = $env:USERPROFILE; '%win%' = $env:WINDIR
        '%pf86%' = ${env:ProgramFiles(x86)}; '%pf%' = $env:ProgramFiles; '%pd%' = $env:ProgramData
    }
    foreach ($k in $map.Keys) {
        $v = ([string]$map[$k]).ToLower().TrimEnd('\')
        if ($v -and ($p -eq $v -or $p.StartsWith($v + '\'))) { return $k + $p.Substring($v.Length) }
    }
    return $p
}
function Get-DeleteSafety([string]$path) {
    $p = ConvertTo-SafetyPath $path
    foreach ($r in $SafetyRules) {
        if ($p -match $r.R) { return [pscustomobject]@{ Level = $r.L; Label = $SafetyLabels[$r.L]; Why = $r.T } }
    }
    return [pscustomobject]@{ Level = 'review'; Label = $SafetyLabels['review']; Why = 'Không thuộc nhóm nào em biết. Xem kỹ nội dung trước khi xoá.' }
}
# Kiểm tra thêm cho một tệp: có đang được mở không, lâu rồi chưa sửa không.
function Get-FileFacts([string]$path) {
    $facts = @()
    if ([IO.File]::Exists($path)) {
        try { $s = [IO.File]::Open($path, 'Open', 'Read', 'None'); $s.Close() }
        catch { $facts += 'đang được chương trình khác mở – đóng chương trình đó trước' }
        $fi = Get-Item -LiteralPath $path -Force
        $days = ((Get-Date) - $fi.LastWriteTime).TotalDays
        if ($days -gt 365) { $facts += ('không sửa từ {0:dd/MM/yyyy} ({1:N0} năm)' -f $fi.LastWriteTime, ($days / 365)) }
        if ($fi.Attributes -band [IO.FileAttributes]::System) { $facts += 'có thuộc tính "hệ thống"' }
    }
    return $facts
}

# Chuyển vào Thùng rác. Không bao giờ xoá vĩnh viễn: mục quá lớn sẽ bị bỏ qua.
function Move-ToRecycleBin([string]$path, [string]$mode = 'Item') {
    if ($mode -eq 'Item' -and (Get-DeleteSafety $path).Level -eq 'system') {
        return [pscustomobject]@{ Path = $path; Result = 'Bị chặn'; Detail = 'Mục hệ thống, công cụ không cho xoá.'; Freed = 0 }
    }
    Add-Type -AssemblyName Microsoft.VisualBasic
    $FS = [Microsoft.VisualBasic.FileIO.FileSystem]
    $ui = [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs
    $rb = [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin
    $ce = [Microsoft.VisualBasic.FileIO.UICancelOption]::ThrowException
    if ($mode -eq 'None') { return [pscustomobject]@{ Path = $path; Result = 'Bỏ qua'; Detail = 'Mục này bạn cần tự xử lý.'; Freed = 0 } }
    if (-not (Test-Path -LiteralPath $path)) { return [pscustomobject]@{ Path = $path; Result = 'Không còn'; Detail = 'Đã không còn tồn tại.'; Freed = 0 } }
    $size = Get-PathSize $path
    if ($size -gt (Get-RecycleRoom $path)) {
        return [pscustomobject]@{ Path = $path; Result = 'Quá lớn'; Detail = "($(Format-Size $size)) lớn hơn chỗ trống của Thùng rác. Bạn tự xoá bằng Shift + Delete nếu chắc chắn."; Freed = 0 }
    }
    if ($mode -eq 'Contents') {
        $ok = 0; $skip = 0; $freed = 0
        foreach ($f in Get-ChildItem -LiteralPath $path -Recurse -File -Force) {
            try { $len = $f.Length; $FS::DeleteFile($f.FullName, $ui, $rb, $ce); $ok++; $freed += $len } catch { $skip++ }
        }
        return [pscustomobject]@{ Path = $path; Result = 'Đã chuyển'; Detail = "$ok file vào Thùng rác, $skip file đang dùng nên bỏ qua."; Freed = $freed }
    }
    try {
        if (Test-Path -LiteralPath $path -PathType Container) { $FS::DeleteDirectory($path, $ui, $rb, $ce) } else { $FS::DeleteFile($path, $ui, $rb, $ce) }
        return [pscustomobject]@{ Path = $path; Result = 'Đã chuyển'; Detail = 'Đã vào Thùng rác.'; Freed = $size }
    } catch {
        return [pscustomobject]@{ Path = $path; Result = 'Lỗi'; Detail = $_.Exception.Message; Freed = 0 }
    }
}

function Move-ManyToRecycleBin($list) {
    foreach ($x in $list) { Move-ToRecycleBin $x.Path $x.Mode }
}

# ----- Tab Ứng dụng -----
$SystemAppPattern = '(?i)Visual C\+\+|\.NET|Redistributable|Runtime|Driver|Intel\(R\)|NVIDIA|Realtek|PhysX|DirectX|SDK|Update Health|GameInput|Compatibility|Python Launcher|WebView2|^Steam$|Xbox|Microsoft Edge$|Genuine Service|Steamworks|Launcher Prerequisites|UE4 Prerequisites|Windows Subsystem'

function Get-AppUsage {
    # Prefetch: lịch sử chạy chương trình của Windows (cần quyền quản trị).
    $pf = @{}; $pfStart = $null
    foreach ($f in Get-ChildItem "$env:WINDIR\Prefetch" -Filter *.pf) {
        $exe = ($f.Name -replace '-[0-9A-F]{8}\.pf$', '').ToLower()
        if (-not $pf.ContainsKey($exe) -or $pf[$exe] -lt $f.LastWriteTime) { $pf[$exe] = $f.LastWriteTime }
        if (-not $pfStart -or $f.CreationTime -lt $pfStart) { $pfStart = $f.CreationTime }
    }
    # UserAssist: lần mở bằng biểu tượng / menu Start.
    $ua = @{}
    foreach ($g in Get-ChildItem 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist') {
        $k = Get-Item "$($g.PSPath)\Count"
        if (-not $k) { continue }
        foreach ($n in $k.GetValueNames()) {
            $v = $k.GetValue($n)
            if ($v -isnot [byte[]] -or $v.Length -lt 68) { continue }
            $ft = [BitConverter]::ToInt64($v, 60)
            if ($ft -le 0) { continue }
            $name = -join ($n.ToCharArray() | ForEach-Object {
                $c = [int]$_
                if ($c -ge 65 -and $c -le 90) { [char](65 + ($c - 65 + 13) % 26) }
                elseif ($c -ge 97 -and $c -le 122) { [char](97 + ($c - 97 + 13) % 26) }
                else { $_ } })
            $leaf = [IO.Path]::GetFileName($name).ToLower()
            $t = [DateTime]::FromFileTime($ft)
            if ($t.Year -lt 2000) { continue }
            if (-not $ua.ContainsKey($leaf) -or $ua[$leaf] -lt $t) { $ua[$leaf] = $t }
        }
    }
    # Steam: ngày chơi cuối và dung lượng từng game.
    $steamLast = @{}; $steamSize = @{}
    $libs = @(Get-SteamLibraries)
    foreach ($lib in $libs) {
        foreach ($cfg in Get-ChildItem "$lib\userdata\*\config\localconfig.vdf") {
            $txt = Get-Content $cfg.FullName -Raw
            foreach ($m in [regex]::Matches($txt, '"(\d+)"\s*\{[^{}]*?"LastPlayed"\s*"(\d+)"')) {
                $steamLast[$m.Groups[1].Value] = [DateTimeOffset]::FromUnixTimeSeconds([long]$m.Groups[2].Value).LocalDateTime
            }
        }
        foreach ($acf in Get-ChildItem "$lib\steamapps\appmanifest_*.acf") {
            $t = Get-Content $acf.FullName -Raw
            $id = [regex]::Match($t, '"appid"\s*"(\d+)"').Groups[1].Value
            $sz = [regex]::Match($t, '"SizeOnDisk"\s*"(\d+)"').Groups[1].Value
            if ($id) { $steamSize[$id] = [double]$sz }
        }
    }
    $keys = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*'
    $apps = Get-ItemProperty $keys | Where-Object { $_.DisplayName -and $_.UninstallString -and -not $_.SystemComponent -and -not $_.ParentKeyName -and $_.ReleaseType -notmatch 'Update|Hotfix' } | Sort-Object DisplayName -Unique
    $skipExe = '(?i)unins|uninst|setup|update|crash|helper|install|repair|redist|vc_|elevat'
    foreach ($a in $apps) {
        $steamId = [regex]::Match([string]$a.UninstallString, 'steam://uninstall/(\d+)').Groups[1].Value
        $exes = @()
        if ($a.DisplayIcon) { $p = ($a.DisplayIcon -split ',')[0].Trim('"'); if ($p -match '(?i)\.exe$') { $exes += [IO.Path]::GetFileName($p) } }
        $loc = [string]$a.InstallLocation; $loc = $loc.Trim('"')
        if ($loc -and (Test-Path -LiteralPath $loc)) {
            $exes += (Get-ChildItem -LiteralPath $loc -Filter *.exe -File -Recurse -Depth 3 | Select-Object -First 200).Name
        }
        $exes = @($exes | Where-Object { $_ -and $_ -notmatch $skipExe } | ForEach-Object { $_.ToLower() } | Select-Object -Unique)
        $pLast = $exes | ForEach-Object { $pf[$_] } | Where-Object { $_ } | Sort-Object -Descending | Select-Object -First 1
        $uLast = $exes | ForEach-Object { $ua[$_] } | Where-Object { $_ } | Sort-Object -Descending | Select-Object -First 1
        $last = $null; $source = ''
        if ($steamId -and $steamLast.ContainsKey($steamId)) { $last = $steamLast[$steamId]; $source = 'Steam (chính xác)' }
        elseif ($pLast) { $last = $pLast; $source = 'Lịch sử chạy (Prefetch)' }
        elseif ($uLast) { $last = $uLast; $source = 'Lần mở bằng biểu tượng (ước lượng)' }
        else { $source = if ($pfStart) { "Không thấy chạy từ $($pfStart.ToString('dd/MM/yyyy'))" } else { 'Không có dữ liệu' } }
        $size = 0
        if ($steamId -and $steamSize.ContainsKey($steamId)) { $size = $steamSize[$steamId] }
        elseif ($a.EstimatedSize) { $size = [double]$a.EstimatedSize * 1KB }
        $kind = 'Ứng dụng'
        if ($steamId) { $kind = 'Game Steam' }
        if ($a.DisplayName -match $SystemAppPattern) { $kind = 'Hệ thống – nên giữ' }
        [pscustomobject]@{
            Name = $a.DisplayName; Size = $size; LastUsed = $last; Source = $source; Kind = $kind
            Publisher = $a.Publisher; Version = $a.DisplayVersion; Location = $loc
            Uninstall = [string]$a.UninstallString; SteamId = $steamId
        }
    }
}

function Get-PrefetchStart {
    $f = Get-ChildItem "$env:WINDIR\Prefetch" -Filter *.pf | Sort-Object CreationTime | Select-Object -First 1
    if ($f) { return $f.CreationTime }
}

function Uninstall-App($app) {
    try {
        if ($app.SteamId) {
            Start-Process "steam://uninstall/$($app.SteamId)"
            return [pscustomobject]@{ Name = $app.Name; Result = 'Đã mở'; Detail = 'Steam sẽ hỏi xác nhận gỡ.' }
        }
        $cmd = $app.Uninstall.Trim()
        $exe = $null; $argList = ''
        if ($cmd.StartsWith('"')) {
            $end = $cmd.IndexOf('"', 1)
            $exe = $cmd.Substring(1, $end - 1); $argList = $cmd.Substring($end + 1).Trim()
        } else {
            $i = $cmd.ToLower().IndexOf('.exe')
            if ($i -lt 0) { $exe = $cmd } else { $exe = $cmd.Substring(0, $i + 4); $argList = $cmd.Substring($i + 4).Trim() }
        }
        if ($exe -match '(?i)msiexec') { $argList = $argList -replace '(?i)/I\s*\{', '/X{' }
        $sp = @{ FilePath = $exe; Wait = $true; PassThru = $true; ErrorAction = 'Stop' }
        if ($argList) { $sp.ArgumentList = $argList }
        $p = Start-Process @sp
        $code = $p.ExitCode
        $res = if ($code -eq 0 -or $code -eq 3010) { 'Xong' } else { "Mã $code" }
        $det = if ($code -eq 3010) { 'Cần khởi động lại máy để gỡ xong.' } elseif ($code -eq 0) { '' } else { 'Trình gỡ báo lỗi hoặc bạn đã huỷ.' }
        return [pscustomobject]@{ Name = $app.Name; Result = $res; Detail = $det }
    } catch {
        return [pscustomobject]@{ Name = $app.Name; Result = 'Lỗi'; Detail = $_.Exception.Message }
    }
}

function Uninstall-Many($list) { foreach ($a in $list) { Uninstall-App $a } }

# ----- Tab Bảo mật -----
function Get-SecurityReport {
    $rows = New-Object System.Collections.ArrayList
    function Add-Row($level, $group, $item, $detail) {
        [void]$rows.Add([pscustomobject]@{ Level = $level; Group = $group; Item = $item; Detail = $detail })
    }

    # Trình diệt virus
    $st = $null; try { $st = Get-MpComputerStatus -ErrorAction Stop } catch { }
    $pol = Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender'
    $av = @(Get-CimInstance -Namespace root/SecurityCenter2 -ClassName AntivirusProduct | Where-Object { $_.displayName -notmatch 'Windows Defender' })
    if ($pol.DisableAntiVirus -eq 1 -or $pol.DisableAntiSpyware -eq 1) {
        Add-Row 'Cao' 'Diệt virus' 'Windows Defender bị tắt bằng chính sách' 'Trong HKLM\SOFTWARE\Policies\Microsoft\Windows Defender có DisableAntiVirus/DisableAntiSpyware = 1. Bạn xoá hai giá trị này, khởi động lại rồi bật lại trong Windows Security.'
    }
    if ($st -and $st.RealTimeProtectionEnabled) {
        Add-Row 'Thông tin' 'Diệt virus' 'Windows Defender đang bật' "Cập nhật lần cuối: $($st.AntivirusSignatureLastUpdated)"
        if (-not $st.IsTamperProtected) { Add-Row 'Trung bình' 'Diệt virus' 'Tamper Protection đang tắt' 'Bật trong Windows Security > Virus & threat protection > Manage settings.' }
    } elseif ($av.Count -gt 0) {
        Add-Row 'Thông tin' 'Diệt virus' "Đang dùng: $(($av.displayName) -join ', ')" ''
    } else {
        Add-Row 'Cao' 'Diệt virus' 'Không có trình diệt virus nào đang chạy' 'Máy không được bảo vệ. Bạn bật lại Windows Security > Virus & threat protection.'
    }
    $fw = Get-NetFirewallProfile | Where-Object { -not $_.Enabled }
    if ($fw) { Add-Row 'Cao' 'Tường lửa' "Tường lửa tắt ở: $(($fw.Name) -join ', ')" 'Bật lại trong Windows Security > Firewall.' }
    else { Add-Row 'Thông tin' 'Tường lửa' 'Tường lửa đang bật ở mọi chế độ mạng' '' }

    # Tiến trình không có chữ ký số
    $procs = Get-CimInstance Win32_Process
    $sig = @{}
    foreach ($p in $procs | Where-Object ExecutablePath) {
        $path = $p.ExecutablePath
        if ($sig.ContainsKey($path)) { continue }
        $s = Get-AuthenticodeSignature -LiteralPath $path
        $sig[$path] = $s
        if ($s.Status -eq 'Valid') { continue }
        if ($path -match '(?i)\\WindowsApps\\Microsoft\.') { continue }
        $level = 'Trung bình'
        if ($path -match '(?i)\\Temp\\|\\Downloads\\|\\Public\\') { $level = 'Cao' }
        $info = (Get-Item -LiteralPath $path).VersionInfo
        $who = "$($info.FileDescription) – $($info.CompanyName)".Trim(' –')
        $why = if ($s.Status -eq 'NotSigned') { 'Không có chữ ký số.' } else { "Chữ ký số không hợp lệ ($($s.Status))." }
        Add-Row $level 'Tiến trình' $p.Name "$why $who. Đường dẫn: $path"
    }

    # Kết nối Internet chạy nền
    $byPid = @{}; foreach ($p in $procs) { $byPid[[int]$p.ProcessId] = $p }
    $dns = @{}; foreach ($e in Get-DnsClientCache) { $dns["$($e.Data)"] = $e.Entry }
    $priv = '^(127\.|::1|0\.0\.0\.0|::$|10\.|192\.168\.|172\.(1[6-9]|2\d|3[01])\.|fe80|169\.254)'
    $conns = Get-NetTCPConnection -State Established | Where-Object { "$($_.RemoteAddress)" -notmatch $priv }
    foreach ($g in ($conns | Group-Object OwningProcess)) {
        $p = $byPid[[int]$g.Name]
        $name = if ($p) { $p.Name } else { "PID $($g.Name)" }
        $hosts = ($g.Group | ForEach-Object { $ip = "$($_.RemoteAddress)"; $h = $dns[$ip]; if ($h) { $h } else { $ip } } | Select-Object -Unique | Select-Object -First 5) -join ', '
        $level = 'Thông tin'
        if ($p -and $p.ExecutablePath -and $sig[$p.ExecutablePath] -and $sig[$p.ExecutablePath].Status -ne 'Valid') { $level = 'Trung bình' }
        Add-Row $level 'Kết nối Internet' "$name ($($g.Count) kết nối)" "Tới: $hosts. $(if ($p) { $p.ExecutablePath })"
    }
    $listen = Get-NetTCPConnection -State Listen | Where-Object { "$($_.LocalAddress)" -in '0.0.0.0', '::' }
    foreach ($g in ($listen | Group-Object OwningProcess)) {
        $p = $byPid[[int]$g.Name]
        if (-not $p -or $p.Name -in 'System', 'svchost.exe', 'lsass.exe', 'wininit.exe', 'services.exe', 'spoolsv.exe') { continue }
        Add-Row 'Thông tin' 'Mở cổng chờ kết nối' $p.Name "Cổng $(($g.Group.LocalPort | Sort-Object -Unique) -join ', '). $($p.ExecutablePath)"
    }

    # Proxy và file hosts
    $ie = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings'
    if ($ie.ProxyEnable -eq 1 -or $ie.AutoConfigURL) { Add-Row 'Trung bình' 'Mạng' 'Đang dùng proxy' "Proxy: $($ie.ProxyServer) $($ie.AutoConfigURL). Nếu bạn không tự đặt thì nên kiểm tra." }
    foreach ($line in Get-Content "$env:WINDIR\System32\drivers\etc\hosts") {
        if ($line -match '^\s*#' -or $line -notmatch '\S') { continue }
        $parts = $line.Trim() -split '\s+'
        if ($parts.Count -lt 2 -or $parts[1] -match '^localhost$') { continue }
        $level = 'Thấp'
        if ($parts[0] -notmatch '^(127\.|0\.0\.0\.0|::1)') { $level = 'Trung bình' }
        Add-Row $level 'File hosts' $line.Trim() 'Dòng tự thêm trong file hosts. Trỏ về 127.0.0.1 thường là để chặn trang web.'
    }

    # Extension trình duyệt
    foreach ($e in Get-BrowserExtensions) { Add-Row $e.Level 'Extension trình duyệt' "$($e.Name) [$($e.Browser) / $($e.Profile)]" $e.Detail }
    return $rows
}

function Get-BrowserExtensions {
    $browsers = @{
        'Edge'   = "$env:LOCALAPPDATA\Microsoft\Edge\User Data"
        'Chrome' = "$env:LOCALAPPDATA\Google\Chrome\User Data"
        'Brave'  = "$env:LOCALAPPDATA\BraveSoftware\Brave-Browser\User Data"
        'Cốc Cốc' = "$env:LOCALAPPDATA\CocCoc\Browser\User Data"
    }
    foreach ($b in $browsers.GetEnumerator()) {
        if (-not (Test-Path $b.Value)) { continue }
        foreach ($pdir in Get-ChildItem $b.Value -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Extensions') }) {
            $settings = @{}
            foreach ($pf in 'Secure Preferences', 'Preferences') {
                $f = Join-Path $pdir.FullName $pf
                if (-not (Test-Path $f)) { continue }
                try {
                    $j = Get-Content $f -Raw -Encoding UTF8 | ConvertFrom-Json
                    if ($j.extensions.settings) { $j.extensions.settings.PSObject.Properties | ForEach-Object { $settings[$_.Name] = $_.Value } }
                } catch { }
            }
            foreach ($e in Get-ChildItem (Join-Path $pdir.FullName 'Extensions') -Directory) {
                $ver = Get-ChildItem $e.FullName -Directory | Sort-Object Name -Descending | Select-Object -First 1
                if (-not $ver) { continue }
                $mf = Join-Path $ver.FullName 'manifest.json'
                if (-not (Test-Path $mf)) { continue }
                try { $m = Get-Content $mf -Raw -Encoding UTF8 | ConvertFrom-Json } catch { continue }
                $s = $settings[$e.Name]
                if ($s -and ($s.location -eq 5 -or $s.location -eq 10)) { continue }   # thành phần có sẵn của trình duyệt
                $name = Resolve-ExtName $ver.FullName ([string]$m.name)
                $on = -not ($s -and ($s.disable_reasons -or $s.state -eq 0))
                $perms = @(@($m.permissions) + @($m.host_permissions) | Where-Object { $_ -is [string] })
                $all = $perms -match '<all_urls>|^\*://\*/\*|^https?://\*/\*'
                $notes = @()
                $level = 'Thấp'
                if ($all) { $level = 'Trung bình'; $notes += 'đọc và sửa được mọi trang web' }
                if ($perms -contains 'cookies' -and $all) { $level = 'Cao'; $notes += 'đọc được cookie đăng nhập' }
                if ($perms -contains 'proxy') { $level = 'Cao'; $notes += 'điều khiển proxy (thấy mọi lưu lượng)' }
                if ($perms -contains 'management') { $notes += 'quản lý được extension khác' }
                if ($perms -contains 'debugger') { $notes += 'dùng được quyền gỡ lỗi trang' }
                if ($name -match '(?i)wallet|metamask|phantom|petra|keplr|okx|trust') { $level = 'Cao'; $notes += 'ví tiền mã hoá – gỡ nếu không còn dùng' }
                if ($name -match '(?i)\bnode\b|validator|grass|gradient|dawn|nodepay|bandwidth') { $level = 'Cao'; $notes += 'chia sẻ băng thông Internet để "đào airdrop"' }
                if ($name -match '(?i)vpn|proxy') { $notes += 'VPN miễn phí từng bị phát hiện thu thập dữ liệu duyệt web' }
                if ($s -and $s.location -eq 3) { $notes += 'được phần mềm khác cài kèm' }
                if (-not $on -and $level -ne 'Thấp') { $level = 'Thấp'; $notes += 'đang tắt' }
                if (-not $notes) { $notes += 'quyền hạn hẹp' }
                [pscustomobject]@{
                    Browser = $b.Key; Profile = $pdir.Name; Name = $name; Enabled = $on; Level = $level
                    Detail = "$(if ($on) { 'Đang bật' } else { 'Đang tắt' }) · v$($m.version) · $($notes -join '; ') · ID $($e.Name)"
                }
            }
        }
    }
}

function Resolve-ExtName([string]$dir, [string]$n) {
    if ($n -notmatch '^__MSG_(.+)__$') { return $n }
    $key = $Matches[1]
    foreach ($loc in 'vi', 'en', 'en_US') {
        $f = Join-Path $dir "_locales\$loc\messages.json"
        if (-not (Test-Path $f)) { continue }
        try {
            $j = Get-Content $f -Raw -Encoding UTF8 | ConvertFrom-Json
            $m = $j.PSObject.Properties | Where-Object { $_.Name -ieq $key } | Select-Object -First 1
            if ($m) { return $m.Value.message }
        } catch { }
    }
    return $n
}

# ----- Tab Sao lưu -----
function Get-BackupStatus {
    $rows = New-Object System.Collections.ArrayList
    function Add-Row($item, $status, $detail) { [void]$rows.Add([pscustomobject]@{ Item = $item; Status = $status; Detail = $detail }) }

    $wb = (wbadmin get versions 2>&1 | Out-String)
    $times = [regex]::Matches($wb, '(?im)^Backup time:\s*(.+)$') | ForEach-Object { $_.Groups[1].Value.Trim() }
    if ($times) { Add-Row 'Windows Backup (sao lưu toàn bộ máy)' 'Có' "Lần cuối: $($times[-1])" }
    else { Add-Row 'Windows Backup (sao lưu toàn bộ máy)' 'Chưa có' 'Chưa từng chạy.' }

    $fhCfg = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\Windows\FileHistory\Configuration" -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($fhCfg) {
        $target = ''
        try { $x = [xml](Get-Content $fhCfg.FullName -Raw); $target = $x.SelectSingleNode('//Target//TargetUrl').InnerText } catch { }
        Add-Row 'File History (tự sao lưu file)' 'Đã bật' "Lưu vào: $target. Cấu hình cập nhật: $($fhCfg.LastWriteTime)"
    } else { Add-Row 'File History (tự sao lưu file)' 'Chưa bật' 'Bấm "Mở File History" để bật, chọn một ổ cắm ngoài.' }

    $rp = Get-ComputerRestorePoint | Sort-Object SequenceNumber -Descending
    if ($rp) {
        $last = [Management.ManagementDateTimeConverter]::ToDateTime($rp[0].CreationTime)
        Add-Row 'Điểm khôi phục (System Restore)' "$(@($rp).Count) điểm" "Mới nhất: $last – $($rp[0].Description). Chỉ khôi phục được cài đặt Windows, không giữ file của bạn."
    } else { Add-Row 'Điểm khôi phục (System Restore)' 'Không có' '' }

    $od = Get-ItemProperty 'HKCU:\Software\Microsoft\OneDrive\Accounts\*' | Where-Object UserEmail
    if ($od) { foreach ($a in $od) { Add-Row 'OneDrive' 'Đã đăng nhập' "$($a.UserEmail) – thư mục $($a.UserFolder). Đang chạy: $([bool](Get-Process OneDrive))" } }
    else { Add-Row 'OneDrive' 'Chưa dùng' 'Chưa đăng nhập tài khoản nào.' }

    $known = 'Google Drive|Dropbox|Macrium|Acronis|AOMEI|EaseUS Todo|Veeam|FreeFileSync|SyncBack|Seagate Toolkit|Backblaze|iDrive|pCloud|MEGA'
    $apps = Get-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*' |
        Where-Object { $_.DisplayName -match "(?i)$known" } | Select-Object -ExpandProperty DisplayName -Unique
    if ($apps) { Add-Row 'Phần mềm sao lưu / đồng bộ khác' 'Có' ($apps -join ', ') }
    else { Add-Row 'Phần mềm sao lưu / đồng bộ khác' 'Không có' '' }

    foreach ($d in Get-CimInstance Win32_DiskDrive | Where-Object { $_.MediaType -match 'External|Removable' }) {
        Add-Row 'Ổ cắm ngoài' 'Đang cắm' "$($d.Model) – $([math]::Round($d.Size / 1GB)) GB. Có thể dùng làm nơi sao lưu."
    }
    $log = Join-Path $env:LOCALAPPDATA 'CleanVaiLon\saoluu.txt'
    if (Test-Path $log) { Add-Row 'Bản chép thư mục bằng công cụ này' 'Có' ((Get-Content $log -Tail 3) -join ' | ') }
    return $rows
}

function Copy-FolderBackup([string]$src, [string]$dstRoot) {
    $leaf = Split-Path $src -Leaf
    if (-not $leaf) { $leaf = $src.Substring(0, 1) + '_drive' }
    $dst = Join-Path $dstRoot $leaf
    $logFile = Join-Path $env:TEMP "CleanVaiLon_robocopy.txt"
    & robocopy.exe $src $dst /E /R:1 /W:1 /XJ /NP /NFL /NDL /LOG:$logFile | Out-Null
    $code = $LASTEXITCODE
    $ok = $code -lt 8
    $dir = Join-Path $env:LOCALAPPDATA 'CleanVaiLon'
    New-Item $dir -ItemType Directory -Force | Out-Null
    Add-Content (Join-Path $dir 'saoluu.txt') ("{0:yyyy-MM-dd HH:mm}  {1} -> {2}  ({3})" -f (Get-Date), $src, $dst, $(if ($ok) { 'xong' } else { "lỗi mã $code" }))
    $summary = (Get-Content $logFile | Select-String '^\s*(Dirs|Files|Bytes)\s*:' | ForEach-Object { $_.Line.Trim() }) -join "`r`n"
    [pscustomobject]@{ Ok = $ok; Code = $code; Dest = $dst; Summary = $summary }
}
'@

. ([scriptblock]::Create($WorkerCode))
if ($NoGui) { return }

# ---------------------------------------------------------------------------
# Giao diện – phong cách terminal tối
# ---------------------------------------------------------------------------
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()

if (-not ('MTC.BorderlessForm' -as [type])) {
Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing -TypeDefinition @"
using System; using System.Collections; using System.Windows.Forms; using System.Runtime.InteropServices;
namespace MTC {
  // Cửa sổ không viền của Windows nhưng vẫn kéo dãn được ở mép và có bóng đổ.
  public class BorderlessForm : Form {
    public int Grip = 6;
    public BorderlessForm() { FormBorderStyle = FormBorderStyle.None; DoubleBuffered = true; }
    protected override CreateParams CreateParams {
      get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; cp.Style |= 0x00020000 | 0x00010000; return cp; }
    }
    protected override void WndProc(ref Message m) {
      base.WndProc(ref m);
      if (m.Msg != 0x84 || Grip <= 0 || WindowState != FormWindowState.Normal || (int)m.Result != 1) return;
      long lp = m.LParam.ToInt64();
      var p = PointToClient(new System.Drawing.Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
      bool l = p.X < Grip, r = p.X >= ClientSize.Width - Grip, t = p.Y < Grip, b = p.Y >= ClientSize.Height - Grip;
      if (t && l) m.Result = (IntPtr)13; else if (t && r) m.Result = (IntPtr)14;
      else if (b && l) m.Result = (IntPtr)16; else if (b && r) m.Result = (IntPtr)17;
      else if (l) m.Result = (IntPtr)10; else if (r) m.Result = (IntPtr)11;
      else if (t) m.Result = (IntPtr)12; else if (b) m.Result = (IntPtr)15;
    }
  }
  public class LvSorter : IComparer {
    public int Col; public bool Desc;
    public int Compare(object a, object b) {
      var x = ((ListViewItem)a).SubItems[Col]; var y = ((ListViewItem)b).SubItems[Col];
      int r;
      if (x.Tag is double && y.Tag is double) r = ((double)x.Tag).CompareTo((double)y.Tag);
      else r = string.Compare(x.Text, y.Text, StringComparison.CurrentCultureIgnoreCase);
      return Desc ? -r : r;
    }
  }
  // Một nút trong cây thư mục của trang Phân tích. Tệp dưới 1 MB được gộp thành một nút "[N tệp nhỏ]".
  public class DNode {
    public string Name; public string Path; public long Size; public bool IsDir; public int Files;
    public string Ext = ""; public DNode Parent;
    public System.Collections.Generic.List<DNode> Kids = new System.Collections.Generic.List<DNode>();
  }
  public static class DiskTree {
    public static volatile int Scanned;
    public static volatile string Current = "";
    public static DNode Scan(string root) {
      Scanned = 0;
      var di = new System.IO.DirectoryInfo(root);
      var n = new DNode { Name = di.FullName, Path = di.FullName, IsDir = true };
      Walk(di, n);
      return n;
    }
    static void Walk(System.IO.DirectoryInfo d, DNode n) {
      System.IO.FileSystemInfo[] items;
      try { items = d.GetFileSystemInfos(); } catch { return; }
      long small = 0; int smallCount = 0;
      foreach (var it in items) {
        try {
          if ((it.Attributes & System.IO.FileAttributes.ReparsePoint) != 0) continue;
          if ((it.Attributes & System.IO.FileAttributes.Directory) != 0) {
            var c = new DNode { Name = it.Name, Path = it.FullName, IsDir = true, Parent = n };
            Current = it.FullName;
            Walk((System.IO.DirectoryInfo)it, c);
            n.Kids.Add(c); n.Size += c.Size; n.Files += c.Files;
          } else {
            var fi = (System.IO.FileInfo)it; long len = fi.Length;
            Scanned++; n.Files++; n.Size += len;
            if (len >= 1048576) n.Kids.Add(new DNode { Name = fi.Name, Path = fi.FullName, Size = len, Parent = n, Ext = fi.Extension.ToLowerInvariant(), Files = 1 });
            else { small += len; smallCount++; }
          }
        } catch { }
      }
      if (smallCount > 0) n.Kids.Add(new DNode { Name = "[" + smallCount + " tệp nhỏ]", Path = n.Path, Size = small, Parent = n, Ext = "*", Files = smallCount });
      n.Kids.Sort((a, b) => b.Size.CompareTo(a.Size));
    }
  }

  // Bản đồ dung lượng kiểu WizTree: mỗi ô là một thư mục hoặc tệp, diện tích tỉ lệ với dung lượng.
  public class Treemap : Control {
    public DNode Root; public DNode Selected; public int MaxDepth = 3;
    public System.Drawing.Color Line = System.Drawing.Color.FromArgb(20, 35, 27);
    public System.Drawing.Color Header = System.Drawing.Color.FromArgb(160, 200, 180);
    public event Action<DNode> NodeClicked;
    public event Action<DNode> NodeOpened;
    public event Action<DNode> NodeHover;
    readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<System.Drawing.RectangleF, DNode>> hits =
      new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<System.Drawing.RectangleF, DNode>>();
    DNode lastHover;
    public Treemap() {
      SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }
    public static string Fmt(long b) {
      if (b >= 1073741824) return (b / 1073741824.0).ToString("0.0") + " GB";
      if (b >= 1048576) return (b / 1048576.0).ToString("0") + " MB";
      return (b / 1024.0).ToString("0") + " KB";
    }
    public static System.Drawing.Color ColorFor(DNode n) {
      if (n.IsDir) return System.Drawing.Color.FromArgb(46, 74, 60);
      string e = n.Ext;
      if (e == "*") return System.Drawing.Color.FromArgb(52, 66, 58);
      if (",.mp4,.mkv,.avi,.mov,.wmv,.flv,.webm,.m2ts,.ts,.m4v,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(224, 123, 36);
      if (",.jpg,.jpeg,.png,.gif,.bmp,.webp,.psd,.tif,.tiff,.heic,.raw,.cr2,.nef,.arw,.svg,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(155, 93, 229);
      if (",.mp3,.wav,.flac,.aac,.ogg,.m4a,.wma,.bnk,.wem,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(58, 134, 255);
      if (",.zip,.rar,.7z,.iso,.img,.cab,.gz,.tar,.xz,.dmg,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(229, 70, 111);
      if (",.exe,.dll,.sys,.msi,.msp,.so,.node,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(46, 196, 182);
      if (",.pak,.ucas,.utoc,.forge,.bin,.bdt,.archive,.vpk,.bsa,.ba2,.dat,.rpf,.blob,.chunk,.big,.pck,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(212, 194, 28);
      if (",.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.md,.csv,.json,.xml,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(95, 208, 104);
      if (",.vhdx,.vmdk,.vdi,.pagefile,.sys,.log,.etl,.db,.sqlite,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(120, 140, 130);
      return System.Drawing.Color.FromArgb(110, 128, 118);
    }
    protected override void OnPaint(PaintEventArgs e) {
      var g = e.Graphics;
      g.Clear(BackColor);
      hits.Clear();
      if (Root == null || Root.Size <= 0) return;
      Draw(g, Root, new System.Drawing.RectangleF(0, 0, Width - 1, Height - 1), 0);
      if (Selected != null) {
        foreach (var h in hits) if (h.Value == Selected) {
          using (var p = new System.Drawing.Pen(System.Drawing.Color.White, 2)) g.DrawRectangle(p, System.Drawing.Rectangle.Round(h.Key));
          break;
        }
      }
    }
    void Draw(System.Drawing.Graphics g, DNode n, System.Drawing.RectangleF r, int depth) {
      if (r.Width < 1.5f || r.Height < 1.5f) return;
      hits.Add(new System.Collections.Generic.KeyValuePair<System.Drawing.RectangleF, DNode>(r, n));
      var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
      if (n.IsDir && depth < MaxDepth && n.Kids.Count > 0) {
        using (var b = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(10 + depth * 4, 18 + depth * 6, 14 + depth * 4))) g.FillRectangle(b, r);
        using (var p = new System.Drawing.Pen(Line)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
        float hh = (r.Height > 34 && r.Width > 50) ? 16 : 0;
        if (hh > 0) {
          var label = (depth == 0 ? n.Path : n.Name + "\\") + "  " + Fmt(n.Size);
          TextRenderer.DrawText(g, label, Font, new System.Drawing.Rectangle((int)r.X + 4, (int)r.Y + 2, (int)r.Width - 8, 14), Header, flags);
        }
        var inner = new System.Drawing.RectangleF(r.X + 2, r.Y + hh + 1, r.Width - 4, r.Height - hh - 3);
        Squarify(g, n.Kids, inner, depth + 1);
        return;
      }
      var col = ColorFor(n);
      using (var b = new System.Drawing.SolidBrush(col)) g.FillRectangle(b, r);
      using (var p = new System.Drawing.Pen(System.Drawing.Color.FromArgb(7, 11, 9))) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
      if (r.Width > 46 && r.Height > 16) {
        var fg = n.IsDir || n.Ext == "*" ? System.Drawing.Color.FromArgb(200, 225, 210) : System.Drawing.Color.FromArgb(14, 16, 15);
        TextRenderer.DrawText(g, n.Name + (n.IsDir ? "\\" : ""), Font, new System.Drawing.Rectangle((int)r.X + 3, (int)r.Y + 2, (int)r.Width - 6, 14), fg, flags);
        if (r.Height > 32) TextRenderer.DrawText(g, Fmt(n.Size), Font, new System.Drawing.Rectangle((int)r.X + 3, (int)r.Y + 16, (int)r.Width - 6, 14), fg, flags);
      }
    }
    void Squarify(System.Drawing.Graphics g, System.Collections.Generic.List<DNode> kids, System.Drawing.RectangleF r, int depth) {
      double total = 0; foreach (var k in kids) total += k.Size;
      if (total <= 0 || r.Width < 2 || r.Height < 2) return;
      double scale = r.Width * r.Height / total;
      int i = 0; var rem = r;
      while (i < kids.Count && rem.Width >= 1 && rem.Height >= 1) {
        double side = Math.Min(rem.Width, rem.Height);
        var row = new System.Collections.Generic.List<int>(); double rowSum = 0, worst = double.MaxValue;
        while (i < kids.Count) {
          double v = kids[i].Size * scale;
          if (v <= 0) { i++; continue; }
          double sum = rowSum + v, maxV = row.Count == 0 ? v : kids[row[0]].Size * scale;
          double w = Math.Max(side * side * maxV / (sum * sum), (sum * sum) / (side * side * v));
          if (row.Count > 0 && w > worst) break;
          row.Add(i); rowSum = sum; worst = w; i++;
        }
        if (row.Count == 0) break;
        if (rem.Width >= rem.Height) {
          float cw = (float)(rowSum / rem.Height), y = rem.Y;
          foreach (int k in row) { float h = (float)(kids[k].Size * scale / cw); Draw(g, kids[k], new System.Drawing.RectangleF(rem.X, y, cw, h), depth); y += h; }
          rem = new System.Drawing.RectangleF(rem.X + cw, rem.Y, rem.Width - cw, rem.Height);
        } else {
          float rh = (float)(rowSum / rem.Width), x = rem.X;
          foreach (int k in row) { float w2 = (float)(kids[k].Size * scale / rh); Draw(g, kids[k], new System.Drawing.RectangleF(x, rem.Y, w2, rh), depth); x += w2; }
          rem = new System.Drawing.RectangleF(rem.X, rem.Y + rh, rem.Width, rem.Height - rh);
        }
      }
    }
    DNode HitTest(System.Drawing.Point p) {
      for (int i = hits.Count - 1; i >= 0; i--) if (hits[i].Key.Contains(p)) return hits[i].Value;
      return null;
    }
    protected override void OnMouseDown(MouseEventArgs e) {
      base.OnMouseDown(e);
      var n = HitTest(e.Location);
      if (n == null) return;
      Selected = n; Invalidate();
      if (e.Clicks >= 2) { if (NodeOpened != null) NodeOpened(n); }
      else if (NodeClicked != null) NodeClicked(n);
    }
    protected override void OnMouseMove(MouseEventArgs e) {
      base.OnMouseMove(e);
      var n = HitTest(e.Location);
      if (n != lastHover) { lastHover = n; if (NodeHover != null && n != null) NodeHover(n); }
    }
  }

  // Thanh cuộn dọc tự vẽ. Thanh cuộn thật của Target bị che đi (nằm ngoài khung chứa),
  // thanh này đọc vị trí cuộn của Target và điều khiển lại nó.
  public class DarkScroll : Control {
    public Control Target;
    public System.Drawing.Color Thumb = System.Drawing.Color.FromArgb(29, 110, 70);
    public System.Drawing.Color ThumbHot = System.Drawing.Color.FromArgb(59, 227, 143);
    public System.Drawing.Color HeaderBack = System.Drawing.Color.Empty;
    public int TopInset = 0;   // chừa phần tiêu đề cột của ListView
    [StructLayout(LayoutKind.Sequential)]
    struct SCROLLINFO { public int cbSize, fMask, nMin, nMax, nPage, nPos, nTrackPos; }
    [DllImport("user32.dll")] static extern bool GetScrollInfo(IntPtr h, int bar, ref SCROLLINFO si);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    readonly Timer timer = new Timer();
    bool drag, hot; int dragOffset; int lastPos = -1, lastMax = -1, lastPage = -1;
    public DarkScroll() {
      SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
      Width = 10;
      timer.Interval = 60;
      timer.Tick += (s, e) => Sync();
      timer.Start();
    }
    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    SCROLLINFO Info() {
      var si = new SCROLLINFO(); si.cbSize = Marshal.SizeOf(si); si.fMask = 0x17;
      if (Target == null || !Target.IsHandleCreated || !GetScrollInfo(Target.Handle, 1, ref si)) { si.nMin = 0; si.nMax = 0; si.nPage = 1; si.nPos = 0; }
      return si;
    }
    void Sync() {
      if (!Visible) return;
      var si = Info();
      if (si.nPos != lastPos || si.nMax != lastMax || si.nPage != lastPage) { lastPos = si.nPos; lastMax = si.nMax; lastPage = si.nPage; Invalidate(); }
    }
    System.Drawing.Rectangle ThumbRect(SCROLLINFO si) {
      int range = si.nMax - si.nMin + 1;
      if (si.nPage <= 0 || si.nPage >= range) return System.Drawing.Rectangle.Empty;
      int track = Math.Max(1, Height - TopInset);
      int h = Math.Max(28, track * si.nPage / range);
      int maxPos = range - si.nPage;
      int y = maxPos > 0 ? (track - h) * (si.nPos - si.nMin) / maxPos : 0;
      return new System.Drawing.Rectangle(3, TopInset + y + 2, Width - 6, Math.Max(8, h - 4));
    }
    protected override void OnPaint(PaintEventArgs e) {
      e.Graphics.Clear(BackColor);
      if (TopInset > 0 && !HeaderBack.IsEmpty) using (var hb = new System.Drawing.SolidBrush(HeaderBack)) e.Graphics.FillRectangle(hb, 0, 0, Width, TopInset);
      var r = ThumbRect(Info());
      if (r.IsEmpty) return;
      using (var b = new System.Drawing.SolidBrush(drag || hot ? ThumbHot : Thumb)) e.Graphics.FillRectangle(b, r);
    }
    void ScrollTo(int pos) {
      var si = Info();
      int maxPos = si.nMax - si.nMin + 1 - si.nPage;
      pos = Math.Max(si.nMin, Math.Min(si.nMin + Math.Max(0, maxPos), pos));
      if (pos == si.nPos) return;
      var lv = Target as ListView;
      if (lv != null) {
        int rowH = lv.Items.Count > 0 ? lv.GetItemRect(0).Height : 20;
        SendMessage(Target.Handle, 0x1014, IntPtr.Zero, (IntPtr)((pos - si.nPos) * rowH));
      } else {
        SendMessage(Target.Handle, 0x115, (IntPtr)((pos << 16) | 4), IntPtr.Zero);
        SendMessage(Target.Handle, 0x115, (IntPtr)8, IntPtr.Zero);
      }
      Sync();
    }
    protected override void OnMouseDown(MouseEventArgs e) {
      base.OnMouseDown(e);
      var si = Info(); var r = ThumbRect(si);
      if (r.IsEmpty) return;
      if (r.Contains(e.Location)) { drag = true; dragOffset = e.Y - r.Y; Capture = true; Invalidate(); }
      else ScrollTo(si.nPos + (e.Y < r.Y ? -si.nPage : si.nPage));
    }
    protected override void OnMouseMove(MouseEventArgs e) {
      base.OnMouseMove(e);
      var si = Info(); var r = ThumbRect(si);
      bool h = !r.IsEmpty && r.Contains(e.Location);
      if (h != hot) { hot = h; Invalidate(); }
      if (!drag || r.IsEmpty) return;
      int range = si.nMax - si.nMin + 1, maxPos = range - si.nPage, track = Height - TopInset - r.Height - 4;
      if (track <= 0) return;
      ScrollTo(si.nMin + (int)Math.Round((double)(e.Y - dragOffset - TopInset - 2) * maxPos / track));
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); drag = false; Capture = false; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (hot) { hot = false; Invalidate(); } }
    protected override void OnMouseWheel(MouseEventArgs e) {
      base.OnMouseWheel(e);
      var si = Info();
      ScrollTo(si.nPos - Math.Sign(e.Delta) * 3);
    }
  }

  public static class Native {
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] public static extern int SetWindowTheme(IntPtr h, string app, string id);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern bool ShowScrollBar(IntPtr h, int bar, bool show);
    [DllImport("uxtheme.dll", EntryPoint = "#135")] public static extern int SetPreferredAppMode(int mode);
    [DllImport("uxtheme.dll", EntryPoint = "#136")] public static extern void FlushMenuThemes();
    [DllImport("uxtheme.dll", EntryPoint = "#133")] public static extern bool AllowDarkModeForWindow(IntPtr h, bool allow);
    [DllImport("uxtheme.dll", EntryPoint = "#104")] public static extern void RefreshImmersiveColorPolicyState();
  }
}
"@
}

if ($Relaunched) { [void][MTC.Native]::ShowWindow([MTC.Native]::GetConsoleWindow(), 0) }
try { [void][MTC.Native]::SetPreferredAppMode(2); [MTC.Native]::RefreshImmersiveColorPolicyState(); [MTC.Native]::FlushMenuThemes() } catch { }

$DataDir = Join-Path $env:LOCALAPPDATA 'CleanVaiLon'
New-Item $DataDir -ItemType Directory -Force | Out-Null
$LogFile = Join-Path $DataDir 'nhatky.txt'
$AppVersion = '1.3.0'

# ----- Cài đặt (lưu trong caidat.json) -----
$SettingsFile = Join-Path $DataDir 'caidat.json'
$CfgDefaults = [ordered]@{ Lang = 'vi'; StartPage = 'perf'; AutoLoad = $true; HeavyPct = 5; Months = 6; DoubleConfirm = $true; Grid = $true }
$Cfg = @{}
foreach ($k in $CfgDefaults.Keys) { $Cfg[$k] = $CfgDefaults[$k] }
if (Test-Path $SettingsFile) {
    try {
        $j = Get-Content $SettingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($k in $CfgDefaults.Keys) { if ($null -ne $j.$k) { $Cfg[$k] = $j.$k } }
    } catch { }
}
function Save-Settings { [pscustomobject]$Cfg | ConvertTo-Json | Set-Content -Path $SettingsFile -Encoding UTF8 }
$ToolServicesFile = Join-Path $DataDir 'dichvu_da_tat.txt'
function Write-Log([string]$msg) { Add-Content -Path $LogFile -Value ("{0:yyyy-MM-dd HH:mm:ss}  {1}" -f (Get-Date), $msg) -Encoding UTF8 }

# ----- Bảng màu và phông chữ -----
function Hex([string]$h) { [Drawing.ColorTranslator]::FromHtml($h) }
$Theme = @{
    Bg = Hex '#070B09'; Side = Hex '#060907'; Panel = Hex '#0A110D'; Row = Hex '#080D0A'; RowAlt = Hex '#0A120E'
    RowSel = Hex '#0E2619'; Line = Hex '#14231B'; Grid = Hex '#0A130F'
    Green = Hex '#3BE38F'; GreenDim = Hex '#1D6E46'; Text = Hex '#C4E3D2'; Bright = Hex '#E6F5EC'; Muted = Hex '#5C7E6C'
    Amber = Hex '#E8B04A'; Red = Hex '#FF6B6B'; Cyan = Hex '#4FC9DE'
    WarnBg = Hex '#15130A'; BadBg = Hex '#1A0C0E'
}
$fams = (New-Object Drawing.Text.InstalledFontCollection).Families | ForEach-Object Name
$mono = @('JetBrains Mono', 'Cascadia Mono', 'Cascadia Code', 'Consolas') | Where-Object { $fams -contains $_ } | Select-Object -First 1
$Fonts = @{
    Main = New-Object Drawing.Font($mono, 9.5)
    Small = New-Object Drawing.Font($mono, 8.5)
    Bold = New-Object Drawing.Font($mono, 9.5, [Drawing.FontStyle]::Bold)
    Title = New-Object Drawing.Font($mono, 11.5, [Drawing.FontStyle]::Bold)
    Logo = New-Object Drawing.Font($mono, 10.5, [Drawing.FontStyle]::Bold)
}
$Pens = @{ Check = (New-Object Drawing.Pen($Theme.Bg, 2)); Disabled = New-Object Drawing.Pen((Hex '#23392D')); Line = New-Object Drawing.Pen($Theme.Line); Grid = New-Object Drawing.Pen($Theme.Grid); Green = New-Object Drawing.Pen($Theme.Green); GreenDim = New-Object Drawing.Pen($Theme.GreenDim) }
$Brushes = @{ Green = New-Object Drawing.SolidBrush($Theme.Green); Bg = New-Object Drawing.SolidBrush($Theme.Bg); GreenDim = New-Object Drawing.SolidBrush($Theme.GreenDim); Track = New-Object Drawing.SolidBrush((Hex '#12251B')) }

function Set-DoubleBuffer($ctl) {
    $ctl.GetType().GetProperty('DoubleBuffered', [Reflection.BindingFlags]'NonPublic,Instance').SetValue($ctl, $true, $null)
}
# Bọc control trong một khung: thanh cuộn thật bị đẩy ra ngoài mép khung (bị che),
# thay bằng thanh cuộn tự vẽ cùng màu giao diện.
function Add-DarkScroll($ctl) {
    $parent = $ctl.Parent
    $box = New-Object Windows.Forms.Panel
    $box.Dock = $ctl.Dock; $box.BackColor = $ctl.BackColor
    if ($ctl.Dock -eq 'None') { $box.Bounds = $ctl.Bounds }
    $idx = $parent.Controls.GetChildIndex($ctl)
    $parent.Controls.Add($box); $parent.Controls.SetChildIndex($box, $idx)
    $parent.Controls.Remove($ctl)
    $ctl.Dock = 'None'
    $box.Controls.Add($ctl)
    $sb = New-Object MTC.DarkScroll
    $sb.Target = $ctl; $sb.Dock = 'Right'; $sb.Width = 10; $sb.BackColor = $ctl.BackColor
    $sb.Thumb = $Theme.GreenDim; $sb.ThumbHot = $Theme.Green
    $box.Controls.Add($sb); $sb.BringToFront()
    $box.Tag = @{ Inner = $ctl; Bar = $sb }
    $box.Add_Resize({
        param($s, $e)
        $w = $s.ClientSize.Width - 10 + [Windows.Forms.SystemInformation]::VerticalScrollBarWidth
        $s.Tag.Inner.Bounds = New-Object Drawing.Rectangle(0, 0, $w, $s.ClientSize.Height)
    })
    return $box
}
function Set-DarkTitle($f) {
    $on = 1; [void][MTC.Native]::DwmSetWindowAttribute($f.Handle, 20, [ref]$on, 4)
    $round = 2; [void][MTC.Native]::DwmSetWindowAttribute($f.Handle, 33, [ref]$round, 4)
    $border = ($Theme.Line.B -shl 16) -bor ($Theme.Line.G -shl 8) -bor $Theme.Line.R
    [void][MTC.Native]::DwmSetWindowAttribute($f.Handle, 34, [ref]$border, 4)
}
# Kéo cửa sổ khi giữ chuột trên thanh tiêu đề tự vẽ.
function Start-WindowDrag($f) {
    [void][MTC.Native]::ReleaseCapture()
    [void][MTC.Native]::SendMessage($f.Handle, 0xA1, [IntPtr]2, [IntPtr]::Zero)
}
# Phóng to vừa vùng làm việc (không che thanh tác vụ), tính theo màn hình đang chứa cửa sổ.
function Switch-Maximize($f) {
    if ($f.WindowState -eq 'Maximized') { $f.WindowState = 'Normal'; return }
    $scr = [Windows.Forms.Screen]::FromControl($f)
    $wa = $scr.WorkingArea
    $f.MaximizedBounds = New-Object Drawing.Rectangle(($wa.X - $scr.Bounds.X), ($wa.Y - $scr.Bounds.Y), $wa.Width, $wa.Height)
    $f.WindowState = 'Maximized'
}
$iconFont = @('Segoe Fluent Icons', 'Segoe MDL2 Assets') | Where-Object { $fams -contains $_ } | Select-Object -First 1
$Glyph = if ($iconFont) { @{ Min = [string][char]0xE921; Max = [string][char]0xE922; Restore = [string][char]0xE923; Close = [string][char]0xE8BB } } else { @{ Min = '—'; Max = '□'; Restore = '❐'; Close = '✕' } }
$Fonts.Icon = if ($iconFont) { New-Object Drawing.Font($iconFont, 8) } else { $Fonts.Main }

# Nút cửa sổ (thu nhỏ / phóng to / đóng) theo cùng phong cách.
function New-WindowButton($parent, [string]$glyph, [bool]$danger, [scriptblock]$onClick) {
    $b = New-Object Windows.Forms.Label
    $b.Text = $glyph; $b.Font = $Fonts.Icon; $b.ForeColor = $Theme.Muted; $b.BackColor = $Theme.Side
    $b.AutoSize = $false; $b.Size = New-Object Drawing.Size(46, 34); $b.TextAlign = 'MiddleCenter'
    $b.Dock = 'Right'; $b.Cursor = 'Hand'
    $b.Tag = $danger
    $b.Add_MouseEnter({ param($s, $e) if ($s.Tag) { $s.BackColor = $Theme.Red; $s.ForeColor = $Theme.Bg } else { $s.BackColor = $Theme.RowSel; $s.ForeColor = $Theme.Green } })
    $b.Add_MouseLeave({ param($s, $e) $s.BackColor = $Theme.Side; $s.ForeColor = $Theme.Muted })
    $b.Add_Click($onClick)
    $parent.Controls.Add($b)
    return $b
}

# ----- Cửa sổ chính (không dùng thanh tiêu đề của Windows) -----
$form = New-Object MTC.BorderlessForm
$form.Text = 'Clean VaiLon'
$form.Size = New-Object Drawing.Size(1240, 780)
$form.MinimumSize = New-Object Drawing.Size(960, 600)
$form.StartPosition = 'CenterScreen'
$form.BackColor = $Theme.Side
$form.ForeColor = $Theme.Text
$form.Font = $Fonts.Main
$form.Padding = New-Object Windows.Forms.Padding(1)
$form.Grip = 6
$form.Add_HandleCreated({ Set-DarkTitle $this })
$IconPath = Join-Path $ScriptDir 'CleanVaiLon.ico'
if (Test-Path $IconPath) { $form.Icon = New-Object Drawing.Icon($IconPath) }
$form.Add_Paint({ param($s, $e) if ($s.WindowState -eq 'Normal') { $e.Graphics.DrawRectangle($Pens.Line, 0, 0, $s.Width - 1, $s.Height - 1) } })
$form.Add_Resize({
    $form.Padding = $(if ($form.WindowState -eq 'Maximized') { New-Object Windows.Forms.Padding(0) } else { New-Object Windows.Forms.Padding(1) })
    if ($script:btnMax) { $script:btnMax.Text = $(if ($form.WindowState -eq 'Maximized') { $Glyph.Restore } else { $Glyph.Max }) }
    $form.Invalidate()
})

$content = New-Object Windows.Forms.Panel
$content.Dock = 'Fill'; $content.BackColor = $Theme.Bg
Set-DoubleBuffer $content
$content.Add_Paint({
    param($s, $e)
    if (-not $Cfg.Grid) { return }
    for ($x = 0; $x -lt $s.Width; $x += 28) { $e.Graphics.DrawLine($Pens.Grid, $x, 0, $x, $s.Height) }
    for ($y = 0; $y -lt $s.Height; $y += 28) { $e.Graphics.DrawLine($Pens.Grid, 0, $y, $s.Width, $y) }
})

$side = New-Object Windows.Forms.Panel
$side.Dock = 'Left'; $side.Width = 196; $side.BackColor = $Theme.Side
$side.Add_Paint({ param($s, $e) $e.Graphics.DrawLine($Pens.Line, $s.Width - 1, 0, $s.Width - 1, $s.Height) })

$top = New-Object Windows.Forms.Panel
$top.Dock = 'Top'; $top.Height = 44; $top.BackColor = $Theme.Side
$top.Add_Paint({ param($s, $e) $e.Graphics.DrawLine($Pens.Line, 0, $s.Height - 1, $s.Width, $s.Height - 1) })
$logo = New-Object Windows.Forms.Label
$logo.Text = '>_ CLEAN VAILON'; $logo.Font = $Fonts.Logo; $logo.ForeColor = $Theme.Green; $logo.AutoSize = $true
$logo.Location = New-Object Drawing.Point(16, 13); $logo.BackColor = $Theme.Side
$adminTag = New-Object Windows.Forms.Label
$adminTag.Text = $(if ($IsAdmin) { 'QUẢN TRỊ' } else { 'KHÔNG CÓ QUYỀN QUẢN TRỊ' })
$adminTag.Font = $Fonts.Small; $adminTag.AutoSize = $true; $adminTag.BackColor = $Theme.Side
$adminTag.ForeColor = $(if ($IsAdmin) { $Theme.Green } else { $Theme.Amber })
$adminTag.Padding = New-Object Windows.Forms.Padding(6, 3, 6, 3)
$adminTag.Anchor = 'Top, Right'
$adminTag.Add_Paint({ param($s, $e) $p = New-Object Drawing.Pen($s.ForeColor); $e.Graphics.DrawRectangle($p, 0, 0, $s.Width - 1, $s.Height - 1); $p.Dispose() })
$winButtons = New-Object Windows.Forms.Panel
$winButtons.Dock = 'Right'; $winButtons.Width = 138; $winButtons.BackColor = $Theme.Side
$winButtons.Padding = New-Object Windows.Forms.Padding(0)
# Nút thêm sau nằm sát mép phải, nên thêm theo thứ tự thu nhỏ → phóng to → đóng.
[void](New-WindowButton $winButtons $Glyph.Min $false { $form.WindowState = 'Minimized' })
$script:btnMax = New-WindowButton $winButtons $Glyph.Max $false { Switch-Maximize $form }
[void](New-WindowButton $winButtons $Glyph.Close $true { $form.Close() })
$top.Padding = New-Object Windows.Forms.Padding(0, 0, 0, 1)
$top.Controls.Add($logo); $top.Controls.Add($adminTag); $top.Controls.Add($winButtons)
$top.Add_Resize({ $adminTag.Location = New-Object Drawing.Point(($top.Width - $winButtons.Width - $adminTag.Width - 12), 11) })
# Giữ chuột để kéo, bấm đúp để phóng to / thu lại.
foreach ($ctl in @($top, $logo)) {
    $ctl.Add_MouseDown({
        param($s, $e)
        if ($e.Button -ne 'Left') { return }
        if ($e.Clicks -ge 2) { Switch-Maximize $form } else { Start-WindowDrag $form }
    })
}

$form.Controls.Add($content)
$form.Controls.Add($side)
$form.Controls.Add($top)

# Menu bên trái và ô trạng thái ở dưới
$navFlow = New-Object Windows.Forms.FlowLayoutPanel
$navFlow.Dock = 'Fill'; $navFlow.FlowDirection = 'TopDown'; $navFlow.WrapContents = $false
$navFlow.BackColor = $Theme.Side; $navFlow.Padding = New-Object Windows.Forms.Padding(0, 14, 0, 0)
$sideStatus = New-Object Windows.Forms.Panel
$sideStatus.Dock = 'Bottom'; $sideStatus.Height = 128; $sideStatus.BackColor = $Theme.Side
$sideStatus.Add_Paint({ param($s, $e) $e.Graphics.DrawLine($Pens.Line, 0, 0, $s.Width, 0) })
$lblState = New-Object Windows.Forms.Label
$lblState.Font = $Fonts.Bold; $lblState.ForeColor = $Theme.Green; $lblState.AutoSize = $true
$lblState.Location = New-Object Drawing.Point(14, 14); $lblState.Text = '● SẴN SÀNG'
$lblJob = New-Object Windows.Forms.Label
$lblJob.Font = $Fonts.Small; $lblJob.ForeColor = $Theme.Muted; $lblJob.AutoSize = $false
$lblJob.Location = New-Object Drawing.Point(14, 38); $lblJob.Size = New-Object Drawing.Size(170, 40)
$lblJob.Text = $(if ($IsAdmin) { 'đủ quyền để đọc mọi thông tin' } else { 'thiếu quyền – một số mục sẽ trống' })
$sideStatus.Controls.Add($lblState); $sideStatus.Controls.Add($lblJob)
$side.Controls.Add($navFlow)
$side.Controls.Add($sideStatus)

# ----- Chạy việc nặng ở luồng nền để giao diện không bị đơ -----
$script:Jobs = New-Object System.Collections.ArrayList
$script:Spin = 0
function Update-Busy {
    if ($script:Jobs.Count -gt 0) {
        $lblState.ForeColor = $Theme.Amber
        $lblJob.Text = (($script:Jobs | ForEach-Object Label) -join ', ').ToLower() + '…'
    } else {
        $lblState.ForeColor = $Theme.Green
        $lblState.Text = (Tr '● SẴN SÀNG')
    }
}
function Test-Busy([string]$label) { return [bool]($script:Jobs | Where-Object { $_.Label -eq $label }) }
function Start-Work([string]$label, [string]$call, [object[]]$argList, [scriptblock]$onDone, [scriptblock]$progress) {
    if (Test-Busy $label) { return }
    $ps = [powershell]::Create()
    [void]$ps.AddScript("param(`$A0, `$A1)`n" + $WorkerCode + "`n" + $call)
    if ($argList) { foreach ($a in $argList) { [void]$ps.AddArgument($a) } }
    $h = $ps.BeginInvoke()
    $pg = $script:Pages[$script:CurrentPage]
    [void]$script:Jobs.Add([pscustomobject]@{ PS = $ps; H = $h; Done = $onDone; Label = $label; Page = $pg; Start = [DateTime]::Now; Progress = $progress })
    if ($pg) {
        $o = $pg.Overlay
        $o.Title.Text = '>_ ' + $label.ToLower() + '…'
        $o.Bar.Tag = 0.0; $o.Bar.Invalidate(); $o.Sub.Text = ''
        $o.Panel.Location = New-Object Drawing.Point([int](($pg.Panel.Width - 460) / 2), [int](($pg.Panel.Height - 104) / 2))
        $o.Panel.Visible = $true; $o.Panel.BringToFront()
    }
    Update-Busy
}
# Thanh chạy kiểu terminal: hàng ô nhỏ kín chiều ngang, một cụm ô sáng chạy qua lại.
function Draw-ScanBar($g, [int]$w, [int]$h, [double]$seconds) {
    $g.Clear($Theme.Panel)
    $seg = 8; $gap = 3
    $count = [int][Math]::Floor(($w + $gap) / ($seg + $gap))
    $len = [Math]::Max(4, [int]($count / 5))
    $span = [Math]::Max(1, $count - $len)
    $pos = [int]($seconds * 18) % (2 * $span)
    if ($pos -ge $span) { $pos = 2 * $span - $pos }
    for ($i = 0; $i -lt $count; $i++) {
        $brush = if ($i -ge $pos -and $i -lt $pos + $len) { $Brushes.Green } else { $Brushes.Track }
        $g.FillRectangle($brush, $i * ($seg + $gap), 0, $seg, $h)
    }
}
# Vẽ ô tích: cùng một khung cho cả hai trạng thái, dấu tích vẽ bằng nét để không bị lệch.
function Draw-CheckBox($g, [int]$x, [int]$y, [bool]$checked) {
    if ($checked) {
        $g.FillRectangle($Brushes.Green, $x, $y, 14, 14)
        $old = $g.SmoothingMode; $g.SmoothingMode = 'AntiAlias'
        $g.DrawLines($Pens.Check, [Drawing.Point[]]@((New-Object Drawing.Point(($x + 3), ($y + 7))), (New-Object Drawing.Point(($x + 6), ($y + 10))), (New-Object Drawing.Point(($x + 11), ($y + 4)))))
        $g.SmoothingMode = $old
    } else {
        $g.DrawRectangle($Pens.GreenDim, $x, $y, 13, 13)
    }
}
# "đã chọn N · X GB [✕]" cạnh tiêu đề trang; bấm vào để bỏ chọn.
function Update-SelCount($lv) {
    $lbl = $lv.Tag.SelLabel
    if (-not $lbl) { return }
    $n = $lv.CheckedItems.Count
    if ($n -eq 0) { $lbl.Text = ''; return }
    $sum = 0
    foreach ($i in $lv.CheckedItems) { $v = $i.Tag.Size; if ($v) { $sum += [double]$v } }
    $lbl.Text = "·  $(Tr 'đã chọn') $n" + $(if ($sum) { ' · ' + (Format-Size $sum) } else { ' ' + (Tr 'mục') }) + '  [✕]'
}
$timer = New-Object Windows.Forms.Timer
$timer.Interval = 80
$timer.Add_Tick({
    if ($script:Jobs.Count -gt 0) {
        $script:Spin = ($script:Spin + 1) % 12
        $lblState.Text = (Tr '● ĐANG LÀM') + ' ' + @('|', '/', '-', '\')[[int]($script:Spin / 3)]
    }
    foreach ($j in @($script:Jobs)) {
        if ($j.Page -and -not $j.H.IsCompleted) {
            $sec = ([DateTime]::Now - $j.Start).TotalSeconds
            $j.Page.Overlay.Bar.Tag = $sec; $j.Page.Overlay.Bar.Invalidate()
            $extra = ''
            if ($j.Progress) { try { $extra = [string](& $j.Progress) } catch { } }
            $j.Page.Overlay.Sub.Text = ('đã chạy {0:N0} giây' -f $sec) + $(if ($extra) { " · $extra" } else { '' })
        }
        if (-not $j.H.IsCompleted) { continue }
        $script:Jobs.Remove($j)
        if ($j.Page -and -not ($script:Jobs | Where-Object { $_.Page -eq $j.Page })) { $j.Page.Overlay.Panel.Visible = $false }
        $res = $null; $err = $null
        try { $res = $j.PS.EndInvoke($j.H) } catch { $err = $_.Exception.InnerException.Message; if (-not $err) { $err = $_.Exception.Message } }
        $j.PS.Dispose()
        $last = $null
        if ($res -and $res.Count -gt 0) { $last = $res[$res.Count - 1] }
        try { & $j.Done $last $err } catch { Show-Info "Lỗi khi hiển thị kết quả: $($_.Exception.Message)" }
        Update-Busy
        if ($script:Jobs.Count -eq 0) { $lblJob.Text = "xong: $($j.Label.ToLower()) lúc $(Get-Date -Format 'HH:mm:ss')" }
    }
})
$timer.Start()

# ----- Hộp thoại tối -----
function Show-Dialog([string]$title, [string]$text, [bool]$yesNo) {
    $d = New-Object MTC.BorderlessForm
    $d.Grip = 0
    $d.Text = 'Clean VaiLon'; $d.Size = New-Object Drawing.Size(640, 440)
    $d.StartPosition = 'CenterParent'; $d.MaximizeBox = $false; $d.MinimizeBox = $false
    $d.BackColor = $Theme.Bg; $d.ForeColor = $Theme.Text; $d.Font = $Fonts.Main; $d.ShowInTaskbar = $false
    $d.Padding = New-Object Windows.Forms.Padding(1)
    $d.Add_HandleCreated({ Set-DarkTitle $this })
    $d.Add_Paint({ param($s, $e) $e.Graphics.DrawRectangle($Pens.GreenDim, 0, 0, $s.Width - 1, $s.Height - 1) })
    $body = New-Object Windows.Forms.Panel
    $body.Dock = 'Fill'; $body.Padding = New-Object Windows.Forms.Padding(16, 4, 16, 4); $body.BackColor = $Theme.Bg
    $box = New-Object Windows.Forms.Panel
    $box.Dock = 'Fill'; $box.BackColor = $Theme.Panel; $box.Padding = New-Object Windows.Forms.Padding(12, 10, 12, 10)
    $tb = New-Object Windows.Forms.TextBox
    $tb.Multiline = $true; $tb.ReadOnly = $true; $tb.BorderStyle = 'None'; $tb.Dock = 'Fill'; $tb.ScrollBars = 'Vertical'
    $tb.BackColor = $Theme.Panel; $tb.ForeColor = $Theme.Text; $tb.Font = $Fonts.Main; $tb.Text = $text
    $tb.Add_HandleCreated({ param($s, $e) [void][MTC.Native]::AllowDarkModeForWindow($s.Handle, $true); [void][MTC.Native]::SetWindowTheme($s.Handle, 'DarkMode_Explorer', $null) })
    $box.Controls.Add($tb); $body.Controls.Add($box)
    [void](Add-DarkScroll $tb)
    $h = New-Object Windows.Forms.Panel
    $h.Dock = 'Top'; $h.Height = 40; $h.BackColor = $Theme.Bg
    $hl = New-Object Windows.Forms.Label
    $hl.Text = '>_ ' + (Tr $title).ToUpper(); $hl.Font = $Fonts.Bold; $hl.ForeColor = $Theme.Green; $hl.BackColor = $Theme.Bg
    $hl.AutoSize = $true; $hl.Location = New-Object Drawing.Point(16, 14)
    $h.Controls.Add($hl)
    $hx = New-WindowButton $h $Glyph.Close $true { $this.FindForm().Close() }
    $hx.BackColor = $Theme.Bg
    $hx.Add_MouseLeave({ param($s, $e) $s.BackColor = $Theme.Bg })
    foreach ($ctl in @($h, $hl)) { $ctl.Add_MouseDown({ param($s, $e) if ($e.Button -eq 'Left') { Start-WindowDrag $s.FindForm() } }) }
    $bf = New-Object Windows.Forms.FlowLayoutPanel
    $bf.Dock = 'Bottom'; $bf.Height = 52; $bf.FlowDirection = 'RightToLeft'; $bf.Padding = New-Object Windows.Forms.Padding(12, 10, 12, 8)
    if ($yesNo) {
        $y = New-Btn $bf 'có, tiếp tục' $null -Primary; $y.DialogResult = 'Yes'
        $n = New-Btn $bf 'không' $null; $n.DialogResult = 'No'
        $d.CancelButton = $n
    } else {
        $o = New-Btn $bf 'đóng' $null -Primary; $o.DialogResult = 'OK'
        $d.AcceptButton = $o; $d.CancelButton = $o
    }
    $d.Controls.Add($body); $d.Controls.Add($bf); $d.Controls.Add($h)
    $d.Add_Shown({ $tb.SelectionLength = 0; $bf.Controls[0].Focus() })
    Apply-Language $d
    $r = $d.ShowDialog($form)
    $d.Dispose()
    return $r
}
function Confirm([string]$text) { return (Show-Dialog 'xác nhận' $text $true) -eq 'Yes' }
function Show-Info([string]$text) { [void](Show-Dialog 'thông báo' $text $false) }

# Dòng giải thích "xoá được không" cho khung chi tiết.
function Format-SafetyText([string]$path, [bool]$isDir) {
    $s = Get-DeleteSafety $path
    $lines = @('{0,-14} {1} – {2}' -f 'xoá được?:', $s.Label.ToUpper(), $s.Why)
    if (-not $isDir) { foreach ($x in Get-FileFacts $path) { $lines += ('{0,-14} {1}' -f 'lưu ý:', $x) } }
    return $lines -join "`r`n"
}
# Hỏi trước khi chuyển vào Thùng rác, chia theo mức an toàn. Mục "cấm xoá" luôn bị bỏ ra.
function Confirm-Recycle($items) {
    $groups = @{ system = @(); keep = @(); review = @(); safe = @() }
    foreach ($it in $items) {
        $s = Get-DeleteSafety $it.Path
        $groups[$s.Level] += "• $($it.Path)  ($(Format-Size $it.Size))`r`n    $($s.Why)"
    }
    $go = $groups.safe.Count + $groups.review.Count + $groups.keep.Count
    if ($go -eq 0) {
        Show-Info ("Tất cả mục đã chọn đều là mục hệ thống, công cụ không cho xoá:`r`n`r`n" + ($groups.system -join "`r`n"))
        return $false
    }
    $msg = "Chuyển $go mục vào Thùng rác?`r`n"
    if ($groups.keep) { $msg += "`r`n!! KHÔNG NÊN XOÁ ($($groups.keep.Count)) – chỉ tiếp tục nếu bạn chắc chắn:`r`n" + ($groups.keep -join "`r`n") + "`r`n" }
    if ($groups.review) { $msg += "`r`nCÂN NHẮC ($($groups.review.Count)):`r`n" + ($groups.review -join "`r`n") + "`r`n" }
    if ($groups.safe) { $msg += "`r`nAN TOÀN ($($groups.safe.Count)):`r`n" + ($groups.safe -join "`r`n") + "`r`n" }
    if ($groups.system) { $msg += "`r`nSẼ BỎ QUA – mục hệ thống ($($groups.system.Count)):`r`n" + ($groups.system -join "`r`n") + "`r`n" }
    $msg += "`r`nMục nào lớn hơn chỗ trống của Thùng rác sẽ được bỏ qua, không bị xoá vĩnh viễn."
    if (-not (Confirm $msg)) { return $false }
    if ($groups.keep -and $Cfg.DoubleConfirm) { return (Confirm "Xác nhận lần nữa: bạn muốn xoá $($groups.keep.Count) mục được đánh dấu KHÔNG NÊN XOÁ?") }
    return $true
}

# ----- Nút, nút bật/tắt, nhóm bộ lọc -----
function Style-Button($b, [bool]$primary) {
    $b.FlatStyle = 'Flat'; $b.UseVisualStyleBackColor = $false; $b.UseMnemonic = $false
    $b.FlatAppearance.BorderSize = 1
    $b.FlatAppearance.BorderColor = $(if ($primary) { $Theme.Green } else { $Theme.GreenDim })
    $b.FlatAppearance.MouseOverBackColor = $Theme.RowSel
    $b.FlatAppearance.MouseDownBackColor = $Theme.GreenDim
    $b.BackColor = $Theme.Bg; $b.ForeColor = $(if ($primary) { $Theme.Green } else { $Theme.Text })
    $b.Font = $Fonts.Main; $b.AutoSize = $true; $b.AutoSizeMode = 'GrowAndShrink'
    $b.Padding = New-Object Windows.Forms.Padding(6, 1, 6, 1); $b.Cursor = 'Hand'
}
function New-Btn($flow, [string]$text, [scriptblock]$onClick, [switch]$Primary) {
    $b = New-Object Windows.Forms.Button
    $b.Text = $text
    Style-Button $b $Primary.IsPresent
    $b.Margin = New-Object Windows.Forms.Padding(8, 0, 0, 6)
    $b.Tag = @{ Primary = $Primary.IsPresent }
    # Windows vẽ nút bị khoá rất tối trên nền đen, nên tự vẽ lại cho dễ đọc.
    $b.Add_Paint({
        param($s, $e)
        if ($s.Enabled) { return }
        $e.Graphics.Clear($Theme.Bg)
        $e.Graphics.DrawRectangle($Pens.Disabled, 0, 0, $s.Width - 1, $s.Height - 1)
        [Windows.Forms.TextRenderer]::DrawText($e.Graphics, $s.Text, $s.Font, (New-Object Drawing.Rectangle(0, 0, $s.Width, $s.Height)), $Theme.Muted, [Windows.Forms.TextFormatFlags]'HorizontalCenter, VerticalCenter, NoPrefix, SingleLine')
    })
    if ($onClick) { $b.Add_Click($onClick) }
    $flow.Controls.Add($b)
    return $b
}
# Nút chỉ bấm được khi điều kiện đúng (ví dụ đã chọn một dòng); khi không bấm được thì mờ đi.
$script:Bound = New-Object System.Collections.ArrayList
function Bind-Enabled($btn, [scriptblock]$condition) {
    [void]$script:Bound.Add(@{ Btn = $btn; Cond = $condition })
    Update-Bound
}
function Update-Bound {
    foreach ($x in $script:Bound) {
        $b = $x.Btn
        $on = [bool](& $x.Cond)
        if ($b.Enabled -eq $on -and $b.Tag.Styled) { continue }
        $b.Enabled = $on; $b.Tag.Styled = $true
        if ($on) {
            $b.FlatAppearance.BorderColor = $(if ($b.Tag.Primary) { $Theme.Green } else { $Theme.GreenDim })
            $b.ForeColor = $(if ($b.Tag.Primary) { $Theme.Green } else { $Theme.Text }); $b.Cursor = 'Hand'
        } else {
            $b.FlatAppearance.BorderColor = $Theme.Line; $b.ForeColor = $Theme.Muted; $b.Cursor = 'Default'
        }
    }
}
function Set-ChipState($b, [bool]$on) {
    $b.Tag.On = $on
    if ($on) { $b.BackColor = $Theme.Green; $b.ForeColor = $Theme.Bg; $b.FlatAppearance.BorderColor = $Theme.Green; $b.FlatAppearance.MouseOverBackColor = $Theme.Green }
    else { $b.BackColor = $Theme.Bg; $b.ForeColor = $Theme.Green; $b.FlatAppearance.BorderColor = $Theme.GreenDim; $b.FlatAppearance.MouseOverBackColor = $Theme.RowSel }
}
function New-ChipButton($flow, [string]$text, [hashtable]$tag) {
    $b = New-Object Windows.Forms.Button
    $b.Text = $text
    Style-Button $b $false
    $b.Font = $Fonts.Small
    $b.Padding = New-Object Windows.Forms.Padding(2, 0, 2, 0)
    $b.Margin = New-Object Windows.Forms.Padding(0, 0, 6, 6)
    $b.Tag = $tag
    $flow.Controls.Add($b)
    return $b
}
function New-Toggle($flow, [string]$text, [bool]$on, [scriptblock]$onChange) {
    $b = New-ChipButton $flow $text @{ On = $on; Change = $onChange }
    Set-ChipState $b $on
    $b.Add_Click({ param($s, $e) Set-ChipState $s (-not $s.Tag.On); if ($s.Tag.Change) { & $s.Tag.Change } })
    return $b
}
function New-ChipGroup($flow, $options, [scriptblock]$onChange) {
    $list = New-Object System.Collections.ArrayList
    $first = $true
    foreach ($k in $options.Keys) {
        $b = New-ChipButton $flow $options[$k] @{ On = $false; Key = $k; Group = $list; Change = $onChange }
        Set-ChipState $b $first
        $first = $false
        $b.Add_Click({
            param($s, $e)
            foreach ($o in $s.Tag.Group) { Set-ChipState $o $false }
            Set-ChipState $s $true
            if ($s.Tag.Change) { & $s.Tag.Change $s.Tag.Key }
        })
        [void]$list.Add($b)
    }
    return $list
}
function Get-ChipKey($group) { ($group | Where-Object { $_.Tag.On } | Select-Object -First 1).Tag.Key }
function Select-Chip($group, $key) { foreach ($o in $group) { Set-ChipState $o ($o.Tag.Key -eq $key) } }
function New-Text($flow, [string]$text) {
    $l = New-Object Windows.Forms.Label
    $l.Text = $text; $l.AutoSize = $true; $l.ForeColor = $Theme.Muted; $l.Font = $Fonts.Small; $l.BackColor = [Drawing.Color]::Transparent
    $l.Margin = New-Object Windows.Forms.Padding(0, 6, 6, 0)
    $flow.Controls.Add($l)
    return $l
}
# Ô chọn số kiểu  [−] 6 [+]  cùng phong cách với nút lọc. Cuộn chuột trên số cũng đổi được.
function New-Stepper($flow, [int]$value, [int]$min, [int]$max, [scriptblock]$onChange) {
    $state = @{ Value = $value; Min = $min; Max = $max; Change = $onChange; Label = $null }
    $minus = New-ChipButton $flow '−' @{ On = $false; S = $state }
    Set-ChipState $minus $false
    $minus.Margin = New-Object Windows.Forms.Padding(0, 0, 0, 6)
    $val = New-Object Windows.Forms.Label
    $val.Text = "$value"; $val.AutoSize = $false; $val.TextAlign = 'MiddleCenter'
    $val.Font = $Fonts.Bold; $val.ForeColor = $Theme.Green; $val.BackColor = $Theme.Panel
    $val.Size = New-Object Drawing.Size(38, $minus.Height); $val.Margin = New-Object Windows.Forms.Padding(0, 0, 0, 6)
    $val.Tag = @{ S = $state }
    $val.Add_Paint({ param($s, $e) $e.Graphics.DrawLine($Pens.GreenDim, 0, 0, $s.Width, 0); $e.Graphics.DrawLine($Pens.GreenDim, 0, $s.Height - 1, $s.Width, $s.Height - 1) })
    $flow.Controls.Add($val)
    $plus = New-ChipButton $flow '+' @{ On = $false; S = $state }
    Set-ChipState $plus $false
    $state.Label = $val
    $step = {
        param($st, [int]$d)
        $n = [Math]::Max($st.Min, [Math]::Min($st.Max, $st.Value + $d))
        if ($n -eq $st.Value) { return }
        $st.Value = $n; $st.Label.Text = "$n"
        if ($st.Change) { & $st.Change }
    }
    $state.Step = $step
    $minus.Add_Click({ param($s, $e) & $s.Tag.S.Step $s.Tag.S -1 })
    $plus.Add_Click({ param($s, $e) & $s.Tag.S.Step $s.Tag.S 1 })
    $val.Add_MouseWheel({ param($s, $e) & $s.Tag.S.Step $s.Tag.S $(if ($e.Delta -gt 0) { 1 } else { -1 }) })
    $val.Add_MouseEnter({ param($s, $e) $s.Focus() })
    return $state
}

# ----- Trang và menu -----
$script:Pages = [ordered]@{}
$script:NavItems = @()
# Viết hoa chữ cái đầu cho tên menu.
function Cap([string]$s) { if (-not $s) { return $s } return $s.Substring(0, 1).ToUpper() + $s.Substring(1) }
function New-Page([string]$key, [string]$nav, [string]$help) {
    $p = New-Object Windows.Forms.Panel
    $p.Dock = 'Fill'; $p.BackColor = [Drawing.Color]::Transparent; $p.Visible = $false
    $p.Padding = New-Object Windows.Forms.Padding(22, 14, 22, 14)

    $hdr = New-Object Windows.Forms.Panel
    $hdr.Dock = 'Top'; $hdr.Height = 40; $hdr.BackColor = [Drawing.Color]::Transparent
    $tl = New-Object Windows.Forms.FlowLayoutPanel
    # Tiêu đề chiếm phần còn lại sau cụm nút, để không bao giờ đè lên nút.
    $tl.Dock = 'Fill'; $tl.AutoSize = $false; $tl.WrapContents = $false; $tl.BackColor = [Drawing.Color]::Transparent
    $t1 = New-Object Windows.Forms.Label
    $t1.Text = $nav.ToUpper(); $t1.Font = $Fonts.Title; $t1.ForeColor = $Theme.Bright; $t1.AutoSize = $true
    $t1.Margin = New-Object Windows.Forms.Padding(0, 9, 8, 0); $t1.BackColor = [Drawing.Color]::Transparent
    $t2 = New-Object Windows.Forms.Label
    $t2.Text = ''; $t2.Font = $Fonts.Main; $t2.ForeColor = $Theme.Muted; $t2.AutoSize = $true
    $t2.Margin = New-Object Windows.Forms.Padding(0, 11, 0, 0); $t2.BackColor = [Drawing.Color]::Transparent
    $t3 = New-Object Windows.Forms.Label
    $t3.Text = ''; $t3.Font = $Fonts.Main; $t3.ForeColor = $Theme.Green; $t3.AutoSize = $true; $t3.Cursor = 'Hand'
    $t3.Margin = New-Object Windows.Forms.Padding(10, 11, 0, 0); $t3.BackColor = [Drawing.Color]::Transparent
    # Bấm vào dòng 'đã chọn' để bỏ chọn tất cả.
    $t3.Add_Click({ param($s, $e) $lv = $s.Tag; if ($lv) { foreach ($i in @($lv.CheckedItems)) { $i.Checked = $false } } })
    $tl.Controls.Add($t1); $tl.Controls.Add($t2); $tl.Controls.Add($t3)
    $act = New-Object Windows.Forms.FlowLayoutPanel
    $act.Dock = 'Right'; $act.AutoSize = $true; $act.FlowDirection = 'RightToLeft'; $act.WrapContents = $false
    $act.BackColor = [Drawing.Color]::Transparent; $act.Padding = New-Object Windows.Forms.Padding(0, 5, 0, 0)
    $hdr.Controls.Add($tl); $hdr.Controls.Add($act)

    $hl = New-Object Windows.Forms.Label
    $hl.Text = $help; $hl.Dock = 'Top'; $hl.Height = 36; $hl.ForeColor = $Theme.Muted; $hl.Font = $Fonts.Small
    $hl.BackColor = [Drawing.Color]::Transparent

    $flt = New-Object Windows.Forms.FlowLayoutPanel
    $flt.Dock = 'Top'; $flt.AutoSize = $true; $flt.AutoSizeMode = 'GrowAndShrink'; $flt.WrapContents = $true
    $flt.BackColor = [Drawing.Color]::Transparent; $flt.Padding = New-Object Windows.Forms.Padding(0, 0, 0, 4)

    $dp = New-Object Windows.Forms.Panel
    $dp.Dock = 'Bottom'; $dp.Height = 108; $dp.BackColor = $Theme.Panel; $dp.Padding = New-Object Windows.Forms.Padding(12, 10, 12, 8)
    $dp.Add_Paint({ param($s, $e) $e.Graphics.DrawLine($Pens.GreenDim, 0, 0, $s.Width, 0) })
    $tb = New-Object Windows.Forms.TextBox
    $tb.Multiline = $true; $tb.ReadOnly = $true; $tb.BorderStyle = 'None'; $tb.Dock = 'Fill'; $tb.ScrollBars = 'None'
    $tb.BackColor = $Theme.Panel; $tb.ForeColor = $Theme.Text; $tb.Font = $Fonts.Small
    # Chỉ hiện thanh cuộn khi nội dung dài hơn khung.
    $tb.Add_HandleCreated({ param($s, $e) [void][MTC.Native]::AllowDarkModeForWindow($s.Handle, $true); [void][MTC.Native]::SetWindowTheme($s.Handle, 'DarkMode_Explorer', $null) })
    $dp.Controls.Add($tb)
    $tb.ScrollBars = 'Vertical'
    [void](Add-DarkScroll $tb)
    $gap = New-Object Windows.Forms.Panel
    $gap.Dock = 'Bottom'; $gap.Height = 10; $gap.BackColor = [Drawing.Color]::Transparent

    # Thứ tự thêm quyết định thứ tự xếp: mục thêm sau được xếp sát mép trước.
    $p.Controls.Add($flt); $p.Controls.Add($hl); $p.Controls.Add($hdr)
    $p.Controls.Add($gap); $p.Controls.Add($dp)
    $content.Controls.Add($p)

    # Khung "đang chạy" hiện giữa trang trong lúc làm việc nặng.
    $ov = New-Object Windows.Forms.Panel
    $ov.Size = New-Object Drawing.Size(460, 104); $ov.BackColor = $Theme.Panel; $ov.Visible = $false
    $ov.Add_Paint({ param($s, $e) $e.Graphics.DrawRectangle($Pens.Green, 0, 0, $s.Width - 1, $s.Height - 1) })
    $ovT = New-Object Windows.Forms.Label
    $ovT.Font = $Fonts.Bold; $ovT.ForeColor = $Theme.Green; $ovT.BackColor = $Theme.Panel; $ovT.AutoSize = $true
    $ovT.Location = New-Object Drawing.Point(18, 16)
    $ovB = New-Object Windows.Forms.Panel
    $ovB.BackColor = $Theme.Panel; $ovB.Tag = 0.0
    $ovB.Location = New-Object Drawing.Point(18, 46); $ovB.Size = New-Object Drawing.Size(424, 12)
    Set-DoubleBuffer $ovB
    $ovB.Add_Paint({ param($s, $e) Draw-ScanBar $e.Graphics $s.Width $s.Height ([double]$s.Tag) })
    $ovS = New-Object Windows.Forms.Label
    $ovS.Font = $Fonts.Small; $ovS.ForeColor = $Theme.Muted; $ovS.BackColor = $Theme.Panel; $ovS.AutoSize = $false
    $ovS.Location = New-Object Drawing.Point(18, 74); $ovS.Size = New-Object Drawing.Size(424, 18); $ovS.AutoEllipsis = $true
    $ov.Controls.AddRange(@($ovT, $ovB, $ovS))
    $p.Controls.Add($ov)
    $p.Add_Resize({ param($s, $e) foreach ($c in $s.Controls) { if ($c.Size.Width -eq 460 -and $c.Size.Height -eq 104) { $c.Location = New-Object Drawing.Point([int](($s.Width - 460) / 2), [int](($s.Height - 104) / 2)) } } })

    $n = New-Object Windows.Forms.Label
    $n.Text = (Cap $nav); $n.Tag = $key; $n.AutoSize = $false; $n.Size = New-Object Drawing.Size(195, 34)
    $n.TextAlign = 'MiddleLeft'; $n.Padding = New-Object Windows.Forms.Padding(18, 0, 0, 0)
    $n.Margin = New-Object Windows.Forms.Padding(0); $n.ForeColor = $Theme.Muted; $n.BackColor = $Theme.Side
    $n.Font = $Fonts.Main; $n.Cursor = 'Hand'
    $n.Add_Click({ param($s, $e) Show-Page $s.Tag })
    $n.Add_MouseEnter({ param($s, $e) if ($s.Tag -ne $script:CurrentPage) { $s.ForeColor = $Theme.Text } })
    $n.Add_MouseLeave({ param($s, $e) if ($s.Tag -ne $script:CurrentPage) { $s.ForeColor = $Theme.Muted } })
    $n.Add_Paint({ param($s, $e) if ($s.Tag -eq $script:CurrentPage) { $e.Graphics.FillRectangle($Brushes.Green, 0, 0, 2, $s.Height) } })
    $navFlow.Controls.Add($n)
    $script:NavItems += $n

    $page = @{ Key = $key; NavVi = $nav; Nav = $n; Name = $nav.ToUpper(); Panel = $p; T1 = $t1; T2 = $t2; T3 = $t3; Act = $act; Filter = $flt; Detail = $tb; List = $null; Loaded = $false; Auto = $null
        Overlay = @{ Panel = $ov; Title = $ovT; Bar = $ovB; Sub = $ovS } }
    $script:Pages[$key] = $page
    return $page
}
function Show-Page([string]$key) {
    $script:CurrentPage = $key
    foreach ($k in $script:Pages.Keys) { $script:Pages[$k].Panel.Visible = ($k -eq $key) }
    foreach ($n in $script:NavItems) {
        $sel = $n.Tag -eq $key
        $n.BackColor = $(if ($sel) { $Theme.RowSel } else { $Theme.Side })
        $n.ForeColor = $(if ($sel) { $Theme.Green } else { $Theme.Muted })
        $n.Invalidate()
    }
    $pg = $script:Pages[$key]
    if ($pg.Auto -and -not $pg.Loaded -and $Cfg.AutoLoad) { $pg.Loaded = $true; & $pg.Auto }
}
function Set-Title($pg, $count, [string]$sub) {
    $pg.T1.Text = "$($pg.Name) · $count"
    $pg.T2.Text = $sub
}

# ----- Danh sách tối (tự vẽ) -----
function Get-BadgeColor([string]$t) {
    if ($t -match '^(Có thể tắt|Trung bình|Lâu không dùng|Cân nhắc)') { return $Theme.Amber }
    if ($t -match '^(Cần xác nhận|Cao|Chưa có|Chưa bật|Không có|Lỗi|Không nên xoá|Cấm xoá|Bị chặn)') { return $Theme.Red }
    if ($t -match '^(Chưa rõ|Thấp|Đã tắt|Chưa dùng|Bỏ qua)') { return $Theme.Muted }
    if ($t -match '^(Nên giữ|Đang bật|Thông tin|Có|Đã bật|Đang cắm|Đã đăng nhập|Hệ thống|Xong|Đã chuyển|An toàn)') { return $Theme.Green }
    return $Theme.Cyan
}
function Draw-Cell($lv, $e) {
    $g = $e.Graphics; $item = $e.Item; $b = $e.Bounds
    $sel = $item.Selected
    $tone = $item.Name
    $bg = if ($sel) { $Theme.RowSel } elseif ($tone -eq 'bad') { $Theme.BadBg } elseif ($tone -eq 'warn') { $Theme.WarnBg } elseif ($item.Index % 2) { $Theme.RowAlt } else { $Theme.Row }
    $br = New-Object Drawing.SolidBrush($bg); $g.FillRectangle($br, $b); $br.Dispose()
    $g.DrawLine($Pens.Line, $b.Left, $b.Bottom - 1, $b.Right, $b.Bottom - 1)
    $col = $lv.Tag.Cols[$e.ColumnIndex]
    $x = $b.Left + 8
    if ($e.ColumnIndex -eq 0) {
        if ($sel) { $g.FillRectangle($Brushes.Green, $b.Left, $b.Top, 2, $b.Height - 1) }
        if ($lv.CheckBoxes) {
            Draw-CheckBox $g ($b.Left + 6) ($b.Top + [int](($b.Height - 14) / 2)) $item.Checked
            $x += 22
        }
    }
    $text = $e.SubItem.Text
    if (-not $text) { return }
    if ($col.Bar) {
        # Cột phần trăm vẽ thành thanh ngang như WizTree.
        $pct = [double]$e.SubItem.Tag
        $bw = [Math]::Max(10, $b.Right - $x - 8)
        $r0 = New-Object Drawing.Rectangle($x, ($b.Top + [int](($b.Height - 16) / 2)), $bw, 16)
        $g.DrawRectangle($Pens.Line, $r0)
        $fw = [int]([Math]::Max(0, [Math]::Min(100, $pct)) / 100 * ($bw - 1))
        if ($fw -gt 0) { $g.FillRectangle($Brushes.GreenDim, ($r0.X + 1), ($r0.Y + 1), $fw, 15) }
        [Windows.Forms.TextRenderer]::DrawText($g, [string]$text, $Fonts.Small, $r0, $Theme.Bright, [Windows.Forms.TextFormatFlags]'HorizontalCenter, VerticalCenter, NoPrefix, SingleLine')
        return
    }
    if ($col.B) {
        $fc = Get-BadgeColor $text
        $sz = [Windows.Forms.TextRenderer]::MeasureText($g, $text, $Fonts.Small, (New-Object Drawing.Size(1000, 30)), 'NoPadding')
        $w = [Math]::Min($sz.Width + 12, $b.Right - $x - 4)
        $r = New-Object Drawing.Rectangle($x, ($b.Top + [int](($b.Height - 19) / 2)), $w, 18)
        $p = New-Object Drawing.Pen($fc); $g.DrawRectangle($p, $r); $p.Dispose()
        [Windows.Forms.TextRenderer]::DrawText($g, [string]$text, $Fonts.Small, $r, $fc, [Windows.Forms.TextFormatFlags]'HorizontalCenter, VerticalCenter, EndEllipsis, NoPrefix, SingleLine')
        return
    }
    $fc = $Theme.Text
    if ($tone -eq 'dim' -or $col.M) { $fc = $Theme.Muted }
    elseif ($e.ColumnIndex -eq 0) { $fc = $Theme.Bright }
    if ($col.N -and $tone -eq 'warn') { $fc = $Theme.Amber }
    $flags = 'VerticalCenter, EndEllipsis, NoPrefix, SingleLine'
    if ($col.N) { $flags += ', Right' } else { $flags += ', Left' }
    $rect = New-Object Drawing.Rectangle($x, $b.Top, [Math]::Max(0, $b.Right - $x - 8), $b.Height)
    [Windows.Forms.TextRenderer]::DrawText($g, [string]$text, $Fonts.Main, $rect, $fc, [Windows.Forms.TextFormatFlags]$flags)
}
function Fit-LastColumn($lv) {
    # Co cột rộng nhất (ngoài cột cuối) để mọi cột vừa khung, không cần thanh cuộn ngang.
    $n = $lv.Columns.Count
    if ($n -lt 2 -or $lv.Tag.Fitting) { return }
    $cols = $lv.Tag.Cols
    $frame = $lv.Parent
    $avail = if ($frame -and $frame.Tag -is [hashtable] -and $frame.Tag.Inner -eq $lv) { $frame.ClientSize.Width - 10 } else { $lv.ClientSize.Width - 4 }
    if ($lv.Tag.LastAvail -eq $avail -and $lv.Tag.LastCount -eq $lv.Items.Count) { return }
    $lv.Tag.Fitting = $true
    $lv.Tag.LastAvail = $avail; $lv.Tag.LastCount = $lv.Items.Count
    try {
    $want = 0; for ($i = 0; $i -lt $n; $i++) { $want += $cols[$i].W }
    $flex = 0; for ($i = 1; $i -lt $n - 1; $i++) { if ($cols[$i].W -gt $cols[$flex].W) { $flex = $i } }
    for ($i = 0; $i -lt $n - 1; $i++) { $lv.Columns[$i].Width = $cols[$i].W }
    $lastMin = [Math]::Min($cols[$n - 1].W, 160)
    $over = ($want - $cols[$n - 1].W + $lastMin) - $avail
    if ($over -gt 0) { $lv.Columns[$flex].Width = [Math]::Max(120, $cols[$flex].W - $over) }
    $used = 0; for ($i = 0; $i -lt $n - 1; $i++) { $used += $lv.Columns[$i].Width }
    $lv.Columns[$n - 1].Width = [Math]::Max(60, $avail - $used)
    # Các cột đã vừa khung nên ẩn thanh cuộn ngang mà ListView còn giữ lại.
    if ($lv.IsHandleCreated) { [void][MTC.Native]::ShowScrollBar($lv.Handle, 0, $false) }
    } finally { $lv.Tag.Fitting = $false }
}
function New-DarkList($pg, $cols, [bool]$checks) {
    $lv = New-Object Windows.Forms.ListView
    $lv.View = 'Details'; $lv.FullRowSelect = $true; $lv.CheckBoxes = $checks; $lv.HideSelection = $false
    $lv.OwnerDraw = $true; $lv.BorderStyle = 'None'; $lv.Dock = 'Fill'
    $lv.BackColor = $Theme.Row; $lv.ForeColor = $Theme.Text; $lv.Font = $Fonts.Main
    $il = New-Object Windows.Forms.ImageList; $il.ImageSize = New-Object Drawing.Size(1, 32); $lv.SmallImageList = $il
    foreach ($c in $cols) { [void]$lv.Columns.Add($c.H, $c.W) }
    $lv.Tag = @{ Cols = $cols; Sorter = (New-Object MTC.LvSorter) }
    Set-DoubleBuffer $lv
    $lv.Add_HandleCreated({
        param($s, $e)
        [void][MTC.Native]::AllowDarkModeForWindow($s.Handle, $true); [void][MTC.Native]::SetWindowTheme($s.Handle, 'DarkMode_Explorer', $null)
    })
    $lv.Add_ColumnClick({
        param($s, $e)
        $st = $s.Tag.Sorter
        if ($st.Col -eq $e.Column -and $s.ListViewItemSorter) { $st.Desc = -not $st.Desc } else { $st.Col = $e.Column; $st.Desc = $true }
        $s.ListViewItemSorter = $st
        $s.Sort()
        $s.Invalidate()
    })
    $lv.Add_DrawColumnHeader({
        param($s, $e)
        $g = $e.Graphics
        $g.FillRectangle($Brushes.Bg, $e.Bounds)
        $txt = $e.Header.Text
        $st = $s.Tag.Sorter
        if ($s.ListViewItemSorter -and $st.Col -eq $e.ColumnIndex) { $txt += $(if ($st.Desc) { ' ▼' } else { ' ▲' }) }
        $x = $e.Bounds.X + 8; $w = $e.Bounds.Width - 8
        $flags = 'VerticalCenter, EndEllipsis, NoPrefix, SingleLine'
        if ($s.Tag.Cols[$e.ColumnIndex].N) { $flags += ', Right'; $w -= 8 }
        if ($e.ColumnIndex -eq 0 -and $s.CheckBoxes) { $x += 22; $w -= 22 }
        $r = New-Object Drawing.Rectangle($x, $e.Bounds.Y, [Math]::Max(0, $w), $e.Bounds.Height)
        [Windows.Forms.TextRenderer]::DrawText($g, [string]$txt, $Fonts.Small, $r, $Theme.Muted, [Windows.Forms.TextFormatFlags]$flags)
        $g.DrawLine($Pens.Line, $e.Bounds.Left, $e.Bounds.Bottom - 1, $e.Bounds.Right, $e.Bounds.Bottom - 1)
    })
    $lv.Add_DrawItem({ param($s, $e) })
    $lv.Add_DrawSubItem({ param($s, $e) Draw-Cell $s $e })
    $lv.Add_ItemChecked({ param($s, $e) $s.Invalidate($e.Item.Bounds); Update-Bound })
    $lv.Add_SelectedIndexChanged({ Update-Bound })
    $lv.Add_Resize({ param($s, $e) Fit-LastColumn $s })
    $pg.Panel.Controls.Add($lv)
    $lv.BringToFront()
    $pg.ListHost = Add-DarkScroll $lv
    # Thanh cuộn chừa phần tiêu đề cột, tô cùng màu tiêu đề.
    $pg.ListHost.Tag.Bar.HeaderBack = $Theme.Bg
    $pg.ListHost.Tag.Bar.TopInset = 27
    if ($checks) {
        $lv.Tag.SelLabel = $pg.T3
        $lv.Add_ItemChecked({ param($s, $e) Update-SelCount $s })
        $pg.T3.Tag = $lv
    }
    $pg.List = $lv
    return $lv
}
function Fill-List($lv, $items, [scriptblock]$toneOf) {
    $cols = $lv.Tag.Cols
    $lv.BeginUpdate()
    $lv.ListViewItemSorter = $null
    $lv.Items.Clear()
    foreach ($it in $items) {
        $li = $null
        for ($i = 0; $i -lt $cols.Count; $i++) {
            $c = $cols[$i]
            $v = $it.($c.P)
            $text = if ($c.F) { & $c.F $v } else { [string]$v }
            if ($i -eq 0) { $li = New-Object Windows.Forms.ListViewItem([string]$text); $sub = $li.SubItems[0] }
            else { $sub = $li.SubItems.Add([string]$text) }
            if ($c.N) { $sub.Tag = [double]($v -as [double]) }
        }
        $li.Tag = $it
        if ($toneOf) { $li.Name = [string](& $toneOf $it) }
        [void]$lv.Items.Add($li)
    }
    $lv.EndUpdate()
    if ($lv.Tag.SelLabel) { Update-SelCount $lv }
    $lv.Tag.LastAvail = -1
    Fit-LastColumn $lv
    Update-Bound
}
function Show-Detail($tb, $obj, [string[]]$props, [hashtable]$labels) {
    if (-not $obj) { $tb.Text = ''; return }
    $lines = foreach ($p in $props) {
        $v = $obj.$p
        if ($null -eq $v -or "$v" -eq '') { continue }
        if ($v -is [datetime]) { $v = $v.ToString('dd/MM/yyyy HH:mm') }
        if ($p -eq 'Size') { $v = Format-Size $v }
        "{0,-14} {1}" -f ((Tr $labels[$p].ToLower()) + ':'), $v
    }
    $tb.Text = $lines -join "`r`n"
}
function Get-Checked($lv) { @($lv.CheckedItems | ForEach-Object { $_.Tag }) }
function Open-Location([string]$path) {
    if (-not $path) { return }
    if (Test-Path -LiteralPath $path -PathType Leaf) { Start-Process explorer.exe "/select,`"$path`"" }
    elseif (Test-Path -LiteralPath $path) { Start-Process explorer.exe "`"$path`"" }
}
$fmtDate = { param($d) if ($d -is [datetime]) { $d.ToString('dd/MM/yyyy') } else { '' } }
$fmtSize = { param($b) if ($b) { Format-Size $b } else { '' } }

# ===== Hiệu năng =====
$pgPerf = New-Page 'perf' 'hiệu năng' 'Chương trình nào đang dùng CPU và RAM. Dòng vàng là chương trình vượt ngưỡng "nặng" (chỉnh trong cài đặt). Bấm một dòng để xem nó là gì.'
[void](New-DarkList $pgPerf @(
    @{ H = 'chương trình'; W = 200; P = 'Name' }
    @{ H = 'tiến trình'; W = 90; P = 'Count'; N = $true }
    @{ H = 'cpu %'; W = 80; P = 'CpuPct'; N = $true }
    @{ H = 'ram'; W = 100; P = 'MemMB'; N = $true; F = { param($v) "$v MB" } }
    @{ H = 'ram %'; W = 80; P = 'MemPct'; N = $true }
    @{ H = 'là gì'; W = 360; P = 'Explain' }
    @{ H = 'đường dẫn'; W = 260; P = 'Path'; M = $true }
) $false)
$script:PerfData = $null
function Refresh-PerfList {
    if (-not $script:PerfData) { return }
    $all = $script:PerfData.Rows
    $lim = [double]$Cfg.HeavyPct
    $heavy = @($all | Where-Object { $_.CpuPct -gt $lim -or $_.MemPct -gt $lim })
    $rows = if ($tglPerf.Tag.On) { $heavy } else { $all }
    Fill-List $pgPerf.List $rows { param($r) if ($r.CpuPct -gt $lim -or $r.MemPct -gt $lim) { 'warn' } }
    Set-Title $pgPerf @($all).Count "[nặng $($heavy.Count)]"
}
function Run-Perf {
    Start-Work 'Đo hiệu năng' '[pscustomobject]@{ R = (Get-PerfSnapshot) }' @() {
        param($r, $err)
        if (-not $r) { Show-Info "Không đo được. $err"; return }
        $script:PerfData = $r.R
        Refresh-PerfList
        $d = $r.R
        $commitPct = if ($d.LimitGB) { [math]::Round($d.CommitGB / $d.LimitGB * 100) } else { 0 }
        $warn = if ($commitPct -ge 85) { "`r`n! bộ nhớ ảo gần đầy – máy dễ giật. Đóng bớt ứng dụng hoặc tăng pagefile." } else { '' }
        $pgPerf.Detail.Text = ("cpu đang bận:   {0}%`r`nram còn trống:  {1} / {2} GB`r`nbộ nhớ ảo:      {3} / {4} GB ({5}%){6}" -f $d.CpuLoad, [math]::Round($d.FreeMB / 1024, 1), [math]::Round($d.TotalMB / 1024, 1), $d.CommitGB, $d.LimitGB, $commitPct, $warn)
    }
}
$pgPerf.Auto = { Run-Perf }
[void](New-Btn $pgPerf.Act '⟳ đo lại' { Run-Perf } -Primary)
[void](New-Btn $pgPerf.Act 'task manager' { Start-Process taskmgr.exe })
[void](New-Text $pgPerf.Filter 'lọc:')
$tglPerf = New-Toggle $pgPerf.Filter "chỉ hiện > $($Cfg.HeavyPct)%" $true { Refresh-PerfList }
$pgPerf.List.Add_SelectedIndexChanged({
    if ($pgPerf.List.SelectedItems.Count -eq 0) { return }
    Show-Detail $pgPerf.Detail $pgPerf.List.SelectedItems[0].Tag @('Name', 'Explain', 'Company', 'Path') @{ Name = 'Chương trình'; Explain = 'Là gì'; Company = 'Hãng'; Path = 'Đường dẫn' }
})

# ===== Khởi động =====
$pgStart = New-Page 'start' 'khởi động' 'Mọi thứ tự chạy khi bật máy. Tích chọn rồi bấm "tắt" – không xoá gì, bật lại được bất cứ lúc nào. Dịch vụ được chuyển sang "chạy khi cần".'
[void](New-DarkList $pgStart @(
    @{ H = 'tên'; W = 220; P = 'Name' }
    @{ H = 'loại'; W = 150; P = 'Kind'; B = $true }
    @{ H = 'trạng thái'; W = 96; P = 'Enabled'; B = $true; F = { param($v) if ($v) { 'Đang bật' } else { 'Đã tắt' } } }
    @{ H = 'gợi ý'; W = 116; P = 'Advice'; B = $true }
    @{ H = 'là gì'; W = 270; P = 'Note' }
    @{ H = 'lệnh chạy'; W = 300; P = 'Command'; M = $true }
) $true)
$script:StartData = @()
function Get-ToolServices { if (Test-Path $ToolServicesFile) { @(Get-Content $ToolServicesFile) } else { @() } }
function Refresh-StartupList {
    $k = Get-ChipKey $chipStart
    $rows = $script:StartData
    switch ($k) {
        'on' { $rows = $rows | Where-Object Enabled }
        'off' { $rows = $rows | Where-Object { -not $_.Enabled } }
        'run' { $rows = $rows | Where-Object { $_.Kind -in 'Chạy khi đăng nhập', 'Thư mục Startup' } }
        'task' { $rows = $rows | Where-Object Kind -eq 'Tác vụ tự chạy' }
        'svc' { $rows = $rows | Where-Object Kind -eq 'Dịch vụ nền' }
    }
    Fill-List $pgStart.List $rows { param($x) if (-not $x.Enabled) { 'dim' } elseif ($x.Advice -eq 'Cần xác nhận') { 'bad' } }
    Set-Title $pgStart @($script:StartData).Count "[đang bật $(@($script:StartData | Where-Object Enabled).Count)]"
}
function Load-Startup {
    Start-Work 'Đọc danh sách khởi động' '[pscustomobject]@{ R = @(Get-StartupItems $A0) }' @(, [string[]](Get-ToolServices)) {
        param($r, $err)
        if (-not $r) { Show-Info "Không đọc được. $err"; return }
        $order = @('Cần xác nhận', 'Có thể tắt', 'Chưa rõ', 'Nên giữ')
        $script:StartData = @($r.R | Sort-Object @{ e = { -not $_.Enabled } }, @{ e = { $order.IndexOf($_.Advice) } }, Name)
        Refresh-StartupList
    }
}
function Apply-Startup([bool]$enable) {
    $sel = Get-Checked $pgStart.List
    if (-not $sel) { Show-Info 'Bạn tích chọn ít nhất một mục trước.'; return }
    $verb = if ($enable) { 'bật lại' } else { 'tắt' }
    if (-not (Confirm ("$($verb.ToUpper()) khởi động cùng máy cho $($sel.Count) mục sau?`r`n`r`n" + (($sel | ForEach-Object { "• $($_.Name)  ($($_.Kind))" }) -join "`r`n")))) { return }
    $ok = 0; $fail = @()
    $svc = New-Object System.Collections.Generic.List[string]
    foreach ($s in Get-ToolServices) { $svc.Add($s) }
    foreach ($it in $sel) {
        try {
            Set-StartupItem $it $enable
            $ok++
            Write-Log "Khởi động – $verb : $($it.Kind) | $($it.Name)"
            if ($it.Kind -eq 'Dịch vụ nền') { if ($enable) { [void]$svc.Remove($it.Id) } elseif (-not $svc.Contains($it.Id)) { $svc.Add($it.Id) } }
        } catch { $fail += "• $($it.Name): $($_.Exception.Message)" }
    }
    Set-Content -Path $ToolServicesFile -Value $svc -Encoding UTF8
    $msg = "Đã $verb $ok mục. Thay đổi có hiệu lực từ lần bật máy sau."
    if ($fail) { $msg += "`r`n`r`nKhông làm được:`r`n" + ($fail -join "`r`n") }
    Show-Info $msg
    Load-Startup
}
$pgStart.Auto = { Load-Startup }
$btnStartOff = New-Btn $pgStart.Act 'tắt mục đã chọn' { Apply-Startup $false } -Primary
$btnStartOn = New-Btn $pgStart.Act 'bật lại mục đã chọn' { Apply-Startup $true }
Bind-Enabled $btnStartOff { $pgStart.List.CheckedItems.Count -gt 0 }
Bind-Enabled $btnStartOn { $pgStart.List.CheckedItems.Count -gt 0 }
[void](New-Btn $pgStart.Act '⟳ tải lại' { Load-Startup })
[void](New-Text $pgStart.Filter 'lọc:')
$chipStart = New-ChipGroup $pgStart.Filter ([ordered]@{ all = 'tất cả'; on = 'đang bật'; off = 'đã tắt'; run = 'chạy khi đăng nhập'; task = 'tác vụ'; svc = 'dịch vụ' }) { Refresh-StartupList }
$pgStart.List.Add_SelectedIndexChanged({
    if ($pgStart.List.SelectedItems.Count -eq 0) { return }
    Show-Detail $pgStart.Detail $pgStart.List.SelectedItems[0].Tag @('Name', 'Kind', 'Scope', 'Advice', 'Note', 'Command') @{ Name = 'Tên'; Kind = 'Loại'; Scope = 'Phạm vi'; Advice = 'Gợi ý'; Note = 'Là gì'; Command = 'Lệnh chạy' }
})

# ===== Ổ đĩa =====
$pgDisk = New-Page 'disk' 'ổ đĩa' 'Tìm thứ đang chiếm chỗ. Tích chọn rồi chuyển vào Thùng rác – mục quá lớn so với Thùng rác được bỏ qua để bạn tự quyết. Nhớ dọn Thùng rác để lấy lại dung lượng.'
[void](New-DarkList $pgDisk @(
    @{ H = 'đường dẫn'; W = 460; P = 'Path' }
    @{ H = 'kích thước'; W = 110; P = 'Size'; N = $true; F = $fmtSize }
    @{ H = 'ngày sửa'; W = 110; P = 'Modified'; F = $fmtDate }
    @{ H = 'xoá được?'; W = 130; P = 'Safety'; B = $true }
    @{ H = 'ghi chú'; W = 300; P = 'Note'; M = $true }
) $true)
$script:DiskData = $null; $script:DupData = $null
$diskViews = [ordered]@{ TopFiles = '20 tệp lớn nhất'; TopDirs = '20 thư mục lớn nhất'; Installers = 'bộ cài & file nén'; OldDownloads = 'tải xuống cũ'; Caches = 'bộ nhớ đệm & file tạm'; Dups = 'ảnh trùng lặp' }
function Selected-Drives { @($driveToggles | Where-Object { $_.Tag.On } | ForEach-Object { $_.Tag.Drive }) }
function Refresh-DiskList {
    $key = Get-ChipKey $chipDisk
    $rows = @()
    if ($key -eq 'Dups') { if ($script:DupData) { $rows = $script:DupData.Rows } }
    elseif ($script:DiskData) { $rows = $script:DiskData.$key }
    foreach ($x in $rows) { if (-not $x.PSObject.Properties['Safety']) { $x | Add-Member -NotePropertyName Safety -NotePropertyValue (Get-DeleteSafety $x.Path).Label } }
    Fill-List $pgDisk.List $rows { param($x) if ($x.Note -like 'Ở ổ khác*') { 'dim' } }
    $total = ($rows | Measure-Object Size -Sum).Sum
    Set-Title $pgDisk @($rows).Count "[$(Format-Size $total)]"
    if ($key -eq 'Dups' -and -not $script:DupData) { $pgDisk.Detail.Text = 'Bấm "tìm ảnh trùng lặp" để quét.' }
    elseif ($key -ne 'Dups' -and -not $script:DiskData) { $pgDisk.Detail.Text = 'Bấm "quét dung lượng" để bắt đầu.' }
    elseif ($key -eq 'Dups') { $pgDisk.Detail.Text = "Đã so sánh $($script:DupData.Scanned) ảnh. Bản được giữ là bản cũ nhất. Dòng mờ là bản ở ổ khác – có thể là bản sao lưu, nên giữ." }
    else { $pgDisk.Detail.Text = "$(@($rows).Count) mục, tổng $(Format-Size $total). Bấm đúp một dòng để mở vị trí." }
}
$btnRecycle = New-Btn $pgDisk.Act '→ thùng rác' {
    $sel = Get-Checked $pgDisk.List
    if (-not $sel) { Show-Info 'Bạn tích chọn ít nhất một mục trước.'; return }
    if (-not (Confirm-Recycle $sel)) { return }
    $payload = @($sel | ForEach-Object { [pscustomobject]@{ Path = $_.Path; Mode = $_.Mode } })
    Start-Work 'Chuyển vào Thùng rác' '[pscustomobject]@{ R = @(Move-ManyToRecycleBin $A0) }' @(, $payload) {
        param($r, $err)
        if (-not $r) { Show-Info "Có lỗi. $err"; return }
        $freed = 0; $lines = @()
        foreach ($x in $r.R) {
            Write-Log "Thùng rác – $($x.Result): $($x.Path) | $($x.Detail)"
            $freed += $x.Freed
            $lines += "• [$($x.Result)] $($x.Path) – $($x.Detail)"
        }
        $done = @($r.R | Where-Object { $_.Result -eq 'Đã chuyển' -or $_.Result -eq 'Không còn' } | ForEach-Object Path)
        foreach ($li in @($pgDisk.List.Items)) { if ($done -contains $li.Tag.Path -and $li.Tag.Mode -eq 'Item') { $pgDisk.List.Items.Remove($li) } }
        Show-Info ("Đã chuyển $(Format-Size $freed) vào Thùng rác. Dọn Thùng rác để lấy lại dung lượng.`r`n`r`n" + (($lines | Select-Object -First 30) -join "`r`n"))
    }
} -Primary
Bind-Enabled $btnRecycle { $pgDisk.List.CheckedItems.Count -gt 0 }
$btnDiskOpen = New-Btn $pgDisk.Act 'mở vị trí' { if ($pgDisk.List.SelectedItems.Count) { Open-Location $pgDisk.List.SelectedItems[0].Tag.Path } }
Bind-Enabled $btnDiskOpen { $pgDisk.List.SelectedItems.Count -gt 0 }
[void](New-Btn $pgDisk.Act 'tìm ảnh trùng lặp' {
    $dr = Selected-Drives
    if (-not $dr) { Show-Info 'Bạn chọn ít nhất một ổ.'; return }
    Start-Work 'Tìm ảnh trùng lặp' '[pscustomobject]@{ R = (Find-DuplicateImages $A0) }' @(, [string[]]$dr) {
        param($r, $err)
        if (-not $r) { Show-Info "Không quét được. $err"; return }
        $script:DupData = $r.R
        Select-Chip $chipDisk 'Dups'
        Refresh-DiskList
    }
})
[void](New-Btn $pgDisk.Act '⟳ quét dung lượng' {
    $dr = Selected-Drives
    if (-not $dr) { Show-Info 'Bạn chọn ít nhất một ổ.'; return }
    Start-Work 'Quét ổ đĩa' '[pscustomobject]@{ R = (Invoke-DiskScan $A0) }' @(, [string[]]$dr) {
        param($r, $err)
        if (-not $r) { Show-Info "Không quét được. $err"; return }
        $script:DiskData = $r.R
        if ((Get-ChipKey $chipDisk) -eq 'Dups') { Select-Chip $chipDisk 'TopFiles' }
        Refresh-DiskList
    }
} -Primary)
Set-Title $pgDisk '–' '[chưa quét]'
[void](New-Text $pgDisk.Filter 'ổ:')
$driveToggles = @()
foreach ($d in Get-DriveSummary) {
    $t = New-Toggle $pgDisk.Filter ("{0} {1} trống" -f $d.Drive, (Format-Size $d.Free)) ($d.Size -gt 0) $null
    $t.Tag.Drive = $d.Drive
    $driveToggles += $t
}
[void](New-Text $pgDisk.Filter '  xem:')
$chipDisk = New-ChipGroup $pgDisk.Filter $diskViews { Refresh-DiskList }
$pgDisk.List.Add_SelectedIndexChanged({
    if ($pgDisk.List.SelectedItems.Count -eq 0) { return }
    $it = $pgDisk.List.SelectedItems[0].Tag
    Show-Detail $pgDisk.Detail $it @('Path', 'Size', 'Modified', 'Note') @{ Path = 'Đường dẫn'; Size = 'Kích thước'; Modified = 'Ngày sửa'; Note = 'Ghi chú' }
    $pgDisk.Detail.Text += "`r`n" + (Format-SafetyText $it.Path (Test-Path -LiteralPath $it.Path -PathType Container))
})
$pgDisk.List.Add_DoubleClick({ if ($pgDisk.List.SelectedItems.Count) { Open-Location $pgDisk.List.SelectedItems[0].Tag.Path } })

# ===== Phân tích (kiểu WizTree) =====
$pgAn = New-Page 'an' 'phân tích' 'Bản đồ dung lượng: ô càng to càng chiếm nhiều chỗ. Bấm để chọn, bấm đúp vào thư mục để đi vào trong. Màu theo loại tệp, xem chú thích bên dưới.'
[void](New-DarkList $pgAn @(
    @{ H = 'tên'; W = 300; P = 'Name' }
    @{ H = '% thư mục cha'; W = 140; P = 'Pct'; N = $true; Bar = $true; F = { param($v) '{0:N1} %' -f $v } }
    @{ H = 'dung lượng'; W = 110; P = 'Size'; N = $true; F = $fmtSize }
    @{ H = 'số tệp'; W = 90; P = 'Files'; N = $true; F = { param($v) '{0:N0}' -f $v } }
    @{ H = 'xoá được?'; W = 130; P = 'Safety'; B = $true }
    @{ H = 'loại'; W = 120; P = 'Type'; M = $true }
) $true)
$pgAn.ListHost.Dock = 'Top'; $pgAn.ListHost.Height = 230
$anSplit = New-Object Windows.Forms.Splitter
$anSplit.Dock = 'Top'; $anSplit.Height = 6; $anSplit.BackColor = $Theme.Bg; $anSplit.MinSize = 90
$anSplit.Add_Paint({ param($s, $e) $e.Graphics.DrawLine($Pens.GreenDim, 0, 3, $s.Width, 3) })
$treemap = New-Object MTC.Treemap
$treemap.Dock = 'Fill'; $treemap.BackColor = $Theme.Bg; $treemap.Font = $Fonts.Small; $treemap.Line = $Theme.Line; $treemap.Header = $Theme.Text
$pgAn.Panel.Controls.Add($anSplit); $pgAn.Panel.Controls.Add($treemap)
# Thứ tự xếp: danh sách ở trên, thanh kéo ở giữa, bản đồ chiếm phần còn lại.
$pgAn.ListHost.BringToFront(); $anSplit.BringToFront(); $treemap.BringToFront()

$script:AnRoot = $null; $script:AnCur = $null; $script:AnSel = $null
$script:AnTarget = (Get-DriveSummary | Select-Object -First 1).Drive + '\'
function Show-AnDetail($n) {
    if (-not $n) { $pgAn.Detail.Text = ''; return }
    $pctRoot = if ($script:AnRoot.Size) { $n.Size / $script:AnRoot.Size * 100 } else { 0 }
    $kind = if ($n.IsDir) { 'thư mục – bấm đúp để đi vào' } elseif ($n.Ext -eq '*') { 'các tệp dưới 1 MB gộp lại' } else { "tệp $($n.Ext)" }
    $txt = ("{0,-14} {1}`r`n{2,-14} {3}  ({4:N1}% của {5})`r`n{6,-14} {7:N0}`r`n{8,-14} {9}" -f 'đường dẫn:', $n.Path, 'dung lượng:', (Format-Size $n.Size), $pctRoot, $script:AnRoot.Path, 'số tệp:', $n.Files, 'loại:', $kind)
    if ($n.Ext -ne '*') { $txt += "`r`n" + (Format-SafetyText $n.Path $n.IsDir) }
    $pgAn.Detail.Text = $txt
}
function Show-AnFolder($node) {
    $script:AnCur = $node; $script:AnSel = $null
    $kids = @($node.Kids | Select-Object -First 400)
    $rows = foreach ($k in $kids) {
        [pscustomobject]@{
            Name = $(if ($k.IsDir) { $k.Name + '\' } else { $k.Name })
            Pct = $(if ($node.Size) { [math]::Round($k.Size / $node.Size * 100, 1) } else { 0 })
            Size = $k.Size; Files = $k.Files
            Type = $(if ($k.IsDir) { 'thư mục' } elseif ($k.Ext -eq '*') { 'tệp nhỏ gộp lại' } else { $k.Ext })
            Safety = $(if ($k.Ext -eq '*') { '' } else { (Get-DeleteSafety $k.Path).Label })
            Node = $k; Path = $k.Path; Mode = 'Item'
        }
    }
    Fill-List $pgAn.List $rows { param($x) if ($x.Node.Ext -eq '*') { 'dim' } }
    $treemap.Root = $node; $treemap.Selected = $null; $treemap.Invalidate()
    Set-Title $pgAn (Format-Size $node.Size) ('[{0:N0} tệp]' -f $node.Files)
    Show-AnDetail $node
    Update-Bound
}
function Select-AnNode($n) {
    $script:AnSel = $n
    $treemap.Selected = $n; $treemap.Invalidate()
    foreach ($li in $pgAn.List.Items) { if ($li.Tag.Node -eq $n) { if (-not $li.Selected) { $li.Selected = $true; $li.EnsureVisible() } } elseif ($li.Selected) { $li.Selected = $false } }
    Show-AnDetail $n
    Update-Bound
}
function Open-AnNode($n) {
    if ($n -and $n.IsDir -and $n.Kids.Count -gt 0) { Show-AnFolder $n } else { Select-AnNode $n }
}
$treemap.add_NodeClicked([Action[MTC.DNode]] { param($n) Select-AnNode $n })
$treemap.add_NodeOpened([Action[MTC.DNode]] { param($n) Open-AnNode $n })
$treemap.add_NodeHover([Action[MTC.DNode]] { param($n) if (-not $script:AnSel) { Show-AnDetail $n } })
$pgAn.List.Add_SelectedIndexChanged({
    if ($pgAn.List.SelectedItems.Count -eq 0) { return }
    $n = $pgAn.List.SelectedItems[0].Tag.Node
    if ($n -ne $script:AnSel) { $script:AnSel = $n; $treemap.Selected = $n; $treemap.Invalidate(); Show-AnDetail $n }
})
$pgAn.List.Add_DoubleClick({ if ($pgAn.List.SelectedItems.Count) { Open-AnNode $pgAn.List.SelectedItems[0].Tag.Node } })

function Start-AnScan {
    $target = $script:AnTarget
    Start-Work 'Phân tích dung lượng' '[pscustomobject]@{ R = [MTC.DiskTree]::Scan($A0) }' @($target) {
        param($r, $err)
        if (-not $r -or -not $r.R) { Show-Info "Không quét được. $err"; return }
        $script:AnRoot = $r.R
        Show-AnFolder $r.R
    } { '{0:N0} tệp · {1}' -f [MTC.DiskTree]::Scanned, [MTC.DiskTree]::Current }
}
# Các mục sẽ xoá: những dòng đã tích; nếu chưa tích dòng nào thì lấy mục đang chọn trên bản đồ.
function Get-AnTargets {
    $nodes = @($pgAn.List.CheckedItems | ForEach-Object { $_.Tag.Node })
    if (-not $nodes -and $script:AnSel) { $nodes = @($script:AnSel) }
    return @($nodes | Where-Object { $_ -and $_.Ext -ne '*' -and $_.Parent })
}
$btnAnRecycle = New-Btn $pgAn.Act '→ thùng rác' {
    $nodes = Get-AnTargets
    if (-not $nodes) { return }
    if (-not (Confirm-Recycle @($nodes | ForEach-Object { [pscustomobject]@{ Path = $_.Path; Size = $_.Size } }))) { return }
    $payload = @($nodes | ForEach-Object { [pscustomobject]@{ Path = $_.Path; Mode = 'Item' } })
    $script:AnPending = $nodes
    Start-Work 'Chuyển vào Thùng rác' '[pscustomobject]@{ R = @(Move-ManyToRecycleBin $A0) }' @(, $payload) {
        param($r, $err)
        if (-not $r) { Show-Info "Có lỗi. $err"; return }
        $freed = 0; $lines = @()
        foreach ($x in $r.R) {
            Write-Log "Thùng rác – $($x.Result): $($x.Path) | $($x.Detail)"
            $lines += "• [$($x.Result)] $($x.Path) – $($x.Detail)"
            if ($x.Result -ne 'Đã chuyển') { continue }
            $freed += $x.Freed
            # Bỏ mục đã xoá khỏi cây và trừ dung lượng ở các thư mục cha.
            $n = $script:AnPending | Where-Object { $_.Path -eq $x.Path } | Select-Object -First 1
            if (-not $n) { continue }
            [void]$n.Parent.Kids.Remove($n)
            $p = $n.Parent
            while ($p) { $p.Size -= $n.Size; $p.Files -= $n.Files; $p = $p.Parent }
        }
        Show-AnFolder $script:AnCur
        Show-Info ("Đã chuyển $(Format-Size $freed) vào Thùng rác. Dọn Thùng rác để lấy lại dung lượng.`r`n`r`n" + (($lines | Select-Object -First 30) -join "`r`n"))
    }
} -Primary
Bind-Enabled $btnAnRecycle { (Get-AnTargets).Count -gt 0 }
# Không cho tích dòng "[N tệp nhỏ]": đường dẫn của nó là cả thư mục cha.
$pgAn.List.Add_ItemCheck({ param($s, $e) if ($s.Items[$e.Index].Tag.Node.Ext -eq '*') { $e.NewValue = 'Unchecked' } })
$btnAnOpen = New-Btn $pgAn.Act 'mở vị trí' { if ($script:AnSel) { Open-Location $script:AnSel.Path } }
Bind-Enabled $btnAnOpen { [bool]$script:AnSel }
$btnAnUp = New-Btn $pgAn.Act '↑ lên trên' { if ($script:AnCur.Parent) { $c = $script:AnCur; Show-AnFolder $c.Parent; Select-AnNode $c } }
Bind-Enabled $btnAnUp { $script:AnCur -and $script:AnCur.Parent }
[void](New-Btn $pgAn.Act '⟳ quét' { Start-AnScan })
[void](New-Text $pgAn.Filter 'quét:')
$anDrives = [ordered]@{}
foreach ($d in Get-DriveSummary) { $anDrives["$($d.Drive)\"] = "$($d.Drive) $(Format-Size ($d.Size - $d.Free)) đã dùng" }
$chipAn = New-ChipGroup $pgAn.Filter $anDrives { param($k) $script:AnTarget = $k; Start-AnScan }
$btnAnPick = New-ChipButton $pgAn.Filter 'chọn thư mục…' @{ On = $false }
Set-ChipState $btnAnPick $false
$btnAnPick.Add_Click({
    $dlg = New-Object Windows.Forms.FolderBrowserDialog
    $dlg.Description = 'Chọn thư mục cần phân tích'
    if ($dlg.ShowDialog() -ne 'OK') { return }
    foreach ($o in $chipAn) { Set-ChipState $o $false }
    $script:AnTarget = $dlg.SelectedPath
    Start-AnScan
})
$btnAnCheck = New-ChipButton $pgAn.Filter 'kiểm tra một tệp…' @{ On = $false }
Set-ChipState $btnAnCheck $false
$btnAnCheck.Add_Click({
    $dlg = New-Object Windows.Forms.OpenFileDialog
    $dlg.Title = 'Chọn tệp cần kiểm tra xoá được không'
    $dlg.Filter = 'Mọi tệp (*.*)|*.*'
    if ($dlg.ShowDialog() -ne 'OK') { return }
    $p = $dlg.FileName
    $size = (Get-Item -LiteralPath $p -Force).Length
    Show-Info ("{0}`r`n{1}`r`n`r`n{2}" -f $p, (Format-Size $size), (Format-SafetyText $p $false))
})
[void](New-Text $pgAn.Filter '   màu:')
foreach ($lg in @(@('video', 224, 123, 36), @('ảnh', 155, 93, 229), @('âm thanh', 58, 134, 255), @('nén / bộ cài', 229, 70, 111), @('chương trình', 46, 196, 182), @('dữ liệu game', 212, 194, 28), @('tài liệu', 95, 208, 104), @('khác', 110, 128, 118))) {
    $l = New-Text $pgAn.Filter ('■ ' + $lg[0])
    $l.ForeColor = [Drawing.Color]::FromArgb($lg[1], $lg[2], $lg[3])
}
Set-Title $pgAn '–' '[chưa quét – chọn một ổ để bắt đầu]'

# ===== Ứng dụng =====
$pgApps = New-Page 'apps' 'ứng dụng' 'Ứng dụng đã cài và lần dùng cuối. Dữ liệu Steam là chính xác; ứng dụng khác dựa vào lịch sử chạy của Windows nên chỉ biết được từ một mốc nhất định.'
[void](New-DarkList $pgApps @(
    @{ H = 'ứng dụng'; W = 290; P = 'Name' }
    @{ H = 'dung lượng'; W = 110; P = 'Size'; N = $true; F = $fmtSize }
    @{ H = 'dùng lần cuối'; W = 120; P = 'LastUsed'; F = $fmtDate }
    @{ H = 'loại'; W = 170; P = 'Kind'; B = $true }
    @{ H = 'nguồn dữ liệu'; W = 230; P = 'Source'; M = $true }
    @{ H = 'nhà phát hành'; W = 200; P = 'Publisher'; M = $true }
) $true)
$script:AppsData = $null
function Refresh-AppsList {
    if (-not $script:AppsData) { return }
    $cut = (Get-Date).AddMonths(-[int]$numMonths.Value)
    $rows = $script:AppsData
    if ($tglUnused.Tag.On) { $rows = $rows | Where-Object { -not $_.LastUsed -or $_.LastUsed -lt $cut } }
    switch (Get-ChipKey $chipApps) {
        'app' { $rows = $rows | Where-Object Kind -eq 'Ứng dụng' }
        'game' { $rows = $rows | Where-Object Kind -eq 'Game Steam' }
        'sys' { $rows = $rows | Where-Object Kind -eq 'Hệ thống – nên giữ' }
    }
    $rows = @($rows | Sort-Object @{ e = { $_.Kind -eq 'Hệ thống – nên giữ' } }, @{ e = { $_.Size }; Descending = $true })
    Fill-List $pgApps.List $rows { param($x) if ($x.Kind -eq 'Hệ thống – nên giữ') { 'dim' } }
    $old = @($script:AppsData | Where-Object { $_.Kind -ne 'Hệ thống – nên giữ' -and (-not $_.LastUsed -or $_.LastUsed -lt $cut) }).Count
    Set-Title $pgApps $rows.Count "[lâu không dùng $old]"
    $pgApps.Detail.Text = "Tích chọn rồi bấm ""gỡ mục đã chọn"". Trình gỡ của từng ứng dụng sẽ hiện ra để bạn xác nhận. Dòng mờ là thành phần hệ thống – nên giữ."
}
function Load-Apps {
    Start-Work 'Phân tích ứng dụng' '[pscustomobject]@{ R = @(Get-AppUsage) }' @() {
        param($r, $err)
        if (-not $r) { Show-Info "Không đọc được. $err"; return }
        $script:AppsData = $r.R
        Refresh-AppsList
    }
}
$btnUninstall = New-Btn $pgApps.Act 'gỡ mục đã chọn' {
    $sel = Get-Checked $pgApps.List
    if (-not $sel) { Show-Info 'Bạn tích chọn ít nhất một ứng dụng trước.'; return }
    $sys = @($sel | Where-Object Kind -eq 'Hệ thống – nên giữ')
    $warn = if ($sys) { "`r`n`r`n! Có $($sys.Count) mục là thành phần hệ thống, gỡ có thể làm game hoặc phần mềm khác lỗi." } else { '' }
    if (-not (Confirm ("Gỡ $($sel.Count) ứng dụng sau? Mỗi trình gỡ sẽ hiện cửa sổ để bạn xác nhận.`r`n`r`n" + (($sel | ForEach-Object { "• $($_.Name)" }) -join "`r`n") + $warn))) { return }
    Start-Work 'Gỡ ứng dụng' '[pscustomobject]@{ R = @(Uninstall-Many $A0) }' @(, @($sel)) {
        param($r, $err)
        if (-not $r) { Show-Info "Có lỗi. $err"; return }
        foreach ($x in $r.R) { Write-Log "Gỡ – $($x.Result): $($x.Name) | $($x.Detail)" }
        Show-Info ("Kết quả:`r`n`r`n" + (($r.R | ForEach-Object { "• [$($_.Result)] $($_.Name) $($_.Detail)" }) -join "`r`n"))
        Load-Apps
    }
} -Primary
Bind-Enabled $btnUninstall { $pgApps.List.CheckedItems.Count -gt 0 }
$btnAppDir = New-Btn $pgApps.Act 'mở thư mục cài' { if ($pgApps.List.SelectedItems.Count) { Open-Location $pgApps.List.SelectedItems[0].Tag.Location } }
Bind-Enabled $btnAppDir { $pgApps.List.SelectedItems.Count -gt 0 -and $pgApps.List.SelectedItems[0].Tag.Location }
[void](New-Btn $pgApps.Act '⟳ phân tích' { Load-Apps })
[void](New-Text $pgApps.Filter 'lọc:')
$tglUnused = New-Toggle $pgApps.Filter 'không dùng trong' $true { Refresh-AppsList }
$numMonths = New-Stepper $pgApps.Filter ([int]$Cfg.Months) 1 36 { Refresh-AppsList }
[void](New-Text $pgApps.Filter 'tháng  ·')
$chipApps = New-ChipGroup $pgApps.Filter ([ordered]@{ all = 'tất cả'; app = 'ứng dụng'; game = 'game steam'; sys = 'hệ thống' }) { Refresh-AppsList }
$pgApps.Auto = { Load-Apps }
$pgApps.List.Add_SelectedIndexChanged({
    if ($pgApps.List.SelectedItems.Count -eq 0) { return }
    Show-Detail $pgApps.Detail $pgApps.List.SelectedItems[0].Tag @('Name', 'Version', 'Publisher', 'Size', 'LastUsed', 'Source', 'Location', 'Uninstall') @{ Name = 'Ứng dụng'; Version = 'Phiên bản'; Publisher = 'Hãng'; Size = 'Dung lượng'; LastUsed = 'Dùng lần cuối'; Source = 'Nguồn'; Location = 'Thư mục cài'; Uninstall = 'Lệnh gỡ' }
})

# ===== Bảo mật =====
$pgSec = New-Page 'sec' 'bảo mật' 'Chỉ kiểm tra, không thay đổi gì. Gỡ extension trong trang quản lý extension của từng trình duyệt – mỗi hồ sơ (profile) một trang riêng.'
[void](New-DarkList $pgSec @(
    @{ H = 'mức'; W = 110; P = 'Level'; B = $true }
    @{ H = 'nhóm'; W = 180; P = 'Group' }
    @{ H = 'mục'; W = 340; P = 'Item' }
    @{ H = 'chi tiết'; W = 520; P = 'Detail'; M = $true }
) $false)
$script:SecData = @()
function Refresh-SecList {
    $rows = $script:SecData
    switch (Get-ChipKey $chipSec) {
        'risk' { $rows = $rows | Where-Object { $_.Level -in 'Cao', 'Trung bình' } }
        'av' { $rows = $rows | Where-Object { $_.Group -in 'Diệt virus', 'Tường lửa' } }
        'proc' { $rows = $rows | Where-Object Group -eq 'Tiến trình' }
        'net' { $rows = $rows | Where-Object { $_.Group -in 'Kết nối Internet', 'Mở cổng chờ kết nối', 'Mạng', 'File hosts' } }
        'ext' { $rows = $rows | Where-Object Group -eq 'Extension trình duyệt' }
    }
    Fill-List $pgSec.List $rows { param($x) if ($x.Level -eq 'Cao') { 'bad' } elseif ($x.Level -eq 'Trung bình') { 'warn' } }
    $hi = @($script:SecData | Where-Object Level -eq 'Cao').Count; $mid = @($script:SecData | Where-Object Level -eq 'Trung bình').Count
    Set-Title $pgSec @($rows).Count "[cao $hi · cần xem $mid]"
}
function Load-Sec {
    Start-Work 'Kiểm tra bảo mật' '[pscustomobject]@{ R = @(Get-SecurityReport) }' @() {
        param($r, $err)
        if (-not $r) { Show-Info "Không kiểm tra được. $err"; return }
        $order = @('Cao', 'Trung bình', 'Thấp', 'Thông tin')
        $script:SecData = @($r.R | Sort-Object @{ e = { $order.IndexOf($_.Level) } }, Group, Item)
        Refresh-SecList
        $pgSec.Detail.Text = 'Bấm một dòng để xem chi tiết.'
    }
}
[void](New-Btn $pgSec.Act '⟳ kiểm tra' { Load-Sec } -Primary)
[void](New-Text $pgSec.Filter 'lọc:')
$chipSec = New-ChipGroup $pgSec.Filter ([ordered]@{ all = 'tất cả'; risk = 'cần xem'; av = 'diệt virus'; proc = 'tiến trình'; net = 'mạng'; ext = 'extension' }) { Refresh-SecList }
$pgSec.List.Add_SelectedIndexChanged({
    if ($pgSec.List.SelectedItems.Count -eq 0) { return }
    Show-Detail $pgSec.Detail $pgSec.List.SelectedItems[0].Tag @('Level', 'Group', 'Item', 'Detail') @{ Level = 'Mức'; Group = 'Nhóm'; Item = 'Mục'; Detail = 'Chi tiết' }
})

# ===== Sao lưu =====
$pgBak = New-Page 'bak' 'sao lưu' 'Máy đã được sao lưu chưa và ở đâu. Cách đơn giản nhất: bật File History lên một ổ cắm ngoài, và chép các thư mục quan trọng sang một ổ khác.'
[void](New-DarkList $pgBak @(
    @{ H = 'hình thức'; W = 300; P = 'Item' }
    @{ H = 'trạng thái'; W = 140; P = 'Status'; B = $true }
    @{ H = 'chi tiết'; W = 640; P = 'Detail'; M = $true }
) $false)
function Load-Backup {
    Start-Work 'Kiểm tra sao lưu' '[pscustomobject]@{ R = @(Get-BackupStatus) }' @() {
        param($r, $err)
        if (-not $r) { Show-Info "Không kiểm tra được. $err"; return }
        Fill-List $pgBak.List $r.R { param($x) if ($x.Status -in 'Chưa có', 'Chưa bật') { 'bad' } }
        $has = @($r.R | Where-Object { $_.Status -in 'Có', 'Đã bật', 'Đã đăng nhập' } | Where-Object { $_.Item -notlike 'Phần mềm*' }).Count
        Set-Title $pgBak @($r.R).Count $(if ($has) { "[có $has hình thức]" } else { '[chưa có sao lưu]' })
        $pgBak.Detail.Text = 'Mẹo: muốn File History sao lưu cả thư mục ngoài Documents (ví dụ E:\Works), chuột phải thư mục đó > Show more options > Include in library > Documents.'
    }
}
$pgBak.Auto = { Load-Backup }
[void](New-Btn $pgBak.Act 'chép thư mục…' {
    $src = New-Object Windows.Forms.FolderBrowserDialog
    $src.Description = 'Chọn thư mục cần sao lưu'
    if ($src.ShowDialog() -ne 'OK') { return }
    $dst = New-Object Windows.Forms.FolderBrowserDialog
    $dst.Description = 'Chọn nơi lưu bản sao (nên là ổ khác)'
    if ($dst.ShowDialog() -ne 'OK') { return }
    if ($dst.SelectedPath.Substring(0, 1) -eq $src.SelectedPath.Substring(0, 1)) {
        if (-not (Confirm 'Nơi lưu nằm cùng ổ với thư mục gốc – nếu ổ hỏng sẽ mất cả hai. Vẫn tiếp tục?')) { return }
    }
    if (-not (Confirm "Chép`r`n  $($src.SelectedPath)`r`nsang`r`n  $($dst.SelectedPath)`r`n`r`nChỉ thêm và cập nhật file, không xoá gì ở nơi lưu.")) { return }
    Start-Work 'Chép thư mục' '[pscustomobject]@{ R = (Copy-FolderBackup $A0 $A1) }' @($src.SelectedPath, $dst.SelectedPath) {
        param($r, $err)
        if (-not $r) { Show-Info "Có lỗi. $err"; return }
        Write-Log "Sao lưu: $($r.R.Dest) | mã $($r.R.Code)"
        $head = if ($r.R.Ok) { "Đã chép xong vào $($r.R.Dest)." } else { "Chép có lỗi (mã $($r.R.Code))." }
        Show-Info "$head`r`n`r`n$($r.R.Summary)"
        Load-Backup
    }
} -Primary)
[void](New-Btn $pgBak.Act 'mở file history' { Start-Process control.exe '/name Microsoft.FileHistory' })
[void](New-Btn $pgBak.Act '⟳ kiểm tra' { Load-Backup })

# ===== Nhật ký =====
$pgLog = New-Page 'log' 'nhật ký' 'Mọi thay đổi công cụ đã làm trên máy: tắt/bật khởi động, chuyển vào Thùng rác, gỡ ứng dụng, sao lưu.'
[void](New-DarkList $pgLog @(
    @{ H = 'thời gian'; W = 170; P = 'Time'; M = $true }
    @{ H = 'nội dung'; W = 900; P = 'Text' }
) $false)
function Load-Log {
    $rows = @()
    if (Test-Path $LogFile) {
        $rows = Get-Content $LogFile -Encoding UTF8 | Where-Object { $_ } | ForEach-Object {
            [pscustomobject]@{ Time = $_.Substring(0, [Math]::Min(19, $_.Length)); Text = $(if ($_.Length -gt 21) { $_.Substring(21) } else { '' }) }
        }
        [array]::Reverse($rows)
    }
    Fill-List $pgLog.List $rows $null
    Set-Title $pgLog @($rows).Count ''
    $pgLog.Detail.Text = "File nhật ký: $LogFile"
}
[void](New-Btn $pgLog.Act 'mở bằng notepad' { if (Test-Path $LogFile) { Start-Process notepad.exe "`"$LogFile`"" } else { Show-Info 'Chưa có thao tác nào được ghi lại.' } })
[void](New-Btn $pgLog.Act '⟳ tải lại' { Load-Log } -Primary)
$pgLog.Auto = { Load-Log }

# ===== Ngôn ngữ =====
# Bảng dịch các chữ cố định của giao diện. Nội dung dữ liệu (giải thích, nhãn trạng thái) giữ tiếng Việt.
$Dict = New-Object 'System.Collections.Generic.Dictionary[string,string]'
foreach ($pair in @(
    # menu
    'hiệu năng|performance', 'khởi động|startup', 'ổ đĩa|disks', 'phân tích|analyzer', 'ứng dụng|apps', 'bảo mật|security',
    'sao lưu|backup', 'nhật ký|log', 'cài đặt|settings', 'xác nhận|confirm', 'thông báo|notice',
    # thanh bên, nhãn quyền
    '● SẴN SÀNG|● READY', '● ĐANG LÀM|● WORKING', 'QUẢN TRỊ|ADMIN', 'KHÔNG CÓ QUYỀN QUẢN TRỊ|NOT ADMIN',
    'đủ quyền để đọc mọi thông tin|full access to all information', 'thiếu quyền – một số mục sẽ trống|limited rights – some data will be missing',
    # nút
    '⟳ đo lại|⟳ measure', 'tắt mục đã chọn|disable selected', 'bật lại mục đã chọn|re-enable selected', '⟳ tải lại|⟳ reload',
    '→ thùng rác|→ recycle bin', 'mở vị trí|open location', 'tìm ảnh trùng lặp|find duplicate images', '⟳ quét dung lượng|⟳ scan space',
    '↑ lên trên|↑ up', '⟳ quét|⟳ scan', 'chọn thư mục…|choose folder…', 'kiểm tra một tệp…|check a file…',
    'gỡ mục đã chọn|uninstall selected', 'mở thư mục cài|open install folder', '⟳ phân tích|⟳ analyze', '⟳ kiểm tra|⟳ check',
    'chép thư mục…|copy folder…', 'mở file history|open file history', 'mở bằng notepad|open in notepad',
    'có, tiếp tục|yes, continue', 'không|no', 'đóng|close', 'mở|open', '↺ KHÔI PHỤC MẶC ĐỊNH|↺ RESET TO DEFAULTS',
    # bộ lọc
    'lọc:|filter:', 'ổ:|drives:', '  xem:|  view:', 'quét:|scan:', '   màu:|   colors:', 'tháng  ·|months  ·', 'không dùng trong|unused for',
    'chỉ hiện >|only show >', 'tất cả|all', 'đang bật|enabled', 'đã tắt|disabled', 'chạy khi đăng nhập|at sign-in', 'tác vụ|tasks', 'dịch vụ|services',
    '20 tệp lớn nhất|20 largest files', '20 thư mục lớn nhất|20 largest folders', 'bộ cài & file nén|installers & archives',
    'tải xuống cũ|old downloads', 'bộ nhớ đệm & file tạm|caches & temp files', 'ảnh trùng lặp|duplicate images',
    'game steam|steam games', 'hệ thống|system', 'cần xem|needs review', 'diệt virus|antivirus', 'mạng|network', 'extension|extensions',
    '■ video|■ video', '■ ảnh|■ images', '■ âm thanh|■ audio', '■ nén / bộ cài|■ archives / installers', '■ chương trình|■ programs',
    '■ dữ liệu game|■ game data', '■ tài liệu|■ documents', '■ khác|■ other',
    # cột
    'chương trình|program', 'tiến trình|processes', 'là gì|what it is', 'đường dẫn|path', 'tên|name', 'loại|type', 'trạng thái|status',
    'gợi ý|advice', 'lệnh chạy|command', 'kích thước|size', 'ngày sửa|modified', 'xoá được?|deletable?', 'ghi chú|note',
    '% thư mục cha|% of parent', 'dung lượng|disk size', 'số tệp|files', 'dùng lần cuối|last used', 'nguồn dữ liệu|source',
    'nhà phát hành|publisher', 'mức|level', 'nhóm|group', 'mục|item', 'chi tiết|details', 'hình thức|method', 'thời gian|time', 'nội dung|message',
    # lời giới thiệu từng trang
    'Chương trình nào đang dùng CPU và RAM. Dòng vàng là chương trình vượt ngưỡng "nặng" (chỉnh trong cài đặt). Bấm một dòng để xem nó là gì.|Which programs are using CPU and RAM. Yellow rows exceed the "heavy" threshold (set in settings). Click a row to see what it is.',
    'Mọi thứ tự chạy khi bật máy. Tích chọn rồi bấm "tắt" – không xoá gì, bật lại được bất cứ lúc nào. Dịch vụ được chuyển sang "chạy khi cần".|Everything that starts with Windows. Tick items and press "disable" – nothing is deleted and you can re-enable any time. Services are set to "manual".',
    'Tìm thứ đang chiếm chỗ. Tích chọn rồi chuyển vào Thùng rác – mục quá lớn so với Thùng rác được bỏ qua để bạn tự quyết. Nhớ dọn Thùng rác để lấy lại dung lượng.|Find what is taking space. Tick items to move them to the Recycle Bin – items too big for it are skipped so you decide. Empty the Recycle Bin to get the space back.',
    'Bản đồ dung lượng: ô càng to càng chiếm nhiều chỗ. Bấm để chọn, bấm đúp vào thư mục để đi vào trong. Màu theo loại tệp, xem chú thích bên dưới.|Space map: bigger blocks take more space. Click to select, double-click a folder to go inside. Colours follow file type, see the legend below.',
    'Ứng dụng đã cài và lần dùng cuối. Dữ liệu Steam là chính xác; ứng dụng khác dựa vào lịch sử chạy của Windows nên chỉ biết được từ một mốc nhất định.|Installed apps and when they were last used. Steam data is exact; other apps rely on Windows run history, which only goes back to a certain date.',
    'Chỉ kiểm tra, không thay đổi gì. Gỡ extension trong trang quản lý extension của từng trình duyệt – mỗi hồ sơ (profile) một trang riêng.|Read-only check, nothing is changed. Remove extensions from each browser''s extension page – one page per profile.',
    'Máy đã được sao lưu chưa và ở đâu. Cách đơn giản nhất: bật File History lên một ổ cắm ngoài, và chép các thư mục quan trọng sang một ổ khác.|Whether this PC is backed up, and where. Simplest setup: turn on File History to an external drive, and copy important folders to another drive.',
    'Mọi thay đổi công cụ đã làm trên máy: tắt/bật khởi động, chuyển vào Thùng rác, gỡ ứng dụng, sao lưu.|Every change this tool made: startup items, Recycle Bin moves, uninstalls, backups.',
    'Tuỳ chỉnh cách công cụ hoạt động. Thay đổi được lưu ngay.|Customize how the tool works. Changes are saved immediately.',
    # trang cài đặt
    'ngôn ngữ|language', 'khởi động cùng windows|start with windows', 'trang mở đầu|start page', 'tự tải dữ liệu khi mở trang|load data when a page opens',
    'ngưỡng "nặng" ở trang hiệu năng (%)|"heavy" threshold on performance page (%)', 'số tháng "không dùng" mặc định|default "unused" months',
    'hỏi lại lần 2 khi xoá mục "không nên xoá"|ask twice before deleting "shouldn''t delete" items', 'lưới nền|background grid',
    'thư mục dữ liệu|data folder', 'phiên bản|version',
    # nhãn ở khung chi tiết
    'đã chọn|selected', 'phạm vi|scope', 'hãng|vendor', 'nguồn|source', 'thư mục cài|install folder', 'lệnh gỡ|uninstall command'
)) { $kv = $pair -split '\|', 2; $Dict[$kv[0]] = $kv[1] }
$DictRev = New-Object 'System.Collections.Generic.Dictionary[string,string]'
foreach ($k in $Dict.Keys) { if (-not $DictRev.ContainsKey($Dict[$k])) { $DictRev[$Dict[$k]] = $k } }
$script:LangHooks = New-Object System.Collections.ArrayList

function Tr([string]$s) { if ($Cfg.Lang -eq 'en' -and $Dict.ContainsKey($s)) { return $Dict[$s] } return $s }
# Duyệt mọi control, đổi chữ cố định theo ngôn ngữ đang chọn (chỉ đổi chữ có trong bảng dịch).
function Apply-Language($root) {
    $en = $Cfg.Lang -eq 'en'
    $stack = New-Object System.Collections.Stack
    $stack.Push($root)
    while ($stack.Count) {
        $c = $stack.Pop()
        $txt = $c.Text
        if ($txt) {
            if ($en -and $Dict.ContainsKey($txt)) { $c.Text = $Dict[$txt] }
            elseif (-not $en -and $DictRev.ContainsKey($txt)) { $c.Text = $DictRev[$txt] }
        }
        if ($c -is [Windows.Forms.ListView]) {
            foreach ($h in $c.Columns) {
                if ($en -and $Dict.ContainsKey($h.Text)) { $h.Text = $Dict[$h.Text] }
                elseif (-not $en -and $DictRev.ContainsKey($h.Text)) { $h.Text = $DictRev[$h.Text] }
            }
        }
        foreach ($ch in $c.Controls) { $stack.Push($ch) }
    }
    if ($root -ne $form) { return }
    foreach ($k in $script:Pages.Keys) {
        $pg = $script:Pages[$k]
        $pg.Name = (Tr $pg.NavVi).ToUpper()
        $pg.Nav.Text = Cap (Tr $pg.NavVi)
        $rest = ($pg.T1.Text -split ' · ', 2)
        $pg.T1.Text = $pg.Name + $(if ($rest.Count -gt 1) { ' · ' + $rest[1] } else { '' })
    }
    foreach ($h in $script:LangHooks) { & $h }
}
[void]$script:LangHooks.Add({ $tglPerf.Text = (Tr 'chỉ hiện >') + " $($Cfg.HeavyPct)%" })
[void]$script:LangHooks.Add({ $top.Width = $top.Width; $adminTag.Location = New-Object Drawing.Point(($top.Width - $winButtons.Width - $adminTag.Width - 12), 11) })
[void]$script:LangHooks.Add({ if (-not $script:Jobs.Count) { $lblState.Text = (Tr '● SẴN SÀNG') } })

# ===== Cài đặt =====
# Công tắc bật/tắt kiểu terminal: khung nhỏ, nút vuông chạy sang phải khi bật.
function New-Switch($parent, [bool]$on, [scriptblock]$onChange) {
    $sw = New-Object Windows.Forms.Panel
    $sw.Size = New-Object Drawing.Size(32, 18); $sw.Cursor = 'Hand'; $sw.BackColor = $Theme.Panel
    $sw.Margin = New-Object Windows.Forms.Padding(0, 4, 0, 0)
    $sw.Tag = @{ On = $on; Change = $onChange }
    Set-DoubleBuffer $sw
    $sw.Add_Paint({
        param($s, $e)
        $g = $e.Graphics
        $g.Clear($Theme.Panel)
        if ($s.Tag.On) {
            $g.DrawRectangle($Pens.Green, 0, 0, $s.Width - 1, $s.Height - 1)
            $g.FillRectangle($Brushes.Green, $s.Width - 14, 3, 11, $s.Height - 6)
        } else {
            $g.DrawRectangle($Pens.Disabled, 0, 0, $s.Width - 1, $s.Height - 1)
            $g.FillRectangle($Brushes.Track, 3, 3, 11, $s.Height - 6)
        }
    })
    $sw.Add_Click({ param($s, $e) $s.Tag.On = -not $s.Tag.On; $s.Invalidate(); if ($s.Tag.Change) { & $s.Tag.Change $s.Tag.On } })
    $parent.Controls.Add($sw)
    return $sw
}
function Set-Switch($sw, [bool]$on) { $sw.Tag.On = $on; $sw.Invalidate() }

# Khởi động cùng Windows bằng Task Scheduler với quyền cao nhất, nên không bị hỏi quyền quản trị mỗi lần đăng nhập.
$StartupTaskName = 'CleanVaiLon'
function Test-RunAtLogon { return [bool](Get-ScheduledTask -TaskName $StartupTaskName -ErrorAction SilentlyContinue) }
function Set-RunAtLogon([bool]$on) {
    if (-not $on) { Unregister-ScheduledTask -TaskName $StartupTaskName -Confirm:$false -ErrorAction Stop; Write-Log 'Cài đặt: tắt khởi động cùng Windows'; return }
    $user = "$env:USERDOMAIN\$env:USERNAME"
    $act = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Minimized -File `"$ScriptPath`" -Relaunched -StartMinimized"
    $trg = New-ScheduledTaskTrigger -AtLogOn -User $user
    $pri = New-ScheduledTaskPrincipal -UserId $user -RunLevel Highest -LogonType Interactive
    $set = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
    Register-ScheduledTask -TaskName $StartupTaskName -Action $act -Trigger $trg -Principal $pri -Settings $set -Force -ErrorAction Stop | Out-Null
    Write-Log 'Cài đặt: bật khởi động cùng Windows'
}

$pgSet = New-Page 'set' 'cài đặt' 'Tuỳ chỉnh cách công cụ hoạt động. Thay đổi được lưu ngay.'
$pgSet.Detail.Parent.Parent.Visible = $false
$verLbl = New-Text $pgSet.Act ("v$AppVersion")
$verLbl.Font = $Fonts.Main; $verLbl.Margin = New-Object Windows.Forms.Padding(0, 8, 0, 0)
$verTitle = New-Text $pgSet.Act 'phiên bản'
$verTitle.Font = $Fonts.Main; $verTitle.Margin = New-Object Windows.Forms.Padding(8, 8, 6, 0)

$setBox = New-Object Windows.Forms.Panel
$setBox.Dock = 'Top'; $setBox.BackColor = $Theme.Panel; $setBox.Padding = New-Object Windows.Forms.Padding(1)
$setBox.Add_Paint({ param($s, $e) $e.Graphics.DrawRectangle($Pens.GreenDim, 0, 0, $s.Width - 1, $s.Height - 1) })
$script:SetRows = New-Object System.Collections.ArrayList
# Một dòng cài đặt: nhãn bên trái, điều khiển bên phải.
function Add-SettingRow([string]$label) {
    $row = New-Object Windows.Forms.Panel
    $row.Dock = 'Top'; $row.Height = 40; $row.BackColor = $Theme.Panel
    $row.Add_Paint({ param($s, $e) $e.Graphics.DrawLine($Pens.Line, 0, $s.Height - 1, $s.Width, $s.Height - 1) })
    $l = New-Object Windows.Forms.Label
    $l.Text = $label; $l.Dock = 'Fill'; $l.TextAlign = 'MiddleLeft'; $l.ForeColor = (Hex '#9DB8A9'); $l.BackColor = $Theme.Panel
    $l.Padding = New-Object Windows.Forms.Padding(14, 0, 0, 0)
    $right = New-Object Windows.Forms.FlowLayoutPanel
    $right.Dock = 'Right'; $right.AutoSize = $true; $right.WrapContents = $false; $right.BackColor = $Theme.Panel
    $right.Padding = New-Object Windows.Forms.Padding(0, 7, 12, 0)
    $row.Controls.Add($l); $row.Controls.Add($right)
    [void]$script:SetRows.Add($row)
    return $right
}

$r = Add-SettingRow 'ngôn ngữ'
$chipLang = New-ChipGroup $r ([ordered]@{ vi = 'VI'; en = 'EN' }) { param($k) $Cfg.Lang = $k; Save-Settings; Apply-Language $form }
Select-Chip $chipLang $Cfg.Lang

$r = Add-SettingRow 'khởi động cùng windows'
$swLogon = New-Switch $r (Test-RunAtLogon) {
    param($on)
    try { Set-RunAtLogon $on }
    catch { Set-Switch $swLogon (-not $on); Show-Info "Không đổi được: $($_.Exception.Message)`r`n`r`nCần mở công cụ với quyền quản trị." }
}

$r = Add-SettingRow 'trang mở đầu'
$startOpts = [ordered]@{}
foreach ($k in $script:Pages.Keys) { if ($k -ne 'set') { $startOpts[$k] = $script:Pages[$k].NavVi } }
$chipStartPage = New-ChipGroup $r $startOpts { param($k) $Cfg.StartPage = $k; Save-Settings }
Select-Chip $chipStartPage $Cfg.StartPage

$r = Add-SettingRow 'tự tải dữ liệu khi mở trang'
[void](New-Switch $r ([bool]$Cfg.AutoLoad) { param($on) $Cfg.AutoLoad = $on; Save-Settings })

$r = Add-SettingRow 'ngưỡng "nặng" ở trang hiệu năng (%)'
$stHeavy = New-Stepper $r ([int]$Cfg.HeavyPct) 1 50 { $Cfg.HeavyPct = $stHeavy.Value; Save-Settings; $tglPerf.Text = (Tr 'chỉ hiện >') + " $($Cfg.HeavyPct)%"; Refresh-PerfList }

$r = Add-SettingRow 'số tháng "không dùng" mặc định'
$stMonths = New-Stepper $r ([int]$Cfg.Months) 1 36 { $Cfg.Months = $stMonths.Value; Save-Settings }

$r = Add-SettingRow 'hỏi lại lần 2 khi xoá mục "không nên xoá"'
[void](New-Switch $r ([bool]$Cfg.DoubleConfirm) { param($on) $Cfg.DoubleConfirm = $on; Save-Settings })

$r = Add-SettingRow 'lưới nền'
[void](New-Switch $r ([bool]$Cfg.Grid) { param($on) $Cfg.Grid = $on; Save-Settings; $content.Invalidate($true) })

$r = Add-SettingRow 'thư mục dữ liệu'
$dataLbl = New-Text $r $DataDir
$dataLbl.Margin = New-Object Windows.Forms.Padding(0, 5, 10, 0)
[void](New-ChipButton $r 'mở' @{ On = $false }).Add_Click({ Start-Process explorer.exe "`"$DataDir`"" })
foreach ($c in @($r.Controls)) { if ($c -is [Windows.Forms.Button]) { Set-ChipState $c $false } }

# Dòng thêm sau nằm trên, nên thêm ngược thứ tự để dòng đầu tiên ở trên cùng.
for ($i = $script:SetRows.Count - 1; $i -ge 0; $i--) { $setBox.Controls.Add($script:SetRows[$i]) }
$setBox.Height = $script:SetRows.Count * 40 + 2

$resetBar = New-Object Windows.Forms.FlowLayoutPanel
$resetBar.Dock = 'Top'; $resetBar.AutoSize = $true; $resetBar.BackColor = [Drawing.Color]::Transparent
$resetBar.Padding = New-Object Windows.Forms.Padding(0, 14, 0, 0)
$btnReset = New-Btn $resetBar '↺ KHÔI PHỤC MẶC ĐỊNH' {
    if (-not (Confirm 'Đưa mọi cài đặt về mặc định? (Không ảnh hưởng tới các mục đã tắt khởi động hay đã xoá.)')) { return }
    foreach ($k in $CfgDefaults.Keys) { $Cfg[$k] = $CfgDefaults[$k] }
    Save-Settings
    Show-Info 'Đã khôi phục mặc định. Mở lại công cụ để áp dụng hết.'
}
$btnReset.Margin = New-Object Windows.Forms.Padding(0)
$btnReset.ForeColor = $Theme.Red; $btnReset.FlatAppearance.BorderColor = $Theme.Red
$btnReset.FlatAppearance.MouseOverBackColor = $Theme.BadBg; $btnReset.Font = $Fonts.Bold
$pgSet.Panel.Controls.Add($setBox); $pgSet.Panel.Controls.Add($resetBar)
$setBox.BringToFront(); $resetBar.BringToFront()

# ----- Chạy -----
$form.Add_Shown({
    if ($StartMinimized) { $form.WindowState = 'Minimized' }
    else {
        # Khi tự xin quyền quản trị, PowerShell được mở ẩn; luôn đưa cửa sổ về kích thước bình thường.
        [void][MTC.Native]::ShowWindow($form.Handle, 9)
        if ($form.WindowState -ne 'Normal') { $form.WindowState = 'Normal' }
        [void][MTC.Native]::SetForegroundWindow($form.Handle)
        $form.Activate()
    }
    Apply-Language $form
    $first = if ($script:Pages.Contains([string]$Cfg.StartPage)) { [string]$Cfg.StartPage } else { 'perf' }
    Show-Page $first
})
[void]$form.ShowDialog()
$timer.Stop()
