// Các trang Hiệu năng, Khởi động, Ổ đĩa.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static CleanVaiLon.DarkList;

namespace CleanVaiLon
{
    public partial class MainForm
    {
        static string FmtSize(object v) { double d = Num(v); return d != 0 ? Fmt.Size(d) : ""; }
        static string FmtDate(object v) => v is DateTime d ? d.ToString("dd/MM/yyyy") : "";
        static string T(string vi, string en) => Lang.T(vi, en);
        static string D(string s) => Lang.D(s);

        static string RecycledMessage(double freed) =>
            string.Format(T("Đã chuyển {0} vào Thùng rác. Dọn Thùng rác để lấy lại dung lượng.", "Moved {0} to the Recycle Bin. Empty the Recycle Bin to get the space back."), Fmt.Size(freed));

        // Dòng giải thích "xoá được không" cho khung chi tiết, theo ngôn ngữ đang dùng.
        static string SafetyText(string path, bool isDir)
        {
            var s = Safety.Get(path);
            var lines = new List<string> { string.Format("{0,-14} {1} – {2}", T("xoá được?:", "deletable?:"), D(s.Label).ToUpper(), D(s.Why)) };
            if (!isDir) foreach (var x in Safety.FileFacts(path)) lines.Add(string.Format("{0,-14} {1}", T("lưu ý:", "note:"), D(x)));
            return string.Join("\r\n", lines);
        }

        // ===== Hiệu năng =====
        Page pgPerf; Button tglPerf; PerfSnapshot PerfData;

        void BuildPerf()
        {
            pgPerf = NewPage("perf", "hiệu năng", "Chương trình nào đang dùng CPU và RAM. Dòng vàng là chương trình vượt ngưỡng \"nặng\" (chỉnh trong cài đặt). Bấm một dòng để xem nó là gì.");
            Create(pgPerf, new[]
            {
                new Col("chương trình", 200, r => ((PerfRow)r).Name),
                new Col("tiến trình", 90, r => ((PerfRow)r).Count) { N = true },
                new Col("cpu %", 80, r => ((PerfRow)r).CpuPct) { N = true },
                new Col("ram", 100, r => ((PerfRow)r).MemMB) { N = true, F = v => Str(v) + " MB" },
                new Col("ram %", 80, r => ((PerfRow)r).MemPct) { N = true },
                new Col("là gì", 360, r => ((PerfRow)r).Explain),
                new Col("đường dẫn", 260, r => ((PerfRow)r).Path) { M = true },
            }, false);
            pgPerf.Auto = RunPerf;
            Ui.NewBtn(pgPerf.Act, "⟳ đo lại", (s, e) => RunPerf(), true);
            Ui.NewBtn(pgPerf.Act, "task manager", (s, e) => Shell("taskmgr.exe"));
            Ui.NewText(pgPerf.Filter, "lọc:");
            tglPerf = Ui.NewToggle(pgPerf.Filter, "chỉ hiện > " + Cfg.HeavyPct + "%", true, RefreshPerfList);
            pgPerf.List.SelectedIndexChanged += (s, e) =>
            {
                var r = Selected<PerfRow>(pgPerf.List);
                if (r == null) return;
                ShowDetail(pgPerf.Detail, new[] { P("Chương trình", r.Name), P("Là gì", r.Explain), P("Hãng", r.Company), P("Đường dẫn", r.Path) });
            };
            LangHooks.Add(() => tglPerf.Text = Lang.Tr("chỉ hiện >") + " " + Cfg.HeavyPct + "%");
            LangHooks.Add(() => { RefreshPerfList(); ShowPerfSummary(); });
        }

        void RefreshPerfList()
        {
            if (PerfData == null) return;
            var all = PerfData.Rows;
            double lim = Cfg.HeavyPct;
            var heavy = all.Where(r => r.CpuPct > lim || r.MemPct > lim).ToList();
            var rows = Ui.Chip(tglPerf).On ? heavy : all;
            Fill(pgPerf.List, rows, o => { var r = (PerfRow)o; return r.CpuPct > lim || r.MemPct > lim ? "warn" : null; });
            pgPerf.SetTitle(all.Count, "[" + T("nặng", "heavy") + " " + heavy.Count + "]");
        }

