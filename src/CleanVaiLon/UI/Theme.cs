// Bảng màu, phông chữ và bảng dịch VI/EN của giao diện.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CleanVaiLon
{
    public static class Theme
    {
        static Color Hex(string h) => ColorTranslator.FromHtml(h);

        public static readonly Color Bg = Hex("#070B09"), Side = Hex("#060907"), Panel = Hex("#0A110D"), Row = Hex("#080D0A"), RowAlt = Hex("#0A120E");
        public static readonly Color RowSel = Hex("#0E2619"), Line = Hex("#14231B"), Grid = Hex("#0A130F");
        public static readonly Color Green = Hex("#3BE38F"), GreenDim = Hex("#1D6E46"), Text = Hex("#C4E3D2"), Bright = Hex("#E6F5EC"), Muted = Hex("#5C7E6C");
        public static readonly Color Amber = Hex("#E8B04A"), Red = Hex("#FF6B6B"), Cyan = Hex("#4FC9DE");
        public static readonly Color WarnBg = Hex("#15130A"), BadBg = Hex("#1A0C0E");
        public static readonly Color SettingLabel = Hex("#9DB8A9"), DisabledLine = Hex("#23392D"), TrackColor = Hex("#12251B");

        public static readonly Font Main, Small, Bold, Title, Logo, Icon;
        public static readonly string GlyphMin, GlyphMax, GlyphRestore, GlyphClose;

        public static readonly Pen PenCheck = new Pen(Bg, 2), PenDisabled = new Pen(DisabledLine), PenLine = new Pen(Line), PenGrid = new Pen(Grid);
        public static readonly Pen PenGreen = new Pen(Green), PenGreenDim = new Pen(GreenDim);
        public static readonly SolidBrush BrushGreen = new SolidBrush(Green), BrushBg = new SolidBrush(Bg), BrushGreenDim = new SolidBrush(GreenDim), BrushTrack = new SolidBrush(TrackColor);

        static Theme()
        {
            var fams = new HashSet<string>(new InstalledFontCollection().Families.Select(f => f.Name));
            string mono = new[] { "JetBrains Mono", "Cascadia Mono", "Cascadia Code", "Consolas" }.FirstOrDefault(fams.Contains) ?? "Consolas";
            Main = new Font(mono, 9.5f);
            Small = new Font(mono, 8.5f);
            Bold = new Font(mono, 9.5f, FontStyle.Bold);
            Title = new Font(mono, 11.5f, FontStyle.Bold);
            Logo = new Font(mono, 10.5f, FontStyle.Bold);
            string iconFont = new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" }.FirstOrDefault(fams.Contains);
            if (iconFont != null)
            {
                GlyphMin = ""; GlyphMax = ""; GlyphRestore = ""; GlyphClose = "";
                Icon = new Font(iconFont, 8f);
            }
            else
            {
                GlyphMin = "—"; GlyphMax = "□"; GlyphRestore = "❐"; GlyphClose = "✕";
                Icon = Main;
            }
        }
    }

    // Bảng dịch các chữ cố định của giao diện. Nội dung dữ liệu (giải thích, nhãn trạng thái) giữ tiếng Việt.
    public static partial class Lang
    {
        public static Settings Cfg;
        public static readonly Dictionary<string, string> Dict = new Dictionary<string, string>();
        public static readonly Dictionary<string, string> DictRev = new Dictionary<string, string>();

        static readonly string[][] Pairs =
        {
            new[] { @"hiệu năng", @"performance" },
            new[] { @"khởi động", @"startup" },
            new[] { @"ổ đĩa", @"disks" },
            new[] { @"phân tích", @"analyzer" },
            new[] { @"ứng dụng", @"apps" },
            new[] { @"bảo mật", @"security" },
            new[] { @"sao lưu", @"backup" },
            new[] { @"nhật ký", @"log" },
            new[] { @"cài đặt", @"settings" },
            new[] { @"xác nhận", @"confirm" },
            new[] { @"thông báo", @"notice" },
            new[] { @"● SẴN SÀNG", @"● READY" },
            new[] { @"● ĐANG LÀM", @"● WORKING" },
            new[] { @"QUẢN TRỊ", @"ADMIN" },
            new[] { @"KHÔNG CÓ QUYỀN QUẢN TRỊ", @"NOT ADMIN" },
            new[] { @"đủ quyền để đọc mọi thông tin", @"full access to all information" },
            new[] { @"thiếu quyền – một số mục sẽ trống", @"limited rights – some data will be missing" },
            new[] { @"⟳ đo lại", @"⟳ measure" },
            new[] { @"tắt mục đã chọn", @"disable selected" },
            new[] { @"bật lại mục đã chọn", @"re-enable selected" },
            new[] { @"⟳ tải lại", @"⟳ reload" },
            new[] { @"→ thùng rác", @"→ recycle bin" },
            new[] { @"mở vị trí", @"open location" },
            new[] { @"tìm ảnh trùng lặp", @"find duplicate images" },
            new[] { @"⟳ quét dung lượng", @"⟳ scan space" },
            new[] { @"↑ lên trên", @"↑ up" },
            new[] { @"⟳ quét", @"⟳ scan" },
            new[] { @"chọn thư mục…", @"choose folder…" },
            new[] { @"kiểm tra một tệp…", @"check a file…" },
            new[] { @"gỡ mục đã chọn", @"uninstall selected" },
            new[] { @"mở thư mục cài", @"open install folder" },
            new[] { @"⟳ phân tích", @"⟳ analyze" },
            new[] { @"⟳ kiểm tra", @"⟳ check" },
            new[] { @"chép thư mục…", @"copy folder…" },
            new[] { @"mở file history", @"open file history" },
            new[] { @"mở bằng notepad", @"open in notepad" },
            new[] { @"có, tiếp tục", @"yes, continue" },
            new[] { @"không", @"no" },
            new[] { @"đóng", @"close" },
            new[] { @"mở", @"open" },
            new[] { @"↺ KHÔI PHỤC MẶC ĐỊNH", @"↺ RESET TO DEFAULTS" },
            new[] { @"lọc:", @"filter:" },
            new[] { @"ổ:", @"drives:" },
            new[] { @"  xem:", @"  view:" },
            new[] { @"quét:", @"scan:" },
            new[] { @"   màu:", @"   colors:" },
            new[] { @"tháng  ·", @"months  ·" },
            new[] { @"không dùng trong", @"unused for" },
            new[] { @"chỉ hiện >", @"only show >" },
            new[] { @"tất cả", @"all" },
            new[] { @"đang bật", @"enabled" },
            new[] { @"đã tắt", @"disabled" },
            new[] { @"chạy khi đăng nhập", @"at sign-in" },
            new[] { @"tác vụ", @"tasks" },
            new[] { @"dịch vụ", @"services" },
            new[] { @"20 tệp lớn nhất", @"20 largest files" },
            new[] { @"20 thư mục lớn nhất", @"20 largest folders" },
            new[] { @"bộ cài & file nén", @"installers & archives" },
            new[] { @"tải xuống cũ", @"old downloads" },
            new[] { @"bộ nhớ đệm & file tạm", @"caches & temp files" },
            new[] { @"ảnh trùng lặp", @"duplicate images" },
            new[] { @"game steam", @"steam games" },
            new[] { @"hệ thống", @"system" },
            new[] { @"cần xem", @"needs review" },
            new[] { @"diệt virus", @"antivirus" },
            new[] { @"mạng", @"network" },
            new[] { @"extension", @"extensions" },
            new[] { @"■ video", @"■ video" },
            new[] { @"■ ảnh", @"■ images" },
            new[] { @"■ âm thanh", @"■ audio" },
            new[] { @"■ nén / bộ cài", @"■ archives / installers" },
            new[] { @"■ chương trình", @"■ programs" },
            new[] { @"■ dữ liệu game", @"■ game data" },
            new[] { @"■ tài liệu", @"■ documents" },
            new[] { @"■ khác", @"■ other" },
            new[] { @"chương trình", @"program" },
            new[] { @"tiến trình", @"processes" },
            new[] { @"là gì", @"what it is" },
            new[] { @"đường dẫn", @"path" },
            new[] { @"tên", @"name" },
            new[] { @"loại", @"type" },
            new[] { @"trạng thái", @"status" },
            new[] { @"gợi ý", @"advice" },
            new[] { @"lệnh chạy", @"command" },
            new[] { @"kích thước", @"size" },
            new[] { @"ngày sửa", @"modified" },
            new[] { @"xoá được?", @"deletable?" },
            new[] { @"ghi chú", @"note" },
            new[] { @"% thư mục cha", @"% of parent" },
            new[] { @"dung lượng", @"disk size" },
            new[] { @"số tệp", @"files" },
            new[] { @"dùng lần cuối", @"last used" },
            new[] { @"nguồn dữ liệu", @"source" },
            new[] { @"nhà phát hành", @"publisher" },
            new[] { @"mức", @"level" },
            new[] { @"nhóm", @"group" },
            new[] { @"mục", @"item" },
            new[] { @"chi tiết", @"details" },
            new[] { @"hình thức", @"method" },
            new[] { @"thời gian", @"time" },
            new[] { @"nội dung", @"message" },
            new[] { @"Chương trình nào đang dùng CPU và RAM. Dòng vàng là chương trình vượt ngưỡng ""nặng"" (chỉnh trong cài đặt). Bấm một dòng để xem nó là gì.", @"Which programs are using CPU and RAM. Yellow rows exceed the ""heavy"" threshold (set in settings). Click a row to see what it is." },
            new[] { @"Mọi thứ tự chạy khi bật máy. Tích chọn rồi bấm ""tắt"" – không xoá gì, bật lại được bất cứ lúc nào. Dịch vụ được chuyển sang ""chạy khi cần"".", @"Everything that starts with Windows. Tick items and press ""disable"" – nothing is deleted and you can re-enable any time. Services are set to ""manual""." },
            new[] { @"Tìm thứ đang chiếm chỗ. Tích chọn rồi chuyển vào Thùng rác – mục quá lớn so với Thùng rác được bỏ qua để bạn tự quyết. Nhớ dọn Thùng rác để lấy lại dung lượng.", @"Find what is taking space. Tick items to move them to the Recycle Bin – items too big for it are skipped so you decide. Empty the Recycle Bin to get the space back." },
            new[] { @"Bản đồ dung lượng: ô càng to càng chiếm nhiều chỗ. Bấm để chọn, bấm đúp vào thư mục để đi vào trong. Màu theo loại tệp, xem chú thích bên dưới.", @"Space map: bigger blocks take more space. Click to select, double-click a folder to go inside. Colours follow file type, see the legend below." },
            new[] { @"Ứng dụng đã cài và lần dùng cuối. Dữ liệu Steam là chính xác; ứng dụng khác dựa vào lịch sử chạy của Windows nên chỉ biết được từ một mốc nhất định.", @"Installed apps and when they were last used. Steam data is exact; other apps rely on Windows run history, which only goes back to a certain date." },
            new[] { @"Chỉ kiểm tra, không thay đổi gì. Gỡ extension trong trang quản lý extension của từng trình duyệt – mỗi hồ sơ (profile) một trang riêng.", @"Read-only check, nothing is changed. Remove extensions from each browser's extension page – one page per profile." },
            new[] { @"Máy đã được sao lưu chưa và ở đâu. Cách đơn giản nhất: bật File History lên một ổ cắm ngoài, và chép các thư mục quan trọng sang một ổ khác.", @"Whether this PC is backed up, and where. Simplest setup: turn on File History to an external drive, and copy important folders to another drive." },
            new[] { @"Mọi thay đổi công cụ đã làm trên máy: tắt/bật khởi động, chuyển vào Thùng rác, gỡ ứng dụng, sao lưu.", @"Every change this tool made: startup items, Recycle Bin moves, uninstalls, backups." },
            new[] { @"Tuỳ chỉnh cách công cụ hoạt động. Thay đổi được lưu ngay.", @"Customize how the tool works. Changes are saved immediately." },
            new[] { @"ngôn ngữ", @"language" },
            new[] { @"khởi động cùng windows", @"start with windows" },
            new[] { @"trang mở đầu", @"start page" },
            new[] { @"tự tải dữ liệu khi mở trang", @"load data when a page opens" },
            new[] { @"ngưỡng ""nặng"" ở trang hiệu năng (%)", @"""heavy"" threshold on performance page (%)" },
            new[] { @"số tháng ""không dùng"" mặc định", @"default ""unused"" months" },
            new[] { @"hỏi lại lần 2 khi xoá mục ""không nên xoá""", @"ask twice before deleting ""shouldn't delete"" items" },
            new[] { @"lưới nền", @"background grid" },
            new[] { @"thư mục dữ liệu", @"data folder" },
            new[] { @"phiên bản", @"version" },
            new[] { @"đã chọn", @"selected" },
            new[] { @"phạm vi", @"scope" },
            new[] { @"hãng", @"vendor" },
            new[] { @"nguồn", @"source" },
            new[] { @"thư mục cài", @"install folder" },
            new[] { @"lệnh gỡ", @"uninstall command" },
        };

        static Lang()
        {
            foreach (var p in Pairs) Dict[p[0]] = p[1];
            foreach (var kv in Dict) if (!DictRev.ContainsKey(kv.Value)) DictRev[kv.Value] = kv.Key;
            foreach (var p in DataPairs) Data[p[0]] = p[1];
        }

        public static bool En => Cfg != null && Cfg.Lang == "en";

        // Chữ do giao diện tự tạo (thông báo, tiêu đề phụ): chọn theo ngôn ngữ đang dùng.
        public static string T(string vi, string en) => En ? en : vi;

        // ----- Nội dung dữ liệu -----
        // Dữ liệu luôn giữ tiếng Việt (bộ lọc so sánh theo chữ tiếng Việt, nhật ký ghi tiếng Việt);
        // chỉ lúc hiển thị mới dịch, nên đổi ngôn ngữ là danh sách đổi theo ngay.
        static readonly Dictionary<string, string> Data = new Dictionary<string, string>();

        static readonly KeyValuePair<Regex, Func<Match, string>>[] Rules =
        {
            R(@"^Trong (.+), hơn 90 ngày$", m => "In " + m.Groups[1].Value + ", older than 90 days"),
            R(@"^Ở ổ khác – có thể là bản sao lưu\. Giống hệt: (.+)$", m => "On another drive – may be a backup. Identical to: " + m.Groups[1].Value),
            R(@"^Giống hệt: (.+)$", m => "Identical to: " + m.Groups[1].Value),
            R(@"^Không thấy chạy từ (.+)$", m => "Not run since " + m.Groups[1].Value),
            R(@"^\[(\d+) tệp nhỏ\]$", m => "[" + m.Groups[1].Value + " small files]"),
            R(@"^Mã (-?\d+)$", m => "Code " + m.Groups[1].Value),
            R(@"^\((.+)\) lớn hơn chỗ trống của Thùng rác\. Bạn tự xoá bằng Shift \+ Delete nếu chắc chắn\.$",
                m => "(" + m.Groups[1].Value + ") is larger than the free space in the Recycle Bin. Delete it yourself with Shift + Delete if you are sure."),
            R(@"^(\d+) file vào Thùng rác, (\d+) file đang dùng nên bỏ qua\.$", m => m.Groups[1].Value + " files moved to the Recycle Bin, " + m.Groups[2].Value + " in use were skipped."),
            R(@"^không sửa từ (.+) \((.+) năm\)$", m => "not modified since " + m.Groups[1].Value + " (" + m.Groups[2].Value + " years)"),
            // bảo mật
            R(@"^Tường lửa tắt ở: (.+)$", m => "Firewall is off for: " + m.Groups[1].Value),
            R(@"^Đang dùng: (.+)$", m => "In use: " + m.Groups[1].Value),
            R(@"^Cập nhật lần cuối: (.*)$", m => "Last updated: " + m.Groups[1].Value),
            R(@"^(Không có chữ ký số\.|Chữ ký số không hợp lệ \((\w+)\)\.) (.*)\. Đường dẫn: (.*)$",
                m => (m.Groups[2].Success ? "Invalid digital signature (" + m.Groups[2].Value + ")." : "Not digitally signed.") + " " + m.Groups[3].Value + ". Path: " + m.Groups[4].Value),
            R(@"^(.+) \((\d+) kết nối\)$", m => m.Groups[1].Value + " (" + m.Groups[2].Value + " connections)"),
            R(@"^Tới: (.*?)\. (.*)$", m => "To: " + m.Groups[1].Value + ". " + m.Groups[2].Value),
            R(@"^Cổng (.*?)\. (.*)$", m => "Port " + m.Groups[1].Value + ". " + m.Groups[2].Value),
            R(@"^Proxy: (.*)\. Nếu bạn không tự đặt thì nên kiểm tra\.$", m => "Proxy: " + m.Groups[1].Value + ". If you didn't set it yourself, check it."),
            R(@"^(Đang bật|Đang tắt) · (v.*?) · (.*) · ID (.*)$",
                m => D(m.Groups[1].Value) + " · " + m.Groups[2].Value + " · " + string.Join("; ", m.Groups[3].Value.Split(new[] { "; " }, StringSplitOptions.None).Select(D)) + " · ID " + m.Groups[4].Value),
            // sao lưu
            R(@"^Lần cuối: (.+)$", m => "Last: " + m.Groups[1].Value),
            R(@"^Lưu vào: (.*)\. Cấu hình cập nhật: (.*)$", m => "Saved to: " + m.Groups[1].Value + ". Settings updated: " + m.Groups[2].Value),
            R(@"^(\d+) điểm$", m => m.Groups[1].Value + " points"),
            R(@"^Mới nhất: (.*?) – (.*)\. Chỉ khôi phục được cài đặt Windows, không giữ file của bạn\.$",
                m => "Latest: " + m.Groups[1].Value + " – " + m.Groups[2].Value + ". Only restores Windows settings, it does not keep your files."),
            R(@"^(.*) – thư mục (.*)\. Đang chạy: (True|False)$", m => m.Groups[1].Value + " – folder " + m.Groups[2].Value + ". Running: " + m.Groups[3].Value),
            R(@"^(.*) – (\d+) GB\. Có thể dùng làm nơi sao lưu\.$", m => m.Groups[1].Value + " – " + m.Groups[2].Value + " GB. Can be used as a backup target."),
            // nhật ký
            R(@"^Khởi động – (tắt|bật lại) : (.*?) \| (.*)$", m => "Startup – " + (m.Groups[1].Value == "tắt" ? "disable" : "re-enable") + " : " + D(m.Groups[2].Value) + " | " + m.Groups[3].Value),
            R(@"^Thùng rác – (.*?): (.*) \| (.*)$", m => "Recycle Bin – " + D(m.Groups[1].Value) + ": " + m.Groups[2].Value + " | " + D(m.Groups[3].Value)),
            R(@"^Gỡ – (.*?): (.*) \| (.*)$", m => "Uninstall – " + D(m.Groups[1].Value) + ": " + m.Groups[2].Value + " | " + D(m.Groups[3].Value)),
            R(@"^Sao lưu: (.*) \| mã (-?\d+)$", m => "Backup: " + m.Groups[1].Value + " | code " + m.Groups[2].Value),
        };

        static KeyValuePair<Regex, Func<Match, string>> R(string pattern, Func<Match, string> f) =>
            new KeyValuePair<Regex, Func<Match, string>>(new Regex(pattern, RegexOptions.Compiled), f);

        // Dịch một giá trị dữ liệu để hiển thị. Không có trong bảng thì giữ nguyên (đường dẫn, tên chương trình…).
        public static string D(string s)
        {
            if (!En || string.IsNullOrEmpty(s)) return s;
            if (Data.TryGetValue(s, out var v)) return v;
            foreach (var r in Rules)
            {
                var m = r.Key.Match(s);
                if (m.Success) return r.Value(m);
            }
            return s;
        }

        // Có bản dịch trong bảng dữ liệu không (dùng cho kiểm thử).
        public static bool HasData(string s) => s != null && Data.ContainsKey(s);

        // Dịch cả bảng gốc giao diện lẫn dữ liệu (cho nhãn ngắn như tên việc chạy nền).
        public static string Any(string s) => En && s != null && Dict.TryGetValue(s, out var v) ? v : D(s);

        public static string Tr(string s) => En && s != null && Dict.TryGetValue(s, out var v) ? v : s;

        static string Swap(string txt, bool en)
        {
            if (string.IsNullOrEmpty(txt)) return txt;
            if (en && Dict.TryGetValue(txt, out var e)) return e;
            if (!en && DictRev.TryGetValue(txt, out var v)) return v;
            return txt;
        }

        // Duyệt mọi control, đổi chữ cố định theo ngôn ngữ đang chọn (chỉ đổi chữ có trong bảng dịch).
        public static void Apply(Control root)
        {
            bool en = En;
            var stack = new Stack<Control>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                var t = Swap(c.Text, en);
                if (t != c.Text) c.Text = t;
                if (c is ListView lv)
                    foreach (ColumnHeader h in lv.Columns) { var ht = Swap(h.Text, en); if (ht != h.Text) h.Text = ht; }
                foreach (Control ch in c.Controls) stack.Push(ch);
            }
        }
    }
}
