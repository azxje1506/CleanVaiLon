// Một trang: tiêu đề, cụm nút, lời giới thiệu, bộ lọc, danh sách, khung chi tiết và khung "đang chạy".
using System;
using System.Drawing;
using System.Windows.Forms;

namespace CleanVaiLon
{
    public class Page
    {
        public string Key, NavVi, Name;
        public Label Nav, T1, T2, T3;
        public Panel Panel;
        public FlowLayoutPanel Act, Filter;
        public TextBox Detail;
        public ListView List;
        public ScrollHost ListHost;
        public bool Loaded;
        public Action Auto;
        public Panel OvPanel, OvBar;
        public Label OvTitle, OvSub;
        public double OvSeconds;

        public void SetTitle(object count, string sub)
        {
            T1.Text = Name + " · " + count;
            T2.Text = sub;
        }
    }
}