        void RunPerf()
        {
            StartWork("Đo hiệu năng", () => Perf.Snapshot(), (r, err) =>
            {
                if (!(r is PerfSnapshot d)) { ShowInfo(T("Không đo được. ", "Could not measure. ") + err); return; }
                PerfData = d;
                RefreshPerfList();
                ShowPerfSummary();
            });
        }

        void ShowPerfSummary()
        {
            var d = PerfData;
            if (d == null) return;
            double commitPct = d.LimitGB != 0 ? Math.Round(d.CommitGB / d.LimitGB * 100) : 0;
            string warn = commitPct >= 85 ? T("\r\n! bộ nhớ ảo gần đầy – máy dễ giật. Đóng bớt ứng dụng hoặc tăng pagefile.",
                                             "\r\n! virtual memory is nearly full – the PC may stutter. Close some apps or enlarge the pagefile.") : "";
            pgPerf.Detail.Text = string.Format(T("cpu đang bận:   {0}%\r\nram còn trống:  {1} / {2} GB\r\nbộ nhớ ảo:      {3} / {4} GB ({5}%){6}",
                                                 "cpu busy:       {0}%\r\nfree ram:       {1} / {2} GB\r\nvirtual memory: {3} / {4} GB ({5}%){6}"),
                d.CpuLoad, Math.Round(d.FreeMB / 1024, 1), Math.Round(d.TotalMB / 1024, 1), d.CommitGB, d.LimitGB, commitPct, warn);
        }

        // ===== Khởi động =====
        Page pgStart; List<Button> chipStart; List<StartupItem> StartData = new List<StartupItem>();

        void BuildStartup()
        {
            pgStart = NewPage("start", "khởi động", "Mọi thứ tự chạy khi bật máy. Tích chọn rồi bấm \"tắt\" – không xoá gì, bật lại được bất cứ lúc nào. Dịch vụ được chuyển sang \"chạy khi cần\".");
            Create(pgStart, new[]
            {
                new Col("tên", 220, r => ((StartupItem)r).Name),
                new Col("loại", 150, r => ((StartupItem)r).Kind) { B = true },
                new Col("trạng thái", 96, r => ((StartupItem)r).Enabled) { B = true, F = v => (bool)v ? "Đang bật" : "Đã tắt" },
                new Col("gợi ý", 116, r => ((StartupItem)r).Advice) { B = true },
                new Col("là gì", 270, r => ((StartupItem)r).Note),
                new Col("lệnh chạy", 300, r => ((StartupItem)r).Command) { M = true },
            }, true);
            pgStart.Auto = LoadStartup;
            var off = Ui.NewBtn(pgStart.Act, "tắt mục đã chọn", (s, e) => ApplyStartup(false), true);
            var on = Ui.NewBtn(pgStart.Act, "bật lại mục đã chọn", (s, e) => ApplyStartup(true));
            Ui.BindEnabled(off, () => pgStart.List.CheckedItems.Count > 0);
            Ui.BindEnabled(on, () => pgStart.List.CheckedItems.Count > 0);
            // Chỉ hiện nút hợp với các mục đã chọn: "tắt" khi có mục đang bật, "bật lại" khi có mục đã tắt.
            // Chưa chọn gì thì hiện cả hai (mờ) để bạn biết có hai thao tác.
            Ui.BindVisible(off, () => { var c = Checked<StartupItem>(pgStart.List); return c.Count == 0 || c.Any(x => x.Enabled); });
            Ui.BindVisible(on, () => { var c = Checked<StartupItem>(pgStart.List); return c.Count == 0 || c.Any(x => !x.Enabled); });
            Ui.NewBtn(pgStart.Act, "⟳ tải lại", (s, e) => LoadStartup());
            Ui.NewText(pgStart.Filter, "lọc:");
            chipStart = Ui.NewChipGroup(pgStart.Filter, new Dictionary<string, string>
            {
                ["all"] = "tất cả", ["on"] = "đang bật", ["off"] = "đã tắt", ["run"] = "chạy khi đăng nhập", ["task"] = "tác vụ", ["svc"] = "dịch vụ",
            }, k => RefreshStartupList());
            pgStart.List.SelectedIndexChanged += (s, e) =>
            {
                var r = Selected<StartupItem>(pgStart.List);
                if (r == null) return;
                ShowDetail(pgStart.Detail, new[] { P("Tên", r.Name), P("Loại", r.Kind), P("Phạm vi", r.Scope), P("Gợi ý", r.Advice), P("Là gì", r.Note), P("Lệnh chạy", r.Command) });
            };
            LangHooks.Add(() => { if (StartData.Count > 0) RefreshStartupList(); });
        }

