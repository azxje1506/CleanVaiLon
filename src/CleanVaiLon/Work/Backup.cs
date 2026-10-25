// Trang Sao lưu: máy đã được sao lưu chưa; chép thư mục bằng robocopy.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Win32;

namespace CleanVaiLon
{
    public class BackupRow { public string Item, Status, Detail; }

    public class CopyResult { public bool Ok; public int Code; public string Dest, Summary; }

    public static class Backup
    {
        static string Run(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            using (var p = Process.Start(psi))
            {
                var err = p.StandardError.ReadToEndAsync();
                string o = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return o + err.Result;
            }
        }

        public static List<BackupRow> Status()
        {
            var rows = new List<BackupRow>();
            void Add(string item, string status, string detail) => rows.Add(new BackupRow { Item = item, Status = status, Detail = detail });

            string wb = "";
            try { wb = Run(Path.Combine(AppInfo.Env("WINDIR"), @"System32\wbadmin.exe"), "get versions"); } catch { }
            var times = Regex.Matches(wb, @"^Backup time:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline).Cast<Match>().Select(m => m.Groups[1].Value.Trim()).ToList();
            if (times.Count > 0) Add("Windows Backup (sao lưu toàn bộ máy)", "Có", "Lần cuối: " + times[times.Count - 1]);
            else Add("Windows Backup (sao lưu toàn bộ máy)", "Chưa có", "Chưa từng chạy.");

            FileInfo fhCfg = null;
            try
            {
                var dir = new DirectoryInfo(Path.Combine(AppInfo.Env("LOCALAPPDATA"), @"Microsoft\Windows\FileHistory\Configuration"));
                if (dir.Exists) fhCfg = dir.GetFiles().OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
            }
            catch { }
            if (fhCfg != null)
            {
                string target = "";
                try { var x = new XmlDocument(); x.Load(fhCfg.FullName); target = x.SelectSingleNode("//Target//TargetUrl")?.InnerText ?? ""; } catch { }
                Add("File History (tự sao lưu file)", "Đã bật", "Lưu vào: " + target + ". Cấu hình cập nhật: " + Fmt.Inv(fhCfg.LastWriteTime));
            }
            else Add("File History (tự sao lưu file)", "Chưa bật", "Bấm \"Mở File History\" để bật, chọn một ổ cắm ngoài.");

            var rp = new List<ManagementObject>();
            try
            {
                using (var q = new ManagementObjectSearcher(new ManagementScope(@"root\default"), new ObjectQuery("SELECT * FROM SystemRestore")))
                    foreach (ManagementObject o in q.Get()) rp.Add(o);
            }
            catch { }
            rp = rp.OrderByDescending(o => Convert.ToInt64(o["SequenceNumber"])).ToList();
            if (rp.Count > 0)
            {
                string last = "";
                try { last = Fmt.Inv(ManagementDateTimeConverter.ToDateTime(Convert.ToString(rp[0]["CreationTime"]))); } catch { }
                Add("Điểm khôi phục (System Restore)", rp.Count + " điểm", "Mới nhất: " + last + " – " + rp[0]["Description"] + ". Chỉ khôi phục được cài đặt Windows, không giữ file của bạn.");
            }
            else Add("Điểm khôi phục (System Restore)", "Không có", "");

            var od = new List<string[]>();
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts"))
                    if (k != null)
                        foreach (var n in k.GetSubKeyNames())
                            using (var a = k.OpenSubKey(n))
                            {
                                var mail = a?.GetValue("UserEmail") as string;
                                if (!string.IsNullOrEmpty(mail)) od.Add(new[] { mail, Convert.ToString(a.GetValue("UserFolder")) });
                            }
            }
            catch { }
            if (od.Count > 0)
            {
                bool running = Process.GetProcessesByName("OneDrive").Length > 0;
                foreach (var a in od) Add("OneDrive", "Đã đăng nhập", a[0] + " – thư mục " + a[1] + ". Đang chạy: " + (running ? "True" : "False"));
            }
            else Add("OneDrive", "Chưa dùng", "Chưa đăng nhập tài khoản nào.");

            const string known = "Google Drive|Dropbox|Macrium|Acronis|AOMEI|EaseUS Todo|Veeam|FreeFileSync|SyncBack|Seagate Toolkit|Backblaze|iDrive|pCloud|MEGA";
            var apps = Apps.UninstallEntries()
                .Select(d => d.TryGetValue("DisplayName", out var v) ? Convert.ToString(v) : "")
                .Where(n => Rx.M(n, known)).Distinct().ToList();
            if (apps.Count > 0) Add("Phần mềm sao lưu / đồng bộ khác", "Có", string.Join(", ", apps));
            else Add("Phần mềm sao lưu / đồng bộ khác", "Không có", "");

            try
            {
                using (var q = new ManagementObjectSearcher("SELECT Model, Size, MediaType FROM Win32_DiskDrive"))
                    foreach (ManagementObject d in q.Get())
                    {
                        if (!Rx.M(Convert.ToString(d["MediaType"]), "External|Removable")) continue;
                        double gb = Math.Round(Convert.ToDouble(d["Size"] ?? 0) / Fmt.GB);
                        Add("Ổ cắm ngoài", "Đang cắm", d["Model"] + " – " + gb + " GB. Có thể dùng làm nơi sao lưu.");
                    }
            }
            catch { }
            try
            {
                if (File.Exists(AppInfo.BackupLogFile))
                {
                    var lines = TextFiles.ReadLines(AppInfo.BackupLogFile);
                    Add("Bản chép thư mục bằng công cụ này", "Có", string.Join(" | ", lines.Skip(Math.Max(0, lines.Count - 3))));
                }
            }
            catch { }
            return rows;
        }

