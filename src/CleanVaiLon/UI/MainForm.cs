// Cửa sổ chính – phong cách terminal tối, không dùng thanh tiêu đề của Windows.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace CleanVaiLon
{
    public partial class MainForm : BorderlessForm
    {
        readonly Settings Cfg;
        readonly bool IsAdmin, StartMinimized;
        readonly Panel content, side, top, winButtons, navFlow, sideStatus;
        readonly Label logo, adminTag, lblState, lblJob, btnMax;
        readonly Dictionary<string, Page> Pages = new Dictionary<string, Page>();
        readonly List<string> PageOrder = new List<string>();
        readonly List<Label> NavItems = new List<Label>();
        readonly List<Action> LangHooks = new List<Action>();
        string CurrentPage;

        public MainForm(Settings cfg, bool isAdmin, bool startMinimized)
        {
            Cfg = cfg; IsAdmin = isAdmin; StartMinimized = startMinimized;
            Text = "Clean VaiLon";
            Size = new Size(1240, 780);
            MinimumSize = new Size(960, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Side;
            ForeColor = Theme.Text;
            Font = Theme.Main;
            Padding = new Padding(1);
            Grip = 6;
            HandleCreated += (s, e) => Ui.SetDarkTitle(this);
            Icon = Program.AppIcon;
            Paint += (s, e) => { if (WindowState == FormWindowState.Normal) e.Graphics.DrawRectangle(Theme.PenLine, 0, 0, Width - 1, Height - 1); };
            Resize += (s, e) =>
            {
                Padding = WindowState == FormWindowState.Maximized ? new Padding(0) : new Padding(1);
                if (btnMax != null) btnMax.Text = WindowState == FormWindowState.Maximized ? Theme.GlyphRestore : Theme.GlyphMax;
                Invalidate();
            };

            content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            Ui.SetDoubleBuffer(content);
            ApplyGrid();

            side = new Panel { Dock = DockStyle.Left, Width = 196, BackColor = Theme.Side };
            side.Paint += (s, e) => e.Graphics.DrawLine(Theme.PenLine, side.Width - 1, 0, side.Width - 1, side.Height);

            top = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.Side };
            top.Paint += (s, e) => e.Graphics.DrawLine(Theme.PenLine, 0, top.Height - 1, top.Width, top.Height - 1);
            logo = new Label { Text = ">_ CLEAN VAILON", Font = Theme.Logo, ForeColor = Theme.Green, AutoSize = true, Location = new Point(16, 13), BackColor = Theme.Side };
            adminTag = new Label
            {
                Text = IsAdmin ? "QUẢN TRỊ" : "KHÔNG CÓ QUYỀN QUẢN TRỊ", Font = Theme.Small, AutoSize = true, BackColor = Theme.Side,
                ForeColor = IsAdmin ? Theme.Green : Theme.Amber, Padding = new Padding(6, 3, 6, 3), Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            adminTag.Paint += (s, e) => { using (var p = new Pen(adminTag.ForeColor)) e.Graphics.DrawRectangle(p, 0, 0, adminTag.Width - 1, adminTag.Height - 1); };
            winButtons = new Panel { Dock = DockStyle.Right, Width = 138, BackColor = Theme.Side, Padding = new Padding(0) };
            // Nút thêm sau nằm sát mép phải, nên thêm theo thứ tự thu nhỏ → phóng to → đóng.
            Ui.NewWindowButton(winButtons, Theme.GlyphMin, false, (s, e) => WindowState = FormWindowState.Minimized);
            btnMax = Ui.NewWindowButton(winButtons, Theme.GlyphMax, false, (s, e) => Ui.SwitchMaximize(this));
            Ui.NewWindowButton(winButtons, Theme.GlyphClose, true, (s, e) => Close());
            top.Padding = new Padding(0, 0, 0, 1);
            top.Controls.Add(logo); top.Controls.Add(adminTag); top.Controls.Add(winButtons);
            top.Resize += (s, e) => PlaceAdminTag();
            // Giữ chuột để kéo, bấm đúp để phóng to / thu lại.
            foreach (Control ctl in new Control[] { top, logo })
                ctl.MouseDown += (s, e) =>
                {
                    if (e.Button != MouseButtons.Left) return;
                    if (e.Clicks >= 2) Ui.SwitchMaximize(this); else Ui.StartWindowDrag(this);
                };

            Controls.Add(content);
            Controls.Add(side);
            Controls.Add(top);

            // Menu bên trái và ô trạng thái ở dưới
            navFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Side, Padding = new Padding(0, 14, 0, 0) };
            sideStatus = new Panel { Dock = DockStyle.Bottom, Height = 128, BackColor = Theme.Side };
            sideStatus.Paint += (s, e) => e.Graphics.DrawLine(Theme.PenLine, 0, 0, sideStatus.Width, 0);
            lblState = new Label { Font = Theme.Bold, ForeColor = Theme.Green, AutoSize = true, Location = new Point(14, 14), Text = "● SẴN SÀNG" };
            lblJob = new Label
            {
                Font = Theme.Small, ForeColor = Theme.Muted, AutoSize = false, Location = new Point(14, 38), Size = new Size(170, 40),
                Text = IsAdmin ? "đủ quyền để đọc mọi thông tin" : "thiếu quyền – một số mục sẽ trống",
            };
            sideStatus.Controls.Add(lblState); sideStatus.Controls.Add(lblJob);
            side.Controls.Add(navFlow);
            side.Controls.Add(sideStatus);

            timer.Interval = 80;
            timer.Tick += (s, e) => Tick();
            timer.Start();

            BuildPerf();
            BuildStartup();
            BuildDisk();
            BuildAnalyzer();
            BuildApps();
            BuildSecurity();
            BuildBackup();
            BuildLog();
            LangHooks.Add(() => PlaceAdminTag());
            LangHooks.Add(() => { if (Jobs.Count == 0) lblState.Text = Lang.Tr("● SẴN SÀNG"); });
            LangHooks.Add(() => { if (Jobs.Count > 0) UpdateBusy(); else if (lastDone != null) ShowLastDone(); });
            BuildSettings();

            Shown += (s, e) =>
            {
                if (StartMinimized) WindowState = FormWindowState.Minimized;
                else
                {
                    Native.ShowWindow(Handle, 9);
                    if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
                    Native.SetForegroundWindow(Handle);
                    Activate();
                }
                ApplyLanguage(this);
                ShowPage(Pages.ContainsKey(Cfg.StartPage ?? "") ? Cfg.StartPage : "perf");
            };
            FormClosed += (s, e) => timer.Stop();
        }

        // Lưới nền: một ô 28×28 có nét ở cạnh trên và trái, lát kín vùng nội dung (nhanh hơn vẽ từng đường mỗi lần).
        static Bitmap gridTile;
        void ApplyGrid()
        {
            if (gridTile == null)
            {
                gridTile = new Bitmap(28, 28);
                using (var g = Graphics.FromImage(gridTile))
                {
                    g.Clear(Theme.Bg);
                    g.DrawLine(Theme.PenGrid, 0, 0, 27, 0);
                    g.DrawLine(Theme.PenGrid, 0, 0, 0, 27);
                }
            }
            content.BackgroundImageLayout = ImageLayout.Tile;
            content.BackgroundImage = Cfg.Grid ? gridTile : null;
            content.Invalidate(true);
        }

        void PlaceAdminTag() => adminTag.Location = new Point(top.Width - winButtons.Width - adminTag.Width - 12, 11);

        // ----- Chạy việc nặng ở luồng nền để giao diện không bị đơ -----
        class Job
        {
            public string Label; public Page Page; public DateTime Start; public Func<string> Progress;
            public Action<object, string> Done; public volatile bool Completed; public object Result; public string Error;
        }

        readonly List<Job> Jobs = new List<Job>();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        int Spin;
        string lastDone; DateTime lastDoneAt;

        void ShowLastDone() =>
            lblJob.Text = string.Format(Lang.T("xong: {0} lúc {1}", "done: {0} at {1}"), Lang.Any(lastDone).ToLower(), lastDoneAt.ToString("HH:mm:ss"));

        void UpdateBusy()
        {
            if (Jobs.Count > 0)
            {
                lblState.ForeColor = Theme.Amber;
                lblJob.Text = string.Join(", ", Jobs.Select(j => Lang.Any(j.Label))).ToLower() + "…";
            }
            else
            {
                lblState.ForeColor = Theme.Green;
                lblState.Text = Lang.Tr("● SẴN SÀNG");
            }
        }

        bool IsBusy(string label) => Jobs.Any(j => j.Label == label);

        void StartWork(string label, Func<object> work, Action<object, string> onDone, Func<string> progress = null)
        {
            if (IsBusy(label)) return;
            Pages.TryGetValue(CurrentPage ?? "", out var pg);
            var job = new Job { Label = label, Page = pg, Start = DateTime.Now, Progress = progress, Done = onDone };
            var th = new Thread(() =>
            {
                try { job.Result = work(); }
                catch (Exception ex) { job.Error = (ex.InnerException ?? ex).Message; }
                job.Completed = true;
            }) { IsBackground = true };
            th.SetApartmentState(ApartmentState.STA);
            Jobs.Add(job);
            if (pg != null)
            {
                pg.OvTitle.Text = ">_ " + Lang.Any(label).ToLower() + "…";
                pg.OvSeconds = 0; pg.OvBar.Invalidate(); pg.OvSub.Text = "";
                pg.OvPanel.Location = new Point((pg.Panel.Width - 460) / 2, (pg.Panel.Height - 104) / 2);
                pg.OvPanel.Visible = true; pg.OvPanel.BringToFront();
            }
            th.Start();
            UpdateBusy();
        }

        void Tick()
        {
            if (Jobs.Count > 0)
            {
                Spin = (Spin + 1) % 12;
                lblState.Text = Lang.Tr("● ĐANG LÀM") + " " + new[] { "|", "/", "-", "\\" }[Spin / 3];
            }
            foreach (var j in Jobs.ToList())
            {
                if (j.Page != null && !j.Completed)
                {
                    double sec = (DateTime.Now - j.Start).TotalSeconds;
                    j.Page.OvSeconds = sec; j.Page.OvBar.Invalidate();
                    string extra = "";
                    if (j.Progress != null) { try { extra = j.Progress() ?? ""; } catch { } }
                    j.Page.OvSub.Text = string.Format(Lang.T("đã chạy {0:N0} giây", "running for {0:N0} s"), sec) + (extra.Length > 0 ? " · " + extra : "");
                }
                if (!j.Completed) continue;
                Jobs.Remove(j);
                if (j.Page != null && !Jobs.Any(x => x.Page == j.Page)) j.Page.OvPanel.Visible = false;
                try { j.Done(j.Result, j.Error); } catch (Exception ex) { ShowInfo(Lang.T("Lỗi khi hiển thị kết quả: ", "Error while showing the result: ") + ex.Message); }
                UpdateBusy();
                if (Jobs.Count == 0) { lastDone = j.Label; lastDoneAt = DateTime.Now; ShowLastDone(); }
            }
        }

        // ----- Hộp thoại tối -----
        DialogResult ShowDialogBox(string title, string text, bool yesNo)
        {
            using (var d = new BorderlessForm())
            {
                d.Grip = 0;
                d.Text = "Clean VaiLon"; d.Size = new Size(640, 440);
                d.StartPosition = FormStartPosition.CenterParent; d.MaximizeBox = false; d.MinimizeBox = false;
                d.BackColor = Theme.Bg; d.ForeColor = Theme.Text; d.Font = Theme.Main; d.ShowInTaskbar = false;
                d.Padding = new Padding(1);
                d.HandleCreated += (s, e) => Ui.SetDarkTitle(d);
                d.Paint += (s, e) => e.Graphics.DrawRectangle(Theme.PenGreenDim, 0, 0, d.Width - 1, d.Height - 1);
                var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 4, 16, 4), BackColor = Theme.Bg };
                var box = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel, Padding = new Padding(12, 10, 12, 10) };
                var tb = new TextBox
                {
                    Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical,
                    BackColor = Theme.Panel, ForeColor = Theme.Text, Font = Theme.Main, Text = text,
                };
                Ui.DarkControl(tb);
                box.Controls.Add(tb); body.Controls.Add(box);
                Ui.AddDarkScroll(tb);
                var h = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.Bg };
                var hl = new Label { Text = ">_ " + Lang.Tr(title).ToUpper(), Font = Theme.Bold, ForeColor = Theme.Green, BackColor = Theme.Bg, AutoSize = true, Location = new Point(16, 14) };
                h.Controls.Add(hl);
                var hx = Ui.NewWindowButton(h, Theme.GlyphClose, true, (s, e) => d.Close());
                hx.BackColor = Theme.Bg;
                hx.MouseLeave += (s, e) => hx.BackColor = Theme.Bg;
                foreach (Control ctl in new Control[] { h, hl }) ctl.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) Ui.StartWindowDrag(d); };
                var bf = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 10, 12, 8) };
                if (yesNo)
                {
                    var y = Ui.NewBtn(bf, "có, tiếp tục", null, true); y.DialogResult = DialogResult.Yes;
                    var n = Ui.NewBtn(bf, "không", null); n.DialogResult = DialogResult.No;
                    d.CancelButton = n;
                }
                else
                {
                    var o = Ui.NewBtn(bf, "đóng", null, true); o.DialogResult = DialogResult.OK;
                    d.AcceptButton = o; d.CancelButton = o;
                }
                d.Controls.Add(body); d.Controls.Add(bf); d.Controls.Add(h);
                d.Shown += (s, e) => { tb.SelectionLength = 0; bf.Controls[0].Focus(); };
                Lang.Apply(d);
                return d.ShowDialog(this);
            }
        }

        bool Confirm(string text) => ShowDialogBox("xác nhận", text, true) == DialogResult.Yes;
        void ShowInfo(string text) => ShowDialogBox("thông báo", text, false);

        // Hỏi trước khi chuyển vào Thùng rác, chia theo mức an toàn. Mục "cấm xoá" luôn bị bỏ ra.
        bool ConfirmRecycle(IEnumerable<Tuple<string, double>> items)
        {
            var groups = new Dictionary<string, List<string>> { ["system"] = new List<string>(), ["keep"] = new List<string>(), ["review"] = new List<string>(), ["safe"] = new List<string>() };
            foreach (var it in items)
            {
                var s = Safety.Get(it.Item1);
                groups[s.Level].Add("• " + it.Item1 + "  (" + Fmt.Size(it.Item2) + ")\r\n    " + Lang.D(s.Why));
            }
            int go = groups["safe"].Count + groups["review"].Count + groups["keep"].Count;
            if (go == 0)
            {
                ShowInfo(Lang.T("Tất cả mục đã chọn đều là mục hệ thống, công cụ không cho xoá:", "Every selected item is a system item; the tool does not allow deleting them:") + "\r\n\r\n" + string.Join("\r\n", groups["system"]));
                return false;
            }
            string msg = string.Format(Lang.T("Chuyển {0} mục vào Thùng rác?", "Move {0} items to the Recycle Bin?"), go) + "\r\n";
            if (groups["keep"].Count > 0) msg += "\r\n" + string.Format(Lang.T("!! KHÔNG NÊN XOÁ ({0}) – chỉ tiếp tục nếu bạn chắc chắn:", "!! SHOULDN'T DELETE ({0}) – only continue if you are sure:"), groups["keep"].Count) + "\r\n" + string.Join("\r\n", groups["keep"]) + "\r\n";
            if (groups["review"].Count > 0) msg += "\r\n" + string.Format(Lang.T("CÂN NHẮC ({0}):", "REVIEW ({0}):"), groups["review"].Count) + "\r\n" + string.Join("\r\n", groups["review"]) + "\r\n";
            if (groups["safe"].Count > 0) msg += "\r\n" + string.Format(Lang.T("AN TOÀN ({0}):", "SAFE ({0}):"), groups["safe"].Count) + "\r\n" + string.Join("\r\n", groups["safe"]) + "\r\n";
            if (groups["system"].Count > 0) msg += "\r\n" + string.Format(Lang.T("SẼ BỎ QUA – mục hệ thống ({0}):", "WILL BE SKIPPED – system items ({0}):"), groups["system"].Count) + "\r\n" + string.Join("\r\n", groups["system"]) + "\r\n";
            msg += "\r\n" + Lang.T("Mục nào lớn hơn chỗ trống của Thùng rác sẽ được bỏ qua, không bị xoá vĩnh viễn.", "Anything larger than the free space in the Recycle Bin is skipped, never deleted permanently.");
            if (!Confirm(msg)) return false;
            if (groups["keep"].Count > 0 && Cfg.DoubleConfirm) return Confirm(string.Format(Lang.T("Xác nhận lần nữa: bạn muốn xoá {0} mục được đánh dấu KHÔNG NÊN XOÁ?", "Confirm again: do you want to delete {0} items marked SHOULDN'T DELETE?"), groups["keep"].Count));
            return true;
        }

        static void Shell(string file, string args = null)
        {
            try { Process.Start(new ProcessStartInfo(file, args ?? "") { UseShellExecute = true }); } catch { }
        }

        static void OpenLocation(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (File.Exists(path)) Shell("explorer.exe", "/select,\"" + path + "\"");
            else if (Directory.Exists(path)) Shell("explorer.exe", "\"" + path + "\"");
        }

        // ----- Trang và menu -----
        Page NewPage(string key, string nav, string help)
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false, Padding = new Padding(22, 14, 22, 14) };
            var hdr = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.Transparent };
            // Tiêu đề chiếm phần còn lại sau cụm nút, để không bao giờ đè lên nút.
            var tl = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = false, WrapContents = false, BackColor = Color.Transparent };
            var t1 = new Label { Text = nav.ToUpper(), Font = Theme.Title, ForeColor = Theme.Bright, AutoSize = true, Margin = new Padding(0, 9, 8, 0), BackColor = Color.Transparent };
            var t2 = new Label { Text = "", Font = Theme.Main, ForeColor = Theme.Muted, AutoSize = true, Margin = new Padding(0, 11, 0, 0), BackColor = Color.Transparent };
            var t3 = new Label { Text = "", Font = Theme.Main, ForeColor = Theme.Green, AutoSize = true, Cursor = Cursors.Hand, Margin = new Padding(10, 11, 0, 0), BackColor = Color.Transparent };
            // Bấm vào dòng 'đã chọn' để bỏ chọn tất cả.
            t3.Click += (s, e) => { if (t3.Tag is ListView lv) foreach (ListViewItem i in lv.CheckedItems.Cast<ListViewItem>().ToList()) i.Checked = false; };
            tl.Controls.Add(t1); tl.Controls.Add(t2); tl.Controls.Add(t3);
            var act = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 5, 0, 0) };
            hdr.Controls.Add(tl); hdr.Controls.Add(act);

            var hl = new Label { Text = help, Dock = DockStyle.Top, Height = 36, ForeColor = Theme.Muted, Font = Theme.Small, BackColor = Color.Transparent };
            var flt = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 4) };

            var dp = new Panel { Dock = DockStyle.Bottom, Height = 108, BackColor = Theme.Panel, Padding = new Padding(12, 10, 12, 8) };
            dp.Paint += (s, e) => e.Graphics.DrawLine(Theme.PenGreenDim, 0, 0, dp.Width, 0);
            var tb = new TextBox
            {
                Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, ScrollBars = ScrollBars.None,
                BackColor = Theme.Panel, ForeColor = Theme.Text, Font = Theme.Small,
            };
            Ui.DarkControl(tb);
            dp.Controls.Add(tb);
            tb.ScrollBars = ScrollBars.Vertical;
            Ui.AddDarkScroll(tb);
            var gap = new Panel { Dock = DockStyle.Bottom, Height = 10, BackColor = Color.Transparent };

            // Thứ tự thêm quyết định thứ tự xếp: mục thêm sau được xếp sát mép trước.
            p.Controls.Add(flt); p.Controls.Add(hl); p.Controls.Add(hdr);
            p.Controls.Add(gap); p.Controls.Add(dp);
            content.Controls.Add(p);

            // Khung "đang chạy" hiện giữa trang trong lúc làm việc nặng.
            var page = new Page { Key = key, NavVi = nav, Name = nav.ToUpper(), Panel = p, T1 = t1, T2 = t2, T3 = t3, Act = act, Filter = flt, Detail = tb };
            var ov = new Panel { Size = new Size(460, 104), BackColor = Theme.Panel, Visible = false };
            ov.Paint += (s, e) => e.Graphics.DrawRectangle(Theme.PenGreen, 0, 0, ov.Width - 1, ov.Height - 1);
            var ovT = new Label { Font = Theme.Bold, ForeColor = Theme.Green, BackColor = Theme.Panel, AutoSize = true, Location = new Point(18, 16) };
            var ovB = new Panel { BackColor = Theme.Panel, Location = new Point(18, 46), Size = new Size(424, 12) };
            Ui.SetDoubleBuffer(ovB);
            ovB.Paint += (s, e) => Ui.DrawScanBar(e.Graphics, ovB.Width, ovB.Height, page.OvSeconds);
            var ovS = new Label { Font = Theme.Small, ForeColor = Theme.Muted, BackColor = Theme.Panel, AutoSize = false, Location = new Point(18, 74), Size = new Size(424, 18), AutoEllipsis = true };
            ov.Controls.AddRange(new Control[] { ovT, ovB, ovS });
            p.Controls.Add(ov);
            p.Resize += (s, e) => ov.Location = new Point((p.Width - 460) / 2, (p.Height - 104) / 2);
            page.OvPanel = ov; page.OvTitle = ovT; page.OvBar = ovB; page.OvSub = ovS;

            var n = new Label
            {
                Text = Ui.Cap(nav), Tag = key, AutoSize = false, Size = new Size(195, 34), TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 0, 0, 0), Margin = new Padding(0), ForeColor = Theme.Muted, BackColor = Theme.Side, Font = Theme.Main, Cursor = Cursors.Hand,
            };
            n.Click += (s, e) => ShowPage(key);
            n.MouseEnter += (s, e) => { if (key != CurrentPage) n.ForeColor = Theme.Text; };
            n.MouseLeave += (s, e) => { if (key != CurrentPage) n.ForeColor = Theme.Muted; };
            n.Paint += (s, e) => { if (key == CurrentPage) e.Graphics.FillRectangle(Theme.BrushGreen, 0, 0, 2, n.Height); };
            navFlow.Controls.Add(n);
            NavItems.Add(n);
            page.Nav = n;
            Pages[key] = page;
            PageOrder.Add(key);
            return page;
        }

        void ShowPage(string key)
        {
            CurrentPage = key;
            foreach (var kv in Pages) kv.Value.Panel.Visible = kv.Key == key;
            foreach (var n in NavItems)
            {
                bool sel = (string)n.Tag == key;
                n.BackColor = sel ? Theme.RowSel : Theme.Side;
                n.ForeColor = sel ? Theme.Green : Theme.Muted;
                n.Invalidate();
            }
            var pg = Pages[key];
            if (pg.Auto != null && !pg.Loaded && Cfg.AutoLoad) { pg.Loaded = true; pg.Auto(); }
        }

        void ApplyLanguage(Control root)
        {
            Lang.Apply(root);
            if (root != this) return;
            foreach (var key in PageOrder)
            {
                var pg = Pages[key];
                pg.Name = Lang.Tr(pg.NavVi).ToUpper();
                pg.Nav.Text = Ui.Cap(Lang.Tr(pg.NavVi));
                var rest = pg.T1.Text.Split(new[] { " · " }, 2, StringSplitOptions.None);
                pg.T1.Text = pg.Name + (rest.Length > 1 ? " · " + rest[1] : "");
            }
            foreach (var h in LangHooks) h();
        }
    }
}
