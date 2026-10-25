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
        }

        void RefreshPerfList()
        {
            if (PerfData == null) return;
            var all = PerfData.Rows;
            double lim = Cfg.HeavyPct;
            var heavy = all.Where(r => r.CpuPct > lim || r.MemPct > lim).ToList();
            var rows = Ui.Chip(tglPerf).On ? heavy : all;
            Fill(pgPerf.List, rows, o => { var r = (PerfRow)o; return r.CpuPct > lim || r.MemPct > lim ? "warn" : null; });
            pgPerf.SetTitle(all.Count, "[nặng " + heavy.Count + "]");
        }

        void RunPerf()
        {
            StartWork("Đo hiệu năng", () => Perf.Snapshot(), (r, err) =>
            {
                if (!(r is PerfSnapshot d)) { ShowInfo("Không đo được. " + err); return; }
                PerfData = d;
                RefreshPerfList();
                double commitPct = d.LimitGB != 0 ? Math.Round(d.CommitGB / d.LimitGB * 100) : 0;
                string warn = commitPct >= 85 ? "\r\n! bộ nhớ ảo gần đầy – máy dễ giật. Đóng bớt ứng dụng hoặc tăng pagefile." : "";
                pgPerf.Detail.Text = string.Format("cpu đang bận:   {0}%\r\nram còn trống:  {1} / {2} GB\r\nbộ nhớ ảo:      {3} / {4} GB ({5}%){6}",
                    d.CpuLoad, Math.Round(d.FreeMB / 1024, 1), Math.Round(d.TotalMB / 1024, 1), d.CommitGB, d.LimitGB, commitPct, warn);
            });
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
            pgStart.SetTitle(StartData.Count, "[đang bật " + StartData.Count(x => x.Enabled) + "]");
        }

        void LoadStartup()
        {
            var svc = ToolServices.Load();
            StartWork("Đọc danh sách khởi động", () => Startup.Items(svc), (r, err) =>
            {
                if (!(r is List<StartupItem> list)) { ShowInfo("Không đọc được. " + err); return; }
                var order = new List<string> { "Cần xác nhận", "Có thể tắt", "Chưa rõ", "Nên giữ" };
                StartData = list.OrderBy(x => !x.Enabled).ThenBy(x => order.IndexOf(x.Advice)).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                RefreshStartupList();
            });
        }

        void ApplyStartup(bool enable)
        {
            var sel = Checked<StartupItem>(pgStart.List);
            if (sel.Count == 0) { ShowInfo("Bạn tích chọn ít nhất một mục trước."); return; }
            string verb = enable ? "bật lại" : "tắt";
            if (!Confirm(verb.ToUpper() + " khởi động cùng máy cho " + sel.Count + " mục sau?\r\n\r\n" + string.Join("\r\n", sel.Select(x => "• " + x.Name + "  (" + x.Kind + ")")))) return;
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
            string msg = "Đã " + verb + " " + ok + " mục. Thay đổi có hiệu lực từ lần bật máy sau.";
            if (fail.Count > 0) msg += "\r\n\r\nKhông làm được:\r\n" + string.Join("\r\n", fail);
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
                if (dr.Count == 0) { ShowInfo("Bạn chọn ít nhất một ổ."); return; }
                StartWork("Tìm ảnh trùng lặp", () => Disk.FindDuplicateImages(dr), (r, err) =>
                {
                    if (!(r is DupScan d)) { ShowInfo("Không quét được. " + err); return; }
                    DupData = d;
                    Ui.SelectChip(chipDisk, "Dups");
                    RefreshDiskList();
                });
            });
            Ui.NewBtn(pgDisk.Act, "⟳ quét dung lượng", (s, e) =>
            {
                var dr = SelectedDrives();
                if (dr.Count == 0) { ShowInfo("Bạn chọn ít nhất một ổ."); return; }
                StartWork("Quét ổ đĩa", () => Disk.Scan(dr), (r, err) =>
                {
                    if (!(r is DiskScan d)) { ShowInfo("Không quét được. " + err); return; }
                    DiskData = d;
                    if (Ui.ChipKey(chipDisk) == "Dups") Ui.SelectChip(chipDisk, "TopFiles");
                    RefreshDiskList();
                });
            }, true);
            pgDisk.SetTitle("–", "[chưa quét]");
            Ui.NewText(pgDisk.Filter, "ổ:");
            foreach (var d in Disk.Drives())
            {
                var t = Ui.NewToggle(pgDisk.Filter, d.Drive + " " + Fmt.Size(d.Free) + " trống", d.Size > 0, null);
                Ui.Chip(t).Drive = d.Drive;
                driveToggles.Add(t);
            }
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
                pgDisk.Detail.Text += "\r\n" + Safety.FormatText(it.Path, Directory.Exists(it.Path));
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
            if (key == "Dups" && DupData == null) pgDisk.Detail.Text = "Bấm \"tìm ảnh trùng lặp\" để quét.";
            else if (key != "Dups" && DiskData == null) pgDisk.Detail.Text = "Bấm \"quét dung lượng\" để bắt đầu.";
            else if (key == "Dups") pgDisk.Detail.Text = "Đã so sánh " + DupData.Scanned + " ảnh. Bản được giữ là bản cũ nhất. Dòng mờ là bản ở ổ khác – có thể là bản sao lưu, nên giữ.";
            else pgDisk.Detail.Text = rows.Count + " mục, tổng " + Fmt.Size(total) + ". Bấm đúp một dòng để mở vị trí.";
        }

        void DiskRecycle()
        {
            var sel = Checked<DiskRow>(pgDisk.List);
            if (sel.Count == 0) { ShowInfo("Bạn tích chọn ít nhất một mục trước."); return; }
            if (!ConfirmRecycle(sel.Select(x => Tuple.Create(x.Path, x.Size)))) return;
            var payload = sel.Select(x => Tuple.Create(x.Path, x.Mode)).ToList();
            StartWork("Chuyển vào Thùng rác", () => payload.Select(x => Recycle.Move(x.Item1, x.Item2)).ToList(), (r, err) =>
            {
                if (!(r is List<RecycleResult> res)) { ShowInfo("Có lỗi. " + err); return; }
                double freed = 0; var lines = new List<string>();
                foreach (var x in res)
                {
                    Log.Write("Thùng rác – " + x.Result + ": " + x.Path + " | " + x.Detail);
                    freed += x.Freed;
                    lines.Add("• [" + x.Result + "] " + x.Path + " – " + x.Detail);
                }
                var done = new HashSet<string>(res.Where(x => x.Result == "Đã chuyển" || x.Result == "Không còn").Select(x => x.Path), StringComparer.OrdinalIgnoreCase);
                foreach (var li in pgDisk.List.Items.Cast<ListViewItem>().ToList())
                    if (li.Tag is DiskRow d && done.Contains(d.Path) && d.Mode == "Item") pgDisk.List.Items.Remove(li);
                ShowInfo("Đã chuyển " + Fmt.Size(freed) + " vào Thùng rác. Dọn Thùng rác để lấy lại dung lượng.\r\n\r\n" + string.Join("\r\n", lines.Take(30)));
            });
        }
    }
}