        // Đặt một tham số trong ngoặc kép theo quy tắc của CommandLineToArgvW (dấu \ cuối được nhân đôi).
        public static string Quote(string a)
        {
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0 && !a.EndsWith("\\")) return a;
            var sb = new StringBuilder("\"");
            int bs = 0;
            foreach (char c in a)
            {
                if (c == '\\') { bs++; continue; }
                if (c == '"') sb.Append('\\', bs * 2 + 1).Append('"');
                else sb.Append('\\', bs).Append(c);
                bs = 0;
            }
            sb.Append('\\', bs * 2).Append('"');
            return sb.ToString();
        }

        // Chép thư mục: chỉ thêm và cập nhật file, không xoá gì ở nơi lưu.
        public static CopyResult CopyFolder(string src, string dstRoot)
        {
            string leaf = Path.GetFileName(src.TrimEnd('\\'));
            if (src.TrimEnd('\\').Length <= 2) leaf = "";
            if (string.IsNullOrEmpty(leaf)) leaf = src.Substring(0, 1) + "_drive";
            string dst = Path.Combine(dstRoot, leaf);
            string logFile = Path.Combine(AppInfo.Env("TEMP"), "CleanVaiLon_robocopy.txt");
            int code;
            var psi = new ProcessStartInfo(Path.Combine(AppInfo.Env("WINDIR"), @"System32\robocopy.exe"),
                Quote(src) + " " + Quote(dst) + " /E /R:1 /W:1 /XJ /NP /NFL /NDL /LOG:" + Quote(logFile))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using (var p = Process.Start(psi)) { p.StandardOutput.ReadToEnd(); p.WaitForExit(); code = p.ExitCode; }
            bool ok = code < 8;
            AppInfo.EnsureDataDir();
            File.AppendAllText(AppInfo.BackupLogFile,
                string.Format("{0:yyyy-MM-dd HH:mm}  {1} -> {2}  ({3})", DateTime.Now, src, dst, ok ? "xong" : "lỗi mã " + code) + "\r\n", new UTF8Encoding(false));
            string summary = "";
            try
            {
                summary = string.Join("\r\n", File.ReadAllLines(logFile, Encoding.Default)
                    .Where(l => Regex.IsMatch(l, @"^\s*(Dirs|Files|Bytes)\s*:", RegexOptions.IgnoreCase)).Select(l => l.Trim()));
            }
            catch { }
            return new CopyResult { Ok = ok, Code = code, Dest = dst, Summary = summary };
        }
    }
}
