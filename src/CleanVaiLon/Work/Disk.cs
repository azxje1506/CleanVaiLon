// Trang Ổ đĩa: tìm thứ đang chiếm chỗ; ảnh trùng lặp.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CleanVaiLon
{
    public class DriveRow { public string Drive, Label; public double Size, Free; }

    public class DiskRow
    {
        public string Path, Note, Mode = "Item", Safety;
        public double Size;
        public DateTime? Modified;
    }

    public class DiskScan
    {
        public List<DiskRow> TopFiles, TopDirs, Installers, OldDownloads, Caches;
        public int FileCount;
        public List<DiskRow> View(string key)
        {
            switch (key)
            {
                case "TopFiles": return TopFiles;
                case "TopDirs": return TopDirs;
                case "Installers": return Installers;
                case "OldDownloads": return OldDownloads;
                case "Caches": return Caches;
            }
            return new List<DiskRow>();
        }
    }

    public class DupScan { public List<DiskRow> Rows; public int Scanned; }

    public static class Disk
    {
        public static List<DriveRow> Drives()
        {
            var list = new List<DriveRow>();
            try
            {
                using (var q = new ManagementObjectSearcher("SELECT DeviceID, VolumeName, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3 OR DriveType=2"))
                    foreach (ManagementObject d in q.Get())
                        list.Add(new DriveRow
                        {
                            Drive = Convert.ToString(d["DeviceID"]), Label = Convert.ToString(d["VolumeName"]),
                            Size = Convert.ToDouble(d["Size"] ?? 0), Free = Convert.ToDouble(d["FreeSpace"] ?? 0),
                        });
            }
            catch { }
            return list;
        }

        public static List<string> DownloadFolders()
        {
            var list = new List<string> { Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads" };
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\DownloadManager"))
                    if (k?.GetValue("LocalPathW") is byte[] v) list.Add(Encoding.Unicode.GetString(v).TrimEnd('\0'));
            }
            catch { }
            return list.Where(x => !string.IsNullOrEmpty(x) && Directory.Exists(x)).Distinct().ToList();
        }

        public static List<string> SteamLibraries()
        {
            var roots = new List<string>();
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    var steam = k?.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(steam))
                    {
                        steam = steam.Replace('/', '\\');
                        roots.Add(steam);
                        var vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                        if (File.Exists(vdf))
                            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
                                roots.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
                    }
                }
            }
            catch { }
            var res = new List<string>();
            foreach (var r in roots)
            {
                try { if (Directory.Exists(r)) res.Add(Path.GetFullPath(r).TrimEnd('\\')); } catch { }
            }
            return res.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        static string Parent(string p) { try { return Path.GetDirectoryName(p) ?? ""; } catch { return ""; } }
        static string Leaf(string p) { try { return Path.GetFileName(p) ?? ""; } catch { return ""; } }

        public static DiskScan Scan(IEnumerable<string> drives)
        {
            var files = new List<FileHit>();
            var dirs = new List<DirHit>();
            foreach (var d in drives)
            {
                var w = new Walker { MinFile = 1048576, MaxDepth = 3 };
                w.Walk(new DirectoryInfo(d + "\\"), 0);
                files.AddRange(w.Files); dirs.AddRange(w.Dirs);
            }
            var topFiles = files.OrderByDescending(f => f.Size).Take(20)
                .Select(f => new DiskRow { Path = f.Path, Size = f.Size, Modified = f.Modified, Note = "" }).ToList();

            // Thư mục lớn nhất: bỏ thư mục cha nếu gần hết dung lượng nằm trong một thư mục con.
            var childMax = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in dirs)
            {
                if (x.Depth < 2) continue;
                var parent = Parent(x.Path);
                if (!childMax.TryGetValue(parent, out var cm) || cm < x.Size) childMax[parent] = x.Size;
            }
            var topDirs = dirs.Where(x => x.Depth >= 1 && x.Depth <= 3)
                .Where(x => !childMax.TryGetValue(x.Path, out var cm) || cm < 0.9 * x.Size)
                .OrderByDescending(x => x.Size).Take(20)
                .Select(x => new DiskRow { Path = x.Path, Size = x.Size, Modified = null, Note = "" }).ToList();

            const string instExt = @"\.(exe|msi|iso|img|rar|zip|7z|bin|cab)$";
            const string instName = @"setup|install|repack|fitgirl|dodi|linkneverdie|\.part\d+\.rar$|fg-\d+\.bin$|\.iso$|offline";
            const string instDir = @"\\(Downloads|IDM Download)\\";
            const string exclude = @"\\(Windows|Program Files|Program Files \(x86\)|ProgramData|AppData)\\|\\steamapps\\|\\Riot Games\\|\\node_modules\\|\\\$Recycle";
            var installers = files.Where(f => f.Size >= 50 * Fmt.MB && Rx.M(f.Path, instExt) && !Rx.M(f.Path, exclude) &&
                    (Rx.M(Leaf(f.Path), instName) || Rx.M(f.Path, instDir) || Rx.M(Leaf(Parent(f.Path)), instName)))
                .OrderByDescending(f => f.Size)
                .Select(f => new DiskRow { Path = f.Path, Size = f.Size, Modified = f.Modified, Note = "Bộ cài / file nén. Kiểm tra phần mềm đã cài xong chưa." }).ToList();

            var cut = DateTime.Now.AddDays(-90);
            var old = new List<DiskRow>();
            foreach (var dl in DownloadFolders())
                foreach (var f in Recycle.AllFiles(dl))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        if (fi.LastWriteTime < cut && !string.Equals(fi.Name, "desktop.ini", StringComparison.OrdinalIgnoreCase))
                            old.Add(new DiskRow { Path = fi.FullName, Size = fi.Length, Modified = fi.LastWriteTime, Note = "Trong " + dl + ", hơn 90 ngày" });
                    }
                    catch { }
                }

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string E(string n) => AppInfo.Env(n);
            var caches = new List<string[]>
            {
                new[] { local + @"\NVIDIA\DXCache", "Bộ nhớ đệm đồ hoạ NVIDIA. Tự tạo lại, game có thể hơi giật vài lần đầu." },
                new[] { E("USERPROFILE") + @"\AppData\LocalLow\NVIDIA\DXCache", "Bộ nhớ đệm đồ hoạ NVIDIA." },
                new[] { local + @"\D3DSCache", "Bộ nhớ đệm DirectX. Tự tạo lại." },
                new[] { local + @"\pip\cache", "Bộ nhớ đệm của Python pip. Tự tải lại khi cần." },
                new[] { local + @"\npm-cache", "Bộ nhớ đệm của npm. Tự tải lại khi cần." },
                new[] { local + @"\CrashDumps", "File ghi lỗi khi ứng dụng bị treo." },
                new[] { E("TEMP"), "File tạm của tài khoản. File đang dùng sẽ được bỏ qua." },
                new[] { E("WINDIR") + @"\Temp", "File tạm của Windows. File đang dùng sẽ được bỏ qua." },
            };
            foreach (var lib in SteamLibraries()) caches.Add(new[] { lib + @"\steamapps\workshop\downloads", "Phần tải dở / thừa của Steam Workshop. Chỉ xoá khi Steam đang tắt." });
            var cacheRows = new List<DiskRow>();
            foreach (var c in caches)
            {
                if (string.IsNullOrEmpty(c[0]) || (!Directory.Exists(c[0]) && !File.Exists(c[0]))) continue;
                long s = Recycle.PathSize(c[0]);
                if (s < 50 * Fmt.MB) continue;
                cacheRows.Add(new DiskRow { Path = c[0], Size = s, Note = c[1], Mode = "Contents" });
            }
            foreach (var d in drives)
            {
                string rb = d + "\\$Recycle.Bin";
                long s = Recycle.PathSize(rb);
                if (s > 100 * Fmt.MB) cacheRows.Add(new DiskRow { Path = rb, Size = s, Note = "Thùng rác. Bạn tự dọn: chuột phải Thùng rác > Empty Recycle Bin.", Mode = "None" });
            }
            return new DiskScan
            {
                TopFiles = topFiles, TopDirs = topDirs, Installers = installers,
                OldDownloads = old.OrderByDescending(x => x.Size).ToList(), Caches = cacheRows.OrderByDescending(x => x.Size).ToList(),
                FileCount = files.Count,
            };
        }

        public static DupScan FindDuplicateImages(IEnumerable<string> drives)
        {
            var w = new Walker { MinFile = 50000, MaxDepth = -1, Ext = new HashSet<string>() };
            foreach (var x in new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic", ".tif", ".tiff", ".psd", ".cr2", ".nef", ".arw", ".raw" }) w.Ext.Add(x);
            w.Skip = new[] { @"\windows", @"\program files", @"\program files (x86)", @"\programdata", @"\appdata", @"\$recycle.bin", @"\steamapps", @"\steamlibrary", @"\node_modules", @"\.git", @"\system volume information", @"\riot games", @"\hoyoplay" };
            foreach (var d in drives) w.Walk(new DirectoryInfo(d + "\\"), 0);
            var rows = new List<DiskRow>();
            using (var md5 = MD5.Create())
            {
                foreach (var g in w.Files.GroupBy(f => f.Size).Where(g => g.Count() > 1))
                {
                    var byHash = g.GroupBy(f =>
                    {
                        try { using (var s = File.OpenRead(f.Path)) return BitConverter.ToString(md5.ComputeHash(s)); }
                        catch { return f.Path; }
                    }, StringComparer.OrdinalIgnoreCase).Where(h => h.Count() > 1);
                    foreach (var h in byHash)
                    {
                        var sorted = h.OrderBy(x => x.Modified).ToList();
                        var keep = sorted[0];
                        foreach (var dup in sorted.Skip(1))
                        {
                            string note = "Giống hệt: " + keep.Path;
                            if (!string.Equals(dup.Path.Substring(0, 2), keep.Path.Substring(0, 2), StringComparison.OrdinalIgnoreCase))
                                note = "Ở ổ khác – có thể là bản sao lưu. Giống hệt: " + keep.Path;
                            rows.Add(new DiskRow { Path = dup.Path, Size = dup.Size, Modified = dup.Modified, Note = note });
                        }
                    }
                }
            }
            return new DupScan { Rows = rows.OrderByDescending(x => x.Size).ToList(), Scanned = w.Files.Count };
        }
    }
}
