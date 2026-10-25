// Các lớp C# vốn được nhúng trong bản PowerShell (Add-Type), giữ nguyên nội dung.
using System; using System.Collections; using System.IO; using System.Collections.Generic;
using System.Windows.Forms; using System.Runtime.InteropServices;
namespace CleanVaiLon {
  public class FileHit { public string Path; public long Size; public DateTime Modified; }
  public class DirHit { public string Path; public long Size; public int Depth; }
  public class Walker {
    public List<FileHit> Files = new List<FileHit>();
    public List<DirHit> Dirs = new List<DirHit>();
    public long MinFile = 1048576;
    public int MaxDepth = 4;
    public HashSet<string> Ext = null;
    public string[] Skip = new string[0];
    public long Walk(DirectoryInfo d, int depth) {
      string low = d.FullName.ToLowerInvariant();
      foreach (var s in Skip) if (low.EndsWith(s)) return 0;
      long total = 0; FileSystemInfo[] items;
      try { items = d.GetFileSystemInfos(); } catch { return 0; }
      foreach (var it in items) {
        try {
          if ((it.Attributes & FileAttributes.ReparsePoint) != 0) continue;
          if ((it.Attributes & FileAttributes.Directory) != 0) { total += Walk((DirectoryInfo)it, depth + 1); continue; }
          var fi = (FileInfo)it; long len = fi.Length; total += len;
          if (len < MinFile) continue;
          if (Ext != null && !Ext.Contains(fi.Extension.ToLowerInvariant())) continue;
          Files.Add(new FileHit { Path = fi.FullName, Size = len, Modified = fi.LastWriteTime });
        } catch { }
      }
      if (depth <= MaxDepth) Dirs.Add(new DirHit { Path = d.FullName, Size = total, Depth = depth });
      return total;
    }
  }
  // Cửa sổ không viền của Windows nhưng vẫn kéo dãn được ở mép và có bóng đổ.
  public class BorderlessForm : Form {
    public int Grip = 6;
    public BorderlessForm() { FormBorderStyle = FormBorderStyle.None; DoubleBuffered = true; }
    public void SetMaximizedBounds(System.Drawing.Rectangle r) { MaximizedBounds = r; }
    protected override CreateParams CreateParams {
      get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; cp.Style |= 0x00020000 | 0x00010000; return cp; }
    }
    protected override void WndProc(ref Message m) {
      base.WndProc(ref m);
      if (m.Msg != 0x84 || Grip <= 0 || WindowState != FormWindowState.Normal || (int)m.Result != 1) return;
      long lp = m.LParam.ToInt64();
      var p = PointToClient(new System.Drawing.Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
      bool l = p.X < Grip, r = p.X >= ClientSize.Width - Grip, t = p.Y < Grip, b = p.Y >= ClientSize.Height - Grip;
      if (t && l) m.Result = (IntPtr)13; else if (t && r) m.Result = (IntPtr)14;
      else if (b && l) m.Result = (IntPtr)16; else if (b && r) m.Result = (IntPtr)17;
      else if (l) m.Result = (IntPtr)10; else if (r) m.Result = (IntPtr)11;
      else if (t) m.Result = (IntPtr)12; else if (b) m.Result = (IntPtr)15;
    }
  }
  public class LvSorter : IComparer {
    public int Col; public bool Desc;
    public int Compare(object a, object b) {
      var x = ((ListViewItem)a).SubItems[Col]; var y = ((ListViewItem)b).SubItems[Col];
      int r;
      if (x.Tag is double && y.Tag is double) r = ((double)x.Tag).CompareTo((double)y.Tag);
      else r = string.Compare(x.Text, y.Text, StringComparison.CurrentCultureIgnoreCase);
      return Desc ? -r : r;
    }
  }
  // Một nút trong cây thư mục của trang Phân tích. Tệp dưới 1 MB được gộp thành một nút "[N tệp nhỏ]".
  public class DNode {
    public string Name; public string Path; public long Size; public bool IsDir; public int Files;
    public string Ext = ""; public DNode Parent;
    public System.Collections.Generic.List<DNode> Kids = new System.Collections.Generic.List<DNode>();
  }
  public static class DiskTree {
    public static volatile int Scanned;
    public static volatile string Current = "";
    public static DNode Scan(string root) {
      Scanned = 0;
      var di = new System.IO.DirectoryInfo(root);
      var n = new DNode { Name = di.FullName, Path = di.FullName, IsDir = true };
      Walk(di, n);
      return n;
    }
    static void Walk(System.IO.DirectoryInfo d, DNode n) {
      System.IO.FileSystemInfo[] items;
      try { items = d.GetFileSystemInfos(); } catch { return; }
      long small = 0; int smallCount = 0;
      foreach (var it in items) {
        try {
          if ((it.Attributes & System.IO.FileAttributes.ReparsePoint) != 0) continue;
          if ((it.Attributes & System.IO.FileAttributes.Directory) != 0) {
            var c = new DNode { Name = it.Name, Path = it.FullName, IsDir = true, Parent = n };
            Current = it.FullName;
            Walk((System.IO.DirectoryInfo)it, c);
            n.Kids.Add(c); n.Size += c.Size; n.Files += c.Files;
          } else {
            var fi = (System.IO.FileInfo)it; long len = fi.Length;
            Scanned++; n.Files++; n.Size += len;
            if (len >= 1048576) n.Kids.Add(new DNode { Name = fi.Name, Path = fi.FullName, Size = len, Parent = n, Ext = fi.Extension.ToLowerInvariant(), Files = 1 });
            else { small += len; smallCount++; }
          }
        } catch { }
      }
      if (smallCount > 0) n.Kids.Add(new DNode { Name = "[" + smallCount + " tệp nhỏ]", Path = n.Path, Size = small, Parent = n, Ext = "*", Files = smallCount });
      n.Kids.Sort((a, b) => b.Size.CompareTo(a.Size));
    }
  }

