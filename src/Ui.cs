using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace QslPost
{
    public static class Ui
    {
        public static readonly Color Ink=Color.FromArgb(25,48,72), Quiet=Color.FromArgb(74,95,117),Blue=Color.FromArgb(28,91,179),Tint=Color.FromArgb(225,237,250);
        public static Font Font(float size,bool bold){return new Font("Microsoft YaHei UI",size*96f/72f,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);}
        public static GraphicsPath Round(RectangleF r,float radius)
        {float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));var p=new GraphicsPath();p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
        public static Label Label(string text,float size,bool bold){return new GlassLabel{Text=text,Font=Font(size,bold),ForeColor=Ink,BackColor=Color.Transparent,AutoSize=false};}
        public static Button Button(string text,Action action,bool primary)
        {
            var button=new GlassButton{Text=text,Primary=primary,Height=38,Margin=new Padding(0,0,9,0)};
            const TextFormatFlags flags=TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding;
            button.Width=Math.Max(94,TextRenderer.MeasureText(text,button.Font,Size.Empty,flags).Width+26);
            button.Click+=(s,e)=>Guard(button.FindForm(),action);
            return button;
        }
        public static void Guard(IWin32Window owner,Action action){try{action();}catch(Exception ex){MessageBox.Show(owner,ex.Message,"提示",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
        public static TextBox Input(string text){return new TextBox{Text=text,Font=Font(10,false),BorderStyle=BorderStyle.FixedSingle,ForeColor=Ink,BackColor=Color.White,Height=30};}
        public static ComboBox Combo(IEnumerable<string> values)
        {
            var b=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=22,Font=Font(10,false),ForeColor=Ink,BackColor=Color.White,FlatStyle=FlatStyle.Flat,Height=30};
            b.DrawItem+=(s,e)=>{int index=e.Index>=0?e.Index:b.SelectedIndex;if(index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0;using(var brush=new SolidBrush(selected?Tint:Color.White))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,Convert.ToString(b.Items[index]),b.Font,e.Bounds,Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();};
            b.Items.AddRange(values.Cast<object>().ToArray());if(b.Items.Count>0)b.SelectedIndex=0;
            b.DropDown+=(s,e)=>{
                int limit=Math.Max(b.Width,Screen.FromControl(b).WorkingArea.Width-24);
                int width=b.Width;
                foreach(var item in b.Items)
                {
                    width=Math.Max(width,TextRenderer.MeasureText(b.GetItemText(item),b.Font).Width+SystemInformation.VerticalScrollBarWidth+8);
                    if(width>=limit)break;
                }
                b.DropDownWidth=Math.Min(limit,width);
            };
            return b;
        }
        internal static ComboBox DemoAddressLanguage(Repository repository,Action refresh)
        {
            var choice=Combo(new[]{"中文地址","English address"});
            choice.Name="demoAddressLanguage";choice.AccessibleName="演示地址语言";choice.Width=150;
            choice.SelectedIndex=repository.Data.Settings.EnglishDemoAddress?1:0;
            choice.SelectedIndexChanged+=(s,e)=>Guard(choice.FindForm(),()=>{
                repository.Change(db=>db.Settings.EnglishDemoAddress=choice.SelectedIndex==1);
                refresh();
            });
            return choice;
        }
        public static FlowLayoutPanel Toolbar(){return new SurfaceFlowPanel{Dock=DockStyle.Top,Height=52,Padding=new Padding(0,5,0,8),WrapContents=false,BackColor=Color.Transparent,AutoScroll=true};}
        public static DataGridView Grid()
        {
            var g=new SurfaceGrid{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,
                MultiSelect=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.FromArgb(246,250,254),BorderStyle=BorderStyle.None,CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor=Color.FromArgb(227,235,244),EnableHeadersVisualStyles=false,ColumnHeadersHeight=39,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing,Font=Font(9.5f,false)};
            g.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Color.FromArgb(232,240,250),ForeColor=Quiet,Font=Font(9,true),Padding=new Padding(8,0,0,0),SelectionBackColor=Color.FromArgb(232,240,250)};
            g.DefaultCellStyle=new DataGridViewCellStyle{BackColor=Color.FromArgb(249,252,255),ForeColor=Ink,SelectionBackColor=Color.FromArgb(214,232,254),SelectionForeColor=Color.FromArgb(18,64,119),Padding=new Padding(7,0,2,0)};
            g.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(241,247,253);return g;
        }
        public static void Columns(DataGridView g,params string[] names)
        {foreach(string name in names){int n=g.Columns.Add(name,name);g.Columns[n].SortMode=DataGridViewColumnSortMode.Automatic;}g.Columns[0].Visible=false;}
        public static void FitColumns(DataGridView grid,params string[] samples)
        {
            var flags=TextFormatFlags.SingleLine|TextFormatFlags.NoPadding;
            var header=grid.ColumnHeadersDefaultCellStyle;
            using(var drawing=grid.CreateGraphics())
            {
                for(int i=0;i<samples.Length;i++)
                {
                    var column=grid.Columns[i+1];
                    int textWidth=TextRenderer.MeasureText(drawing,samples[i],grid.Font,Size.Empty,flags).Width+grid.DefaultCellStyle.Padding.Horizontal;
                    int headerWidth=TextRenderer.MeasureText(drawing,column.HeaderText,header.Font,Size.Empty,flags).Width+header.Padding.Horizontal;
                    column.MinimumWidth=Math.Max(textWidth,headerWidth)+9;
                    column.FillWeight=column.MinimumWidth;
                }
            }
        }
        public static string Selected(DataGridView g){return g.CurrentRow==null?"":Convert.ToString(g.CurrentRow.Cells[0].Value);}
        public static string[] SelectedIds(DataGridView g){return g.SelectedRows.Cast<DataGridViewRow>().Select(r=>Convert.ToString(r.Cells[0].Value)).ToArray();}
        public static Panel Stack(params Control[] controls)
        {var p=new SurfacePanel{Dock=DockStyle.Fill,BackColor=Color.Transparent};foreach(Control c in controls.Reverse()){c.Dock=DockStyle.Top;p.Controls.Add(c);}return p;}
        public static Label Note(string text){var l=Label(text,9,false);l.ForeColor=Quiet;l.Height=42;return l;}
        public static void Icon(Graphics g,string kind,RectangleF r,Color color)
        {
            var s=g.Save();g.TranslateTransform(r.X,r.Y);g.ScaleTransform(r.Width/24,r.Height/24);using(var p=new Pen(color,1.7f)){p.StartCap=LineCap.Round;p.EndCap=LineCap.Round;p.LineJoin=LineJoin.Round;
                if(kind=="cards"){g.DrawRectangle(p,3,5,17,14);g.DrawLine(p,3,8,20,8);g.DrawLine(p,7,12,16,12);g.DrawLine(p,7,16,12,16);}
                else if(kind=="bags"){g.DrawLines(p,new[]{new PointF(6,8),new PointF(18,8),new PointF(21,20),new PointF(3,20),new PointF(6,8)});g.DrawArc(p,8,2,8,10,180,180);g.DrawLine(p,8,13,16,13);}
                else if(kind=="track"){g.DrawEllipse(p,3,3,13,13);g.DrawLine(p,14,14,21,21);g.DrawLine(p,7,10,9,12);g.DrawLine(p,9,12,13,7);}
                else if(kind=="print"){g.DrawRectangle(p,6,2,12,6);g.DrawRectangle(p,3,8,18,10);g.DrawRectangle(p,6,14,12,8);g.DrawLine(p,16,11,18,11);}
                else if(kind=="cases"){g.DrawRectangle(p,5,3,14,19);g.DrawLine(p,9,8,15,8);g.DrawLine(p,9,12,15,12);g.DrawLine(p,9,16,13,16);}
                else if(kind=="contacts"){g.DrawEllipse(p,8,3,8,8);g.DrawArc(p,4,12,16,15,180,180);}
                else if(kind=="settings"){g.DrawEllipse(p,4,4,16,16);g.DrawEllipse(p,9,9,6,6);g.DrawLine(p,12,1,12,4);g.DrawLine(p,12,20,12,23);g.DrawLine(p,1,12,4,12);g.DrawLine(p,20,12,23,12);}
                else if(kind=="window-menu"){using(var brush=new SolidBrush(color))for(int x=5;x<=17;x+=6)g.FillEllipse(brush,x,11,2.6f,2.6f);}
                else if(kind=="window-minimize"){g.DrawLine(p,5,16,19,16);}
                else if(kind=="window-maximize"){g.DrawRectangle(p,5,5,14,14);}
                else if(kind=="window-restore"){g.DrawRectangle(p,5,8,11,11);g.DrawLines(p,new[]{new Point(8,5),new Point(19,5),new Point(19,16)});}
                else if(kind=="window-close"){g.DrawLine(p,6,6,18,18);g.DrawLine(p,18,6,6,18);}
                else{g.DrawRectangle(p,3,3,7,7);g.DrawRectangle(p,14,3,7,7);g.DrawRectangle(p,3,14,7,7);g.DrawRectangle(p,14,14,7,7);}}
            g.Restore(s);
        }
    }
    public class GlassForm:Form
    {
        static readonly Bitmap Backdrop=CreateBackdrop();
        static readonly Icon ApplicationIcon=Icon.ExtractAssociatedIcon(typeof(GlassForm).Assembly.Location);
        Bitmap currentBackdrop,alternateBackdrop;
        float layoutScale=1f;
        bool ready,draggingWindow;
        internal float LayoutScale { get { return layoutScale; } }
        protected virtual bool FitWindow { get { return false; } }
        public GlassForm(){SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);Font=Ui.Font(10,false);ForeColor=Ui.Ink;BackColor=Color.FromArgb(224,236,249);AutoScaleMode=AutoScaleMode.None;Icon=ApplicationIcon;}
        protected override void OnLoad(EventArgs e)
        {
            if(WindowState==FormWindowState.Normal)FitWorkingArea();
            var owner=Owner as GlassForm;
            if(owner!=null&&owner.LayoutScale>1f&&WindowState==FormWindowState.Normal)
            {
                Rectangle area=Screen.FromControl(owner).WorkingArea;
                var desired=WindowLayout.Fit(Math.Min(owner.LayoutScale,Math.Min((area.Width-24f)/Width,(area.Height-24f)/Height)));
                if(desired>1f)
                {
                    var size=new Size(WindowLayout.Pixels(Width,desired),WindowLayout.Pixels(Height,desired));
                    Bounds=new Rectangle(Math.Max(area.Left,Math.Min(area.Right-size.Width,owner.Left+(owner.Width-size.Width)/2)),
                        Math.Max(area.Top,Math.Min(area.Bottom-size.Height,owner.Top+(owner.Height-size.Height)/2)),size.Width,size.Height);
                    ApplyLayoutScale(desired);
                }
            }
            if(FitWindow&&layoutScale==1f)ApplyLayoutScale(FittingScale());
            ready=true;
            base.OnLoad(e);
        }
        void FitWorkingArea()
        {
            var area=Screen.FromControl(Owner??this).WorkingArea;
            MinimumSize=new Size(Math.Min(MinimumSize.Width,area.Width),Math.Min(MinimumSize.Height,area.Height));
            var bounds=Bounds;
            bounds.Width=Math.Min(bounds.Width,area.Width);
            bounds.Height=Math.Min(bounds.Height,area.Height);
            bounds.X=Math.Max(area.Left,Math.Min(bounds.X,area.Right-bounds.Width));
            bounds.Y=Math.Max(area.Top,Math.Min(bounds.Y,area.Bottom-bounds.Height));
            if(bounds!=Bounds)Bounds=bounds;
        }
        protected override void OnResize(EventArgs e)
        {
            // Lay out the new content before WinForms paints the resized client area.
            if(ready&&FitWindow&&!draggingWindow&&WindowState!=FormWindowState.Minimized)
                ApplyLayoutScale(FittingScale());
            base.OnResize(e);
        }
        protected override void OnResizeBegin(EventArgs e){draggingWindow=true;base.OnResizeBegin(e);}
        protected override void OnResizeEnd(EventArgs e)
        {
            draggingWindow=false;
            if(ready&&FitWindow&&WindowState!=FormWindowState.Minimized)ApplyLayoutScale(FittingScale());
            base.OnResizeEnd(e);
            Invalidate(true);
        }
        float FittingScale(){return WindowLayout.Fit(Math.Min(ClientSize.Width/1360f,ClientSize.Height/800f));}
        void ApplyLayoutScale(float scale)
        {
            if(scale==layoutScale)return;
            float factor=scale/layoutScale;
            layoutScale=scale;
            WindowLayout.Scale(this,factor,true);
            Invalidate();
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            PaintBackdrop(e.Graphics,e.ClipRectangle,Point.Empty);
        }
        internal void PaintBackdrop(Graphics g,Rectangle clip,Point offset)
        {
            var size=ClientSize;
            if(size.Width<1||size.Height<1)return;
            var sample=clip;sample.Offset(offset);sample.Intersect(ClientRectangle);
            if(sample.Width<1||sample.Height<1)return;
            var dest=sample;dest.Offset(-offset.X,-offset.Y);
            if(!draggingWindow&&(long)size.Width*size.Height<=9000000)
            {
                g.DrawImage(BackdropAt(size),dest,sample,GraphicsUnit.Pixel);
                return;
            }
            float xScale=Backdrop.Width/(float)size.Width,yScale=Backdrop.Height/(float)size.Height;
            g.DrawImage(Backdrop,dest,new RectangleF(sample.X*xScale,sample.Y*yScale,sample.Width*xScale,sample.Height*yScale),GraphicsUnit.Pixel);
        }
        Bitmap BackdropAt(Size size)
        {
            if(currentBackdrop!=null&&currentBackdrop.Size==size)return currentBackdrop;
            if(alternateBackdrop!=null&&alternateBackdrop.Size==size)
            {
                var previous=currentBackdrop;currentBackdrop=alternateBackdrop;alternateBackdrop=previous;
                return currentBackdrop;
            }
            if(alternateBackdrop!=null)alternateBackdrop.Dispose();
            alternateBackdrop=currentBackdrop;
            currentBackdrop=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb);
            using(var graphics=Graphics.FromImage(currentBackdrop))
            using(var attributes=new ImageAttributes())
            {
                graphics.InterpolationMode=InterpolationMode.Bilinear;
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(Backdrop,new Rectangle(Point.Empty,size),0,0,Backdrop.Width,Backdrop.Height,GraphicsUnit.Pixel,attributes);
            }
            return currentBackdrop;
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                if(currentBackdrop!=null){currentBackdrop.Dispose();currentBackdrop=null;}
                if(alternateBackdrop!=null){alternateBackdrop.Dispose();alternateBackdrop=null;}
            }
            base.Dispose(disposing);
        }
        static Bitmap CreateBackdrop()
        {
            const int width=640,height=400;
            var image=new Bitmap(width,height,PixelFormat.Format32bppPArgb);
            using(var graphics=Graphics.FromImage(image))
            {
                var bounds=new Rectangle(0,0,width,height);
                using(var brush=new LinearGradientBrush(bounds,Color.FromArgb(207,231,247),Color.FromArgb(225,221,246),25))graphics.FillRectangle(brush,bounds);
                Wash(graphics,new RectangleF(-width*.2f,height*.4f,width*.8f,height),Color.FromArgb(100,173,225,229));
                Wash(graphics,new RectangleF(width*.35f,-height*.7f,width,height*1.6f),Color.FromArgb(160,249,253,255));
            }
            return image;
        }
        static void Wash(Graphics g,RectangleF r,Color c){using(var path=new GraphicsPath()){path.AddEllipse(r);using(var b=new PathGradientBrush(path)){b.CenterColor=c;b.SurroundColors=new[]{Color.FromArgb(0,c)};g.FillPath(b,path);}}}
    }
    public static class UiSnapshot
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        public static void Save(Form form,string destination)
        {
            form.Activate();SetForegroundWindow(form.Handle);form.Refresh();Application.DoEvents();System.Threading.Thread.Sleep(180);Application.DoEvents();
            if(GetForegroundWindow()==form.Handle)
            {
                Point origin=form.PointToScreen(Point.Empty);using(var image=new Bitmap(form.ClientSize.Width,form.ClientSize.Height,System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                {using(var g=Graphics.FromImage(image))g.CopyFromScreen(origin.X,origin.Y,0,0,image.Size);image.Save(destination);}return;
            }
            using(var image=new Bitmap(form.Width,form.Height,System.Drawing.Imaging.PixelFormat.Format24bppRgb))
            {
                bool native;using(var g=Graphics.FromImage(image)){g.Clear(Color.FromArgb(224,236,249));IntPtr dc=g.GetHdc();try{native=PrintWindow(form.Handle,dc,2);}finally{g.ReleaseHdc(dc);}}
                if(!native)form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(destination);
            }
        }
    }
    internal static class Surface
    {
        internal static void PaintBehind(Control child,PaintEventArgs e){Paint(e.Graphics,e.ClipRectangle,child.Parent,child.Location,child.BackColor);}
        static void Paint(Graphics g,Rectangle clip,Control parent,Point offset,Color fallback)
        {
            if(parent==null){using(var b=new SolidBrush(fallback.A==255?fallback:Color.FromArgb(224,236,249)))g.FillRectangle(b,clip);return;}
            var form=parent as GlassForm;if(form!=null){form.PaintBackdrop(g,clip,offset);return;}
            var panel=parent as GlassPanel;
            if(panel==null&&parent.BackColor.A==255){using(var b=new SolidBrush(parent.BackColor))g.FillRectangle(b,clip);return;}
            Paint(g,clip,parent.Parent,new Point(offset.X+parent.Left,offset.Y+parent.Top),fallback);
            if(panel!=null)panel.PaintOverlay(g,clip,offset);
        }
    }
    public class SurfacePanel:Panel
    {
        public SurfacePanel(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);}
        protected override void OnPaintBackground(PaintEventArgs e){Surface.PaintBehind(this,e);}
    }
    public class SurfaceFlowPanel:FlowLayoutPanel
    {
        public SurfaceFlowPanel(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);}
        protected override void OnPaintBackground(PaintEventArgs e){Surface.PaintBehind(this,e);}
    }
    public class SurfaceTablePanel:TableLayoutPanel
    {
        public SurfaceTablePanel(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);}
        protected override void ScaleControl(SizeF factor,BoundsSpecified specified)
        {
            float[] widths=ColumnStyles.Cast<ColumnStyle>().Select(style=>style.Width).ToArray();
            float[] heights=RowStyles.Cast<RowStyle>().Select(style=>style.Height).ToArray();
            base.ScaleControl(factor,specified);
            for(int i=0;i<widths.Length;i++)if(ColumnStyles[i].SizeType==SizeType.Absolute)ColumnStyles[i].Width=widths[i]*factor.Width;
            for(int i=0;i<heights.Length;i++)if(RowStyles[i].SizeType==SizeType.Absolute)RowStyles[i].Height=heights[i]*factor.Height;
        }
        protected override void OnPaintBackground(PaintEventArgs e){Surface.PaintBehind(this,e);}
    }
    public class GlassLabel:Label
    {
        internal bool HideWhenClipped { get; set; }
        public GlassLabel(){SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);}
        protected override void OnPaintBackground(PaintEventArgs e){if(BackColor.A==255){using(var b=new SolidBrush(BackColor))e.Graphics.FillRectangle(b,e.ClipRectangle);}else Surface.PaintBehind(this,e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            if(HideWhenClipped)
            {
                var boundsOnScreen=RectangleToScreen(ClientRectangle);
                for(var parent=Parent;parent!=null;parent=parent.Parent)
                {
                    var viewport=parent as ScrollableControl;
                    if(viewport==null||!viewport.AutoScroll)continue;
                    var visible=viewport.RectangleToScreen(viewport.ClientRectangle);
                    if(boundsOnScreen.Top<visible.Top||boundsOnScreen.Bottom>visible.Bottom)return;
                }
            }
            var bounds=new RectangleF(Padding.Left,Padding.Top,Math.Max(1,ClientSize.Width-Padding.Horizontal),Math.Max(1,ClientSize.Height-Padding.Vertical));
            using(var format=new StringFormat{Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.LineLimit})
            using(var brush=new SolidBrush(ForeColor))
            {
                format.Alignment=TextAlign==ContentAlignment.TopCenter||TextAlign==ContentAlignment.MiddleCenter||TextAlign==ContentAlignment.BottomCenter?StringAlignment.Center:TextAlign==ContentAlignment.TopRight||TextAlign==ContentAlignment.MiddleRight||TextAlign==ContentAlignment.BottomRight?StringAlignment.Far:StringAlignment.Near;
                format.LineAlignment=TextAlign==ContentAlignment.MiddleLeft||TextAlign==ContentAlignment.MiddleCenter||TextAlign==ContentAlignment.MiddleRight?StringAlignment.Center:TextAlign==ContentAlignment.BottomLeft||TextAlign==ContentAlignment.BottomCenter||TextAlign==ContentAlignment.BottomRight?StringAlignment.Far:StringAlignment.Near;
                e.Graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;e.Graphics.DrawString(Text,Font,brush,bounds,format);
            }
        }
    }
    public class GlassPanel:SurfacePanel
    {
        public int Radius=18;
        Bitmap overlay;
        int overlayRadius;
        public GlassPanel(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor|ControlStyles.ResizeRedraw,true);BackColor=Color.Transparent;Padding=new Padding(18);}
        protected override void OnPaint(PaintEventArgs e)
        {
            PaintOverlay(e.Graphics,e.ClipRectangle,Point.Empty);base.OnPaint(e);
        }
        internal void PaintOverlay(Graphics g,Rectangle clip,Point offset)
        {
            if(Width<=4||Height<=5)return;
            if((long)Width*Height>9000000)
            {
                var state=g.Save();
                g.TranslateTransform(-offset.X,-offset.Y);
                DrawOverlay(g);
                g.Restore(state);
                return;
            }
            if(overlay==null||overlay.Size!=ClientSize||overlayRadius!=Radius)
            {
                var bitmap=new Bitmap(ClientSize.Width,ClientSize.Height,PixelFormat.Format32bppPArgb);
                using(var canvas=Graphics.FromImage(bitmap))DrawOverlay(canvas);
                if(overlay!=null)overlay.Dispose();
                overlay=bitmap;overlayRadius=Radius;
            }
            Rectangle source=clip;
            source.Offset(offset);source.Intersect(ClientRectangle);
            if(source.Width<=0||source.Height<=0)return;
            Rectangle destination=source;destination.Offset(-offset.X,-offset.Y);
            g.DrawImage(overlay,destination,source,GraphicsUnit.Pixel);
        }
        void DrawOverlay(Graphics g)
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            var bounds=new RectangleF(1,1,Width-3,Height-5);
            using(var shadow=Ui.Round(new RectangleF(2,4,Width-4,Height-5),Radius))
            using(var brush=new SolidBrush(Color.FromArgb(16,57,88,121)))g.FillPath(brush,shadow);
            using(var shape=Ui.Round(bounds,Radius))
            using(var glass=new LinearGradientBrush(bounds,Color.FromArgb(211,255,255,255),Color.FromArgb(165,245,250,255),90))
            using(var edge=new Pen(Color.FromArgb(230,255,255,255),1))
            {
                g.FillPath(glass,shape);g.DrawPath(edge,shape);
            }
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing&&overlay!=null){overlay.Dispose();overlay=null;}
            base.Dispose(disposing);
        }
    }
    public class GlassButton:Button
    {
        public bool Primary,Selected;public string IconKind="";private bool hover;
        public GlassButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.SupportsTransparentBackColor|ControlStyles.Opaque,true);BackColor=Color.Transparent;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;Font=Ui.Font(10,false);TabStop=true;}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            // ButtonBase can omit OnPaintBackground for a flat button; initialize its buffer before any translucent fill.
            Surface.PaintBehind(this,e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new RectangleF(1,1,Width-3,Height-4);if(r.Width<=0||r.Height<=0)return;
            float scale=Font.SizeInPoints/10f;
            bool primary=Primary&&Enabled;
            using(var p=Ui.Round(r,Math.Min(16*scale,Height/2-1)))
            {
                Color top=primary?Color.FromArgb(49,113,198):Selected?Color.FromArgb(245,253,255):Color.FromArgb(205,255,255,255),bottom=primary?Color.FromArgb(23,77,154):Selected?Color.FromArgb(218,236,255):Color.FromArgb(174,236,244,255);
                if(hover&&Enabled){top=primary?Color.FromArgb(66,130,217):Color.White;bottom=primary?Color.FromArgb(31,92,178):Color.FromArgb(224,239,255);}
                using(var b=new LinearGradientBrush(r,top,bottom,90))e.Graphics.FillPath(b,p);using(var pen=new Pen(primary?Color.FromArgb(125,205,238,255):Color.FromArgb(205,255,255,255),1))e.Graphics.DrawPath(pen,p);
            }
            var color=Enabled?(primary?Color.White:Ui.Ink):Color.FromArgb(100,119,139);
            int inset=WindowLayout.Pixels(9,scale),left=IconKind.Length>0?WindowLayout.Pixels(46,scale):inset;
            float iconSize=21*scale;
            if(IconKind.Length>0)Ui.Icon(e.Graphics,IconKind,new RectangleF(Text.Length==0?(Width-iconSize)/2:15*scale,(Height-iconSize)/2,iconSize,iconSize),color);
            TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(left,0,Math.Max(0,Width-left-inset),Height-3),color,(IconKind.Length>0?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter)|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.PreserveGraphicsClipping|TextFormatFlags.PreserveGraphicsTranslateTransform);
            if(Focused)using(var pen=new Pen(primary?Color.White:Ui.Blue,1)){pen.DashStyle=DashStyle.Dot;using(var p=Ui.Round(new RectangleF(5,5,Width-11,Height-12),11))e.Graphics.DrawPath(pen,p);}
        }
    }
    public class Field
    {
        public string Key,Label,Value;public string[] Options;public bool Multiline;
        public Field(string key,string label,string value){Key=key;Label=label;Value=value??"";}
        public Field Choices(params string[] options){Options=options;return this;}public Field Multi(){Multiline=true;return this;}
    }
    public class RecordDialog:GlassForm
    {
        private Dictionary<string,Control> fields=new Dictionary<string,Control>();
        public string Value(string key){return fields[key].Text.Trim();}
        public RecordDialog(string title,string description,IEnumerable<Field> specifications,Action<RecordDialog> save)
        {
            Text=title;Width=780;Height=760;MinimumSize=new Size(650,520);StartPosition=FormStartPosition.CenterParent;MinimizeBox=false;ShowInTaskbar=false;
            var head=new SurfacePanel{Dock=DockStyle.Top,Height=description.Length>0?94:66,Padding=new Padding(22,15,22,0),BackColor=Color.Transparent};
            var h=Ui.Label(title,21,true);h.Dock=DockStyle.Top;h.Height=40;head.Controls.Add(h);
            if(description.Length>0){var d=Ui.Note(description);d.Dock=DockStyle.Bottom;head.Controls.Add(d);}
            Controls.Add(head);
            var footer=new SurfaceFlowPanel{Dock=DockStyle.Bottom,Height=70,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(22,14,22,14),BackColor=Color.Transparent};
            var commit=Ui.Button("保存",()=>{save(this);DialogResult=DialogResult.OK;Close();},true);var cancel=Ui.Button("取消",()=>{DialogResult=DialogResult.Cancel;Close();},false);footer.Controls.Add(commit);footer.Controls.Add(cancel);Controls.Add(footer);AcceptButton=commit;CancelButton=cancel;
            var scroll=new SurfacePanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(22,0,22,10),BackColor=Color.Transparent};var table=new SurfaceTablePanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,BackColor=Color.Transparent,Padding=new Padding(0,0,8,0)};table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            table.LocationChanged+=(s,e)=>table.Invalidate(true);
            int row=0,col=0;foreach(Field spec in specifications)
            {
                if(spec.Multiline && col!=0){row++;col=0;}
                var group=new SurfacePanel{Dock=DockStyle.Fill,Height=spec.Multiline?112:68,Margin=new Padding(0,0,14,5),BackColor=Color.Transparent};var label=(GlassLabel)Ui.Label(spec.Label,9,true);label.Dock=DockStyle.Top;label.Height=25;label.HideWhenClipped=true;Control input;
                if(spec.Options!=null){var combo=Ui.Combo(spec.Options);if(!spec.Options.Contains(spec.Value)&&spec.Value.Length>0)combo.Items.Add(spec.Value);combo.SelectedItem=spec.Value;if(combo.SelectedIndex<0&&combo.Items.Count>0)combo.SelectedIndex=0;input=combo;}
                else{var text=Ui.Input(spec.Value);text.Multiline=spec.Multiline;text.Height=spec.Multiline?78:29;if(spec.Multiline)text.ScrollBars=ScrollBars.Vertical;input=text;}
                input.Name=spec.Key;input.AccessibleName=spec.Label;input.Dock=DockStyle.Top;fields[spec.Key]=input;group.Controls.Add(input);group.Controls.Add(label);table.Controls.Add(group,col,row);if(spec.Multiline){table.SetColumnSpan(group,2);row++;col=0;}else{col++;if(col==2){row++;col=0;}}
            }
            scroll.Controls.Add(table);Controls.Add(scroll);scroll.BringToFront();Shown+=(s,e)=>{var first=fields.Values.FirstOrDefault();if(first!=null)first.Focus();};
        }
    }
    public class Timeline:Control
    {
        public List<AuditEntry> AuditEvents=new List<AuditEntry>();
        public Timeline(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;}
        protected override void OnPaintBackground(PaintEventArgs e){Surface.PaintBehind(this,e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            var form=FindForm() as GlassForm;
            float scale=form==null?1f:form.LayoutScale;
            float width=Width/scale,height=Height/scale;
            var saved=e.Graphics.Save();e.Graphics.ScaleTransform(scale,scale);
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            int y=6;foreach(var item in AuditEvents.Take(8))
            {
                if(y+70>height)break;
                using(var p=new Pen(Color.FromArgb(167,193,221),1))e.Graphics.DrawLine(p,9,y+5,9,y+66);using(var b=new SolidBrush(Ui.Blue))e.Graphics.FillEllipse(b,5,y+4,8,8);
                using(var f=Ui.Font(9,true))using(var b=new SolidBrush(Ui.Ink))e.Graphics.DrawString(item.Action,f,b,new RectangleF(25,y,width-30,22));
                using(var f=Ui.Font(8,false))using(var b=new SolidBrush(Ui.Quiet)){e.Graphics.DrawString(Clock.Display(item.TimeUtc),f,b,new RectangleF(25,y+22,width-30,18));e.Graphics.DrawString(item.Details,f,b,new RectangleF(25,y+41,width-30,28));}y+=78;
            }
            e.Graphics.Restore(saved);
        }
    }
}

