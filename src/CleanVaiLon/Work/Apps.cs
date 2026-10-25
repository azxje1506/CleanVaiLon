// Trang Ứng dụng: ứng dụng đã cài, lần dùng cuối, gỡ cài đặt.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CleanVaiLon
{
    public class AppRow
    {
        public string Name, Source, Kind, Publisher, Version, Location, Uninstall, SteamId;
        public double Size;
        public DateTime? LastUsed;
    }

    public class UninstallResult { public string Name, Result, Detail; }

    public static class Apps
    {
        public const string KApp = "Ứng dụng", KGame = "Game Steam", KSystem = "Hệ thống – nên giữ";
        const string SystemAppPattern = @"Visual C\+\+|\.NET|Redistributable|Runtime|Driver|Intel\(R\)|NVIDIA|Realtek|PhysX|DirectX|SDK|Update Health|GameInput|Compatibility|Python Launcher|WebView2|^Steam$|Xbox|Microsoft Edge$|Genuine Service|Steamworks|Launcher Prerequisites|UE4 Prerequisites|Windows Subsystem";

        public static readonly string[] UninstallRoots =
        {
            @"HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall",
        };

        // Mọi khoá con trong các khoá Uninstall, dạng tên giá trị → giá trị.
        public static List<Dictionary<string, object>> UninstallEntries()
        {
            var list = new List<Dictionary<string, object>>();
            foreach (var root in UninstallRoots)
            {
                try
                {
                    using (var k = Startup.OpenKey(root))
                    {
                        if (k == null) continue;
                        foreach (var n in k.GetSubKeyNames())
                        {
                            try
                            {
                                using (var s = k.OpenSubKey(n))
                                {
                                    if (s == null) continue;
                                    var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                                    foreach (var v in s.GetValueNames()) if (v.Length > 0) d[v] = s.GetValue(v);
                                    list.Add(d);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            return list;
        }

        static string S(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : "";
        static bool T(Dictionary<string, object> d, string k)
        {
            if (!d.TryGetValue(k, out var v) || v == null) return false;
            if (v is int i) return i != 0;
            if (v is long l) return l != 0;
            if (v is string s) return s.Length > 0;
            return TextFiles.Truthy(v);
        }

        static string Rot13(string n)
        {
            var c = n.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                int x = c[i];
                if (x >= 65 && x <= 90) c[i] = (char)(65 + (x - 65 + 13) % 26);
                else if (x >= 97 && x <= 122) c[i] = (char)(97 + (x - 97 + 13) % 26);
            }
            return new string(c);
        }

        static void Max(Dictionary<string, DateTime> d, string k, DateTime t) { if (!d.TryGetValue(k, out var o) || o < t) d[k] = t; }

        // Tệp .exe trong thư mục cài, sâu tối đa 3 tầng thư mục con, tối đa 200 tệp.
        static IEnumerable<string> ExeNames(string dir)
        {
            int count = 0;
            var stack = new Stack<KeyValuePair<string, int>>();
            stack.Push(new KeyValuePair<string, int>(dir, 0));
            var result = new List<string>();
            while (stack.Count > 0 && count < 200)
            {
                var cur = stack.Pop();
                try
                {
                    foreach (var f in Directory.GetFiles(cur.Key, "*.exe")) { result.Add(Path.GetFileName(f)); if (++count >= 200) break; }
                    if (cur.Value < 3)
                        foreach (var sub in new DirectoryInfo(cur.Key).GetDirectories().Reverse())
                            if ((sub.Attributes & FileAttributes.ReparsePoint) == 0) stack.Push(new KeyValuePair<string, int>(sub.FullName, cur.Value + 1));
                }
                catch { }
            }
            return result;
        }

        public static List<AppRow> Usage()
        {
            // Prefetch: lịch sử chạy chương trình của Windows (cần quyền quản trị).
            var pf = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            DateTime? pfStart = null;
            try
            {
                foreach (var f in new DirectoryInfo(Path.Combine(AppInfo.Env("WINDIR"), "Prefetch")).GetFiles("*.pf"))
                {
                    string exe = Regex.Replace(f.Name, "-[0-9A-F]{8}\\.pf$", "", RegexOptions.IgnoreCase).ToLower();
                    Max(pf, exe, f.LastWriteTime);
                    if (!pfStart.HasValue || f.CreationTime < pfStart) pfStart = f.CreationTime;
                }
            }
            catch { }
            // UserAssist: lần mở bằng biểu tượng / menu Start.
            var ua = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var root = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist"))
                    if (root != null)
                        foreach (var g in root.GetSubKeyNames())
                            using (var k = root.OpenSubKey(g + "\\Count"))
                            {
                                if (k == null) continue;
                                foreach (var n in k.GetValueNames())
                                {
                                    try
                                    {
                                        if (!(k.GetValue(n) is byte[] v) || v.Length < 68) continue;
                                        long ft = BitConverter.ToInt64(v, 60);
                                        if (ft <= 0) continue;
                                        string leaf = Path.GetFileName(Rot13(n)).ToLower();
                                        var t = DateTime.FromFileTime(ft);
                                        if (t.Year < 2000) continue;
                                        Max(ua, leaf, t);
                                    }
                                    catch { }
                                }
                            }
            }
            catch { }
            // Steam: ngày chơi cuối và dung lượng từng game.
            var steamLast = new Dictionary<string, DateTime>();
            var steamSize = new Dictionary<string, double>();
            foreach (var lib in Disk.SteamLibraries())
            {
                try
                {
                    var ud = Path.Combine(lib, "userdata");
                    if (Directory.Exists(ud))
                        foreach (var u in Directory.GetDirectories(ud))
                        {
                            var cfg = Path.Combine(u, @"config\localconfig.vdf");
                            if (!File.Exists(cfg)) continue;
                            var txt = File.ReadAllText(cfg);
                            foreach (Match m in Regex.Matches(txt, "\"(\\d+)\"\\s*\\{[^{}]*?\"LastPlayed\"\\s*\"(\\d+)\""))
                                steamLast[m.Groups[1].Value] = DateTimeOffset.FromUnixTimeSeconds(long.Parse(m.Groups[2].Value)).LocalDateTime;
                        }
                }
                catch { }
                try
                {
                    var sa = Path.Combine(lib, "steamapps");
                    if (Directory.Exists(sa))
                        foreach (var acf in Directory.GetFiles(sa, "appmanifest_*.acf"))
                        {
                            var t = File.ReadAllText(acf);
                            var id = Regex.Match(t, "\"appid\"\\s*\"(\\d+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
                            var sz = Regex.Match(t, "\"SizeOnDisk\"\\s*\"(\\d+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
                            if (id.Length > 0) steamSize[id] = sz.Length > 0 ? double.Parse(sz, CultureInfo.InvariantCulture) : 0;
                        }
                }
                catch { }
            }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var apps = new List<Dictionary<string, object>>();
            foreach (var a in UninstallEntries())
            {
                string dn = S(a, "DisplayName");
                if (dn.Length == 0 || S(a, "UninstallString").Length == 0 || T(a, "SystemComponent") || T(a, "ParentKeyName") || Rx.M(S(a, "ReleaseType"), "Update|Hotfix")) continue;
                if (seen.Add(dn)) apps.Add(a);
            }
            apps = apps.OrderBy(a => S(a, "DisplayName"), StringComparer.CurrentCultureIgnoreCase).ToList();
            const string skipExe = "unins|uninst|setup|update|crash|helper|install|repair|redist|vc_|elevat";
            var rows = new List<AppRow>();
            foreach (var a in apps)
            {
                string un = S(a, "UninstallString");
                string steamId = Regex.Match(un, @"steam://uninstall/(\d+)").Groups[1].Value;
                var exes = new List<string>();
                string icon = S(a, "DisplayIcon");
                if (icon.Length > 0)
                {
                    string p = icon.Split(',')[0].Trim('"');
                    if (Rx.M(p, @"\.exe$")) { try { exes.Add(Path.GetFileName(p)); } catch { } }
                }
                string loc = S(a, "InstallLocation").Trim('"');
                if (loc.Length > 0 && (Directory.Exists(loc) || File.Exists(loc)))
                    exes.AddRange(ExeNames(loc));
                exes = exes.Where(x => !string.IsNullOrEmpty(x) && !Rx.M(x, skipExe)).Select(x => x.ToLower()).Distinct().ToList();
                DateTime? pLast = exes.Where(pf.ContainsKey).Select(x => (DateTime?)pf[x]).Max();
                DateTime? uLast = exes.Where(ua.ContainsKey).Select(x => (DateTime?)ua[x]).Max();
                DateTime? last = null; string source;
                if (steamId.Length > 0 && steamLast.ContainsKey(steamId)) { last = steamLast[steamId]; source = "Steam (chính xác)"; }
                else if (pLast.HasValue) { last = pLast; source = "Lịch sử chạy (Prefetch)"; }
                else if (uLast.HasValue) { last = uLast; source = "Lần mở bằng biểu tượng (ước lượng)"; }
                else source = pfStart.HasValue ? "Không thấy chạy từ " + pfStart.Value.ToString("dd/MM/yyyy") : "Không có dữ liệu";
                double size = 0;
                if (steamId.Length > 0 && steamSize.ContainsKey(steamId)) size = steamSize[steamId];
                else if (a.TryGetValue("EstimatedSize", out var es) && es != null) { try { size = Convert.ToDouble(es, CultureInfo.InvariantCulture) * 1024; } catch { } }
                string kind = KApp;
                if (steamId.Length > 0) kind = KGame;
                if (Rx.M(S(a, "DisplayName"), SystemAppPattern)) kind = KSystem;
                rows.Add(new AppRow
                {
                    Name = S(a, "DisplayName"), Size = size, LastUsed = last, Source = source, Kind = kind,
                    Publisher = S(a, "Publisher"), Version = S(a, "DisplayVersion"), Location = loc, Uninstall = un, SteamId = steamId,
                });
            }
            return rows;
        }

        public static UninstallResult Uninstall(AppRow app)
        {
            try
            {
                if (!string.IsNullOrEmpty(app.SteamId))
                {
                    Process.Start(new ProcessStartInfo("steam://uninstall/" + app.SteamId) { UseShellExecute = true });
                    return new UninstallResult { Name = app.Name, Result = "Đã mở", Detail = "Steam sẽ hỏi xác nhận gỡ." };
                }
                string cmd = app.Uninstall.Trim();
                string exe, args = "";
                if (cmd.StartsWith("\""))
                {
                    int end = cmd.IndexOf('"', 1);
                    exe = cmd.Substring(1, end - 1); args = cmd.Substring(end + 1).Trim();
                }
                else
                {
                    int i = cmd.ToLower().IndexOf(".exe", StringComparison.Ordinal);
                    if (i < 0) exe = cmd; else { exe = cmd.Substring(0, i + 4); args = cmd.Substring(i + 4).Trim(); }
                }
                if (Rx.M(exe, "msiexec")) args = Regex.Replace(args, @"/I\s*\{", "/X{", RegexOptions.IgnoreCase);
                using (var p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true }))
                {
                    if (p == null) return new UninstallResult { Name = app.Name, Result = "Xong", Detail = "" };
                    p.WaitForExit();
                    int code = p.ExitCode;
                    string res = code == 0 || code == 3010 ? "Xong" : "Mã " + code;
                    string det = code == 3010 ? "Cần khởi động lại máy để gỡ xong." : code == 0 ? "" : "Trình gỡ báo lỗi hoặc bạn đã huỷ.";
                    return new UninstallResult { Name = app.Name, Result = res, Detail = det };
                }
            }
            catch (Exception ex)
            {
                return new UninstallResult { Name = app.Name, Result = "Lỗi", Detail = ex.Message };
            }
        }
    }
}
