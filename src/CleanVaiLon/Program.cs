// Clean VaiLon – công cụ kiểm tra và dọn dẹp máy Windows.
// Không tự xoá gì: mọi thao tác thay đổi đều hỏi xác nhận, và chỉ chuyển vào Thùng rác.
using System;
using System.Drawing;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;

namespace CleanVaiLon
{
    static class Program
    {
        public static Icon AppIcon;

        [STAThread]
        static int Main(string[] args)
        {
            // --minimized: mở sẵn ở dạng thu nhỏ (khi tự chạy lúc đăng nhập). -StartMinimized giữ cho tương thích với bản PowerShell.
            bool minimized = args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) || a.Equals("-StartMinimized", StringComparison.OrdinalIgnoreCase));
            bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

            try { Native.SetPreferredAppMode(2); Native.RefreshImmersiveColorPolicyState(); Native.FlushMenuThemes(); } catch { }
            Application.EnableVisualStyles();
            // Không gọi SetCompatibleTextRenderingDefault(false): giữ cách vẽ chữ mặc định (GDI+) như bản PowerShell.
            try
            {
                using (var s = typeof(Program).Assembly.GetManifestResourceStream("CleanVaiLon.ico"))
                    if (s != null) AppIcon = new Icon(s);
            }
            catch { }

            AppInfo.EnsureDataDir();
            var cfg = Settings.Load();
            Lang.Cfg = cfg;
            if (isAdmin) LogonTask.MigrateIfNeeded();
            Application.Run(new MainForm(cfg, isAdmin, minimized));
            return 0;
        }
    }
}