        void RefreshStartupList()
        {
            IEnumerable<StartupItem> rows = StartData;
            switch (Ui.ChipKey(chipStart))
            {
                case "on": rows = rows.Where(x => x.Enabled); break;
                case "off": rows = rows.Where(x => !x.Enabled); break;
                case "run": rows = rows.Where(x => x.Kind == Startup.KRun || x.Kind == Startup.KFolder); break;
                case "task": rows = rows.Where(x => x.Kind == Startup.KTask); break;
                case "svc": rows = rows.Where(x => x.Kind == Startup.KService); break;
            }
            Fill(pgStart.List, rows.ToList(), o => { var x = (StartupItem)o; return !x.Enabled ? "dim" : x.Advice == "Cần xác nhận" ? "bad" : null; });
            pgStart.SetTitle(StartData.Count, "[" + T("đang bật", "enabled") + " " + StartData.Count(x => x.Enabled) + "]");
        }

        void LoadStartup()
        {
            var svc = ToolServices.Load();
            StartWork("Đọc danh sách khởi động", () => Startup.Items(svc), (r, err) =>
            {
                if (!(r is List<StartupItem> list)) { ShowInfo(T("Không đọc được. ", "Could not read. ") + err); return; }
                var order = new List<string> { "Cần xác nhận", "Có thể tắt", "Chưa rõ", "Nên giữ" };
                StartData = list.OrderBy(x => !x.Enabled).ThenBy(x => order.IndexOf(x.Advice)).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                RefreshStartupList();
            });
        }

        void ApplyStartup(bool enable)
        {
            // Chỉ làm với mục cần đổi: "tắt" bỏ qua mục đã tắt, "bật lại" bỏ qua mục đang bật.
            var sel = Checked<StartupItem>(pgStart.List).Where(x => x.Enabled != enable).ToList();
            if (sel.Count == 0) { ShowInfo(T("Bạn tích chọn ít nhất một mục trước.", "Tick at least one item first.")); return; }
            string verb = enable ? "bật lại" : "tắt";
            string list = string.Join("\r\n", sel.Select(x => "• " + x.Name + "  (" + D(x.Kind) + ")"));
            string ask = enable ? T("BẬT LẠI khởi động cùng máy cho {0} mục sau?", "RE-ENABLE starting with Windows for these {0} items?")
                                : T("TẮT khởi động cùng máy cho {0} mục sau?", "DISABLE starting with Windows for these {0} items?");
            if (!Confirm(string.Format(ask, sel.Count) + "\r\n\r\n" + list)) return;
            int ok = 0; var fail = new List<string>();
            var svc = ToolServices.Load();
            foreach (var it in sel)
            {
                try
                {
                    Startup.Set(it, enable);
                    ok++;
                    Log.Write("Khởi động – " + verb + " : " + it.Kind + " | " + it.Name);
                    if (it.Kind == Startup.KService) { if (enable) svc.Remove(it.Id); else if (!svc.Contains(it.Id)) svc.Add(it.Id); }
                }
                catch (Exception ex) { fail.Add("• " + it.Name + ": " + ex.Message); }
            }
            try { ToolServices.Save(svc); } catch { }
            string msg = string.Format(enable ? T("Đã bật lại {0} mục. Thay đổi có hiệu lực từ lần bật máy sau.", "Re-enabled {0} items. The change takes effect the next time Windows starts.")
                                              : T("Đã tắt {0} mục. Thay đổi có hiệu lực từ lần bật máy sau.", "Disabled {0} items. The change takes effect the next time Windows starts."), ok);
            if (fail.Count > 0) msg += T("\r\n\r\nKhông làm được:\r\n", "\r\n\r\nCould not change:\r\n") + string.Join("\r\n", fail);
            ShowInfo(msg);
            LoadStartup();
        }

        // ===== Ổ đĩa =====
        Page pgDisk; List<Button> chipDisk; readonly List<Button> driveToggles = new List<Button>();
        DiskScan DiskData; DupScan DupData;