  // Bản đồ dung lượng kiểu WizTree: mỗi ô là một thư mục hoặc tệp, diện tích tỉ lệ với dung lượng.
  public class Treemap : Control {
    public DNode Root; public DNode Selected; public int MaxDepth = 3;
    public System.Drawing.Color Line = System.Drawing.Color.FromArgb(20, 35, 27);
    public System.Drawing.Color Header = System.Drawing.Color.FromArgb(160, 200, 180);
    public event Action<DNode> NodeClicked;
    public event Action<DNode> NodeOpened;
    public event Action<DNode> NodeHover;
    readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<System.Drawing.RectangleF, DNode>> hits =
      new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<System.Drawing.RectangleF, DNode>>();
    DNode lastHover;
    public Treemap() {
      SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }
    public static string Fmt(long b) {
      if (b >= 1073741824) return (b / 1073741824.0).ToString("0.0") + " GB";
      if (b >= 1048576) return (b / 1048576.0).ToString("0") + " MB";
      return (b / 1024.0).ToString("0") + " KB";
    }
    public static System.Drawing.Color ColorFor(DNode n) {
      if (n.IsDir) return System.Drawing.Color.FromArgb(46, 74, 60);
      string e = n.Ext;
      if (e == "*") return System.Drawing.Color.FromArgb(52, 66, 58);
      if (",.mp4,.mkv,.avi,.mov,.wmv,.flv,.webm,.m2ts,.ts,.m4v,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(224, 123, 36);
      if (",.jpg,.jpeg,.png,.gif,.bmp,.webp,.psd,.tif,.tiff,.heic,.raw,.cr2,.nef,.arw,.svg,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(155, 93, 229);
      if (",.mp3,.wav,.flac,.aac,.ogg,.m4a,.wma,.bnk,.wem,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(58, 134, 255);
      if (",.zip,.rar,.7z,.iso,.img,.cab,.gz,.tar,.xz,.dmg,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(229, 70, 111);
      if (",.exe,.dll,.sys,.msi,.msp,.so,.node,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(46, 196, 182);
      if (",.pak,.ucas,.utoc,.forge,.bin,.bdt,.archive,.vpk,.bsa,.ba2,.dat,.rpf,.blob,.chunk,.big,.pck,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(212, 194, 28);
      if (",.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.md,.csv,.json,.xml,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(95, 208, 104);
      if (",.vhdx,.vmdk,.vdi,.pagefile,.sys,.log,.etl,.db,.sqlite,".Contains("," + e + ",")) return System.Drawing.Color.FromArgb(120, 140, 130);
      return System.Drawing.Color.FromArgb(110, 128, 118);
    }
    protected override void OnPaint(PaintEventArgs e) {
      var g = e.Graphics;
      g.Clear(BackColor);
      hits.Clear();
      if (Root == null || Root.Size <= 0) return;
      Draw(g, Root, new System.Drawing.RectangleF(0, 0, Width - 1, Height - 1), 0);
      if (Selected != null) {
        foreach (var h in hits) if (h.Value == Selected) {
          using (var p = new System.Drawing.Pen(System.Drawing.Color.White, 2)) g.DrawRectangle(p, System.Drawing.Rectangle.Round(h.Key));
          break;
        }
      }
    }
    void Draw(System.Drawing.Graphics g, DNode n, System.Drawing.RectangleF r, int depth) {
      if (r.Width < 1.5f || r.Height < 1.5f) return;
      hits.Add(new System.Collections.Generic.KeyValuePair<System.Drawing.RectangleF, DNode>(r, n));
      var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
      if (n.IsDir && depth < MaxDepth && n.Kids.Count > 0) {
        using (var b = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(10 + depth * 4, 18 + depth * 6, 14 + depth * 4))) g.FillRectangle(b, r);
        using (var p = new System.Drawing.Pen(Line)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
        float hh = (r.Height > 34 && r.Width > 50) ? 16 : 0;
        if (hh > 0) {
          var label = (depth == 0 ? n.Path : n.Name + "\\") + "  " + Fmt(n.Size);
          TextRenderer.DrawText(g, label, Font, new System.Drawing.Rectangle((int)r.X + 4, (int)r.Y + 2, (int)r.Width - 8, 14), Header, flags);
        }
        var inner = new System.Drawing.RectangleF(r.X + 2, r.Y + hh + 1, r.Width - 4, r.Height - hh - 3);
        Squarify(g, n.Kids, inner, depth + 1);
        return;
      }
      var col = ColorFor(n);
      using (var b = new System.Drawing.SolidBrush(col)) g.FillRectangle(b, r);
      using (var p = new System.Drawing.Pen(System.Drawing.Color.FromArgb(7, 11, 9))) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
      if (r.Width > 46 && r.Height > 16) {
        var fg = n.IsDir || n.Ext == "*" ? System.Drawing.Color.FromArgb(200, 225, 210) : System.Drawing.Color.FromArgb(14, 16, 15);
        TextRenderer.DrawText(g, n.Name + (n.IsDir ? "\\" : ""), Font, new System.Drawing.Rectangle((int)r.X + 3, (int)r.Y + 2, (int)r.Width - 6, 14), fg, flags);
        if (r.Height > 32) TextRenderer.DrawText(g, Fmt(n.Size), Font, new System.Drawing.Rectangle((int)r.X + 3, (int)r.Y + 16, (int)r.Width - 6, 14), fg, flags);
      }
    }
    void Squarify(System.Drawing.Graphics g, System.Collections.Generic.List<DNode> kids, System.Drawing.RectangleF r, int depth) {
      double total = 0; foreach (var k in kids) total += k.Size;
      if (total <= 0 || r.Width < 2 || r.Height < 2) return;
      double scale = r.Width * r.Height / total;
      int i = 0; var rem = r;
      while (i < kids.Count && rem.Width >= 1 && rem.Height >= 1) {
        double side = Math.Min(rem.Width, rem.Height);
        var row = new System.Collections.Generic.List<int>(); double rowSum = 0, worst = double.MaxValue;
        while (i < kids.Count) {
          double v = kids[i].Size * scale;
          if (v <= 0) { i++; continue; }
          double sum = rowSum + v, maxV = row.Count == 0 ? v : kids[row[0]].Size * scale;
          double w = Math.Max(side * side * maxV / (sum * sum), (sum * sum) / (side * side * v));
          if (row.Count > 0 && w > worst) break;
          row.Add(i); rowSum = sum; worst = w; i++;
        }
        if (row.Count == 0) break;
        if (rem.Width >= rem.Height) {
          float cw = (float)(rowSum / rem.Height), y = rem.Y;
          foreach (int k in row) { float h = (float)(kids[k].Size * scale / cw); Draw(g, kids[k], new System.Drawing.RectangleF(rem.X, y, cw, h), depth); y += h; }
          rem = new System.Drawing.RectangleF(rem.X + cw, rem.Y, rem.Width - cw, rem.Height);
        } else {
          float rh = (float)(rowSum / rem.Width), x = rem.X;
          foreach (int k in row) { float w2 = (float)(kids[k].Size * scale / rh); Draw(g, kids[k], new System.Drawing.RectangleF(x, rem.Y, w2, rh), depth); x += w2; }
          rem = new System.Drawing.RectangleF(rem.X, rem.Y + rh, rem.Width, rem.Height - rh);
        }
      }
    }
    DNode HitTest(System.Drawing.Point p) {
      for (int i = hits.Count - 1; i >= 0; i--) if (hits[i].Key.Contains(p)) return hits[i].Value;
      return null;
    }
    protected override void OnMouseDown(MouseEventArgs e) {
      base.OnMouseDown(e);
      var n = HitTest(e.Location);
      if (n == null) return;
      Selected = n; Invalidate();
      if (e.Clicks >= 2) { if (NodeOpened != null) NodeOpened(n); }
      else if (NodeClicked != null) NodeClicked(n);
    }
    protected override void OnMouseMove(MouseEventArgs e) {
      base.OnMouseMove(e);
      var n = HitTest(e.Location);
      if (n != lastHover) { lastHover = n; if (NodeHover != null && n != null) NodeHover(n); }
    }
  }

  // Thanh cuộn dọc tự vẽ. Thanh cuộn thật của Target bị che đi (nằm ngoài khung chứa),
  // thanh này đọc vị trí cuộn của Target và điều khiển lại nó.
  public class DarkScroll : Control {
    public Control Target;
    public System.Drawing.Color Thumb = System.Drawing.Color.FromArgb(29, 110, 70);
    public System.Drawing.Color ThumbHot = System.Drawing.Color.FromArgb(59, 227, 143);
    public System.Drawing.Color HeaderBack = System.Drawing.Color.Empty;
    public int TopInset = 0;   // chừa phần tiêu đề cột của ListView
    [StructLayout(LayoutKind.Sequential)]
    struct SCROLLINFO { public int cbSize, fMask, nMin, nMax, nPage, nPos, nTrackPos; }
    [DllImport("user32.dll")] static extern bool GetScrollInfo(IntPtr h, int bar, ref SCROLLINFO si);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    readonly Timer timer = new Timer();
    bool drag, hot; int dragOffset; int lastPos = -1, lastMax = -1, lastPage = -1;
    public DarkScroll() {
      SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
      Width = 10;
      timer.Interval = 60;
      timer.Tick += (s, e) => Sync();
      timer.Start();
    }
    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    SCROLLINFO Info() {
      var si = new SCROLLINFO(); si.cbSize = Marshal.SizeOf(si); si.fMask = 0x17;
      if (Target == null || !Target.IsHandleCreated || !GetScrollInfo(Target.Handle, 1, ref si)) { si.nMin = 0; si.nMax = 0; si.nPage = 1; si.nPos = 0; }
      return si;
    }
    void Sync() {
      if (!Visible) return;
      var si = Info();
      if (si.nPos != lastPos || si.nMax != lastMax || si.nPage != lastPage) { lastPos = si.nPos; lastMax = si.nMax; lastPage = si.nPage; Invalidate(); }
    }
    System.Drawing.Rectangle ThumbRect(SCROLLINFO si) {
      int range = si.nMax - si.nMin + 1;
      if (si.nPage <= 0 || si.nPage >= range) return System.Drawing.Rectangle.Empty;
      int track = Math.Max(1, Height - TopInset);
      int h = Math.Max(28, track * si.nPage / range);
      int maxPos = range - si.nPage;
      int y = maxPos > 0 ? (track - h) * (si.nPos - si.nMin) / maxPos : 0;
      return new System.Drawing.Rectangle(3, TopInset + y + 2, Width - 6, Math.Max(8, h - 4));
    }
    protected override void OnPaint(PaintEventArgs e) {
      e.Graphics.Clear(BackColor);
      if (TopInset > 0 && !HeaderBack.IsEmpty) using (var hb = new System.Drawing.SolidBrush(HeaderBack)) e.Graphics.FillRectangle(hb, 0, 0, Width, TopInset);
      var r = ThumbRect(Info());
      if (r.IsEmpty) return;
      using (var b = new System.Drawing.SolidBrush(drag || hot ? ThumbHot : Thumb)) e.Graphics.FillRectangle(b, r);
    }
    void ScrollTo(int pos) {
      var si = Info();
      int maxPos = si.nMax - si.nMin + 1 - si.nPage;
      pos = Math.Max(si.nMin, Math.Min(si.nMin + Math.Max(0, maxPos), pos));
      if (pos == si.nPos) return;
      var lv = Target as ListView;
      if (lv != null) {
        int rowH = lv.Items.Count > 0 ? lv.GetItemRect(0).Height : 20;
        SendMessage(Target.Handle, 0x1014, IntPtr.Zero, (IntPtr)((pos - si.nPos) * rowH));
      } else {
        SendMessage(Target.Handle, 0x115, (IntPtr)((pos << 16) | 4), IntPtr.Zero);
        SendMessage(Target.Handle, 0x115, (IntPtr)8, IntPtr.Zero);
      }
      Sync();
    }
    protected override void OnMouseDown(MouseEventArgs e) {
      base.OnMouseDown(e);
      var si = Info(); var r = ThumbRect(si);
      if (r.IsEmpty) return;
      if (r.Contains(e.Location)) { drag = true; dragOffset = e.Y - r.Y; Capture = true; Invalidate(); }
      else ScrollTo(si.nPos + (e.Y < r.Y ? -si.nPage : si.nPage));
    }
    protected override void OnMouseMove(MouseEventArgs e) {
      base.OnMouseMove(e);
      var si = Info(); var r = ThumbRect(si);
      bool h = !r.IsEmpty && r.Contains(e.Location);
      if (h != hot) { hot = h; Invalidate(); }
      if (!drag || r.IsEmpty) return;
      int range = si.nMax - si.nMin + 1, maxPos = range - si.nPage, track = Height - TopInset - r.Height - 4;
      if (track <= 0) return;
      ScrollTo(si.nMin + (int)Math.Round((double)(e.Y - dragOffset - TopInset - 2) * maxPos / track));
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); drag = false; Capture = false; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (hot) { hot = false; Invalidate(); } }
    protected override void OnMouseWheel(MouseEventArgs e) {
      base.OnMouseWheel(e);
      var si = Info();
      ScrollTo(si.nPos - Math.Sign(e.Delta) * 3);
    }
  }

  public static partial class Native {
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] public static extern int SetWindowTheme(IntPtr h, string app, string id);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern bool ShowScrollBar(IntPtr h, int bar, bool show);
    [DllImport("uxtheme.dll", EntryPoint = "#135")] public static extern int SetPreferredAppMode(int mode);
    [DllImport("uxtheme.dll", EntryPoint = "#136")] public static extern void FlushMenuThemes();
    [DllImport("uxtheme.dll", EntryPoint = "#133")] public static extern bool AllowDarkModeForWindow(IntPtr h, bool allow);
    [DllImport("uxtheme.dll", EntryPoint = "#104")] public static extern void RefreshImmersiveColorPolicyState();
  }
}
