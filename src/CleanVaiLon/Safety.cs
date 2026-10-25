// Đánh giá mức an toàn khi xoá và chuyển vào Thùng rác. Không bao giờ xoá vĩnh viễn.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;

namespace CleanVaiLon
{
    public class SafetyRule
    {
        public readonly string R, L, T;
        public readonly Regex Rx;
        public SafetyRule(string r, string l, string t) { R = r; L = l; T = t; Rx = new Regex(r, RegexOptions.IgnoreCase); }
    }

    public class SafetyResult
    {
        public string Level, Label, Why;
    }

    public static class Safety
    {
        // Mức: safe = an toàn, review = cân nhắc, keep = không nên xoá, system = cấm xoá.
        public static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            ["safe"] = "An toàn", ["review"] = "Cân nhắc", ["keep"] = "Không nên xoá", ["system"] = "Cấm xoá",
        };

        // Mỗi luật: R = mẫu đường dẫn (đã thay các thư mục quen thuộc bằng %win%, %user%…), L = mức, T = lý do.
        // Luật đứng trước được ưu tiên. Giữ đúng thứ tự và nội dung của $SafetyRules trong bản PowerShell.
        public static readonly SafetyRule[] Rules =
        {
            // Hệ thống – tuyệt đối không xoá
            new SafetyRule(@"^[a-z]:\\?$", "system", "Gốc của cả ổ đĩa."),
            new SafetyRule(@"^[a-z]:\\(pagefile|hiberfil|swapfile)\.sys$", "system", "File bộ nhớ ảo / ngủ đông của Windows. Muốn thu nhỏ thì chỉnh trong cài đặt Windows, không xoá tay."),
            new SafetyRule(@"^[a-z]:\\(system volume information|recovery|boot|efi|\$windows\.~bt|\$windows\.~ws|\$winreagent|\$sysreset)(\\|$)", "system", "Thư mục hệ thống ẩn, dùng để khởi động và khôi phục Windows."),
            new SafetyRule(@"^[a-z]:\\(bootmgr|bootnxt|dumpstack\.log(\.tmp)?)$", "system", "File khởi động của Windows."),
            new SafetyRule(@"^%user%$|^%user%\\appdata$|^%local%$|^%roaming%$|^[a-z]:\\users$", "system", "Thư mục gốc của tài khoản người dùng."),
            // Bên trong Windows: vài chỗ dọn được, còn lại không đụng vào
            new SafetyRule(@"^%win%\\temp(\\|$)", "safe", "File tạm của Windows. File đang dùng sẽ tự được bỏ qua."),
            new SafetyRule(@"^%win%\\softwaredistribution\\download(\\|$)", "safe", "Bản tải về của Windows Update đã cài xong. Windows tự tải lại khi cần."),
            new SafetyRule(@"^%win%\\(logs|minidump|livekernelreports)(\\|$)|^%win%\\memory\.dmp$", "safe", "Nhật ký và file ghi lỗi của Windows."),
            new SafetyRule(@"^%win%\\installer(\\|$)", "keep", "Windows cần các file này để gỡ và cập nhật phần mềm. Xoá nhầm thì phần mềm không gỡ / cập nhật được nữa."),
            new SafetyRule(@"^%win%(\\|$)", "system", "Thư mục của Windows. Xoá có thể làm máy không khởi động được."),
            // Bộ nhớ đệm và file tạm – tự tạo lại
            new SafetyRule(@"^%temp%(\\|$)", "safe", "File tạm của tài khoản. File đang dùng sẽ tự được bỏ qua."),
            new SafetyRule(@"\\(dxcache|glcache|d3dscache|shadercache|gpucache|grshadercache|code cache|cache_data|cache2|inetcache|webcache)(\\|$)", "safe", "Bộ nhớ đệm, chương trình sẽ tự tạo lại (lần đầu mở có thể hơi chậm)."),
            new SafetyRule(@"^(%local%|%roaming%|%locallow%)\\.*\\cache(\\|$)", "safe", "Bộ nhớ đệm của ứng dụng / trình duyệt, tự tạo lại."),
            new SafetyRule(@"^%local%\\crashdumps(\\|$)|\\crashpad\\(reports|completed)(\\|$)", "safe", "File ghi lỗi khi ứng dụng bị treo."),
            new SafetyRule(@"\\(pip\\cache|npm-cache|yarn\\cache|\.gradle\\caches|nuget\\v3-cache|go-build|\.cache\\huggingface)(\\|$)", "safe", "Bộ nhớ đệm của công cụ lập trình, tự tải lại khi cần."),
            new SafetyRule(@"\\steamapps\\(workshop\\downloads|downloading|temp)(\\|$)", "safe", "Phần tải dở / thừa của Steam. Chỉ xoá khi Steam đang tắt."),
            new SafetyRule(@"\\\$recycle\.bin(\\|$)", "review", "Thùng rác. Dọn bằng chuột phải Thùng rác > Empty Recycle Bin."),
            // Dữ liệu không nên xoá tay
            new SafetyRule(@"\\\.git(\\|$)", "keep", "Lịch sử mã nguồn của dự án. Xoá là mất toàn bộ lịch sử."),
            new SafetyRule(@"\\(saves?|savegames?|saved games|my games|savedata)(\\|$)", "keep", "Dữ liệu lưu game. Xoá là mất tiến trình chơi."),
            new SafetyRule(@"\\steamapps\\common\\[^\\]+", "keep", "Game đã cài qua Steam. Gỡ trong Steam (hoặc tab ứng dụng) để không hỏng thư viện game."),
            new SafetyRule(@"\\steamapps\\workshop\\content(\\|$)", "review", "Nội dung Workshop đang dùng (hình nền, mod). Huỷ đăng ký trong Steam thay vì xoá tay."),
            new SafetyRule(@"\\(riot games|epic games|ubisoft game launcher|battle\.net|ea games|ea desktop|gog galaxy|hoyoplay|garena)(\\|$)", "keep", "Game hoặc trình mở game. Gỡ bằng chính launcher đó."),
            new SafetyRule(@"\\node_modules(\\|$)", "review", "Thư viện của dự án lập trình. Xoá được, cài lại bằng npm install."),
            new SafetyRule(@"(fitgirl|dodi|repack|linkneverdie)", "review", "Bộ cài game / phần mềm dạng repack. Xoá được nếu đã cài xong."),
            new SafetyRule(@"^%pf%(\\|$)|^%pf86%(\\|$)", "keep", "Phần mềm đã cài. Gỡ trong tab ứng dụng để không để lại lỗi."),
            new SafetyRule(@"^%pd%(\\|$)", "keep", "Dữ liệu chung của phần mềm đã cài."),
            new SafetyRule(@"^%user%\\(documents|desktop|pictures|videos|music|onedrive[^\\]*)(\\|$)", "keep", "Dữ liệu cá nhân. Chỉ xoá khi chắc chắn không cần."),
            new SafetyRule(@"^%roaming%(\\|$)|^%local%(\\|$)|^%locallow%(\\|$)", "keep", "Dữ liệu và cài đặt của ứng dụng. Chỉ xoá khi đã gỡ ứng dụng đó."),
            // Theo loại tệp
            new SafetyRule(@"\.(iso|img|rar|zip|7z|cab|msi|dmg)$|\\[^\\]*(setup|install|\.part\d+)[^\\]*$", "review", "Bộ cài / file nén. Xoá được nếu đã cài hoặc giải nén xong."),
            new SafetyRule(@"^%user%\\downloads(\\|$)", "review", "File đã tải về. Xem lại còn cần không trước khi xoá."),
            new SafetyRule(@"\.(tmp|temp|bak|old|log|etl|dmp|chk|crdownload|part)$", "safe", "File tạm, nhật ký hoặc bản tải dở."),
            new SafetyRule(@"\.(exe|dll|sys|ocx|drv|msp)$", "keep", "File chương trình. Xoá có thể làm phần mềm hỏng – hãy gỡ phần mềm đó."),
            new SafetyRule(@"\.(psd|ai|fig|xd|sketch|blend|aep|prproj|c4d|docx?|xlsx?|pptx?|pdf|jpe?g|png|heic|raw|cr2|nef|arw|mp4|mov|mkv)$", "keep", "Có thể là dữ liệu cá nhân hoặc dự án. Chỉ xoá khi chắc chắn."),
        };

