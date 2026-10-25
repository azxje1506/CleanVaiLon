// Trang Bảo mật: chỉ kiểm tra, không thay đổi gì.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace CleanVaiLon
{
    public class SecRow { public string Level, Group, Item, Detail; }

    class Proc { public string Name, Path; public int Pid; }

    public class ExtInfo { public string Browser, Profile, Name, Level, Detail; public bool Enabled; }

    public static class Security
    {
        static IEnumerable<ManagementObject> Wmi(string ns, string query)
        {
            var list = new List<ManagementObject>();
            try
            {
                using (var q = new ManagementObjectSearcher(new ManagementScope(ns), new ObjectQuery(query)))
                    foreach (ManagementObject o in q.Get()) list.Add(o);
            }
            catch { }
            return list;
        }

        static object RegValue(RegistryKey root, string sub, string name)
        {
            try { using (var k = root.OpenSubKey(sub)) return k?.GetValue(name); } catch { return null; }
        }

        static bool Eq1(object v) { try { return v != null && Convert.ToInt64(v) == 1; } catch { return false; } }

        // ----- Kết nối TCP (thay cho Get-NetTCPConnection) -----
        public class TcpConn { public string LocalAddress, RemoteAddress; public int LocalPort, State, Pid; }

        [DllImport("iphlpapi.dll")] static extern int GetExtendedTcpTable(IntPtr table, ref int size, bool sort, int af, int tableClass, int reserved);

        public static List<TcpConn> TcpConnections()
        {
            var list = new List<TcpConn>();
            foreach (int af in new[] { 2, 23 })
            {
                int size = 0;
                GetExtendedTcpTable(IntPtr.Zero, ref size, false, af, 5, 0);
                if (size <= 0) continue;
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (GetExtendedTcpTable(buf, ref size, false, af, 5, 0) != 0) continue;
                    int n = Marshal.ReadInt32(buf);
                    IntPtr row = buf + 4;
                    for (int i = 0; i < n; i++)
                    {
                        if (af == 2)
                        {
                            // MIB_TCPROW_OWNER_PID: state, localAddr, localPort, remoteAddr, remotePort, pid
                            int state = Marshal.ReadInt32(row);
                            var la = new IPAddress((uint)Marshal.ReadInt32(row + 4));
                            int lp = Port(Marshal.ReadInt32(row + 8));
                            var ra = new IPAddress((uint)Marshal.ReadInt32(row + 12));
                            int pid = Marshal.ReadInt32(row + 20);
                            list.Add(new TcpConn { State = state, LocalAddress = la.ToString(), LocalPort = lp, RemoteAddress = ra.ToString(), Pid = pid });
                            row += 24;
                        }
                        else
                        {
                            // MIB_TCP6ROW_OWNER_PID: localAddr[16], localScope, localPort, remoteAddr[16], remoteScope, remotePort, state, pid
                            var lab = new byte[16]; Marshal.Copy(row, lab, 0, 16);
                            int lp = Port(Marshal.ReadInt32(row + 20));
                            var rab = new byte[16]; Marshal.Copy(row + 24, rab, 0, 16);
                            int state = Marshal.ReadInt32(row + 48);
                            int pid = Marshal.ReadInt32(row + 52);
                            list.Add(new TcpConn { State = state, LocalAddress = new IPAddress(lab).ToString(), LocalPort = lp, RemoteAddress = new IPAddress(rab).ToString(), Pid = pid });
                            row += 56;
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            return list;
        }

        static int Port(int raw) => ((raw & 0xFF) << 8) | ((raw >> 8) & 0xFF);

        public static List<SecRow> Report()
        {
            var rows = new List<SecRow>();
            void Add(string level, string group, string item, string detail) => rows.Add(new SecRow { Level = level, Group = group, Item = item, Detail = detail });

            // Trình diệt virus
            var st = Wmi(@"root\Microsoft\Windows\Defender", "SELECT RealTimeProtectionEnabled, IsTamperProtected, AntivirusSignatureLastUpdated FROM MSFT_MpComputerStatus").FirstOrDefault();
            const string polKey = @"SOFTWARE\Policies\Microsoft\Windows Defender";
            var av = Wmi(@"root\SecurityCenter2", "SELECT displayName FROM AntivirusProduct")
                .Select(o => Convert.ToString(o["displayName"])).Where(n => !Rx.M(n, "Windows Defender")).ToList();
            if (Eq1(RegValue(Registry.LocalMachine, polKey, "DisableAntiVirus")) || Eq1(RegValue(Registry.LocalMachine, polKey, "DisableAntiSpyware")))
                Add("Cao", "Diệt virus", "Windows Defender bị tắt bằng chính sách", @"Trong HKLM\SOFTWARE\Policies\Microsoft\Windows Defender có DisableAntiVirus/DisableAntiSpyware = 1. Bạn xoá hai giá trị này, khởi động lại rồi bật lại trong Windows Security.");
            if (st != null && st["RealTimeProtectionEnabled"] is bool rt && rt)
            {
                string upd = "";
                try { var s = st["AntivirusSignatureLastUpdated"] as string; if (!string.IsNullOrEmpty(s)) upd = Fmt.Inv(ManagementDateTimeConverter.ToDateTime(s)); } catch { }
                Add("Thông tin", "Diệt virus", "Windows Defender đang bật", "Cập nhật lần cuối: " + upd);
                if (!(st["IsTamperProtected"] is bool tp && tp)) Add("Trung bình", "Diệt virus", "Tamper Protection đang tắt", "Bật trong Windows Security > Virus & threat protection > Manage settings.");
            }
            else if (av.Count > 0) Add("Thông tin", "Diệt virus", "Đang dùng: " + string.Join(", ", av), "");
            else Add("Cao", "Diệt virus", "Không có trình diệt virus nào đang chạy", "Máy không được bảo vệ. Bạn bật lại Windows Security > Virus & threat protection.");

            var fwOff = Wmi(@"root\StandardCimv2", "SELECT Name, Enabled FROM MSFT_NetFirewallProfile")
                .Where(o => { try { return Convert.ToInt32(o["Enabled"]) == 0; } catch { return false; } })
                .Select(o => Convert.ToString(o["Name"])).ToList();
            if (fwOff.Count > 0) Add("Cao", "Tường lửa", "Tường lửa tắt ở: " + string.Join(", ", fwOff), "Bật lại trong Windows Security > Firewall.");
            else Add("Thông tin", "Tường lửa", "Tường lửa đang bật ở mọi chế độ mạng", "");

            // Tiến trình không có chữ ký số
            var procs = Wmi(@"root\cimv2", "SELECT Name, ExecutablePath, ProcessId FROM Win32_Process")
                .Select(o => new Proc { Name = Convert.ToString(o["Name"]), Path = o["ExecutablePath"] as string, Pid = Convert.ToInt32(o["ProcessId"]) }).ToList();
            var sig = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in procs.Where(x => !string.IsNullOrEmpty(x.Path)))
            {
                if (sig.ContainsKey(p.Path)) continue;
                string s; try { s = Signature.Status(p.Path); } catch { s = "UnknownError"; }
                sig[p.Path] = s;
                if (s == "Valid") continue;
                if (Rx.M(p.Path, @"\\WindowsApps\\Microsoft\.")) continue;
                string level = Rx.M(p.Path, @"\\Temp\\|\\Downloads\\|\\Public\\") ? "Cao" : "Trung bình";
                string who = "";
                try { var vi = FileVersionInfo.GetVersionInfo(p.Path); who = (vi.FileDescription + " – " + vi.CompanyName).Trim(' ', '–'); } catch { }
                string why = s == "NotSigned" ? "Không có chữ ký số." : "Chữ ký số không hợp lệ (" + s + ").";
                Add(level, "Tiến trình", p.Name, why + " " + who + ". Đường dẫn: " + p.Path);
            }

            // Kết nối Internet chạy nền
            var byPid = new Dictionary<int, Proc>();
            foreach (var p in procs) byPid[p.Pid] = p;
            var dns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in Wmi(@"root\StandardCimv2", "SELECT Entry, Data FROM MSFT_DNSClientCache"))
                dns[Convert.ToString(e["Data"])] = Convert.ToString(e["Entry"]);
            const string priv = @"^(127\.|::1|0\.0\.0\.0|::$|10\.|192\.168\.|172\.(1[6-9]|2\d|3[01])\.|fe80|169\.254)";
            var tcp = TcpConnections();
            foreach (var g in tcp.Where(c => c.State == 5 && !Rx.M(c.RemoteAddress, priv)).GroupBy(c => c.Pid))
            {
                byPid.TryGetValue(g.Key, out var p);
                string name = p != null ? (string)p.Name : "PID " + g.Key;
                string hosts = string.Join(", ", g.Select(c => dns.TryGetValue(c.RemoteAddress, out var h) && h.Length > 0 ? h : c.RemoteAddress).Distinct().Take(5));
                string level = "Thông tin";
                string exe = p != null ? (string)p.Path : null;
                if (!string.IsNullOrEmpty(exe) && sig.TryGetValue(exe, out var ss) && ss != "Valid") level = "Trung bình";
                Add(level, "Kết nối Internet", name + " (" + g.Count() + " kết nối)", "Tới: " + hosts + ". " + exe);
            }
            var skipListen = new[] { "System", "svchost.exe", "lsass.exe", "wininit.exe", "services.exe", "spoolsv.exe" };
            foreach (var g in tcp.Where(c => c.State == 2 && (c.LocalAddress == "0.0.0.0" || c.LocalAddress == "::")).GroupBy(c => c.Pid))
            {
                if (!byPid.TryGetValue(g.Key, out var p) || skipListen.Contains((string)p.Name, StringComparer.OrdinalIgnoreCase)) continue;
                Add("Thông tin", "Mở cổng chờ kết nối", (string)p.Name, "Cổng " + string.Join(", ", g.Select(c => c.LocalPort).Distinct().OrderBy(x => x)) + ". " + (string)p.Path);
            }

            // Proxy và file hosts
            const string ieKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
            var auto = Convert.ToString(RegValue(Registry.CurrentUser, ieKey, "AutoConfigURL"));
            if (Eq1(RegValue(Registry.CurrentUser, ieKey, "ProxyEnable")) || !string.IsNullOrEmpty(auto))
                Add("Trung bình", "Mạng", "Đang dùng proxy", "Proxy: " + Convert.ToString(RegValue(Registry.CurrentUser, ieKey, "ProxyServer")) + " " + auto + ". Nếu bạn không tự đặt thì nên kiểm tra.");
            try
            {
                foreach (var line in File.ReadAllLines(Path.Combine(AppInfo.Env("WINDIR"), @"System32\drivers\etc\hosts"), Encoding.Default))
                {
                    if (Rx.M(line, @"^\s*#") || !Rx.M(line, @"\S")) continue;
                    var parts = System.Text.RegularExpressions.Regex.Split(line.Trim(), @"\s+");
                    if (parts.Length < 2 || Rx.M(parts[1], "^localhost$")) continue;
                    string level = Rx.M(parts[0], @"^(127\.|0\.0\.0\.0|::1)") ? "Thấp" : "Trung bình";
                    Add(level, "File hosts", line.Trim(), "Dòng tự thêm trong file hosts. Trỏ về 127.0.0.1 thường là để chặn trang web.");
                }
            }
            catch { }

            // Extension trình duyệt
            foreach (var e in BrowserExtensions()) Add(e.Level, "Extension trình duyệt", e.Name + " [" + e.Browser + " / " + e.Profile + "]", e.Detail);
            return rows;
        }

        static object ReadJson(string path)
        {
            try { return TextFiles.ParseJson(File.ReadAllText(path, Encoding.UTF8)); } catch { return null; }
        }

        static IEnumerable<object> Items(object v) => v is IEnumerable e && !(v is string) && !(v is IDictionary) ? e.Cast<object>() : Enumerable.Empty<object>();

        static int? Int(object v) { try { return v == null ? (int?)null : Convert.ToInt32(v); } catch { return null; } }

        public static List<ExtInfo> BrowserExtensions()
        {
            var res = new List<ExtInfo>();
            string la = AppInfo.Env("LOCALAPPDATA");
            var browsers = new[]
            {
                new[] { "Edge", la + @"\Microsoft\Edge\User Data" },
                new[] { "Chrome", la + @"\Google\Chrome\User Data" },
                new[] { "Brave", la + @"\BraveSoftware\Brave-Browser\User Data" },
                new[] { "Cốc Cốc", la + @"\CocCoc\Browser\User Data" },
            };
            foreach (var b in browsers)
            {
                if (!Directory.Exists(b[1])) continue;
                DirectoryInfo[] profiles;
                try { profiles = new DirectoryInfo(b[1]).GetDirectories(); } catch { continue; }
                foreach (var pdir in profiles)
                {
                    string extRoot = Path.Combine(pdir.FullName, "Extensions");
                    if (!Directory.Exists(extRoot)) continue;
                    var settings = new Dictionary<string, object>();
                    foreach (var pf in new[] { "Secure Preferences", "Preferences" })
                    {
                        var f = Path.Combine(pdir.FullName, pf);
                        if (!File.Exists(f)) continue;
                        var j = ReadJson(f);
                        if (TextFiles.Get(TextFiles.Get(j, "extensions"), "settings") is IDictionary<string, object> s)
                            foreach (var kv in s) settings[kv.Key] = kv.Value;
                    }
                    DirectoryInfo[] exts;
                    try { exts = new DirectoryInfo(extRoot).GetDirectories(); } catch { continue; }
                    foreach (var e in exts)
                    {
                        try
                        {
                            var ver = e.GetDirectories().OrderByDescending(x => x.Name, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault();
                            if (ver == null) continue;
                            var mf = Path.Combine(ver.FullName, "manifest.json");
                            if (!File.Exists(mf)) continue;
                            var m = ReadJson(mf);
                            if (m == null) continue;
                            settings.TryGetValue(e.Name, out var s);
                            var loc = Int(TextFiles.Get(s, "location"));
                            if (s != null && (loc == 5 || loc == 10)) continue;   // thành phần có sẵn của trình duyệt
                            string name = ResolveExtName(ver.FullName, Convert.ToString(TextFiles.Get(m, "name")));
                            bool on = !(s != null && (TextFiles.Truthy(TextFiles.Get(s, "disable_reasons")) || Int(TextFiles.Get(s, "state")) == 0));
                            var perms = Items(TextFiles.Get(m, "permissions")).Concat(Items(TextFiles.Get(m, "host_permissions"))).OfType<string>().ToList();
                            bool all = perms.Any(x => Rx.M(x, @"<all_urls>|^\*://\*/\*|^https?://\*/\*"));
                            bool Has(string x) => perms.Contains(x, StringComparer.OrdinalIgnoreCase);
                            var notes = new List<string>();
                            string level = "Thấp";
                            if (all) { level = "Trung bình"; notes.Add("đọc và sửa được mọi trang web"); }
                            if (Has("cookies") && all) { level = "Cao"; notes.Add("đọc được cookie đăng nhập"); }
                            if (Has("proxy")) { level = "Cao"; notes.Add("điều khiển proxy (thấy mọi lưu lượng)"); }
                            if (Has("management")) notes.Add("quản lý được extension khác");
                            if (Has("debugger")) notes.Add("dùng được quyền gỡ lỗi trang");
                            if (Rx.M(name, "wallet|metamask|phantom|petra|keplr|okx|trust")) { level = "Cao"; notes.Add("ví tiền mã hoá – gỡ nếu không còn dùng"); }
                            if (Rx.M(name, @"\bnode\b|validator|grass|gradient|dawn|nodepay|bandwidth")) { level = "Cao"; notes.Add("chia sẻ băng thông Internet để \"đào airdrop\""); }
                            if (Rx.M(name, "vpn|proxy")) notes.Add("VPN miễn phí từng bị phát hiện thu thập dữ liệu duyệt web");
                            if (s != null && loc == 3) notes.Add("được phần mềm khác cài kèm");
                            if (!on && level != "Thấp") { level = "Thấp"; notes.Add("đang tắt"); }
                            if (notes.Count == 0) notes.Add("quyền hạn hẹp");
                            res.Add(new ExtInfo
                            {
                                Browser = b[0], Profile = pdir.Name, Name = name, Enabled = on, Level = level,
                                Detail = (on ? "Đang bật" : "Đang tắt") + " · v" + Convert.ToString(TextFiles.Get(m, "version")) + " · " + string.Join("; ", notes) + " · ID " + e.Name,
                            });
                        }
                        catch { }
                    }
                }
            }
            return res;
        }

        static string ResolveExtName(string dir, string n)
        {
            var mt = System.Text.RegularExpressions.Regex.Match(n ?? "", "^__MSG_(.+)__$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!mt.Success) return n;
            string key = mt.Groups[1].Value;
            foreach (var loc in new[] { "vi", "en", "en_US" })
            {
                var f = Path.Combine(dir, "_locales", loc, "messages.json");
                if (!File.Exists(f)) continue;
                if (ReadJson(f) is IDictionary<string, object> j)
                    foreach (var kv in j)
                        if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                            return Convert.ToString(TextFiles.Get(kv.Value, "message"));
            }
            return n;
        }
    }
}
