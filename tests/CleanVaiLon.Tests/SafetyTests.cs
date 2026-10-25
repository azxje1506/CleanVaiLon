// Kiểm tra bộ luật "xoá được?" trên các đường dẫn mẫu (cùng bộ mẫu với legacy\tests\Test-Safety.ps1). Không đụng tới tệp thật.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CleanVaiLon;
using Xunit;

namespace CleanVaiLon.Tests
{
    public class SafetyTests
    {
        static string E(string n) => Environment.GetEnvironmentVariable(n);

        public static IEnumerable<object[]> Cases()
        {
            string sd = E("SystemDrive");
            var c = new[]
            {
                new[] { sd + "\\", "system" }, new[] { sd + "\\pagefile.sys", "system" }, new[] { @"D:\pagefile.sys", "system" }, new[] { sd + "\\System Volume Information", "system" },
                new[] { E("WINDIR"), "system" }, new[] { E("WINDIR") + @"\System32\drivers", "system" }, new[] { E("WINDIR") + @"\WinSxS", "system" },
                new[] { E("WINDIR") + @"\Installer", "keep" }, new[] { E("WINDIR") + @"\Temp", "safe" }, new[] { E("WINDIR") + @"\SoftwareDistribution\Download", "safe" },
                new[] { sd + "\\Users", "system" }, new[] { E("USERPROFILE"), "system" }, new[] { E("USERPROFILE") + @"\AppData", "system" },
                new[] { E("TEMP"), "safe" }, new[] { E("TEMP") + @"\abc.tmp", "safe" }, new[] { E("LOCALAPPDATA") + @"\NVIDIA\DXCache", "safe" },
                new[] { E("LOCALAPPDATA") + @"\pip\cache", "safe" }, new[] { E("LOCALAPPDATA") + @"\npm-cache", "safe" }, new[] { E("LOCALAPPDATA") + @"\CrashDumps", "safe" },
                new[] { E("LOCALAPPDATA") + @"\Microsoft\Edge\User Data\Default\Cache", "safe" },
                new[] { E("LOCALAPPDATA") + @"\Google\Chrome\User Data\Default\Code Cache", "safe" },
                new[] { @"D:\Steam\steamapps\workshop\downloads", "safe" }, new[] { @"D:\Steam\steamapps\workshop\content\431960", "review" },
                new[] { @"D:\Steam\steamapps\common\SomeGame", "keep" }, new[] { @"D:\Games\Riot Games\VALORANT", "keep" },
                new[] { @"D:\Games\SomeGame [FitGirl Repack]", "review" }, new[] { @"D:\Games\SomeGame [FitGirl Repack]\fg-01.bin", "review" },
                new[] { E("USERPROFILE") + @"\Documents\My Games\SomeGame\Saves", "keep" }, new[] { E("USERPROFILE") + @"\Documents", "keep" },
                new[] { E("USERPROFILE") + @"\Downloads\project.zip", "review" }, new[] { E("USERPROFILE") + @"\Downloads\notes.txt", "review" },
                new[] { E("ProgramFiles") + @"\Adobe", "keep" }, new[] { E("ProgramFiles(x86)") + @"\Steam", "keep" }, new[] { E("ProgramData") + @"\NVIDIA Corporation", "keep" },
                new[] { E("APPDATA") + @"\Figma", "keep" }, new[] { @"D:\Projects\client\banner.psd", "keep" }, new[] { @"D:\Projects\web\node_modules", "review" },
                new[] { @"D:\Projects\web\.git", "keep" }, new[] { @"D:\Windows.iso", "review" }, new[] { @"D:\Tools\setup_x64.exe", "review" }, new[] { @"D:\Tools\App\app.exe", "keep" },
                new[] { @"D:\Video\clip.crdownload", "safe" }, new[] { @"D:\random\data.xyz", "review" },
            };
            return c.Select(x => new object[] { x[0], x[1] });
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void Level(string path, string expected) => Assert.Equal(expected, Safety.Get(path).Level);

        [Fact]
        public void LabelsMatchLevels()
        {
            Assert.Equal("Cấm xoá", Safety.Get(E("WINDIR")).Label);
            Assert.Equal("An toàn", Safety.Get(E("TEMP")).Label);
            Assert.Equal(Safety.UnknownWhy, Safety.Get(@"D:\random\data.xyz").Why);
        }

        [Fact]
        public void SystemItemIsBlockedInsideDeleteFunction()
        {
            var r = Recycle.Move(E("WINDIR") + @"\System32\drivers", "Item");
            Assert.Equal("Bị chặn", r.Result);
            Assert.Equal(0, r.Freed);
            Assert.True(Directory.Exists(E("WINDIR") + @"\System32\drivers"));
        }

        [Fact]
        public void DriveRootIsBlocked() => Assert.Equal("Bị chặn", Recycle.Move(E("SystemDrive") + "\\", "Item").Result);

        [Fact]
        public void MissingItemIsReported() => Assert.Equal("Không còn", Recycle.Move(@"Z:\khong\ton\tai\" + Guid.NewGuid(), "Item").Result);

        [Fact]
        public void NoneModeIsSkipped() => Assert.Equal("Bỏ qua", Recycle.Move(E("SystemDrive") + @"\$Recycle.Bin", "None").Result);

        // Khi bản PowerShell còn trong legacy\, bộ luật C# phải giống hệt $SafetyRules (cùng thứ tự, mẫu, mức, lý do).
        [Fact]
        public void RulesMatchLegacyScript()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string ps = null;
            for (var d = new DirectoryInfo(dir); d != null; d = d.Parent)
            {
                var f = Path.Combine(d.FullName, "legacy", "CleanVaiLon.ps1");
                if (File.Exists(f)) { ps = f; break; }
            }
            if (ps == null) return;   // bản PowerShell đã được gỡ khỏi repo
            string src = File.ReadAllText(ps, Encoding.UTF8);
            int a = src.IndexOf("$SafetyRules = @(", StringComparison.Ordinal);
            int b = src.IndexOf("\n)", a, StringComparison.Ordinal);
            var rules = Regex.Matches(src.Substring(a, b - a), @"@\{ R = '((?:[^']|'')*)'; L = '(\w+)'; T = '((?:[^']|'')*)' \}")
                .Cast<Match>().Select(m => new[] { m.Groups[1].Value.Replace("''", "'"), m.Groups[2].Value, m.Groups[3].Value.Replace("''", "'") }).ToList();
            Assert.Equal(rules.Count, Safety.Rules.Length);
            for (int i = 0; i < rules.Count; i++)
            {
                Assert.Equal(rules[i][0], Safety.Rules[i].R);
                Assert.Equal(rules[i][1], Safety.Rules[i].L);
                Assert.Equal(rules[i][2], Safety.Rules[i].T);
            }
        }

        [Fact]
        public void SafetyTextHasLabelAndReason()
        {
            var t = Safety.FormatText(E("WINDIR"), true);
            Assert.StartsWith("xoá được?:     CẤM XOÁ – ", t);
        }
    }
}
