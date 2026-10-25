// Nút, nút bật/tắt, nhóm bộ lọc, ô chọn số, công tắc, thanh cuộn tối và các hàm vẽ nhỏ.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace CleanVaiLon
{
    public class BtnTag { public bool Primary, Styled; }

    public class ChipTag
    {
        public bool On;
        public string Key, Drive;
        public List<Button> Group;
        public Action<string> Change;
        public StepperState Stepper;
    }

    public class StepperState
    {
        public int Value, Min, Max;
        public Action Change;
        public Label Label;
        public void Step(int d)
        {
            int n = Math.Max(Min, Math.Min(Max, Value + d));
            if (n == Value) return;
            Value = n; Label.Text = n.ToString();
            Change?.Invoke();
        }
    }

    public class SwitchTag { public bool On; public Action<bool> Change; }

    // Khung bọc control có thanh cuộn tự vẽ.
    public class ScrollHost : Panel { public Control Inner; public DarkScroll Bar; }

    public static class Ui
    {
        public static void SetDoubleBuffer(Control c) =>
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(c, true, null);

        // Bọc control trong một khung: thanh cuộn thật bị đẩy ra ngoài mép khung (bị che),
        // thay bằng thanh cuộn tự vẽ cùng màu giao diện.
        public static ScrollHost AddDarkScroll(Control ctl)
        {
            var parent = ctl.Parent;
            var box = new ScrollHost { Dock = ctl.Dock, BackColor = ctl.BackColor };
            if (ctl.Dock == DockStyle.None) box.Bounds = ctl.Bounds;
            int idx = parent.Controls.GetChildIndex(ctl);
            parent.Controls.Add(box); parent.Controls.SetChildIndex(box, idx);
            parent.Controls.Remove(ctl);
            ctl.Dock = DockStyle.None;
            box.Controls.Add(ctl);
            var sb = new DarkScroll { Target = ctl, Dock = DockStyle.Right, Width = 10, BackColor = ctl.BackColor, Thumb = Theme.GreenDim, ThumbHot = Theme.Green };
            box.Controls.Add(sb); sb.BringToFront();
            box.Inner = ctl; box.Bar = sb;
            box.Resize += (s, e) =>
            {
                int w = box.ClientSize.Width - 10 + SystemInformation.VerticalScrollBarWidth;
                box.Inner.Bounds = new Rectangle(0, 0, w, box.ClientSize.Height);
            };
            return box;
        }

        public static void SetDarkTitle(Form f)
        {
            int on = 1; Native.DwmSetWindowAttribute(f.Handle, 20, ref on, 4);
            int round = 2; Native.DwmSetWindowAttribute(f.Handle, 33, ref round, 4);
            int border = (Theme.Line.B << 16) | (Theme.Line.G << 8) | Theme.Line.R;
            Native.DwmSetWindowAttribute(f.Handle, 34, ref border, 4);
        }

        // PowerShell truyền $null thành chuỗi rỗng, nên tham số thứ ba là "" để vẽ giống bản cũ.
        public static void DarkControl(Control c) =>
            c.HandleCreated += (s, e) => { try { Native.AllowDarkModeForWindow(c.Handle, true); Native.SetWindowTheme(c.Handle, "DarkMode_Explorer", ""); } catch { } };

        // Kéo cửa sổ khi giữ chuột trên thanh tiêu đề tự vẽ.
        public static void StartWindowDrag(Form f)
        {
            Native.ReleaseCapture();
            Native.SendMessage(f.Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        // Phóng to vừa vùng làm việc (không che thanh tác vụ), tính theo màn hình đang chứa cửa sổ.
        public static void SwitchMaximize(Form f)
        {
            if (f.WindowState == FormWindowState.Maximized) { f.WindowState = FormWindowState.Normal; return; }
            var scr = Screen.FromControl(f);
            var wa = scr.WorkingArea;
            var r = new Rectangle(wa.X - scr.Bounds.X, wa.Y - scr.Bounds.Y, wa.Width, wa.Height);
            if (f is BorderlessForm bf) bf.SetMaximizedBounds(r);
            f.WindowState = FormWindowState.Maximized;
        }

        // Nút cửa sổ (thu nhỏ / phóng to / đóng) theo cùng phong cách.
        public static Label NewWindowButton(Control parent, string glyph, bool danger, EventHandler onClick)
        {
            var b = new Label
            {
                Text = glyph, Font = Theme.Icon, ForeColor = Theme.Muted, BackColor = Theme.Side, AutoSize = false,
                Size = new Size(46, 34), TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Right, Cursor = Cursors.Hand, Tag = danger,
            };
            b.MouseEnter += (s, e) => { if (danger) { b.BackColor = Theme.Red; b.ForeColor = Theme.Bg; } else { b.BackColor = Theme.RowSel; b.ForeColor = Theme.Green; } };
            b.MouseLeave += (s, e) => { b.BackColor = Theme.Side; b.ForeColor = Theme.Muted; };
            b.Click += onClick;
            parent.Controls.Add(b);
            return b;
        }

        public static void StyleButton(Button b, bool primary)
        {
            b.FlatStyle = FlatStyle.Flat; b.UseVisualStyleBackColor = false; b.UseMnemonic = false;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = primary ? Theme.Green : Theme.GreenDim;
            b.FlatAppearance.MouseOverBackColor = Theme.RowSel;
            b.FlatAppearance.MouseDownBackColor = Theme.GreenDim;
            b.BackColor = Theme.Bg; b.ForeColor = primary ? Theme.Green : Theme.Text;
            b.Font = Theme.Main; b.AutoSize = true; b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.Padding = new Padding(6, 1, 6, 1); b.Cursor = Cursors.Hand;
        }

        public static Button NewBtn(Control flow, string text, EventHandler onClick, bool primary = false)
        {
            var b = new Button { Text = text };
            StyleButton(b, primary);
            b.Margin = new Padding(8, 0, 0, 6);
            b.Tag = new BtnTag { Primary = primary };
            // Windows vẽ nút bị khoá rất tối trên nền đen, nên tự vẽ lại cho dễ đọc.
            b.Paint += (s, e) =>
            {
                if (b.Enabled) return;
                e.Graphics.Clear(Theme.Bg);
                e.Graphics.DrawRectangle(Theme.PenDisabled, 0, 0, b.Width - 1, b.Height - 1);
                TextRenderer.DrawText(e.Graphics, b.Text, b.Font, new Rectangle(0, 0, b.Width, b.Height), Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            };
            if (onClick != null) b.Click += onClick;
            flow.Controls.Add(b);
            return b;
        }

        // Nút chỉ bấm được khi điều kiện đúng (ví dụ đã chọn một dòng); khi không bấm được thì mờ đi.
        static readonly List<KeyValuePair<Button, Func<bool>>> Bound = new List<KeyValuePair<Button, Func<bool>>>();

        public static void BindEnabled(Button b, Func<bool> cond)
        {
            Bound.Add(new KeyValuePair<Button, Func<bool>>(b, cond));
            UpdateBound();
        }

        // Nút chỉ hiện khi điều kiện đúng (ví dụ chỉ hiện "tắt" khi có mục đang bật được chọn).
        static readonly List<KeyValuePair<Button, Func<bool>>> Shown = new List<KeyValuePair<Button, Func<bool>>>();

        public static void BindVisible(Button b, Func<bool> cond)
        {
            Shown.Add(new KeyValuePair<Button, Func<bool>>(b, cond));
            UpdateBound();
        }

        public static void UpdateBound()
        {
            foreach (var x in Shown)
            {
                bool show; try { show = x.Value(); } catch { show = true; }
                if (x.Key.Visible != show) x.Key.Visible = show;
            }
            foreach (var x in Bound)
            {
                var b = x.Key; var tag = (BtnTag)b.Tag;
                bool on; try { on = x.Value(); } catch { on = false; }
                if (b.Enabled == on && tag.Styled) continue;
                b.Enabled = on; tag.Styled = true;
                if (on)
                {
                    b.FlatAppearance.BorderColor = tag.Primary ? Theme.Green : Theme.GreenDim;
                    b.ForeColor = tag.Primary ? Theme.Green : Theme.Text; b.Cursor = Cursors.Hand;
                }
                else { b.FlatAppearance.BorderColor = Theme.Line; b.ForeColor = Theme.Muted; b.Cursor = Cursors.Default; }
            }
        }

        public static ChipTag Chip(Button b) => (ChipTag)b.Tag;

        public static void SetChipState(Button b, bool on)
        {
            Chip(b).On = on;
            if (on) { b.BackColor = Theme.Green; b.ForeColor = Theme.Bg; b.FlatAppearance.BorderColor = Theme.Green; b.FlatAppearance.MouseOverBackColor = Theme.Green; }
            else { b.BackColor = Theme.Bg; b.ForeColor = Theme.Green; b.FlatAppearance.BorderColor = Theme.GreenDim; b.FlatAppearance.MouseOverBackColor = Theme.RowSel; }
        }

        public static Button NewChipButton(Control flow, string text, ChipTag tag)
        {
            var b = new Button { Text = text };
            StyleButton(b, false);
            b.Font = Theme.Small;
            b.Padding = new Padding(2, 0, 2, 0);
            b.Margin = new Padding(0, 0, 6, 6);
            b.Tag = tag ?? new ChipTag();
            flow.Controls.Add(b);
            return b;
        }

        public static Button NewToggle(Control flow, string text, bool on, Action onChange)
        {
            var b = NewChipButton(flow, text, new ChipTag { On = on });
            SetChipState(b, on);
            b.Click += (s, e) => { SetChipState(b, !Chip(b).On); onChange?.Invoke(); };
            return b;
        }

        public static List<Button> NewChipGroup(Control flow, IEnumerable<KeyValuePair<string, string>> options, Action<string> onChange)
        {
            var list = new List<Button>();
            bool first = true;
            foreach (var kv in options)
            {
                var b = NewChipButton(flow, kv.Value, new ChipTag { Key = kv.Key, Group = list, Change = onChange });
                SetChipState(b, first);
                first = false;
                b.Click += (s, e) =>
                {
                    foreach (var o in Chip(b).Group) SetChipState(o, false);
                    SetChipState(b, true);
                    Chip(b).Change?.Invoke(Chip(b).Key);
                };
                list.Add(b);
            }
            return list;
        }

        public static string ChipKey(List<Button> group)
        {
            foreach (var b in group) if (Chip(b).On) return Chip(b).Key;
            return null;
        }

        public static void SelectChip(List<Button> group, string key)
        {
            foreach (var o in group) SetChipState(o, Chip(o).Key == key);
        }

        public static Label NewText(Control flow, string text)
        {
            var l = new Label { Text = text, AutoSize = true, ForeColor = Theme.Muted, Font = Theme.Small, BackColor = Color.Transparent, Margin = new Padding(0, 6, 6, 0) };
            flow.Controls.Add(l);
            return l;
        }

        // Ô chọn số kiểu  [−] 6 [+]  cùng phong cách với nút lọc. Cuộn chuột trên số cũng đổi được.
        public static StepperState NewStepper(Control flow, int value, int min, int max, Action onChange)
        {
            var st = new StepperState { Value = value, Min = min, Max = max, Change = onChange };
            var minus = NewChipButton(flow, "−", new ChipTag { Stepper = st });
            SetChipState(minus, false);
            minus.Margin = new Padding(0, 0, 0, 6);
            var val = new Label
            {
                Text = value.ToString(), AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = Theme.Bold,
                ForeColor = Theme.Green, BackColor = Theme.Panel, Size = new Size(38, minus.Height), Margin = new Padding(0, 0, 0, 6),
            };
            val.Paint += (s, e) => { e.Graphics.DrawLine(Theme.PenGreenDim, 0, 0, val.Width, 0); e.Graphics.DrawLine(Theme.PenGreenDim, 0, val.Height - 1, val.Width, val.Height - 1); };
            flow.Controls.Add(val);
            var plus = NewChipButton(flow, "+", new ChipTag { Stepper = st });
            SetChipState(plus, false);
            st.Label = val;
            minus.Click += (s, e) => st.Step(-1);
            plus.Click += (s, e) => st.Step(1);
            val.MouseWheel += (s, e) => st.Step(e.Delta > 0 ? 1 : -1);
            val.MouseEnter += (s, e) => val.Focus();
            return st;
        }

        // Công tắc bật/tắt kiểu terminal: khung nhỏ, nút vuông chạy sang phải khi bật.
        public static Panel NewSwitch(Control parent, bool on, Action<bool> onChange)
        {
            var sw = new Panel { Size = new Size(32, 18), Cursor = Cursors.Hand, BackColor = Theme.Panel, Margin = new Padding(0, 4, 0, 0) };
            sw.Tag = new SwitchTag { On = on, Change = onChange };
            SetDoubleBuffer(sw);
            sw.Paint += (s, e) =>
            {
                var g = e.Graphics; var t = (SwitchTag)sw.Tag;
                g.Clear(Theme.Panel);
                if (t.On) { g.DrawRectangle(Theme.PenGreen, 0, 0, sw.Width - 1, sw.Height - 1); g.FillRectangle(Theme.BrushGreen, sw.Width - 14, 3, 11, sw.Height - 6); }
                else { g.DrawRectangle(Theme.PenDisabled, 0, 0, sw.Width - 1, sw.Height - 1); g.FillRectangle(Theme.BrushTrack, 3, 3, 11, sw.Height - 6); }
            };
            sw.Click += (s, e) => { var t = (SwitchTag)sw.Tag; t.On = !t.On; sw.Invalidate(); t.Change?.Invoke(t.On); };
            parent.Controls.Add(sw);
            return sw;
        }

        public static void SetSwitch(Panel sw, bool on) { ((SwitchTag)sw.Tag).On = on; sw.Invalidate(); }

        // Thanh chạy kiểu terminal: hàng ô nhỏ kín chiều ngang, một cụm ô sáng chạy qua lại.
        public static void DrawScanBar(Graphics g, int w, int h, double seconds)
        {
            g.Clear(Theme.Panel);
            const int seg = 8, gap = 3;
            int count = (int)Math.Floor((double)(w + gap) / (seg + gap));
            int len = Math.Max(4, count / 5);
            int span = Math.Max(1, count - len);
            int pos = (int)(seconds * 18) % (2 * span);
            if (pos >= span) pos = 2 * span - pos;
            for (int i = 0; i < count; i++)
                g.FillRectangle(i >= pos && i < pos + len ? Theme.BrushGreen : Theme.BrushTrack, i * (seg + gap), 0, seg, h);
        }

        // Vẽ ô tích: cùng một khung cho cả hai trạng thái, dấu tích vẽ bằng nét để không bị lệch.
        public static void DrawCheckBox(Graphics g, int x, int y, bool on)
        {
            if (on)
            {
                g.FillRectangle(Theme.BrushGreen, x, y, 14, 14);
                var old = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLines(Theme.PenCheck, new[] { new Point(x + 3, y + 7), new Point(x + 6, y + 10), new Point(x + 11, y + 4) });
                g.SmoothingMode = old;
            }
            else g.DrawRectangle(Theme.PenGreenDim, x, y, 13, 13);
        }

        // Viết hoa chữ cái đầu cho tên menu.
        public static string Cap(string s) => string.IsNullOrEmpty(s) ? s : s.Substring(0, 1).ToUpper() + s.Substring(1);
    }
}
