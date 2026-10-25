// Trang Bảo mật, Sao lưu, Nhật ký và Cài đặt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static CleanVaiLon.DarkList;

namespace CleanVaiLon
{
    public class LogRow { public string Time, Text; }

    public partial class MainForm
    {
        // ===== Bảo mật =====
        Page pgSec; List<Button> chipSec; List<SecRow> SecData = new List<SecRow>();

        void BuildSecurity()
        {
            pgSec = NewPage("sec", "bảo mật", "Chỉ kiểm tra, không thay đổi gì. Gỡ extension trong trang quản lý extension của từng trình duyệt – mỗi hồ sơ (profile) một trang riêng.");
            Create(pgSec, new[]
            {
                new Col("mức", 110, r => ((SecRow)r).Level) { B = true },
                new Col("nhóm", 180, r => ((SecRow)r).Group),
                new Col("mục", 340, r => ((SecRow)r).Item),
                new Col("chi tiết", 520, r => ((SecRow)r).Detail) { M = true },
            }, false);
            Ui.NewBtn(pgSec.Act, "⟳ kiểm tra", (s, e) => LoadSec(), true);
            Ui.NewText(pgSec.Filter, "lọc:");
            chipSec = Ui.NewChipGroup(pgSec.Filter, new Dictionary<string, string>
            {
                ["all"] = "tất cả", ["risk"] = "cần xem", ["av"] = "diệt virus", ["proc"] = "tiến trình", ["net"] = "mạng", ["ext"] = "extension",
            }, k => RefreshSecList());
            pgSec.List.SelectedIndexChanged += (s, e) =>
            {
                var r = Selected<SecRow>(pgSec.List);
                if (r == null) return;
                ShowDetail(pgSec.Detail, new[] { P("Mức", r.Level), P("Nhóm", r.Group), P("Mục", r.Item), P("Chi tiết", r.Detail) });
            };
        }

        void RefreshSecList()
        {
            IEnumerable<SecRow> rows = SecData;
            switch (Ui.ChipKey(chipSec))
            {
                case "risk": rows = rows.Where(x => x.Level == "Cao" || x.Level == "Trung bình"); break;
                case "av": rows = rows.Where(x => x.Group == "Diệt virus" || x.Group == "Tường lửa"); break;
                case "proc": rows = rows.Where(x => x.Group == "Tiến trình"); break;
                case "net": rows = rows.Where(x => new[] { "Kết nối Internet", "Mở cổng chờ kết nối", "Mạng", "File hosts" }.Contains(x.Group)); break;
                case "ext": rows = rows.Where(x => x.Group == "Extension trình duyệt"); break;
            }
            var list = rows.ToList();
            Fill(pgSec.List, list, o => { var x = (SecRow)o; return x.Level == "Cao" ? "bad" : x.Level == "Trung bình" ? "warn" : null; });
            int hi = SecData.Count(x => x.Level == "Cao"), mid = SecData.Count(x => x.Level == "Trung bình");
            pgSec.SetTitle(list.Count, "[cao " + hi + " · cần xem " + mid + "]");
        }

        void LoadSec()
        {
            StartWork("Kiểm tra bảo mật", () => Security.Report(), (r, err) =>
            {
                if (!(r is List<SecRow> list)) { ShowInfo("Không kiểm tra được. " + err); return; }
                var order = new List<string> { "Cao", "Trung bình", "Thấp", "Thông tin" };
                SecData = list.OrderBy(x => order.IndexOf(x.Level)).ThenBy(x => x.Group, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.Item, StringComparer.CurrentCultureIgnoreCase).ToList();
                RefreshSecList();
                pgSec.Detail.Text = "Bấm một dòng để xem chi tiết.";
            });
        }

        // ===== Sao lưu =====
        Page pgBak;

        void BuildBackup()
        {
            pgBak = NewPage("bak", "sao lưu", "Máy đã được sao lưu chưa và ở đâu. Cách đơn giản nhất: bật File History lên một ổ cắm ngoài, và chép các thư mục quan trọng sang một ổ khác.");
            Create(pgBak, new[]
            {
                new Col("hình thức", 300, r => ((BackupRow)r).Item),
                new Col("trạng thái", 140, r => ((BackupRow)r).Status) { B = true },
                new Col("chi tiết", 640, r => ((BackupRow)r).Detail) { M = true },
            }, false);
            pgBak.Auto = LoadBackup;
            Ui.NewBtn(pgBak.Act, "chép thư mục…", (s, e) => CopyFolder(), true);
            Ui.NewBtn(pgBak.Act, "mở file history", (s, e) => Shell("control.exe", "/name Microsoft.FileHistory"));
            Ui.NewBtn(pgBak.Act, "⟳ kiểm tra", (s, e) => LoadBackup());
        }

