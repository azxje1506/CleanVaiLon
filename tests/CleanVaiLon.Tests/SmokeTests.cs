// Chạy thử từng phần xử lý trên máy thật (giống legacy\tests\Test-Functions.ps1). Chỉ đọc, không thay đổi gì.
// Chậm và phụ thuộc máy nên CI bỏ qua:  dotnet test --filter Category!=Smoke
using System.Linq;
using CleanVaiLon;
using Xunit;
using Xunit.Abstractions;

namespace CleanVaiLon.Tests
{
    [Trait("Category", "Smoke")]
    public class SmokeTests
    {
        readonly ITestOutputHelper Out;
        public SmokeTests(ITestOutputHelper o) { Out = o; }
        static string Sys => System.Environment.GetEnvironmentVariable("SystemDrive");

        [Fact] public void PerfSnapshot() { var p = Perf.Snapshot(); Assert.NotEmpty(p.Rows); Out.WriteLine($"rows={p.Rows.Count} commit={p.CommitGB}/{p.LimitGB} GB"); }
        [Fact] public void StartupItems() { var s = Startup.Items(new string[0]); Out.WriteLine("items=" + s.Count); }
        [Fact] public void Drives() { var d = Disk.Drives(); Assert.NotEmpty(d); Out.WriteLine(string.Join(" ", d.Select(x => x.Drive + "=" + Fmt.Size(x.Free)))); }
        [Fact] public void DownloadFolders() => Out.WriteLine(Disk.DownloadFolders().Count.ToString());
        [Fact] public void SteamLibraries() => Out.WriteLine(Disk.SteamLibraries().Count.ToString());
        [Fact] public void RecycleRoom() => Out.WriteLine(Fmt.Size(Recycle.Room(Sys + "\\Users")));
        [Fact] public void BackupStatus() { var b = Backup.Status(); Assert.NotEmpty(b); Out.WriteLine(string.Join("\n", b.Select(x => x.Item + " | " + x.Status + " | " + x.Detail))); }
        [Fact] public void BrowserExtensions() { var e = Security.BrowserExtensions(); Out.WriteLine($"n={e.Count} high={e.Count(x => x.Level == "Cao")}"); }
        [Fact] public void SecurityReport() { var r = Security.Report(); Assert.NotEmpty(r); Out.WriteLine(string.Join("\n", r.Select(x => x.Level + " | " + x.Group + " | " + x.Item))); }
        [Fact] public void AppUsage() { var a = Apps.Usage(); Out.WriteLine("apps=" + a.Count + " withDate=" + a.Count(x => x.LastUsed.HasValue)); }
        [Fact] public void DiskScan() { var d = Disk.Scan(new[] { Sys }); Out.WriteLine($"files={d.FileCount} dirs={d.TopDirs.Count} caches={d.Caches.Count}"); }
        [Fact] public void DuplicateImages() { var x = Disk.FindDuplicateImages(new[] { Sys }); Out.WriteLine($"scanned={x.Scanned} dups={x.Rows.Count}"); }
        [Fact] public void Signatures()
        {
            string w = System.Environment.GetEnvironmentVariable("WINDIR");
            Assert.Equal("Valid", Signature.Status(w + @"\explorer.exe"));
            Assert.Equal("Valid", Signature.Status(w + @"\System32\notepad.exe"));
        }
    }
}
