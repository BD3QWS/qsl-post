using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QslPost
{
    internal sealed class PagePreview : ScrollableControl
    {
        const int Inset=16;
        readonly Timer renderTimer;
        DocumentPage page;
        Bitmap image;
        float pixelsPerMillimetre;
        Size paperPixels;
        int magnification=1;
        string renderError="";
        internal Rectangle SelectionArea;

        public PagePreview()
        {
            Name="pagePreview";
            AccessibleName="单据预览";
            BackColor=Color.FromArgb(214,227,241);
            AutoScroll=true;
            ResizeRedraw=true;
            DoubleBuffered=true;
            renderTimer=new Timer{Interval=80};
            renderTimer.Tick+=(s,e)=>{
                renderTimer.Stop();
                ClearImage();
                Invalidate();
            };
        }

        public void ShowPage(DocumentPage value)
        {
            page=value;
            renderTimer.Stop();
            ClearImage();
            AutoScrollPosition=Point.Empty;
            MeasurePaper();
        }

        public void Zoom(int value)
        {
            magnification=value;
            renderTimer.Stop();
            ClearImage();
            AutoScrollPosition=Point.Empty;
            MeasurePaper();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            var previous=paperPixels;
            MeasurePaper();
            if(previous==paperPixels)return;
            renderError="";
            // Move the paper with the viewport. Reuse its raster until resizing settles.
            renderTimer.Stop();
            if(!paperPixels.IsEmpty&&image!=null&&image.Size!=paperPixels)renderTimer.Start();
        }

        void MeasurePaper()
        {
            if(page==null||Width<=2*Inset||Height<=2*Inset)
            {
                paperPixels=Size.Empty;
                AutoScrollMinSize=Size.Empty;Invalidate();return;
            }
            float scale=Math.Min((Width-2*Inset)/page.Width,(Height-2*Inset)/page.Height)*magnification;
            // Limit a single bitmap in the x86 process, including very large custom stock.
            float limit=(float)Math.Sqrt(15000000d/(page.Width*page.Height));
            scale=Math.Min(scale,Math.Min(limit,6000/Math.Max(page.Width,page.Height)));
            pixelsPerMillimetre=scale;
            paperPixels=new Size((int)Math.Ceiling(page.Width*scale),(int)Math.Ceiling(page.Height*scale));
            AutoScrollMinSize=magnification==1?Size.Empty:paperPixels+new Size(2*Inset,2*Inset);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if(page==null||paperPixels.IsEmpty)return;
            if(image==null&&renderError.Length==0)
            {
                try {image=page.RenderAtScale(pixelsPerMillimetre);}
                catch(Exception error) {renderError=error.Message;}
            }
            if(image==null)
            {
                TextRenderer.DrawText(e.Graphics,renderError,Font,Rectangle.Inflate(ClientRectangle,-Inset,-Inset),
                    Ui.Ink,TextFormatFlags.WordBreak);
                return;
            }
            var bounds=PaperBounds;
            if(image.Size!=paperPixels)e.Graphics.InterpolationMode=InterpolationMode.Bilinear;
            // A settled preview is always drawn at its rendered pixel dimensions.
            e.Graphics.DrawImage(image,bounds,0,0,image.Width,image.Height,GraphicsUnit.Pixel);
            if(!SelectionArea.IsEmpty)
            {
                using(var wash=new SolidBrush(Color.FromArgb(48,42,112,205)))e.Graphics.FillRectangle(wash,SelectionArea);
                using(var outline=new Pen(Ui.Blue,1))e.Graphics.DrawRectangle(outline,SelectionArea);
            }
        }

        internal Rectangle PaperBounds
        {
            get
            {
                var offset=AutoScrollPosition;
                int x=Math.Max(Inset,(ClientSize.Width-paperPixels.Width)/2)+offset.X;
                int y=Math.Max(Inset,(ClientSize.Height-paperPixels.Height)/2)+offset.Y;
                return new Rectangle(new Point(x,y),paperPixels);
            }
        }

        void ClearImage()
        {
            if(image!=null){image.Dispose();image=null;}
            renderError="";
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing){renderTimer.Dispose();ClearImage();}
            base.Dispose(disposing);
        }
    }
}