        void LoadBackup()
        {
            StartWork("Kiểm tra sao lưu", () => Backup.Status(), (r, err) =>
            {
                if (!(r is List<BackupRow> list)) { ShowInfo("Không kiểm tra được. " + err); return; }
                Fill(pgBak.List, list, o => { var x = (BackupRow)o; return x.Status == "Chưa có" || x.Status == "Chưa bật" ? "bad" : null; });
                int has = list.Count(x => (x.Status == "Có" || x.Status == "Đã bật" || x.Status == "Đã đăng nhập") && !x.Item.StartsWith("Phần mềm", StringComparison.OrdinalIgnoreCase));
                pgBak.SetTitle(list.Count, has > 0 ? "[có " + has + " hình thức]" : "[chưa có sao lưu]");
                pgBak.Detail.Text = @"Mẹo: muốn File History sao lưu cả thư mục ngoài Documents (ví dụ E:\Works), chuột phải thư mục đó > Show more options > Include in library > Documents.";
            });
        }

        void CopyFolder()
        {
            string srcPath, dstPath;
            using (var src = new FolderBrowserDialog { Description = "Chọn thư mục cần sao lưu" })
            {
                if (src.ShowDialog(this) != DialogResult.OK) return;
                srcPath = src.SelectedPath;
            }
            using (var dst = new FolderBrowserDialog { Description = "Chọn nơi lưu bản sao (nên là ổ khác)" })
            {
                if (dst.ShowDialog(this) != DialogResult.OK) return;
                dstPath = dst.SelectedPath;
            }
            if (string.Equals(dstPath.Substring(0, 1), srcPath.Substring(0, 1), StringComparison.OrdinalIgnoreCase))
                if (!Confirm("Nơi lưu nằm cùng ổ với thư mục gốc – nếu ổ hỏng sẽ mất cả hai. Vẫn tiếp tục?")) return;
            if (!Confirm("Chép\r\n  " + srcPath + "\r\nsang\r\n  " + dstPath + "\r\n\r\nChỉ thêm và cập nhật file, không xoá gì ở nơi lưu.")) return;
            StartWork("Chép thư mục", () => Backup.CopyFolder(srcPath, dstPath), (r, err) =>
            {
                if (!(r is CopyResult c)) { ShowInfo("Có lỗi. " + err); return; }
                Log.Write("Sao lưu: " + c.Dest + " | mã " + c.Code);
                string head = c.Ok ? "Đã chép xong vào " + c.Dest + "." : "Chép có lỗi (mã " + c.Code + ").";
                ShowInfo(head + "\r\n\r\n" + c.Summary);
                LoadBackup();
            });
        }

        // ===== Nhật ký =====
        Page pgLog;

        void BuildLog()
        {
            pgLog = NewPage("log", "nhật ký", "Mọi thay đổi công cụ đã làm trên máy: tắt/bật khởi động, chuyển vào Thùng rác, gỡ ứng dụng, sao lưu.");
            Create(pgLog, new[]
            {
                new Col("thời gian", 170, r => ((LogRow)r).Time) { M = true },
                new Col("nội dung", 900, r => ((LogRow)r).Text),
            }, false);
            Ui.NewBtn(pgLog.Act, "mở bằng notepad", (s, e) =>
            {
                if (File.Exists(AppInfo.LogFile)) Shell("notepad.exe", "\"" + AppInfo.LogFile + "\""); else ShowInfo("Chưa có thao tác nào được ghi lại.");
            });
            Ui.NewBtn(pgLog.Act, "⟳ tải lại", (s, e) => LoadLog(), true);
            pgLog.Auto = LoadLog;
        }

        void LoadLog()
        {
            var rows = new List<LogRow>();
            try
            {
                if (File.Exists(AppInfo.LogFile))
                    foreach (var l in TextFiles.ReadLines(AppInfo.LogFile))
                        if (l.Length > 0) rows.Add(new LogRow { Time = l.Substring(0, Math.Min(19, l.Length)), Text = l.Length > 21 ? l.Substring(21) : "" });
            }
            catch { }
            rows.Reverse();
            Fill(pgLog.List, rows, null);
            pgLog.SetTitle(rows.Count, "");
            pgLog.Detail.Text = "File nhật ký: " + AppInfo.LogFile;
        }

        // ===== Cài đặt =====
        Page pgSet; readonly List<Panel> SetRows = new List<Panel>();