        public const string UnknownWhy = "Không thuộc nhóm nào em biết. Xem kỹ nội dung trước khi xoá.";

        static string[][] PathMap()
        {
            string E(string n) => AppInfo.Env(n);
            return new[]
            {
                new[] { "%temp%", E("TEMP") }, new[] { "%locallow%", E("USERPROFILE") + @"\AppData\LocalLow" }, new[] { "%local%", E("LOCALAPPDATA") },
                new[] { "%roaming%", E("APPDATA") }, new[] { "%user%", E("USERPROFILE") }, new[] { "%win%", E("WINDIR") },
                new[] { "%pf86%", E("ProgramFiles(x86)") }, new[] { "%pf%", E("ProgramFiles") }, new[] { "%pd%", E("ProgramData") },
            };
        }

        public static string ToSafetyPath(string path)
        {
            string p = (path ?? "").ToLowerInvariant().TrimEnd('\\');
            if (Regex.IsMatch(p, "^[a-z]:$")) p += "\\";
            foreach (var kv in PathMap())
            {
                string v = kv[1].ToLowerInvariant().TrimEnd('\\');
                if (v.Length > 0 && (p == v || p.StartsWith(v + "\\", StringComparison.Ordinal))) return kv[0] + p.Substring(v.Length);
            }
            return p;
        }

