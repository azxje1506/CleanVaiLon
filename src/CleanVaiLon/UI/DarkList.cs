// Danh sách tối (ListView tự vẽ), cột tự co cho vừa khung, khung chi tiết.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CleanVaiLon
{
    // Một cột: H = tiêu đề, W = độ rộng, P = lấy giá trị, F = định dạng, N = số (canh phải, sắp theo số),
    // B = nhãn có khung màu, M = chữ mờ, Bar = vẽ thanh phần trăm.
    public class Col
    {
        public string H; public int W; public Func<object, object> P; public Func<object, string> F;
        public bool N, B, M, Bar;
        public Col(string h, int w, Func<object, object> p) { H = h; W = w; P = p; }
    }

    public class ListTag
    {
        public Col[] Cols;
        public LvSorter Sorter = new LvSorter();
        public Label SelLabel;
        public bool Fitting;
        public int LastAvail = -1, LastCount = -1;
    }

    public static class DarkList
    {
        // Chuyển giá trị thành chữ như [string] của PowerShell (định dạng bất biến).
        public static string Str(object v)
        {
            if (v == null) return "";
            if (v is bool b) return b ? "True" : "False";
            if (v is DateTime d) return d.ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static double Num(object v)
        {
            if (v == null) return 0;
            if (v is DateTime) return 0;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return 0; }
        }

        public static ListTag Tag(ListView lv) => (ListTag)lv.Tag;

        public static Color BadgeColor(string t)
        {
            if (Regex.IsMatch(t, "^(Có thể tắt|Trung bình|Lâu không dùng|Cân nhắc)", RegexOptions.IgnoreCase)) return Theme.Amber;
            if (Regex.IsMatch(t, "^(Cần xác nhận|Cao|Chưa có|Chưa bật|Không có|Lỗi|Không nên xoá|Cấm xoá|Bị chặn)", RegexOptions.IgnoreCase)) return Theme.Red;
            if (Regex.IsMatch(t, "^(Chưa rõ|Thấp|Đã tắt|Chưa dùng|Bỏ qua)", RegexOptions.IgnoreCase)) return Theme.Muted;
            if (Regex.IsMatch(t, "^(Nên giữ|Đang bật|Thông tin|Có|Đã bật|Đang cắm|Đã đăng nhập|Hệ thống|Xong|Đã chuyển|An toàn)", RegexOptions.IgnoreCase)) return Theme.Green;
            return Theme.Cyan;
        }

        static void DrawCell(ListView lv, DrawListViewSubItemEventArgs e)
        {
            var g = e.Graphics; var item = e.Item; var b = e.Bounds;
            bool sel = item.Selected;
            string tone = item.Name;
            Color bg = sel ? Theme.RowSel : tone == "bad" ? Theme.BadBg : tone == "warn" ? Theme.WarnBg : item.Index % 2 == 1 ? Theme.RowAlt : Theme.Row;
            using (var br = new SolidBrush(bg)) g.FillRectangle(br, b);
            g.DrawLine(Theme.PenLine, b.Left, b.Bottom - 1, b.Right, b.Bottom - 1);
            var col = Tag(lv).Cols[e.ColumnIndex];
            int x = b.Left + 8;
            if (e.ColumnIndex == 0)
            {
                if (sel) g.FillRectangle(Theme.BrushGreen, b.Left, b.Top, 2, b.Height - 1);
                if (lv.CheckBoxes) { Ui.DrawCheckBox(g, b.Left + 6, b.Top + (b.Height - 14) / 2, item.Checked); x += 22; }
            }
            string text = e.SubItem.Text;
            if (string.IsNullOrEmpty(text)) return;
            if (col.Bar)
            {
                // Cột phần trăm vẽ thành thanh ngang như WizTree.
                double pct = e.SubItem.Tag is double d ? d : 0;
                int bw = Math.Max(10, b.Right - x - 8);
                var r0 = new Rectangle(x, b.Top + (b.Height - 16) / 2, bw, 16);
                g.DrawRectangle(Theme.PenLine, r0);
                int fw = (int)(Math.Max(0, Math.Min(100, pct)) / 100 * (bw - 1));
                if (fw > 0) g.FillRectangle(Theme.BrushGreenDim, r0.X + 1, r0.Y + 1, fw, 15);
                TextRenderer.DrawText(g, text, Theme.Small, r0, Theme.Bright, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                return;
            }
            if (col.B)
            {
                var fc = BadgeColor(text);
                var sz = TextRenderer.MeasureText(g, text, Theme.Small, new Size(1000, 30), TextFormatFlags.NoPadding);
                int w = Math.Min(sz.Width + 12, b.Right - x - 4);
                var r = new Rectangle(x, b.Top + (b.Height - 19) / 2, w, 18);
                using (var p = new Pen(fc)) g.DrawRectangle(p, r);
                TextRenderer.DrawText(g, text, Theme.Small, r, fc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                return;
            }
            Color tc = Theme.Text;
            if (tone == "dim" || col.M) tc = Theme.Muted;
            else if (e.ColumnIndex == 0) tc = Theme.Bright;
            if (col.N && tone == "warn") tc = Theme.Amber;
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | (col.N ? TextFormatFlags.Right : TextFormatFlags.Left);
            var rect = new Rectangle(x, b.Top, Math.Max(0, b.Right - x - 8), b.Height);
            TextRenderer.DrawText(g, text, Theme.Main, rect, tc, flags);
        }

        // Co cột rộng nhất (ngoài cột cuối) để mọi cột vừa khung, không cần thanh cuộn ngang.
        public static void FitLastColumn(ListView lv)
        {
            int n = lv.Columns.Count;
            var t = Tag(lv);
            if (n < 2 || t.Fitting) return;
            var cols = t.Cols;
            int avail = lv.Parent is ScrollHost h && h.Inner == lv ? h.ClientSize.Width - 10 : lv.ClientSize.Width - 4;
            if (t.LastAvail == avail && t.LastCount == lv.Items.Count) return;
            t.Fitting = true;
            t.LastAvail = avail; t.LastCount = lv.Items.Count;
            try
            {
                int want = 0; for (int i = 0; i < n; i++) want += cols[i].W;
                int flex = 0; for (int i = 1; i < n - 1; i++) if (cols[i].W > cols[flex].W) flex = i;
                for (int i = 0; i < n - 1; i++) lv.Columns[i].Width = cols[i].W;
                int lastMin = Math.Min(cols[n - 1].W, 160);
                int over = (want - cols[n - 1].W + lastMin) - avail;
                if (over > 0) lv.Columns[flex].Width = Math.Max(120, cols[flex].W - over);
                int used = 0; for (int i = 0; i < n - 1; i++) used += lv.Columns[i].Width;
                lv.Columns[n - 1].Width = Math.Max(60, avail - used);
                // Các cột đã vừa khung nên ẩn thanh cuộn ngang mà ListView còn giữ lại.
                if (lv.IsHandleCreated) Native.ShowScrollBar(lv.Handle, 0, false);
            }
            finally { t.Fitting = false; }
        }

        public static ListView Create(Page pg, Col[] cols, bool checks)
        {
            var lv = new ListView
            {
                View = View.Details, FullRowSelect = true, CheckBoxes = checks, HideSelection = false, OwnerDraw = true,
                BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, BackColor = Theme.Row, ForeColor = Theme.Text, Font = Theme.Main,
            };
            lv.SmallImageList = new ImageList { ImageSize = new Size(1, 32) };
            foreach (var c in cols) lv.Columns.Add(c.H, c.W);
            var tag = new ListTag { Cols = cols };
            lv.Tag = tag;
            Ui.SetDoubleBuffer(lv);
            Ui.DarkControl(lv);
            lv.ColumnClick += (s, e) =>
            {
                var st = tag.Sorter;
                if (st.Col == e.Column && lv.ListViewItemSorter != null) st.Desc = !st.Desc; else { st.Col = e.Column; st.Desc = true; }
                lv.ListViewItemSorter = st;
                lv.Sort();
                lv.Invalidate();
            };
            lv.DrawColumnHeader += (s, e) =>
            {
                var g = e.Graphics;
                g.FillRectangle(Theme.BrushBg, e.Bounds);
                string txt = e.Header.Text;
                var st = tag.Sorter;
                if (lv.ListViewItemSorter != null && st.Col == e.ColumnIndex) txt += st.Desc ? " ▼" : " ▲";
                int x = e.Bounds.X + 8, w = e.Bounds.Width - 8;
                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
                if (tag.Cols[e.ColumnIndex].N) { flags |= TextFormatFlags.Right; w -= 8; }
                if (e.ColumnIndex == 0 && lv.CheckBoxes) { x += 22; w -= 22; }
                TextRenderer.DrawText(g, txt, Theme.Small, new Rectangle(x, e.Bounds.Y, Math.Max(0, w), e.Bounds.Height), Theme.Muted, flags);
                g.DrawLine(Theme.PenLine, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            };
            lv.DrawItem += (s, e) => { };
            lv.DrawSubItem += (s, e) => DrawCell(lv, e);
            lv.ItemChecked += (s, e) => { lv.Invalidate(e.Item.Bounds); Ui.UpdateBound(); };
            lv.SelectedIndexChanged += (s, e) => Ui.UpdateBound();
            lv.Resize += (s, e) => FitLastColumn(lv);
            pg.Panel.Controls.Add(lv);
            lv.BringToFront();
            pg.ListHost = Ui.AddDarkScroll(lv);
            // Thanh cuộn chừa phần tiêu đề cột, tô cùng màu tiêu đề.
            pg.ListHost.Bar.HeaderBack = Theme.Bg;
            pg.ListHost.Bar.TopInset = 27;
            if (checks)
            {
                tag.SelLabel = pg.T3;
                lv.ItemChecked += (s, e) => UpdateSelCount(lv);
                pg.T3.Tag = lv;
            }
            pg.List = lv;
            return lv;
        }

        public static double? RowSize(object row)
        {
            switch (row)
            {
                case DiskRow d: return d.Size;
                case AppRow a: return a.Size;
                case AnRow r: return r.Size;
            }
            return null;
        }

        // "đã chọn N · X GB [✕]" cạnh tiêu đề trang; bấm vào để bỏ chọn.
        public static void UpdateSelCount(ListView lv)
        {
            var lbl = Tag(lv).SelLabel;
            if (lbl == null) return;
            int n = lv.CheckedItems.Count;
            if (n == 0) { lbl.Text = ""; return; }
            double sum = 0;
            foreach (ListViewItem i in lv.CheckedItems) { var v = RowSize(i.Tag); if (v.HasValue) sum += v.Value; }
            lbl.Text = "·  " + Lang.Tr("đã chọn") + " " + n + (sum != 0 ? " · " + Fmt.Size(sum) : " " + Lang.Tr("mục")) + "  [✕]";
        }

        public static void Fill(ListView lv, IEnumerable rows, Func<object, string> toneOf)
        {
            var cols = Tag(lv).Cols;
            lv.BeginUpdate();
            lv.ListViewItemSorter = null;
            lv.Items.Clear();
            var items = new List<ListViewItem>();
            foreach (var it in rows)
            {
                ListViewItem li = null;
                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i];
                    object v = c.P(it);
                    string text = c.F != null ? c.F(v) : Str(v);
                    ListViewItem.ListViewSubItem sub;
                    if (i == 0) { li = new ListViewItem(text ?? ""); sub = li.SubItems[0]; }
                    else sub = li.SubItems.Add(text ?? "");
                    if (c.N) sub.Tag = Num(v);
                }
                li.Tag = it;
                if (toneOf != null) li.Name = toneOf(it) ?? "";
                items.Add(li);
            }
            lv.Items.AddRange(items.ToArray());
            lv.EndUpdate();
            if (Tag(lv).SelLabel != null) UpdateSelCount(lv);
            Tag(lv).LastAvail = -1;
            FitLastColumn(lv);
            Ui.UpdateBound();
        }

        // Khung chi tiết: mỗi dòng "nhãn:         giá trị". Bỏ qua giá trị trống.
        public static void ShowDetail(TextBox tb, IEnumerable<Tuple<string, object>> props)
        {
            var lines = new List<string>();
            foreach (var p in props)
            {
                object v = p.Item2;
                if (v == null || Str(v) == "") continue;
                string s;
                if (v is DateTime d) s = d.ToString("dd/MM/yyyy HH:mm");
                else if (p.Item1 == "Kích thước" || p.Item1 == "Dung lượng") s = Fmt.Size(Num(v));
                else s = Str(v);
                lines.Add(string.Format("{0,-14} {1}", Lang.Tr(p.Item1.ToLower()) + ":", s));
            }
            tb.Text = string.Join("\r\n", lines);
        }

        public static Tuple<string, object> P(string label, object value) => Tuple.Create(label, value);

        public static List<T> Checked<T>(ListView lv) => lv.CheckedItems.Cast<ListViewItem>().Select(i => (T)i.Tag).ToList();

        public static T Selected<T>(ListView lv) where T : class => lv.SelectedItems.Count > 0 ? lv.SelectedItems[0].Tag as T : null;
    }
}
