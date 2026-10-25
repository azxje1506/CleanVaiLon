// Trang Phân tích (bản đồ dung lượng kiểu WizTree) và Ứng dụng.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static CleanVaiLon.DarkList;

namespace CleanVaiLon
{
    public class AnRow
    {
        public string Name, Type, Safety, Path, Mode = "Item";
        public double Pct, Size;
        public int Files;
        public DNode Node;
    }

    public partial class MainForm
    {
        // ===== Phân tích =====
        Page pgAn; Treemap treemap; List<Button> chipAn;
        DNode AnRoot, AnCur, AnSel; string AnTarget;

        void BuildAnalyzer()
        {
            pgAn = NewPage("an", "phân tích", "Bản đồ dung lượng: ô càng to càng chiếm nhiều chỗ. Bấm để chọn, bấm đúp vào thư mục để đi vào trong. Màu theo loại tệp, xem chú thích bên dưới.");
            Create(pgAn, new[]
            {
                new Col("tên", 300, r => ((AnRow)r).Name),
                new Col("% thư mục cha", 140, r => ((AnRow)r).Pct) { N = true, Bar = true, F = v => string.Format("{0:N1} %", Num(v)) },
                new Col("dung lượng", 110, r => ((AnRow)r).Size) { N = true, F = FmtSize },
                new Col("số tệp", 90, r => ((AnRow)r).Files) { N = true, F = v => string.Format("{0:N0}", Num(v)) },
                new Col("xoá được?", 130, r => ((AnRow)r).Safety) { B = true },
                new Col("loại", 120, r => ((AnRow)r).Type) { M = true },
            }, true);
            pgAn.ListHost.Dock = DockStyle.Top; pgAn.ListHost.Height = 230;
            var split = new Splitter { Dock = DockStyle.Top, Height = 6, BackColor = Theme.Bg, MinSize = 90 };
            split.Paint += (s, e) => e.Graphics.DrawLine(Theme.PenGreenDim, 0, 3, split.Width, 3);
            treemap = new Treemap { Dock = DockStyle.Fill, BackColor = Theme.Bg, Font = Theme.Small, Line = Theme.Line, Header = Theme.Text };
            pgAn.Panel.Controls.Add(split); pgAn.Panel.Controls.Add(treemap);
            // Thứ tự xếp: danh sách ở trên, thanh kéo ở giữa, bản đồ chiếm phần còn lại.
            pgAn.ListHost.BringToFront(); split.BringToFront(); treemap.BringToFront();

            var drives = Disk.Drives();
            AnTarget = (drives.FirstOrDefault()?.Drive ?? "C:") + "\\";
            treemap.NodeClicked += SelectAnNode;
            treemap.NodeOpened += OpenAnNode;
            treemap.NodeHover += n => { if (AnSel == null) ShowAnDetail(n); };
            pgAn.List.SelectedIndexChanged += (s, e) =>
            {
                var r = Selected<AnRow>(pgAn.List);
                if (r == null) return;
                if (r.Node != AnSel) { AnSel = r.Node; treemap.Selected = r.Node; treemap.Invalidate(); ShowAnDetail(r.Node); }
            };
            pgAn.List.DoubleClick += (s, e) => { var r = Selected<AnRow>(pgAn.List); if (r != null) OpenAnNode(r.Node); };

            var btnRecycle = Ui.NewBtn(pgAn.Act, "→ thùng rác", (s, e) => AnRecycle(), true);
            Ui.BindEnabled(btnRecycle, () => AnTargets().Count > 0);
            // Không cho tích dòng "[N tệp nhỏ]": đường dẫn của nó là cả thư mục cha.
            pgAn.List.ItemCheck += (s, e) => { if (pgAn.List.Items[e.Index].Tag is AnRow r && r.Node.Ext == "*") e.NewValue = CheckState.Unchecked; };
            var btnOpen = Ui.NewBtn(pgAn.Act, "mở vị trí", (s, e) => { if (AnSel != null) OpenLocation(AnSel.Path); });
            Ui.BindEnabled(btnOpen, () => AnSel != null);
            var btnUp = Ui.NewBtn(pgAn.Act, "↑ lên trên", (s, e) => { if (AnCur?.Parent != null) { var c = AnCur; ShowAnFolder(c.Parent); SelectAnNode(c); } });
            Ui.BindEnabled(btnUp, () => AnCur != null && AnCur.Parent != null);
            Ui.NewBtn(pgAn.Act, "⟳ quét", (s, e) => StartAnScan());
            Ui.NewText(pgAn.Filter, "quét:");
            var anDrives = new Dictionary<string, string>();
            foreach (var d in drives) anDrives[d.Drive + "\\"] = d.Drive + " " + Fmt.Size(d.Size - d.Free) + " đã dùng";
            chipAn = Ui.NewChipGroup(pgAn.Filter, anDrives, k => { AnTarget = k; StartAnScan(); });
            var pick = Ui.NewChipButton(pgAn.Filter, "chọn thư mục…", new ChipTag());
            Ui.SetChipState(pick, false);
            pick.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog { Description = "Chọn thư mục cần phân tích" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    foreach (var o in chipAn) Ui.SetChipState(o, false);
                    AnTarget = dlg.SelectedPath;
                    StartAnScan();
                }
            };
            var check = Ui.NewChipButton(pgAn.Filter, "kiểm tra một tệp…", new ChipTag());
            Ui.SetChipState(check, false);
            check.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog { Title = "Chọn tệp cần kiểm tra xoá được không", Filter = "Mọi tệp (*.*)|*.*" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    string p = dlg.FileName;
                    long size = 0; try { size = new FileInfo(p).Length; } catch { }
                    ShowInfo(string.Format("{0}\r\n{1}\r\n\r\n{2}", p, Fmt.Size(size), Safety.FormatText(p, false)));
                }
            };
            Ui.NewText(pgAn.Filter, "   màu:");
            var legend = new[]
            {
                Tuple.Create("video", 224, 123, 36), Tuple.Create("ảnh", 155, 93, 229), Tuple.Create("âm thanh", 58, 134, 255),
                Tuple.Create("nén / bộ cài", 229, 70, 111), Tuple.Create("chương trình", 46, 196, 182), Tuple.Create("dữ liệu game", 212, 194, 28),
                Tuple.Create("tài liệu", 95, 208, 104), Tuple.Create("khác", 110, 128, 118),
            };
            foreach (var lg in legend) Ui.NewText(pgAn.Filter, "■ " + lg.Item1).ForeColor = Color.FromArgb(lg.Item2, lg.Item3, lg.Item4);
            pgAn.SetTitle("–", "[chưa quét – chọn một ổ để bắt đầu]");
        }

        void ShowAnDetail(DNode n)
        {
            if (n == null) { pgAn.Detail.Text = ""; return; }
            double pctRoot = AnRoot != null && AnRoot.Size != 0 ? (double)n.Size / AnRoot.Size * 100 : 0;
            string kind = n.IsDir ? "thư mục – bấm đúp để đi vào" : n.Ext == "*" ? "các tệp dưới 1 MB gộp lại" : "tệp " + n.Ext;
            string txt = string.Format("{0,-14} {1}\r\n{2,-14} {3}  ({4:N1}% của {5})\r\n{6,-14} {7:N0}\r\n{8,-14} {9}",
                "đường dẫn:", n.Path, "dung lượng:", Fmt.Size(n.Size), pctRoot, AnRoot?.Path, "số tệp:", n.Files, "loại:", kind);
            if (n.Ext != "*") txt += "\r\n" + Safety.FormatText(n.Path, n.IsDir);
            pgAn.Detail.Text = txt;
        }

        void ShowAnFolder(DNode node)
        {
            AnCur = node; AnSel = null;
            var rows = node.Kids.Take(400).Select(k => new AnRow
            {
                Name = k.IsDir ? k.Name + "\\" : k.Name,
                Pct = node.Size != 0 ? Math.Round((double)k.Size / node.Size * 100, 1) : 0,
                Size = k.Size, Files = k.Files,
                Type = k.IsDir ? "thư mục" : k.Ext == "*" ? "tệp nhỏ gộp lại" : k.Ext,
                Safety = k.Ext == "*" ? "" : Safety.Get(k.Path).Label,
                Node = k, Path = k.Path,
            }).ToList();
            Fill(pgAn.List, rows, o => ((AnRow)o).Node.Ext == "*" ? "dim" : null);
            treemap.Root = node; treemap.Selected = null; treemap.Invalidate();
            pgAn.SetTitle(Fmt.Size(node.Size), string.Format("[{0:N0} tệp]", node.Files));
            ShowAnDetail(node);
            Ui.UpdateBound();
        }

        void SelectAnNode(DNode n)
        {
            AnSel = n;
            treemap.Selected = n; treemap.Invalidate();
            foreach (ListViewItem li in pgAn.List.Items)
            {
                if (((AnRow)li.Tag).Node == n) { if (!li.Selected) { li.Selected = true; li.EnsureVisible(); } }
                else if (li.Selected) li.Selected = false;
            }
            ShowAnDetail(n);
            Ui.UpdateBound();
        }

        void OpenAnNode(DNode n)
        {
            if (n != null && n.IsDir && n.Kids.Count > 0) ShowAnFolder(n); else SelectAnNode(n);
        }

        void StartAnScan()
        {
            string target = AnTarget;
            StartWork("Phân tích dung lượng", () => DiskTree.Scan(target), (r, err) =>
            {
                if (!(r is DNode root)) { ShowInfo("Không quét được. " + err); return; }
                AnRoot = root;
                ShowAnFolder(root);
            }, () => string.Format("{0:N0} tệp · {1}", DiskTree.Scanned, DiskTree.Current));
        }

        // Các mục sẽ xoá: những dòng đã tích; nếu chưa tích dòng nào thì lấy mục đang chọn trên bản đồ.
        List<DNode> AnTargets()
        {
            var nodes = pgAn.List.CheckedItems.Cast<ListViewItem>().Select(i => ((AnRow)i.Tag).Node).ToList();
            if (nodes.Count == 0 && AnSel != null) nodes.Add(AnSel);
            return nodes.Where(n => n != null && n.Ext != "*" && n.Parent != null).ToList();
        }

        void AnRecycle()
        {
            var nodes = AnTargets();
            if (nodes.Count == 0) return;
            if (!ConfirmRecycle(nodes.Select(n => Tuple.Create(n.Path, (double)n.Size)))) return;
            var paths = nodes.Select(n => n.Path).ToList();
            StartWork("Chuyển vào Thùng rác", () => paths.Select(p => Recycle.Move(p, "Item")).ToList(), (r, err) =>
            {
                if (!(r is List<RecycleResult> res)) { ShowInfo("Có lỗi. " + err); return; }
                double freed = 0; var lines = new List<string>();
                foreach (var x in res)
                {
                    Log.Write("Thùng rác – " + x.Result + ": " + x.Path + " | " + x.Detail);
                    lines.Add("• [" + x.Result + "] " + x.Path + " – " + x.Detail);
                    if (x.Result != "Đã chuyển") continue;
                    freed += x.Freed;
                    // Bỏ mục đã xoá khỏi cây và trừ dung lượng ở các thư mục cha.
                    var n = nodes.FirstOrDefault(z => string.Equals(z.Path, x.Path, StringComparison.OrdinalIgnoreCase));
                    if (n == null) continue;
                    n.Parent.Kids.Remove(n);
                    for (var p = n.Parent; p != null; p = p.Parent) { p.Size -= n.Size; p.Files -= n.Files; }
                }
                ShowAnFolder(AnCur);
                ShowInfo("Đã chuyển " + Fmt.Size(freed) + " vào Thùng rác. Dọn Thùng rác để lấy lại dung lượng.\r\n\r\n" + string.Join("\r\n", lines.Take(30)));
            });
        }

        // ===== Ứng dụng =====
        Page pgApps; Button tglUnused; StepperState numMonths; List<Button> chipApps; List<AppRow> AppsData;

        void BuildApps()
        {
            pgApps = NewPage("apps", "ứng dụng", "Ứng dụng đã cài và lần dùng cuối. Dữ liệu Steam là chính xác; ứng dụng khác dựa vào lịch sử chạy của Windows nên chỉ biết được từ một mốc nhất định.");
            Create(pgApps, new[]
            {
                new Col("ứng dụng", 290, r => ((AppRow)r).Name),
                new Col("dung lượng", 110, r => ((AppRow)r).Size) { N = true, F = FmtSize },
                new Col("dùng lần cuối", 120, r => ((AppRow)r).LastUsed) { F = FmtDate },
                new Col("loại", 170, r => ((AppRow)r).Kind) { B = true },
                new Col("nguồn dữ liệu", 230, r => ((AppRow)r).Source) { M = true },
                new Col("nhà phát hành", 200, r => ((AppRow)r).Publisher) { M = true },
            }, true);
            var btnUn = Ui.NewBtn(pgApps.Act, "gỡ mục đã chọn", (s, e) => UninstallSelected(), true);
            Ui.BindEnabled(btnUn, () => pgApps.List.CheckedItems.Count > 0);
            var btnDir = Ui.NewBtn(pgApps.Act, "mở thư mục cài", (s, e) => { var a = Selected<AppRow>(pgApps.List); if (a != null) OpenLocation(a.Location); });
            Ui.BindEnabled(btnDir, () => !string.IsNullOrEmpty(Selected<AppRow>(pgApps.List)?.Location));
            Ui.NewBtn(pgApps.Act, "⟳ phân tích", (s, e) => LoadApps());
            Ui.NewText(pgApps.Filter, "lọc:");
            tglUnused = Ui.NewToggle(pgApps.Filter, "không dùng trong", true, RefreshAppsList);
            numMonths = Ui.NewStepper(pgApps.Filter, Cfg.Months, 1, 36, RefreshAppsList);
            Ui.NewText(pgApps.Filter, "tháng  ·");
            chipApps = Ui.NewChipGroup(pgApps.Filter, new Dictionary<string, string> { ["all"] = "tất cả", ["app"] = "ứng dụng", ["game"] = "game steam", ["sys"] = "hệ thống" }, k => RefreshAppsList());
            pgApps.Auto = LoadApps;
            pgApps.List.SelectedIndexChanged += (s, e) =>
            {
                var a = Selected<AppRow>(pgApps.List);
                if (a == null) return;
                ShowDetail(pgApps.Detail, new[] { P("Ứng dụng", a.Name), P("Phiên bản", a.Version), P("Hãng", a.Publisher), P("Dung lượng", a.Size), P("Dùng lần cuối", a.LastUsed), P("Nguồn", a.Source), P("Thư mục cài", a.Location), P("Lệnh gỡ", a.Uninstall) });
            };
        }

        void RefreshAppsList()
        {
            if (AppsData == null) return;
            var cut = DateTime.Now.AddMonths(-numMonths.Value);
            IEnumerable<AppRow> rows = AppsData;
            if (Ui.Chip(tglUnused).On) rows = rows.Where(x => !x.LastUsed.HasValue || x.LastUsed < cut);
            switch (Ui.ChipKey(chipApps))
            {
                case "app": rows = rows.Where(x => x.Kind == Apps.KApp); break;
                case "game": rows = rows.Where(x => x.Kind == Apps.KGame); break;
                case "sys": rows = rows.Where(x => x.Kind == Apps.KSystem); break;
            }
            var list = rows.OrderBy(x => x.Kind == Apps.KSystem).ThenByDescending(x => x.Size).ToList();
            Fill(pgApps.List, list, o => ((AppRow)o).Kind == Apps.KSystem ? "dim" : null);
            int old = AppsData.Count(x => x.Kind != Apps.KSystem && (!x.LastUsed.HasValue || x.LastUsed < cut));
            pgApps.SetTitle(list.Count, "[lâu không dùng " + old + "]");
            pgApps.Detail.Text = "Tích chọn rồi bấm \"gỡ mục đã chọn\". Trình gỡ của từng ứng dụng sẽ hiện ra để bạn xác nhận. Dòng mờ là thành phần hệ thống – nên giữ.";
        }

        void LoadApps()
        {
            StartWork("Phân tích ứng dụng", () => Apps.Usage(), (r, err) =>
            {
                if (!(r is List<AppRow> list)) { ShowInfo("Không đọc được. " + err); return; }
                AppsData = list;
                RefreshAppsList();
            });
        }

        void UninstallSelected()
        {
            var sel = Checked<AppRow>(pgApps.List);
            if (sel.Count == 0) { ShowInfo("Bạn tích chọn ít nhất một ứng dụng trước."); return; }
            int sys = sel.Count(x => x.Kind == Apps.KSystem);
            string warn = sys > 0 ? "\r\n\r\n! Có " + sys + " mục là thành phần hệ thống, gỡ có thể làm game hoặc phần mềm khác lỗi." : "";
            if (!Confirm("Gỡ " + sel.Count + " ứng dụng sau? Mỗi trình gỡ sẽ hiện cửa sổ để bạn xác nhận.\r\n\r\n" + string.Join("\r\n", sel.Select(x => "• " + x.Name)) + warn)) return;
            StartWork("Gỡ ứng dụng", () => sel.Select(Apps.Uninstall).ToList(), (r, err) =>
            {
                if (!(r is List<UninstallResult> res)) { ShowInfo("Có lỗi. " + err); return; }
                foreach (var x in res) Log.Write("Gỡ – " + x.Result + ": " + x.Name + " | " + x.Detail);
                ShowInfo("Kết quả:\r\n\r\n" + string.Join("\r\n", res.Select(x => "• [" + x.Result + "] " + x.Name + " " + x.Detail)));
                LoadApps();
            });
        }
    }
}
