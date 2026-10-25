// Trang Hiệu năng: chương trình nào đang dùng CPU và RAM.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CleanVaiLon
{
    public class PerfRow
    {
        public string Name, Explain, Path, Company;
        public int Count;
        public double CpuPct, MemMB, MemPct;
    }

    public class PerfSnapshot
    {
        public List<PerfRow> Rows;
        public double TotalMB, FreeMB, CommitGB, LimitGB, CpuLoad;
    }

    public static class Perf
    {
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(int access, bool inherit, int pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder name, ref int size);

        // Đường dẫn exe của tiến trình (quyền truy vấn hạn chế, đọc được cả tiến trình 32-bit).
        public static string ImagePath(int pid)
        {
            IntPtr h = OpenProcess(0x1000, false, pid);
            if (h == IntPtr.Zero) return null;
            try
            {
                var sb = new StringBuilder(1024); int size = sb.Capacity;
                return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString(0, size) : null;
            }
            finally { CloseHandle(h); }
        }

        static double? Cpu(Process p) { try { return p.TotalProcessorTime.TotalSeconds; } catch { return null; } }

        class Raw { public string Name, Path, Desc, Company; public double Cpu, Mem; }

        public static PerfSnapshot Snapshot()
        {
            double totalKB = 0, freeKB = 0;
            using (var q = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"))
                foreach (ManagementObject o in q.Get()) { totalKB = Convert.ToDouble(o["TotalVisibleMemorySize"]); freeKB = Convert.ToDouble(o["FreePhysicalMemory"]); }
            double totalMB = totalKB / 1024;
            int cores = Environment.ProcessorCount;
            var before = new Dictionary<int, double>();
            foreach (var p in Process.GetProcesses()) { var c = Cpu(p); if (c.HasValue) before[p.Id] = c.Value; p.Dispose(); }
            Thread.Sleep(3000);
            var rows = new List<Raw>();
            var infoCache = new Dictionary<string, FileVersionInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses())
            {
                double cpu = 0;
                var now = Cpu(p);
                if (before.TryGetValue(p.Id, out var b0) && now.HasValue) cpu = (now.Value - b0) / 3 / cores * 100;
                long mem = 0; try { mem = p.WorkingSet64; } catch { }
                string path = p.Id > 4 ? ImagePath(p.Id) : null;
                string desc = null, comp = null;
                if (path != null)
                {
                    if (!infoCache.TryGetValue(path, out var vi)) { try { vi = FileVersionInfo.GetVersionInfo(path); } catch { vi = null; } infoCache[path] = vi; }
                    if (vi != null) { desc = vi.FileDescription; comp = vi.CompanyName; }
                }
                rows.Add(new Raw { Name = p.ProcessName, Cpu = cpu, Mem = mem, Path = path, Desc = desc, Company = comp });
                p.Dispose();
            }
            var groups = new List<PerfRow>();
            foreach (var g in rows.GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            {
                double mem = g.Sum(x => x.Mem), cpu = g.Sum(x => x.Cpu);
                var first = g.FirstOrDefault(x => !string.IsNullOrEmpty(x.Path)) ?? g.First();
                string explain;
                if (Knowledge.Process.TryGetValue(g.Key.ToLower(), out var kb)) explain = kb;
                else if (!string.IsNullOrEmpty(first.Desc) || !string.IsNullOrEmpty(first.Company)) explain = (first.Desc + " (" + first.Company + ")").Trim();
                else explain = "Chưa rõ – xem đường dẫn bên dưới.";
                groups.Add(new PerfRow
                {
                    Name = g.Key, Count = g.Count(), CpuPct = Fmt.Round(cpu, 1),
                    MemMB = Fmt.Round(mem / Fmt.MB), MemPct = Fmt.Round(mem / Fmt.MB / totalMB * 100, 1),
                    Explain = explain, Path = first.Path, Company = first.Company,
                });
            }
            double committed = 0, limit = 0, load = 0; int n = 0;
            using (var q = new ManagementObjectSearcher("SELECT CommittedBytes, CommitLimit FROM Win32_PerfFormattedData_PerfOS_Memory"))
                foreach (ManagementObject o in q.Get()) { committed = Convert.ToDouble(o["CommittedBytes"]); limit = Convert.ToDouble(o["CommitLimit"]); }
            using (var q = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor"))
                foreach (ManagementObject o in q.Get()) { if (o["LoadPercentage"] != null) { load += Convert.ToDouble(o["LoadPercentage"]); n++; } }
            return new PerfSnapshot
            {
                Rows = groups.OrderByDescending(x => x.MemMB).ToList(),
                TotalMB = Fmt.Round(totalMB), FreeMB = Fmt.Round(freeKB / 1024),
                CommitGB = Fmt.Round(committed / Fmt.GB, 1), LimitGB = Fmt.Round(limit / Fmt.GB, 1),
                CpuLoad = n > 0 ? load / n : 0,
            };
        }
    }
}