        public static SafetyResult Get(string path)
        {
            string p = ToSafetyPath(path);
            foreach (var r in Rules)
                if (r.Rx.IsMatch(p)) return new SafetyResult { Level = r.L, Label = Labels[r.L], Why = r.T };
            return new SafetyResult { Level = "review", Label = Labels["review"], Why = UnknownWhy };
        }

        // Kiểm tra thêm cho một tệp: có đang được mở không, lâu rồi chưa sửa không.
        public static List<string> FileFacts(string path)
        {
            var facts = new List<string>();
            if (!File.Exists(path)) return facts;
            try { using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None)) { } }
            catch { facts.Add("đang được chương trình khác mở – đóng chương trình đó trước"); }
            try
            {
                var fi = new FileInfo(path);
                double days = (DateTime.Now - fi.LastWriteTime).TotalDays;
                if (days > 365) facts.Add(string.Format("không sửa từ {0:dd/MM/yyyy} ({1:N0} năm)", fi.LastWriteTime, days / 365));
                if ((fi.Attributes & FileAttributes.System) != 0) facts.Add("có thuộc tính \"hệ thống\"");
            }
            catch { }
            return facts;
        }

        // Dòng giải thích "xoá được không" cho khung chi tiết.
        public static string FormatText(string path, bool isDir)
        {
            var s = Get(path);
            var lines = new List<string> { string.Format("{0,-14} {1} – {2}", "xoá được?:", s.Label.ToUpper(), s.Why) };
            if (!isDir) foreach (var x in FileFacts(path)) lines.Add(string.Format("{0,-14} {1}", "lưu ý:", x));
            return string.Join("\r\n", lines);
        }
    }

    public class RecycleResult
    {
        public string Path, Result, Detail;
        public double Freed;
    }

    public static class Recycle
    {
        public static long PathSize(string path)
        {
            try
            {
                if (File.Exists(path)) return new FileInfo(path).Length;
                if (!Directory.Exists(path)) return 0;
                var w = new Walker { MinFile = long.MaxValue, MaxDepth = -1 };
                return w.Walk(new DirectoryInfo(path), 0);
            }
            catch { return 0; }
        }

        // Chỗ trống còn lại của Thùng rác trên ổ chứa path (byte).
        public static double Room(string path)
        {
            try
            {
                string root = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(root)) return 0;
                ManagementObject vol = null;
                using (var q = new ManagementObjectSearcher("SELECT DeviceID, DriveLetter, Capacity FROM Win32_Volume"))
                    foreach (ManagementObject v in q.Get())
                    {
                        var dl = v["DriveLetter"] as string;
                        if (!string.IsNullOrEmpty(dl) && string.Equals(dl + "\\", root, StringComparison.OrdinalIgnoreCase)) { vol = v; break; }
                    }
                if (vol == null) return 0;
                string guid = Regex.Match(Convert.ToString(vol["DeviceID"]), @"\{[^}]+\}").Value;
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\BitBucket\Volume\" + guid))
                {
                    if (k != null && Convert.ToInt64(k.GetValue("NukeOnDelete", 0)) == 1) return 0;
                    double cap = Convert.ToDouble(vol["Capacity"] ?? 0) * 0.05;
                    var mc = k?.GetValue("MaxCapacity");
                    if (mc != null && Convert.ToInt64(mc) != 0) cap = Convert.ToDouble(mc) * Fmt.MB;
                    double used = PathSize(Path.Combine(root, "$Recycle.Bin"));
                    return cap - used;
                }
            }
            catch { return 0; }
        }

        // Chuyển vào Thùng rác. Mode: Item = cả mục, Contents = từng tệp bên trong, None = để bạn tự xử lý.
        // Không bao giờ xoá vĩnh viễn: mục quá lớn so với Thùng rác bị bỏ qua; mục "cấm xoá" luôn bị chặn.
        public static RecycleResult Move(string path, string mode = "Item")
        {
            if (mode == "Item" && Safety.Get(path).Level == "system")
                return new RecycleResult { Path = path, Result = "Bị chặn", Detail = "Mục hệ thống, công cụ không cho xoá.", Freed = 0 };
            if (mode == "None") return new RecycleResult { Path = path, Result = "Bỏ qua", Detail = "Mục này bạn cần tự xử lý.", Freed = 0 };
            if (!File.Exists(path) && !Directory.Exists(path)) return new RecycleResult { Path = path, Result = "Không còn", Detail = "Đã không còn tồn tại.", Freed = 0 };
            long size = PathSize(path);
            if (size > Room(path))
                return new RecycleResult { Path = path, Result = "Quá lớn", Detail = "(" + Fmt.Size(size) + ") lớn hơn chỗ trống của Thùng rác. Bạn tự xoá bằng Shift + Delete nếu chắc chắn.", Freed = 0 };
            const UIOption ui = UIOption.OnlyErrorDialogs;
            const RecycleOption rb = RecycleOption.SendToRecycleBin;
            const UICancelOption ce = UICancelOption.ThrowException;
            if (mode == "Contents")
            {
                int ok = 0, skip = 0; double freed = 0;
                foreach (var f in AllFiles(path))
                {
                    try { long len = new FileInfo(f).Length; FileSystem.DeleteFile(f, ui, rb, ce); ok++; freed += len; } catch { skip++; }
                }
                return new RecycleResult { Path = path, Result = "Đã chuyển", Detail = ok + " file vào Thùng rác, " + skip + " file đang dùng nên bỏ qua.", Freed = freed };
            }
            try
            {
                if (Directory.Exists(path)) FileSystem.DeleteDirectory(path, ui, rb, ce); else FileSystem.DeleteFile(path, ui, rb, ce);
                return new RecycleResult { Path = path, Result = "Đã chuyển", Detail = "Đã vào Thùng rác.", Freed = size };
            }
            catch (Exception ex)
            {
                return new RecycleResult { Path = path, Result = "Lỗi", Detail = ex.Message, Freed = 0 };
            }
        }

        // Mọi tệp bên trong (kể cả tệp ẩn), không đi qua junction / symlink.
        public static IEnumerable<string> AllFiles(string dir)
        {
            var stack = new Stack<string>();
            stack.Push(dir);
            while (stack.Count > 0)
            {
                var d = stack.Pop();
                FileSystemInfo[] items;
                try { items = new DirectoryInfo(d).GetFileSystemInfos(); } catch { continue; }
                foreach (var it in items)
                {
                    if ((it.Attributes & FileAttributes.Directory) != 0)
                    {
                        if ((it.Attributes & FileAttributes.ReparsePoint) == 0) stack.Push(it.FullName);
                    }
                    else yield return it.FullName;
                }
            }
        }
    }
}
