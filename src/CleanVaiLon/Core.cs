// Phần dùng chung: định dạng, thư mục dữ liệu, nhật ký, cài đặt (caidat.json).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CleanVaiLon
{
    public static class Fmt
    {
        public const double KB = 1024, MB = 1048576, GB = 1073741824;

        public static string Size(double b)
        {
            if (b >= GB) return string.Format("{0:N1} GB", b / GB);
            if (b >= MB) return string.Format("{0:N0} MB", b / MB);
            return string.Format("{0:N0} KB", b / KB);
        }

        // Ngày giờ khi chèn vào chuỗi trong PowerShell ("$date") dùng định dạng bất biến.
        public static string Inv(DateTime d) => d.ToString(CultureInfo.InvariantCulture);

        public static string Date(DateTime? d) => d.HasValue ? d.Value.ToString("dd/MM/yyyy") : "";

        // [math]::Round của PowerShell làm tròn kiểu ngân hàng, giống Math.Round.
        public static double Round(double v, int digits = 0) => Math.Round(v, digits);
    }

    public static class AppInfo
    {
        public static readonly string Version = typeof(AppInfo).Assembly.GetName().Version.ToString(3);
        public static readonly string ExePath = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "";
        public static readonly string DataDir = Path.Combine(Env("LOCALAPPDATA"), "CleanVaiLon");
        public static readonly string LogFile = Path.Combine(DataDir, "nhatky.txt");
        public static readonly string SettingsFile = Path.Combine(DataDir, "caidat.json");
        public static readonly string ToolServicesFile = Path.Combine(DataDir, "dichvu_da_tat.txt");
        public static readonly string BackupLogFile = Path.Combine(DataDir, "saoluu.txt");

        public static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? "";

        public static void EnsureDataDir() { try { Directory.CreateDirectory(DataDir); } catch { } }
    }

    // Nhật ký thay đổi: "yyyy-MM-dd HH:mm:ss  nội dung", UTF-8 (có BOM khi tạo tệp), như Add-Content -Encoding UTF8.
    public static class Log
    {
        static readonly object Gate = new object();

        public static void Write(string msg)
        {
            lock (Gate)
            {
                AppInfo.EnsureDataDir();
                File.AppendAllText(AppInfo.LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + "\r\n", new UTF8Encoding(true));
            }
        }
    }

    // Đọc từng dòng: UTF-8 nếu hợp lệ, không thì theo bảng mã ANSI (tệp do Add-Content của PowerShell 5.1 ghi).
    public static class TextFiles
    {
        static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static List<string> ReadLines(string path)
        {
            var lines = new List<string>();
            byte[] all = File.ReadAllBytes(path);
            int start = (all.Length >= 3 && all[0] == 0xEF && all[1] == 0xBB && all[2] == 0xBF) ? 3 : 0;
            for (int i = start; i <= all.Length; i++)
            {
                if (i < all.Length && all[i] != (byte)'\n') continue;
                int end = i;
                if (end > start && all[end - 1] == (byte)'\r') end--;
                if (i == all.Length && end == start) break;
                var seg = new byte[end - start];
                Array.Copy(all, start, seg, 0, seg.Length);
                string s;
                try { s = StrictUtf8.GetString(seg); } catch (DecoderFallbackException) { s = Encoding.Default.GetString(seg); }
                lines.Add(s);
                start = i + 1;
            }
            return lines;
        }

        public static object ParseJson(string text)
        {
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 512 };
            return js.DeserializeObject(text);
        }

        // Lấy giá trị theo khoá không phân biệt hoa thường, như $obj.prop trong PowerShell.
        public static object Get(object obj, string key)
        {
            if (!(obj is IDictionary<string, object> d)) return null;
            if (d.TryGetValue(key, out var v)) return v;
            foreach (var kv in d) if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return null;
        }

        // Giá trị "đúng" theo kiểu PowerShell: null, 0, "" và mảng rỗng là sai.
        public static bool Truthy(object v)
        {
            switch (v)
            {
                case null: return false;
                case bool b: return b;
                case string s: return s.Length > 0;
                case int i: return i != 0;
                case long l: return l != 0;
                case decimal m: return m != 0;
                case double x: return x != 0;
                case System.Collections.ICollection c: return c.Count > 0;
                default: return true;
            }
        }
    }

    // Cài đặt, cùng tên khoá và định dạng với bản PowerShell (ConvertTo-Json của PowerShell 5.1).
    public class Settings
    {
        public string Lang = "vi";
        public string StartPage = "perf";
        public bool AutoLoad = true;
        public int HeavyPct = 5;
        public int Months = 6;
        public bool DoubleConfirm = true;
        public bool Grid = true;

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(AppInfo.SettingsFile)) return s;
                var j = TextFiles.ParseJson(File.ReadAllText(AppInfo.SettingsFile, Encoding.UTF8));
                object v;
                if ((v = TextFiles.Get(j, "Lang")) != null) s.Lang = Convert.ToString(v, CultureInfo.InvariantCulture);
                if ((v = TextFiles.Get(j, "StartPage")) != null) s.StartPage = Convert.ToString(v, CultureInfo.InvariantCulture);
                if ((v = TextFiles.Get(j, "AutoLoad")) != null) s.AutoLoad = TextFiles.Truthy(v);
                if ((v = TextFiles.Get(j, "HeavyPct")) != null) s.HeavyPct = ToInt(v, s.HeavyPct);
                if ((v = TextFiles.Get(j, "Months")) != null) s.Months = ToInt(v, s.Months);
                if ((v = TextFiles.Get(j, "DoubleConfirm")) != null) s.DoubleConfirm = TextFiles.Truthy(v);
                if ((v = TextFiles.Get(j, "Grid")) != null) s.Grid = TextFiles.Truthy(v);
            }
            catch { }
            return s;
        }

        static int ToInt(object v, int def)
        {
            try { return (int)Math.Round(Convert.ToDouble(v, CultureInfo.InvariantCulture)); } catch { return def; }
        }

        public void ResetToDefaults()
        {
            var d = new Settings();
            Lang = d.Lang; StartPage = d.StartPage; AutoLoad = d.AutoLoad; HeavyPct = d.HeavyPct;
            Months = d.Months; DoubleConfirm = d.DoubleConfirm; Grid = d.Grid;
        }

        public string ToJson()
        {
            string Q(string x) => "\"" + (x ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            string B(bool x) => x ? "true" : "false";
            var lines = new[]
            {
                "    \"Lang\":  " + Q(Lang),
                "    \"StartPage\":  " + Q(StartPage),
                "    \"AutoLoad\":  " + B(AutoLoad),
                "    \"HeavyPct\":  " + HeavyPct.ToString(CultureInfo.InvariantCulture),
                "    \"Months\":  " + Months.ToString(CultureInfo.InvariantCulture),
                "    \"DoubleConfirm\":  " + B(DoubleConfirm),
                "    \"Grid\":  " + B(Grid),
            };
            return "{\r\n" + string.Join(",\r\n", lines) + "\r\n}";
        }

        public void Save()
        {
            try
            {
                AppInfo.EnsureDataDir();
                File.WriteAllText(AppInfo.SettingsFile, ToJson() + "\r\n", new UTF8Encoding(true));
            }
            catch { }
        }
    }

    public static class ToolServices
    {
        public static List<string> Load()
        {
            try { return File.Exists(AppInfo.ToolServicesFile) ? TextFiles.ReadLines(AppInfo.ToolServicesFile).Where(x => x.Length > 0).ToList() : new List<string>(); }
            catch { return new List<string>(); }
        }

        public static void Save(IEnumerable<string> names)
        {
            AppInfo.EnsureDataDir();
            var sb = new StringBuilder();
            foreach (var n in names) sb.Append(n).Append("\r\n");
            File.WriteAllText(AppInfo.ToolServicesFile, sb.ToString(), new UTF8Encoding(true));
        }
    }

    public static class Rx
    {
        // -match của PowerShell không phân biệt hoa thường.
        public static bool M(string input, string pattern) => input != null && Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
    }
}
