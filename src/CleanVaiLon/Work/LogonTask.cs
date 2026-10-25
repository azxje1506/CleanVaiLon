// Khởi động cùng Windows bằng Task Scheduler với quyền cao nhất, nên không bị hỏi quyền quản trị mỗi lần đăng nhập.
using System;

namespace CleanVaiLon
{
    public static class LogonTask
    {
        public const string Name = "CleanVaiLon";
        public const string Args = "--minimized";

        static dynamic Service()
        {
            dynamic svc = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
            svc.Connect();
            return svc;
        }

        static dynamic Find()
        {
            try { return Service().GetFolder("\\").GetTask(Name); } catch { return null; }
        }

        public static bool Exists() => Find() != null;

        public static void Set(bool on)
        {
            dynamic svc = Service();
            dynamic root = svc.GetFolder("\\");
            if (!on)
            {
                root.DeleteTask(Name, 0);
                Log.Write("Cài đặt: tắt khởi động cùng Windows");
                return;
            }
            Register(svc, root);
            Log.Write("Cài đặt: bật khởi động cùng Windows");
        }

        static void Register(dynamic svc, dynamic root)
        {
            string user = Environment.UserDomainName + "\\" + Environment.UserName;
            dynamic def = svc.NewTask(0);
            def.RegistrationInfo.Description = "Clean VaiLon";
            def.Principal.UserId = user;
            def.Principal.LogonType = 3;   // TASK_LOGON_INTERACTIVE_TOKEN
            def.Principal.RunLevel = 1;    // TASK_RUNLEVEL_HIGHEST
            def.Settings.DisallowStartIfOnBatteries = false;
            def.Settings.StopIfGoingOnBatteries = false;
            def.Settings.ExecutionTimeLimit = "PT0S";
            dynamic trg = def.Triggers.Create(9);   // TASK_TRIGGER_LOGON
            trg.UserId = user;
            dynamic act = def.Actions.Create(0);    // TASK_ACTION_EXEC
            act.Path = AppInfo.ExePath;
            act.Arguments = Args;
            root.RegisterTaskDefinition(Name, def, 6, user, null, 3);
        }

        // Tác vụ do bản PowerShell tạo trỏ tới powershell.exe + CleanVaiLon.ps1 (đã bị gỡ khi cài bản mới).
        // Bạn đã bật sẵn tuỳ chọn này, nên trỏ lại tác vụ sang CleanVaiLon.exe để nó tiếp tục chạy.
        public static void MigrateIfNeeded()
        {
            try
            {
                dynamic t = Find();
                if (t == null) return;
                string path = "";
                foreach (dynamic a in t.Definition.Actions) { if ((int)a.Type == 0) path = a.Path; break; }
                if (!path.EndsWith("powershell.exe", StringComparison.OrdinalIgnoreCase)) return;
                dynamic svc = Service();
                Register(svc, svc.GetFolder("\\"));
                Log.Write("Cài đặt: chuyển tác vụ khởi động cùng Windows sang CleanVaiLon.exe");
            }
            catch { }
        }
    }
}
