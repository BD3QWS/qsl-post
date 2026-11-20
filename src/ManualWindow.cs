using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QslPost
{
    public sealed partial class ManualDialog
    {
        const int NcCalcSize=0x0083, NcHitTest=0x0084, NcRightButtonUp=0x00A5, SysCommand=0x0112;
        const int HitClient=1, HitCaption=2, HitLeft=10, HitRight=11, HitTop=12,
            HitTopLeft=13, HitTopRight=14, HitBottom=15, HitBottomLeft=16, HitBottomRight=17;
        const int SizeCommand=0xF000, MoveCommand=0xF010, MouseMenu=0xF090, KeyMenu=0xF100;
        const int MenuRadius=12;

        [StructLayout(LayoutKind.Sequential)]
        struct NativeRectangle { public int Left,Top,Right,Bottom; }
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);

        GlassButton menuButton, minimizeButton, maximizeButton, closeButton;
        ContextMenuStrip windowActions;
        ToolStripMenuItem maximizeAction,moveAction,sizeAction;
        ToolTip captionTips;
        Font menuFont;
        float menuScale;

        protected override bool FitWindow { get { return true; } }

        void ConfigureCaption()
        {
            captionTips=new ToolTip();
            windowActions=new ContextMenuStrip{Name="manualWindowActions",ShowImageMargin=false,ShowCheckMargin=false,
                Renderer=new WindowMenuRenderer(),AccessibleName="窗口操作",ForeColor=Ui.Ink};
            windowActions.SizeChanged+=(s,e)=>UpdateMenuOutline();
            menuButton=CaptionButton("manualWindowMenu","window-menu","窗口操作",()=>ShowWindowActions());
            minimizeButton=CaptionButton("manualMinimize","window-minimize","最小化",()=>WindowState=FormWindowState.Minimized);
            maximizeButton=CaptionButton("manualMaximize","window-maximize","最大化",ToggleMaximize);
            closeButton=CaptionButton("manualClose","window-close","关闭手册",Close);
            moveAction=new ToolStripMenuItem("移动",null,(s,e)=>PostMessage(Handle,SysCommand,new IntPtr(MoveCommand),IntPtr.Zero));
            sizeAction=new ToolStripMenuItem("调整大小",null,(s,e)=>PostMessage(Handle,SysCommand,new IntPtr(SizeCommand),IntPtr.Zero));
            windowActions.Items.Add(moveAction);
            windowActions.Items.Add(sizeAction);
            windowActions.Items.Add(new ToolStripMenuItem("最小化",null,(s,e)=>WindowState=FormWindowState.Minimized){Name="minimizeWindow"});
            maximizeAction=new ToolStripMenuItem("最大化",null,(s,e)=>ToggleMaximize()){Name="maximizeWindow"};
            windowActions.Items.Add(maximizeAction);
            windowActions.Items.Add(new ToolStripMenuItem("关闭手册",null,(s,e)=>Close()){Name="closeManual"});
            PerformLayout();
        }

        GlassButton CaptionButton(string name,string glyph,string label,Action action)
        {
            var button=new GlassButton{Name=name,IconKind=glyph,AccessibleName=label,Size=new Size(44,40)};
            button.Click+=(s,e)=>action();
            captionTips.SetToolTip(button,label);
            Controls.Add(button);
            return button;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if(closeButton==null)return;
            int top=(Padding.Top-closeButton.Height)/2;
            int gap=WindowLayout.Pixels(7,LayoutScale);
            menuButton.Location=new Point(Padding.Left,top);
            closeButton.Location=new Point(ClientSize.Width-Padding.Right-closeButton.Width,top);
            maximizeButton.Location=new Point(closeButton.Left-gap-maximizeButton.Width,top);
            minimizeButton.Location=new Point(maximizeButton.Left-gap-minimizeButton.Width,top);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if(minimizeButton==null)return;
            int left=menuButton.Right+WindowLayout.Pixels(14,LayoutScale);
            var title=new Rectangle(left,0,Math.Max(0,minimizeButton.Left-left-10),Padding.Top);
            using(var font=Ui.Font(25*LayoutScale,true))
                TextRenderer.DrawText(e.Graphics,"使用手册",font,title,Ui.Ink,
                    TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if(maximizeAction==null)return;
            bool maximized=WindowState==FormWindowState.Maximized;
            string label=maximized?"还原":"最大化";
            maximizeButton.IconKind=maximized?"window-restore":"window-maximize";
            maximizeButton.AccessibleName=label;
            maximizeAction.Text=label;
            moveAction.Enabled=!maximized;
            sizeAction.Enabled=!maximized;
            captionTips.SetToolTip(maximizeButton,label);
            maximizeButton.Invalidate();
        }

        void ToggleMaximize()
        {
            WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;
        }

        void ShowWindowActions()
        {
            if(windowActions==null||menuButton==null)return;
            if(menuScale!=LayoutScale)
            {
                var previousFont=menuFont;
                menuScale=LayoutScale;
                menuFont=Ui.Font(12.5f*menuScale,false);
                windowActions.Font=menuFont;
                var inset=WindowLayout.Spacing(new Padding(16,9,42,9),menuScale);
                foreach(ToolStripItem item in windowActions.Items)item.Padding=inset;
                UpdateMenuOutline();
                if(previousFont!=null)previousFont.Dispose();
            }
            windowActions.Show(this,new Point(menuButton.Left,menuButton.Bottom+WindowLayout.Pixels(6,LayoutScale)));
        }

        void UpdateMenuOutline()
        {
            var bounds=windowActions.ClientRectangle;
            if(bounds.Width<=1||bounds.Height<=1)return;
            using(var outline=Ui.Round(bounds,MenuRadius))
                windowActions.Region=new Region(outline);
        }

        protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
        {
            if(keyData==(Keys.Alt|Keys.Space))
            {
                ShowWindowActions();
                return true;
            }
            return base.ProcessCmdKey(ref msg,keyData);
        }

        protected override void WndProc(ref Message m)
        {
            if(m.Msg==NcCalcSize&&m.LParam!=IntPtr.Zero)
            {
                // Keep the system's resizable frame styles while drawing the caption in the client area.
                if(IsZoomed(m.HWnd))
                {
                    var bounds=(NativeRectangle)Marshal.PtrToStructure(m.LParam,typeof(NativeRectangle));
                    var window=Rectangle.FromLTRB(bounds.Left,bounds.Top,bounds.Right,bounds.Bottom);
                    var work=Rectangle.Intersect(window,Screen.FromRectangle(window).WorkingArea);
                    if(work.Width>0&&work.Height>0)
                        Marshal.StructureToPtr(new NativeRectangle{Left=work.Left,Top=work.Top,Right=work.Right,Bottom=work.Bottom},m.LParam,false);
                }
                m.Result=IntPtr.Zero;
                return;
            }
            int command=m.Msg==SysCommand?m.WParam.ToInt32()&0xFFF0:0;
            if((m.Msg==NcRightButtonUp&&m.WParam.ToInt32()==HitCaption)||command==MouseMenu||
               (command==KeyMenu&&m.LParam.ToInt64()==(int)Keys.Space))
            {
                ShowWindowActions();
                m.Result=IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
            if(m.Msg!=NcHitTest||m.Result.ToInt32()!=HitClient||closeButton==null)return;
            long packed=m.LParam.ToInt64();
            var point=PointToClient(new Point(unchecked((short)packed),unchecked((short)(packed>>16))));
            m.Result=new IntPtr(HitFrame(point));
        }

        int HitFrame(Point point)
        {
            if(WindowState==FormWindowState.Normal)
            {
                var border=SystemInformation.FrameBorderSize;
                bool left=point.X<border.Width,right=point.X>=ClientSize.Width-border.Width;
                if(point.Y<border.Height)return left?HitTopLeft:right?HitTopRight:HitTop;
                if(point.Y>=ClientSize.Height-border.Height)return left?HitBottomLeft:right?HitBottomRight:HitBottom;
                if(left)return HitLeft;
                if(right)return HitRight;
            }
            if(point.Y<Padding.Top&&!menuButton.Bounds.Contains(point)&&!minimizeButton.Bounds.Contains(point)&&
               !maximizeButton.Bounds.Contains(point)&&!closeButton.Bounds.Contains(point))return HitCaption;
            return HitClient;
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                if(windowActions!=null)windowActions.Dispose();
                if(captionTips!=null)captionTips.Dispose();
                if(menuFont!=null)menuFont.Dispose();
            }
            base.Dispose(disposing);
        }

        sealed class WindowMenuColors:ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Color.FromArgb(247,251,255); } }
            public override Color MenuBorder { get { return Color.FromArgb(181,201,227); } }
        }

        sealed class WindowMenuRenderer:ToolStripProfessionalRenderer
        {
            public WindowMenuRenderer():base(new WindowMenuColors()){}

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if(!e.Item.Selected)return;
                var bounds=Rectangle.Inflate(new Rectangle(Point.Empty,e.Item.Size),-5,-2);
                var state=e.Graphics.Save();
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using(var shape=Ui.Round(bounds,8))
                using(var fill=new SolidBrush(Ui.Tint))e.Graphics.FillPath(fill,shape);
                e.Graphics.Restore(state);
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextRectangle=new Rectangle(e.Item.Padding.Left,0,e.Item.Width-e.Item.Padding.Horizontal,e.Item.Height);
                e.TextFormat=TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix;
                e.TextColor=!e.Item.Enabled?SystemColors.GrayText:e.Item.Selected?Ui.Blue:Ui.Ink;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                var bounds=Rectangle.Inflate(e.ToolStrip.ClientRectangle,-1,-1);
                if(bounds.Width<=0||bounds.Height<=0)return;
                var state=e.Graphics.Save();
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using(var shape=Ui.Round(bounds,MenuRadius-1))
                using(var edge=new Pen(ColorTable.MenuBorder))e.Graphics.DrawPath(edge,shape);
                e.Graphics.Restore(state);
            }
        }
    }
}
