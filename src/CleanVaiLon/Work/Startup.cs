// Trang Khởi động: mọi thứ tự chạy khi bật máy. Tắt / bật lại, không xoá gì.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CleanVaiLon
{
    public class StartupItem
    {
        public string Kind, Name, Scope, Advice, Note, Command, ApKey, Id;
        public bool Enabled;
    }

    public static class Startup
    {
        public const string KRun = "Chạy khi đăng nhập", KFolder = "Thư mục Startup", KTask = "Tác vụ tự chạy", KService = "Dịch vụ nền";

        public static string[] Advice(string name, string cmd)
        {
            string text = name + " " + cmd;
            if (Rx.M(text, @"\b(wscript|cscript|mshta)\b|\.vbs\b|powershell[^|]*-w(indowstyle)?\s+h|\bnode(\.exe)?\b"))
                return new[] { "Cần xác nhận", "Chạy ngầm bằng script. Chỉ giữ nếu bạn tự cài và biết nó là gì." };
            foreach (var k in Knowledge.Startup) if (Rx.M(text, k[0])) return new[] { k[1], k[2] };
            if (Rx.M(cmd, @"\\DriverStore\\")) return new[] { "Nên giữ", "Thành phần của driver phần cứng." };
            return new[] { "Chưa rõ", "Chưa có trong danh sách hiểu biết. Tra tên trên mạng trước khi tắt." };
        }

        // "HKCU:\..." / "HKLM:\..." → khoá registry 64-bit.
        public static RegistryKey OpenKey(string psPath, bool write = false)
        {
            var hive = psPath.StartsWith("HKLM:", StringComparison.OrdinalIgnoreCase) ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
            var sub = psPath.Substring(6);
            using (var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                return write ? root.CreateSubKey(sub, true) : root.OpenSubKey(sub);
        }

        static bool IsOn(RegistryKey ap, string name)
        {
            var b = ap?.GetValue(name) as byte[];
            return !(b != null && b.Length > 0 && (b[0] & 1) != 0);
        }

        static void Add(List<StartupItem> items, string kind, string name, string scope, bool on, string cmd, string apKey, string id, string adviceName = null)
        {
            var adv = Advice(adviceName ?? name, cmd);
            items.Add(new StartupItem { Kind = kind, Name = name, Scope = scope, Enabled = on, Advice = adv[0], Note = adv[1], Command = cmd, ApKey = apKey, Id = id });
        }

        public static List<StartupItem> Items(ICollection<string> toolServices)
        {
            var items = new List<StartupItem>();
            var runKeys = new[]
            {
                new[] { @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Run", @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "Tài khoản này" },
                new[] { @"HKLM:\Software\Microsoft\Windows\CurrentVersion\Run", @"HKLM:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "Mọi tài khoản" },
                new[] { @"HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", @"HKLM:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32", "Mọi tài khoản (32-bit)" },
            };
            foreach (var k in runKeys)
            {
                try
                {
                    using (var key = OpenKey(k[0]))
                    using (var ap = OpenKey(k[1]))
                    {
                        if (key == null) continue;
                        foreach (var n in key.GetValueNames())
                        {
                            if (string.IsNullOrEmpty(n)) continue;
                            string cmd = Convert.ToString(key.GetValue(n));
                            Add(items, KRun, n, k[2], IsOn(ap, n), cmd, k[1], n);
                        }
                    }
                }
                catch { }
            }
            var folders = new[]
            {
                new[] { Environment.GetFolderPath(Environment.SpecialFolder.Startup), @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder", "Tài khoản này" },
                new[] { Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), @"HKLM:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder", "Mọi tài khoản" },
            };
            foreach (var f in folders)
            {
                try
                {
                    if (string.IsNullOrEmpty(f[0]) || !Directory.Exists(f[0])) continue;
                    using (var ap = OpenKey(f[1]))
                        foreach (var file in new DirectoryInfo(f[0]).GetFiles().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            if (string.Equals(file.Name, "desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                            string bn = Path.GetFileNameWithoutExtension(file.Name);
                            Add(items, KFolder, bn, f[2], IsOn(ap, file.Name), file.FullName, f[1], file.Name);
                        }
                }
                catch { }
            }
            try { AddTasks(items); } catch { }
            try
            {
                using (var q = new ManagementObjectSearcher("SELECT Name, DisplayName, PathName, StartMode, State FROM Win32_Service"))
                    foreach (ManagementObject s in q.Get())
                    {
                        string name = Convert.ToString(s["Name"]), path = Convert.ToString(s["PathName"]), mode = Convert.ToString(s["StartMode"]);
                        bool isAuto = string.Equals(mode, "Auto", StringComparison.OrdinalIgnoreCase);
                        bool byTool = toolServices != null && toolServices.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
                        if (!isAuto && !byTool) continue;
                        if (Rx.M(path, @"\\Windows\\(System32|SysWOW64)\\[^\\]+\.exe") || Rx.M(path, @"Windows Defender|\\Microsoft\\|Microsoft Office|\\WindowsApps\\Microsoft")) continue;
                        string disp = Convert.ToString(s["DisplayName"]);
                        Add(items, KService, disp, s["State"] + " / " + mode, isAuto, path, null, name, name + " " + disp);
                    }
            }
            catch { }
            return items;
        }

        // Tác vụ của Task Scheduler (ngoài \Microsoft\) chạy khi đăng nhập hoặc khi bật máy.
        static void AddTasks(List<StartupItem> items)
        {
            dynamic svc = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
            svc.Connect();
            var stack = new Stack<dynamic>();
            stack.Push(svc.GetFolder("\\"));
            while (stack.Count > 0)
            {
                dynamic folder = stack.Pop();
                string fp = folder.Path;
                string taskPath = fp == "\\" ? "\\" : fp + "\\";
                if (taskPath.StartsWith("\\Microsoft\\", StringComparison.OrdinalIgnoreCase)) continue;
                try { foreach (dynamic sub in folder.GetFolders(0)) stack.Push(sub); } catch { }
                dynamic tasks;
                try { tasks = folder.GetTasks(1); } catch { continue; }
                foreach (dynamic t in tasks)
                {
                    try
                    {
                        dynamic def = t.Definition;
                        bool trig = false;
                        foreach (dynamic tr in def.Triggers) { int ty = tr.Type; if (ty == 9 || ty == 8) trig = true; }
                        if (!trig) continue;
                        var acts = new List<string>();
                        foreach (dynamic a in def.Actions)
                        {
                            string s = "";
                            if ((int)a.Type == 0) s = ((string)a.Path + " " + (string)a.Arguments).Trim();
                            acts.Add(s);
                        }
                        string exe = string.Join(" ; ", acts);
                        string name = t.Name;
                        Add(items, KTask, name, taskPath, (int)t.State != 1, exe, null, taskPath + "|" + name);
                    }
                    catch { }
                }
            }
        }

        [DllImport("advapi32.dll", SetLastError = true)] static extern IntPtr OpenSCManager(string machine, string db, int access);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr OpenService(IntPtr scm, string name, int access);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool ChangeServiceConfig(IntPtr svc, int type, int start, int error, string path, string group, IntPtr tag, string deps, string user, string pwd, string display);
        [DllImport("advapi32.dll")] static extern bool CloseServiceHandle(IntPtr h);

        static void SetServiceStart(string name, bool automatic)
        {
            const int NoChange = -1;
            IntPtr scm = OpenSCManager(null, null, 0x0001);
            if (scm == IntPtr.Zero) throw new Win32Exception();
            try
            {
                IntPtr s = OpenService(scm, name, 0x0002);
                if (s == IntPtr.Zero) throw new Win32Exception();
                try
                {
                    if (!ChangeServiceConfig(s, NoChange, automatic ? 2 : 3, NoChange, null, null, IntPtr.Zero, null, null, null, null)) throw new Win32Exception();
                }
                finally { CloseServiceHandle(s); }
            }
            finally { CloseServiceHandle(scm); }
        }

        public static void Set(StartupItem it, bool enable)
        {
            switch (it.Kind)
            {
                case KRun:
                case KFolder:
                    using (var k = OpenKey(it.ApKey, true))
                    {
                        var bytes = new byte[12];
                        bytes[0] = (byte)(enable ? 2 : 3);
                        BitConverter.GetBytes(DateTime.Now.ToFileTime()).CopyTo(bytes, 4);
                        k.SetValue(it.Id, bytes, RegistryValueKind.Binary);
                    }
                    break;
                case KTask:
                {
                    var parts = it.Id.Split(new[] { '|' }, 2);
                    dynamic svc = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
                    svc.Connect();
                    string fp = parts[0].Length > 1 ? parts[0].TrimEnd('\\') : "\\";
                    dynamic t = svc.GetFolder(fp).GetTask(parts[1]);
                    t.Enabled = enable;
                    break;
                }
                case KService:
                    SetServiceStart(it.Id, enable);
                    break;
            }
        }
    }
}
