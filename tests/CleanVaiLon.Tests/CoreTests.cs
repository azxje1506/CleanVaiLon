// Định dạng tệp dữ liệu (để người đang dùng bản PowerShell giữ được cài đặt) và các hàm nhỏ.
using System;
using System.IO;
using System.Text;
using CleanVaiLon;
using Xunit;

namespace CleanVaiLon.Tests
{
    public class CoreTests
    {
        [Fact]
        public void SettingsJsonLooksLikePowerShell()
        {
            var s = new Settings();
            Assert.Equal("{\r\n    \"Lang\":  \"vi\",\r\n    \"StartPage\":  \"perf\",\r\n    \"AutoLoad\":  true,\r\n    \"HeavyPct\":  5,\r\n    \"Months\":  6,\r\n    \"DoubleConfirm\":  true,\r\n    \"Grid\":  true\r\n}", s.ToJson());
        }

        [Fact]
        public void ReadsSettingsWrittenByPowerShell()
        {
            // Bản PowerShell ghi khoá theo thứ tự bất kỳ (hashtable), UTF-8 có BOM.
            string json = "{\r\n    \"Grid\":  false,\r\n    \"Months\":  12,\r\n    \"Lang\":  \"en\",\r\n    \"HeavyPct\":  9,\r\n    \"StartPage\":  \"disk\",\r\n    \"DoubleConfirm\":  false,\r\n    \"AutoLoad\":  false\r\n}";
            var j = TextFiles.ParseJson(json);
            Assert.Equal("en", TextFiles.Get(j, "lang"));
            Assert.Equal(12, TextFiles.Get(j, "Months"));
            Assert.False(TextFiles.Truthy(TextFiles.Get(j, "Grid")));
        }

        [Fact]
        public void ReadLinesHandlesBomUtf8AndAnsi()
        {
            string f = Path.GetTempFileName();
            try
            {
                var bytes = new System.Collections.Generic.List<byte>(new UTF8Encoding(true).GetPreamble());
                bytes.AddRange(Encoding.UTF8.GetBytes("2026-01-01 10:00:00  Thùng rác – Đã chuyển\r\n"));
                bytes.AddRange(Encoding.Default.GetBytes("ansi line\r\n"));
                bytes.AddRange(Encoding.UTF8.GetBytes("cuối"));
                File.WriteAllBytes(f, bytes.ToArray());
                var lines = TextFiles.ReadLines(f);
                Assert.Equal(3, lines.Count);
                Assert.Equal("2026-01-01 10:00:00  Thùng rác – Đã chuyển", lines[0]);
                Assert.Equal("ansi line", lines[1]);
                Assert.Equal("cuối", lines[2]);
            }
            finally { File.Delete(f); }
        }

        [Fact]
        public void TruthyFollowsPowerShell()
        {
            Assert.False(TextFiles.Truthy(null));
            Assert.False(TextFiles.Truthy(0));
            Assert.False(TextFiles.Truthy(new object[0]));
            Assert.True(TextFiles.Truthy(new object[] { 1 }));
            Assert.True(TextFiles.Truthy("x"));
        }

        [Fact]
        public void AdviceUsesKnowledgeBase()
        {
            Assert.Equal("Cần xác nhận", Startup.Advice("x", "wscript.exe a.vbs")[0]);
            Assert.Equal("Nên giữ", Startup.Advice("SecurityHealth", "")[0]);
            Assert.Equal("Có thể tắt", Startup.Advice("Discord", "")[0]);
            Assert.Equal("Nên giữ", Startup.Advice("foo", @"C:\Windows\System32\DriverStore\x.exe")[0]);
            Assert.Equal("Chưa rõ", Startup.Advice("zzz", "zzz")[0]);
        }

        [Fact]
        public void QuoteKeepsTrailingBackslash()
        {
            Assert.Equal(@"C:\a", Backup.Quote(@"C:\a"));
            Assert.Equal("\"C:\\a b\"", Backup.Quote(@"C:\a b"));
            Assert.Equal("\"D:\\\\\"", Backup.Quote(@"D:\"));
        }

        [Fact]
        public void TranslationTableIsReversible()
        {
            Assert.Equal("performance", Lang.Dict["hiệu năng"]);
            Assert.Equal("hiệu năng", Lang.DictRev["performance"]);
        }
    }
}
