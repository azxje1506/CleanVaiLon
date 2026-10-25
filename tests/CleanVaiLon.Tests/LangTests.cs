// Bản tiếng Anh: mọi chữ trong dữ liệu (giải thích, luật an toàn, nhãn) phải có bản dịch.
using System.Linq;
using CleanVaiLon;
using Xunit;

namespace CleanVaiLon.Tests
{
    [Collection("Lang")]
    public class LangTests
    {
        static T WithEnglish<T>(System.Func<T> f)
        {
            var old = Lang.Cfg;
            Lang.Cfg = new Settings { Lang = "en" };
            try { return f(); } finally { Lang.Cfg = old; }
        }

        static bool Translated(string vi) => Lang.HasData(vi) || WithEnglish(() => Lang.D(vi)) != vi;

        [Fact]
        public void EveryKnowledgeTextHasEnglish()
        {
            var missing = Knowledge.Process.Values.Concat(Knowledge.Startup.Select(x => x[2])).Where(x => !Translated(x)).ToList();
            Assert.Empty(missing);
        }

        [Fact]
        public void EverySafetyRuleHasEnglish()
        {
            var missing = Safety.Rules.Select(r => r.T).Concat(Safety.Labels.Values).Append(Safety.UnknownWhy).Where(x => !Translated(x)).ToList();
            Assert.Empty(missing);
        }

        [Fact]
        public void StatusLabelsHaveEnglish()
        {
            foreach (var s in new[] { Startup.KRun, Startup.KFolder, Startup.KTask, Startup.KService, Apps.KApp, Apps.KGame, Apps.KSystem,
                                      "Đang bật", "Đã tắt", "Nên giữ", "Có thể tắt", "Cần xác nhận", "Chưa rõ", "Cao", "Trung bình", "Thấp", "Thông tin",
                                      "Đã chuyển", "Bị chặn", "Quá lớn", "Không còn", "Bỏ qua", "Lỗi" })
                Assert.True(Translated(s), s);
        }

        [Fact]
        public void CompositeTextsAreTranslated()
        {
            Assert.Equal(@"In C:\Users\a\Downloads, older than 90 days", WithEnglish(() => Lang.D(@"Trong C:\Users\a\Downloads, hơn 90 ngày")));
            Assert.Equal("[12 small files]", WithEnglish(() => Lang.D("[12 tệp nhỏ]")));
            Assert.Equal("Enabled · v1.2 · can read and change every website; crypto wallet – remove it if you no longer use it · ID abc",
                WithEnglish(() => Lang.D("Đang bật · v1.2 · đọc và sửa được mọi trang web; ví tiền mã hoá – gỡ nếu không còn dùng · ID abc")));
            Assert.Equal(@"Recycle Bin – Moved: D:\a.zip | Moved to the Recycle Bin.", WithEnglish(() => Lang.D(@"Thùng rác – Đã chuyển: D:\a.zip | Đã vào Thùng rác.")));
        }

        [Fact]
        public void VietnameseIsUnchanged()
        {
            var old = Lang.Cfg;
            Lang.Cfg = new Settings { Lang = "vi" };
            try { Assert.Equal("Đang bật", Lang.D("Đang bật")); } finally { Lang.Cfg = old; }
        }

        [Fact]
        public void PathsAreLeftAlone() => Assert.Equal(@"C:\Program Files\Foo\foo.exe", WithEnglish(() => Lang.D(@"C:\Program Files\Foo\foo.exe")));
    }
}
