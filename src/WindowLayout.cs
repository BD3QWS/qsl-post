using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QslPost
{
    internal static class WindowLayout
    {
        const int SetRedraw=0x000B;
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);

        public static float Fit(float factor)
        {
            return Math.Max(1f,Math.Min(2f,(float)Math.Floor(factor*4)/4f));
        }

        public static int Pixels(int value,float scale) { return (int)Math.Round(value*scale); }
        public static Padding Spacing(Padding value,float scale)
        {
            return new Padding(Pixels(value.Left,scale),Pixels(value.Top,scale),Pixels(value.Right,scale),Pixels(value.Bottom,scale));
        }

        public static void Scale(Control control,float factor,bool contentsOnly=false)
        {
            if(factor==1f)return;
            var tree=Descendants(control).ToArray();
            var roots=contentsOnly?control.Controls.Cast<Control>().ToArray():new[]{control};
            var frozen=roots.Where(root=>root.IsHandleCreated&&root.Visible)
                .Select(root=>new{Control=root,Handle=root.Handle}).ToArray();
            // Keep the caption and window visible while child bounds and fonts change together.
            foreach(var root in frozen)SendMessage(root.Handle,SetRedraw,IntPtr.Zero,IntPtr.Zero);
            foreach(var child in tree)child.SuspendLayout();
            try
            {
                if(contentsOnly)control.Padding=Spacing(control.Padding,factor);
                foreach(var root in roots)
                {
                    root.Scale(new SizeF(factor,factor));
                    ScaleAppearance(root,factor);
                }
            }
            finally
            {
                try {foreach(var child in tree)child.ResumeLayout(true);}
                finally
                {
                    foreach(var root in frozen)
                        if(root.Control.IsHandleCreated&&root.Control.Handle==root.Handle)
                            SendMessage(root.Handle,SetRedraw,new IntPtr(1),IntPtr.Zero);
                    if(frozen.Length>0)control.Invalidate(true);
                }
            }
        }

        static IEnumerable<Control> Descendants(Control root)
        {
            yield return root;
            foreach(Control child in root.Controls)
                foreach(var control in Descendants(child))yield return control;
        }

        internal static Font ScaledFont(Font font,float factor)
        {
            return new Font(font.FontFamily,font.Size*factor,font.Style,font.Unit,font.GdiCharSet,font.GdiVerticalFont);
        }

        static void ScaleAppearance(Control control,float factor)
        {
            if(control is DataGridView)return;
            var combo=control as ComboBox;
            if(combo!=null)
            {
                int height=combo.ItemHeight;
                combo.Font=ScaledFont(combo.Font,factor);
                combo.ItemHeight=Pixels(height,factor);
                return;
            }
            var list=control as ListBox;
            if(list!=null)
            {
                int height=list.ItemHeight;
            // Read the native selection before WinForms rebuilds the items for the new font.
            var selection=list.SelectedItem;
                list.Font=ScaledFont(list.Font,factor);
                if(list.DrawMode==DrawMode.OwnerDrawFixed)list.ItemHeight=Pixels(height,factor);
            if(list.SelectionMode==SelectionMode.One&&!Equals(list.SelectedItem,selection))list.SelectedItem=selection;
                return;
            }
            if(control is Label||control is ButtonBase||control is TextBoxBase||control is UpDownBase||control is PagePreview)
            {
                control.Font=ScaledFont(control.Font,factor);
                return;
            }
            var panel=control as GlassPanel;
            if(panel!=null)panel.Radius=Pixels(panel.Radius,factor);
            foreach(Control child in control.Controls)ScaleAppearance(child,factor);
        }
    }

    internal sealed class SurfaceGridRow : DataGridViewRow
    {
        public override int GetPreferredHeight(int rowIndex,DataGridViewAutoSizeRowMode autoSizeRowMode,bool fixedWidth)
        {
            return Math.Max(MinimumHeight,DataGridView.RowTemplate.Height);
        }
    }

    internal sealed class SurfaceGrid : DataGridView
    {
        public SurfaceGrid()
        {
            RowTemplate=new SurfaceGridRow{Height=37};
            AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
        }

        protected override void OnRowPrePaint(DataGridViewRowPrePaintEventArgs e)
        {
            if(GetRowDisplayRectangle(e.RowIndex,true).Height<e.RowBounds.Height)
                e.PaintParts&=~(DataGridViewPaintParts.ContentForeground|DataGridViewPaintParts.ErrorIcon|DataGridViewPaintParts.Focus);
            base.OnRowPrePaint(e);
        }

        protected override void ScaleControl(SizeF factor,BoundsSpecified specified)
        {
            // WinForms can also scale a header style during ScaleControl. Use the original metrics for both fonts.
            var body=Font;
            var heading=ColumnHeadersDefaultCellStyle.Font??body;
            var padding=DefaultCellStyle.Padding;
            var headerPadding=ColumnHeadersDefaultCellStyle.Padding;
            int headerHeight=ColumnHeadersHeight,rowHeight=RowTemplate.Height;
            int[] columnWidths=Columns.Cast<DataGridViewColumn>().Select(c=>c.MinimumWidth).ToArray();
            base.ScaleControl(factor,specified);
            if(factor.Height==1&&factor.Width==1)return;
            Font=WindowLayout.ScaledFont(body,factor.Height);
            ColumnHeadersDefaultCellStyle.Font=WindowLayout.ScaledFont(heading,factor.Height);
            ColumnHeadersDefaultCellStyle.Padding=WindowLayout.Spacing(headerPadding,factor.Width);
            DefaultCellStyle.Padding=WindowLayout.Spacing(padding,factor.Width);
            ColumnHeadersHeight=WindowLayout.Pixels(headerHeight,factor.Height);
            RowTemplate.Height=WindowLayout.Pixels(rowHeight,factor.Height);
            for(int i=0;i<columnWidths.Length;i++)Columns[i].MinimumWidth=Math.Max(2,WindowLayout.Pixels(columnWidths[i],factor.Width));
            // Keep a uniform height without resizing the entire ledger during a window transition.
            AutoResizeRows(DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders);
        }
    }
}