        void BuildDisk()
        {
            pgDisk = NewPage("disk", "ổ đĩa", "Tìm thứ đang chiếm chỗ. Tích chọn rồi chuyển vào Thùng rác – mục quá lớn so với Thùng rác được bỏ qua để bạn tự quyết. Nhớ dọn Thùng rác để lấy lại dung lượng.");
            Create(pgDisk, new[]
            {
                new Col("đường dẫn", 460, r => ((DiskRow)r).Path),
                new Col("kích thước", 110, r => ((DiskRow)r).Size) { N = true, F = FmtSize },
                new Col("ngày sửa", 110, r => ((DiskRow)r).Modified) { F = FmtDate },
                new Col("xoá được?", 130, r => ((DiskRow)r).Safety) { B = true },
                new Col("ghi chú", 300, r => ((DiskRow)r).Note) { M = true },
            }, true);
            var btnRecycle = Ui.NewBtn(pgDisk.Act, "→ thùng rác", (s, e) => DiskRecycle(), true);
            Ui.BindEnabled(btnRecycle, () => pgDisk.List.CheckedItems.Count > 0);
            var btnOpen = Ui.NewBtn(pgDisk.Act, "mở vị trí", (s, e) => { var r = Selected<DiskRow>(pgDisk.List); if (r != null) OpenLocation(r.Path); });
            Ui.BindEnabled(btnOpen, () => pgDisk.List.SelectedItems.Count > 0);
            Ui.NewBtn(pgDisk.Act, "tìm ảnh trùng lặp", (s, e) =>
            {
                var dr = SelectedDrives();
                if (dr.Count == 0) { ShowInfo(T("Bạn chọn ít nhất một ổ.", "Pick at least one drive.")); return; }
                StartWork("Tìm ảnh trùng lặp", () => Disk.FindDuplicateImages(dr), (r, err) =>
                {
                    if (!(r is DupScan d)) { ShowInfo(T("Không quét được. ", "Could not scan. ") + err); return; }
                    DupData = d;
                    Ui.SelectChip(chipDisk, "Dups");
                    RefreshDiskList();
                });
            });
            Ui.NewBtn(pgDisk.Act, "⟳ quét dung lượng", (s, e) =>
            {
                var dr = SelectedDrives();
                if (dr.Count == 0) { ShowInfo(T("Bạn chọn ít nhất một ổ.", "Pick at least one drive.")); return; }
                StartWork("Quét ổ đĩa", () => Disk.Scan(dr), (r, err) =>
                {
                    if (!(r is DiskScan d)) { ShowInfo(T("Không quét được. ", "Could not scan. ") + err); return; }
                    DiskData = d;
                    if (Ui.ChipKey(chipDisk) == "Dups") Ui.SelectChip(chipDisk, "TopFiles");
                    RefreshDiskList();
                });
            }, true);
            pgDisk.SetTitle("–", "[chưa quét]");
            Ui.NewText(pgDisk.Filter, "ổ:");
            var drives = Disk.Drives();
            foreach (var d in drives)
            {
                var t = Ui.NewToggle(pgDisk.Filter, "", d.Size > 0, null);
                Ui.Chip(t).Drive = d.Drive;
                driveToggles.Add(t);
            }
            void DriveTexts() { for (int i = 0; i < drives.Count; i++) driveToggles[i].Text = drives[i].Drive + " " + Fmt.Size(drives[i].Free) + T(" trống", " free"); }
            DriveTexts();
            LangHooks.Add(() =>
            {
                DriveTexts();
                if (DiskData != null || DupData != null) RefreshDiskList();
                else pgDisk.SetTitle("–", T("[chưa quét]", "[not scanned]"));
            });
            Ui.NewText(pgDisk.Filter, "  xem:");
            chipDisk = Ui.NewChipGroup(pgDisk.Filter, new Dictionary<string, string>
            {
                ["TopFiles"] = "20 tệp lớn nhất", ["TopDirs"] = "20 thư mục lớn nhất", ["Installers"] = "bộ cài & file nén",
                ["OldDownloads"] = "tải xuống cũ", ["Caches"] = "bộ nhớ đệm & file tạm", ["Dups"] = "ảnh trùng lặp",
            }, k => RefreshDiskList());
            pgDisk.List.SelectedIndexChanged += (s, e) =>
            {
                var it = Selected<DiskRow>(pgDisk.List);
                if (it == null) return;
                ShowDetail(pgDisk.Detail, new[] { P("Đường dẫn", it.Path), P("Kích thước", it.Size), P("Ngày sửa", it.Modified), P("Ghi chú", it.Note) });
                pgDisk.Detail.Text += "\r\n" + SafetyText(it.Path, Directory.Exists(it.Path));
            };
            pgDisk.List.DoubleClick += (s, e) => { var r = Selected<DiskRow>(pgDisk.List); if (r != null) OpenLocation(r.Path); };
        }