        // Một dòng cài đặt: nhãn bên trái, điều khiển bên phải.
        FlowLayoutPanel AddSettingRow(string label)
        {
            var row = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.Panel };
            row.Paint += (s, e) => e.Graphics.DrawLine(Theme.PenLine, 0, row.Height - 1, row.Width, row.Height - 1);
            var l = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.SettingLabel, BackColor = Theme.Panel, Padding = new Padding(14, 0, 0, 0) };
            var right = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Panel, Padding = new Padding(0, 7, 12, 0) };
            row.Controls.Add(l); row.Controls.Add(right);
            SetRows.Add(row);
            return right;
        }

        void BuildSettings()
        {
            pgSet = NewPage("set", "cài đặt", "Tuỳ chỉnh cách công cụ hoạt động. Thay đổi được lưu ngay.");
            pgSet.Detail.Parent.Parent.Visible = false;
            var verLbl = Ui.NewText(pgSet.Act, "v" + AppInfo.Version);
            verLbl.Font = Theme.Main; verLbl.Margin = new Padding(0, 8, 0, 0);
            var verTitle = Ui.NewText(pgSet.Act, "phiên bản");
            verTitle.Font = Theme.Main; verTitle.Margin = new Padding(8, 8, 6, 0);

            var setBox = new Panel { Dock = DockStyle.Top, BackColor = Theme.Panel, Padding = new Padding(1) };
            setBox.Paint += (s, e) => e.Graphics.DrawRectangle(Theme.PenGreenDim, 0, 0, setBox.Width - 1, setBox.Height - 1);

            var r = AddSettingRow("ngôn ngữ");
            var chipLang = Ui.NewChipGroup(r, new Dictionary<string, string> { ["vi"] = "VI", ["en"] = "EN" }, k => { Cfg.Lang = k; Cfg.Save(); ApplyLanguage(this); });
            Ui.SelectChip(chipLang, Cfg.Lang);

            r = AddSettingRow("khởi động cùng windows");
            Panel swLogon = null;
            bool logonOn = false; try { logonOn = LogonTask.Exists(); } catch { }
            swLogon = Ui.NewSwitch(r, logonOn, on =>
            {
                try { LogonTask.Set(on); }
                catch (Exception ex) { Ui.SetSwitch(swLogon, !on); ShowInfo("Không đổi được: " + (ex.InnerException ?? ex).Message + "\r\n\r\nCần mở công cụ với quyền quản trị."); }
            });

            r = AddSettingRow("trang mở đầu");
            var startOpts = new Dictionary<string, string>();
            foreach (var k in PageOrder) if (k != "set") startOpts[k] = Pages[k].NavVi;
            var chipStartPage = Ui.NewChipGroup(r, startOpts, k => { Cfg.StartPage = k; Cfg.Save(); });
            Ui.SelectChip(chipStartPage, Cfg.StartPage);

            r = AddSettingRow("tự tải dữ liệu khi mở trang");
            Ui.NewSwitch(r, Cfg.AutoLoad, on => { Cfg.AutoLoad = on; Cfg.Save(); });

            r = AddSettingRow("ngưỡng \"nặng\" ở trang hiệu năng (%)");
            StepperState stHeavy = null;
            stHeavy = Ui.NewStepper(r, Cfg.HeavyPct, 1, 50, () =>
            {
                Cfg.HeavyPct = stHeavy.Value; Cfg.Save();
                tglPerf.Text = Lang.Tr("chỉ hiện >") + " " + Cfg.HeavyPct + "%";
                RefreshPerfList();
            });

            r = AddSettingRow("số tháng \"không dùng\" mặc định");
            StepperState stMonths = null;
            stMonths = Ui.NewStepper(r, Cfg.Months, 1, 36, () => { Cfg.Months = stMonths.Value; Cfg.Save(); });

            r = AddSettingRow("hỏi lại lần 2 khi xoá mục \"không nên xoá\"");
            Ui.NewSwitch(r, Cfg.DoubleConfirm, on => { Cfg.DoubleConfirm = on; Cfg.Save(); });

            r = AddSettingRow("lưới nền");
            Ui.NewSwitch(r, Cfg.Grid, on => { Cfg.Grid = on; Cfg.Save(); content.Invalidate(true); });

            r = AddSettingRow("thư mục dữ liệu");
            var dataLbl = Ui.NewText(r, AppInfo.DataDir);
            dataLbl.Margin = new Padding(0, 5, 10, 0);
            var open = Ui.NewChipButton(r, "mở", new ChipTag());
            open.Click += (s, e) => { AppInfo.EnsureDataDir(); Shell("explorer.exe", "\"" + AppInfo.DataDir + "\""); };
            Ui.SetChipState(open, false);

            // Dòng thêm sau nằm trên, nên thêm ngược thứ tự để dòng đầu tiên ở trên cùng.
            for (int i = SetRows.Count - 1; i >= 0; i--) setBox.Controls.Add(SetRows[i]);
            setBox.Height = SetRows.Count * 40 + 2;

            var resetBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, BackColor = Color.Transparent, Padding = new Padding(0, 14, 0, 0) };
            var btnReset = Ui.NewBtn(resetBar, "↺ KHÔI PHỤC MẶC ĐỊNH", (s, e) =>
            {
                if (!Confirm("Đưa mọi cài đặt về mặc định? (Không ảnh hưởng tới các mục đã tắt khởi động hay đã xoá.)")) return;
                Cfg.ResetToDefaults();
                Cfg.Save();
                ShowInfo("Đã khôi phục mặc định. Mở lại công cụ để áp dụng hết.");
            });
            btnReset.Margin = new Padding(0);
            btnReset.ForeColor = Theme.Red; btnReset.FlatAppearance.BorderColor = Theme.Red;
            btnReset.FlatAppearance.MouseOverBackColor = Theme.BadBg; btnReset.Font = Theme.Bold;
            pgSet.Panel.Controls.Add(setBox); pgSet.Panel.Controls.Add(resetBar);
            setBox.BringToFront(); resetBar.BringToFront();
        }
    }
}