        List<string> SelectedDrives() => driveToggles.Where(t => Ui.Chip(t).On).Select(t => Ui.Chip(t).Drive).ToList();

        void RefreshDiskList()
        {
            string key = Ui.ChipKey(chipDisk);
            var rows = new List<DiskRow>();
            if (key == "Dups") { if (DupData != null) rows = DupData.Rows; }
            else if (DiskData != null) rows = DiskData.View(key);
            foreach (var x in rows) if (x.Safety == null) x.Safety = Safety.Get(x.Path).Label;
            Fill(pgDisk.List, rows, o => ((DiskRow)o).Note != null && ((DiskRow)o).Note.StartsWith("Ở ổ khác", StringComparison.OrdinalIgnoreCase) ? "dim" : null);
            double total = rows.Sum(x => x.Size);
            pgDisk.SetTitle(rows.Count, "[" + Fmt.Size(total) + "]");
            if (key == "Dups" && DupData == null) pgDisk.Detail.Text = T("Bấm \"tìm ảnh trùng lặp\" để quét.", "Click \"find duplicate images\" to scan.");
            else if (key != "Dups" && DiskData == null) pgDisk.Detail.Text = T("Bấm \"quét dung lượng\" để bắt đầu.", "Click \"scan space\" to start.");
            else if (key == "Dups") pgDisk.Detail.Text = string.Format(T("Đã so sánh {0} ảnh. Bản được giữ là bản cũ nhất. Dòng mờ là bản ở ổ khác – có thể là bản sao lưu, nên giữ.",
                                                                       "Compared {0} images. The oldest copy is the one kept. Dimmed rows are on another drive – they may be backups, so keep them."), DupData.Scanned);
            else pgDisk.Detail.Text = string.Format(T("{0} mục, tổng {1}. Bấm đúp một dòng để mở vị trí.", "{0} items, {1} in total. Double-click a row to open its location."), rows.Count, Fmt.Size(total));
        }

        void DiskRecycle()
        {
            var sel = Checked<DiskRow>(pgDisk.List);
            if (sel.Count == 0) { ShowInfo(T("Bạn tích chọn ít nhất một mục trước.", "Tick at least one item first.")); return; }
            if (!ConfirmRecycle(sel.Select(x => Tuple.Create(x.Path, x.Size)))) return;
            var payload = sel.Select(x => Tuple.Create(x.Path, x.Mode)).ToList();
            StartWork("Chuyển vào Thùng rác", () => payload.Select(x => Recycle.Move(x.Item1, x.Item2)).ToList(), (r, err) =>
            {
                if (!(r is List<RecycleResult> res)) { ShowInfo(T("Có lỗi. ", "Something went wrong. ") + err); return; }
                double freed = 0; var lines = new List<string>();
                foreach (var x in res)
                {
                    Log.Write("Thùng rác – " + x.Result + ": " + x.Path + " | " + x.Detail);
                    freed += x.Freed;
                    lines.Add("• [" + D(x.Result) + "] " + x.Path + " – " + D(x.Detail));
                }
                var done = new HashSet<string>(res.Where(x => x.Result == "Đã chuyển" || x.Result == "Không còn").Select(x => x.Path), StringComparer.OrdinalIgnoreCase);
                foreach (var li in pgDisk.List.Items.Cast<ListViewItem>().ToList())
                    if (li.Tag is DiskRow d && done.Contains(d.Path) && d.Mode == "Item") pgDisk.List.Items.Remove(li);
                ShowInfo(RecycledMessage(freed) + "\r\n\r\n" + string.Join("\r\n", lines.Take(30)));
            });
        }
    }
}
