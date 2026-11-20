using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QslPost
{
    public class PrintStudio:GlassForm
    {
        readonly Repository repo;readonly DocumentContext context;
        ComboBox templates,sources,printers,stock;
        NumericUpDown width,height,margin,offsetX,offsetY,scale,repeat;
        CheckBox landscape;
        PagePreview preview;
        Button previous,next;
        Label info, originalSize;
        Button editFields;
        Button placeFields;
        List<DocumentPage> pages=new List<DocumentPage>();int currentPage;
        readonly Dictionary<string,object> objects=new Dictionary<string,object>();
        bool loading=true,calibration;
        protected override bool FitWindow { get { return true; } }
        public PrintStudio(Repository repository,DocumentContext initial,string key,bool demo=false)
        {
            repo=repository;context=initial;calibration=key=="calibration";Text="打印工作室 · QSL 邮务台";Width=1320;Height=880;MinimumSize=new Size(1050,690);StartPosition=FormStartPosition.CenterParent;Padding=new Padding(20);
            var top=new SurfacePanel{Dock=DockStyle.Top,Height=70,BackColor=Color.Transparent};var title=Ui.Label("打印工作室",23,true);title.Dock=DockStyle.Left;title.Width=300;var actions=Ui.Toolbar();actions.Dock=DockStyle.Right;actions.Width=486;actions.Controls.Add(Ui.Button("导出 PDF",ExportPdf,true));actions.Controls.Add(Ui.Button("打印…",Print,false));editFields=Ui.Button("填写单据",EditFields,false);actions.Controls.Add(editFields);placeFields=Ui.Button("设置填报位置",PlaceFields,false);actions.Controls.Add(placeFields);top.Controls.Add(title);top.Controls.Add(actions);Controls.Add(top);
            var body=new SurfaceTablePanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=Color.Transparent};body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,330));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(body);body.BringToFront();
            var left=new GlassPanel{Dock=DockStyle.Fill,Padding=new Padding(16),Margin=new Padding(0,0,18,0)};
            var scroll=new SurfacePanel{Name="printSettings",Dock=DockStyle.Fill,AutoScroll=true,BackColor=Color.Transparent};
            var settings=new SurfaceTablePanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
                ColumnCount=1,Padding=new Padding(0,0,6,0),BackColor=Color.Transparent};
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            settings.LocationChanged+=(s,e)=>settings.Invalidate(true);
            scroll.Controls.Add(settings);left.Controls.Add(scroll);body.Controls.Add(left,0,0);
            templates=Ui.Combo(Documents.Templates.Select(t=>t.Title));templates.Name="printTemplate";
            Add(settings,"单据模板",templates);if(!calibration)templates.SelectedIndex=Math.Max(0,Documents.Templates.IndexOf(Documents.FindTemplate(key)));
            originalSize=Ui.Note("");originalSize.Name="templateSize";originalSize.Dock=DockStyle.Top;originalSize.Height=47;settings.Controls.Add(originalSize);
            objects["空白单据"]=null;foreach(var b in repo.Data.Bags)objects["邮袋 · "+b.DispatchId+" · "+b.To]=b;foreach(var c in repo.Data.Cards)objects["卡片 · "+c.Id+" · "+c.ToCall]=c;foreach(var c in repo.Data.Cases)objects["查验记录 · "+c.Id+" · "+c.Subject]=c;
            sources=Ui.Combo(objects.Keys);Add(settings,"填充来源",sources);object chosen=(object)initial.Case??initial.Card??(object)initial.Bag;string selected=objects.FirstOrDefault(x=>x.Value==chosen).Key;if(selected!=null)sources.SelectedItem=selected;
            if(demo&&!calibration&&DemoData.HasAddressOptions(repo.Data))
            {
                var language=Ui.DemoAddressLanguage(repo,()=>{context.Db=repo.Data;SafeRebuild();});
                Add(settings,"演示地址",language);
            }
            printers=Ui.Combo(PrinterSettings.InstalledPrinters.Cast<string>());
            if(printers.Items.Count==0)printers.Items.Add("未安装打印机，可导出 PDF");
            string defaultPrinter=new PrinterSettings().PrinterName;
            if(printers.Items.Contains(defaultPrinter))printers.SelectedItem=defaultPrinter;
            if(printers.SelectedIndex<0)printers.SelectedIndex=0;
            Add(settings,"输出打印机",printers);
            stock=Ui.Combo(new[]{"原版尺寸 · 100%","A4 拼版","自定义纸张 / 标签纸"});stock.Name="printStock";Add(settings,"输出纸张",stock);
            landscape=new CheckBox{Text="A4 横向",Dock=DockStyle.Top,AutoSize=true,ForeColor=Ui.Ink,BackColor=Color.Transparent,Margin=new Padding(0,5,0,8)};settings.Controls.Add(landscape);
            width=Number(5,1000,148);height=Number(5,1000,210);width.DecimalPlaces=height.DecimalPlaces=2;
            width.AccessibleName="自定义纸张宽度（毫米）";height.AccessibleName="自定义纸张高度（毫米）";
            AddPair(settings,"自定义宽 × 高 / mm",width,height);
            margin=Number(0,50,8);scale=Number(20,200,100);margin.AccessibleName="边距（毫米）";scale.AccessibleName="缩放（百分比）";
            AddPair(settings,"边距 mm / 缩放 %",margin,scale);
            offsetX=Number(-50,50,0);offsetY=Number(-50,50,0);offsetX.AccessibleName="横向偏移（毫米）";offsetY.AccessibleName="纵向偏移（毫米）";
            AddPair(settings,"横向偏移 mm / 纵向偏移 mm",offsetX,offsetY);
            repeat=Number(1,500,1);repeat.DecimalPlaces=0;Add(settings,"同一单据份数 / 标签数量",repeat);
            var update=Ui.Button("更新预览",Rebuild,false);update.Dock=DockStyle.Top;update.Margin=new Padding(0,5,0,0);settings.Controls.Add(update);
            var right=new GlassPanel{Dock=DockStyle.Fill,Padding=new Padding(20)};body.Controls.Add(right,1,0);
            var footer=new SurfaceTablePanel{Dock=DockStyle.Bottom,Height=50,ColumnCount=4,RowCount=1,
                Padding=new Padding(0,5,0,0),BackColor=Color.Transparent};
            foreach(int amount in new[]{76,76,118})footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,amount));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            previous=Ui.Button("上一页",()=>{if(currentPage>0){currentPage--;ShowPage();}},false);
            next=Ui.Button("下一页",()=>{if(currentPage<pages.Count-1){currentPage++;ShowPage();}},false);
            foreach(var button in new[]{previous,next}){button.Dock=DockStyle.Top;footer.Controls.Add(button);}
            var zoom=Ui.Combo(new[]{"整页","放大 2 倍","放大 3 倍","放大 4 倍"});
            zoom.Name="previewZoom";zoom.AccessibleName="预览放大";zoom.Dock=DockStyle.Top;zoom.Margin=new Padding(9,6,9,0);footer.Controls.Add(zoom);
            info=Ui.Label("",9,false);info.Name="previewPageInfo";info.Dock=DockStyle.Fill;
            info.TextAlign=ContentAlignment.MiddleLeft;info.Margin=new Padding(4,0,0,6);footer.Controls.Add(info);
            right.Controls.Add(footer);preview=new PagePreview{Dock=DockStyle.Fill};right.Controls.Add(preview);preview.BringToFront();
            zoom.SelectedIndexChanged+=(s,e)=>preview.Zoom(zoom.SelectedIndex+1);
            templates.SelectedIndexChanged+=(s,e)=>{if(!loading){context.Fields.Clear();context.Placements.Clear();LoadProfile();SafeRebuild();}};sources.SelectedIndexChanged+=(s,e)=>{if(!loading){SetSource();SafeRebuild();}};
            stock.SelectedIndexChanged+=(s,e)=>{UpdatePaperControls();if(!loading&&stock.SelectedIndex==0)SafeRebuild();};
            LoadProfile();loading=false;SetSource();Shown+=(s,e)=>SafeRebuild();
            KeyPreview=true;
            KeyDown+=(s,e)=>{if(e.KeyCode==Keys.F1){using(var manual=new ManualDialog())manual.ShowDialog(this);e.SuppressKeyPress=true;}};
        }
        void Add(TableLayoutPanel settings,string text,Control control)
        {
            var label=(GlassLabel)Ui.Label(text,9,true);label.Dock=DockStyle.Top;label.Height=24;label.Margin=new Padding(0,10,0,0);label.HideWhenClipped=true;
            control.Dock=DockStyle.Top;control.Margin=new Padding(0,0,0,4);
            if(String.IsNullOrEmpty(control.AccessibleName))control.AccessibleName=text;
            settings.Controls.Add(label);settings.Controls.Add(control);
        }
        void AddPair(TableLayoutPanel settings,string text,Control first,Control second)
        {
            var pair=new SurfaceTablePanel{Height=30,ColumnCount=2,RowCount=1,BackColor=Color.Transparent};
            pair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            pair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            first.Margin=new Padding(0,0,4,0);second.Margin=new Padding(4,0,0,0);
            first.Dock=second.Dock=DockStyle.Top;
            pair.Controls.Add(first,0,0);pair.Controls.Add(second,1,0);
            Add(settings,text,pair);
        }
        NumericUpDown Number(decimal min,decimal max,decimal value){return new NumericUpDown{Minimum=min,Maximum=max,Value=value,DecimalPlaces=1,Increment=.5m,Height=30,Font=Ui.Font(10,false),ForeColor=Ui.Ink};}
        FormTemplate Template{get{return Documents.Templates[templates.SelectedIndex];}}
        void SetSource()
        {
            object source=objects[sources.Text];context.Bag=source as Bag;context.Card=source as Card;context.Case=source as CaseRecord;context.Db=repo.Data;
            context.BlankSource=source==null;
            if(context.Case!=null){context.Card=repo.Data.Cards.FirstOrDefault(c=>c.Id==context.Case.CardId);context.Bag=repo.Data.Bags.FirstOrDefault(c=>c.Id==context.Case.BagId);}
            context.Fields.Clear();
        }
        void LoadProfile()
        {
            FormTemplate t=Template;string key=calibration?"calibration":t.Key;
            decimal w=calibration?148:(decimal)t.Width,h=calibration?210:(decimal)t.Height;
            var p=repo.Data.Settings.PrintProfiles.FirstOrDefault(x=>x.Template==key)
                ??repo.Data.Settings.PrintProfiles.FirstOrDefault(x=>Documents.FindTemplate(x.Template)==t)
                ??new PrintProfile{Template=key,Margin=w>=200?0:8,Width=w,Height=h};
            bool wasLoading=loading;loading=true;
            stock.SelectedIndex=(int)p.Stock;landscape.Checked=p.Landscape;
            width.Value=p.Stock==PrintStock.Custom?p.Width:w;height.Value=p.Stock==PrintStock.Custom?p.Height:h;
            margin.Value=p.Margin;offsetX.Value=p.OffsetX;offsetY.Value=p.OffsetY;scale.Value=p.Scale;
            if(printers.Items.Contains(p.Printer))printers.SelectedItem=p.Printer;
            var upu=t as UpuTemplate;
            originalSize.Text="成品尺寸："+(calibration?"148 × 210 mm":t.SizeText);
            if(upu!=null)originalSize.Text+="\nUPU 2026.09 · 第 "+upu.SourcePages+" 页";
            if(t.DoubleSided)originalSize.Text+="\n正面与背面依次输出";
            originalSize.Height=(upu!=null&&t.DoubleSided)?64:47;
            editFields.Enabled=!calibration&&(upu==null||upu.FillFields.Length>0||upu.Columns.Length>0||OriginalForms.HasArt(t));
            placeFields.Visible=!calibration&&OriginalForms.HasArt(t);
            UpdatePaperControls();loading=wasLoading;
        }
        void UpdatePaperControls()
        {
            width.Enabled=height.Enabled=stock.SelectedIndex==(int)PrintStock.Custom;
            landscape.Enabled=stock.SelectedIndex==(int)PrintStock.A4;
            margin.Enabled=scale.Enabled=offsetX.Enabled=offsetY.Enabled=stock.SelectedIndex!=(int)PrintStock.Original;
        }
        PrintProfile Profile()
        {
            return new PrintProfile{Template=calibration?"calibration":Template.Key,Printer=printers.Text,Stock=(PrintStock)stock.SelectedIndex,Landscape=landscape.Checked,Width=width.Value,Height=height.Value,Margin=margin.Value,OffsetX=offsetX.Value,OffsetY=offsetY.Value,Scale=scale.Value};
        }
        void SaveProfile(PrintProfile p)
        {repo.Change(db=>{var old=db.Settings.PrintProfiles.FirstOrDefault(x=>x.Template==p.Template);if(old!=null)db.Settings.PrintProfiles.Remove(old);db.Settings.PrintProfiles.Add(p);});}
        List<DocumentPage> Originals()
        {
            if(!calibration)return Documents.Build(Template,context);
            return new List<DocumentPage>{new DocumentPage{Name="打印校准",Width=148,Height=210,Paint=g=>{
                Ink.C(g,"打印尺寸与偏移校准",10,10,128,12,15);Ink.T(g,"请用直尺测量横向和纵向线段，均应为 100 mm。\n驱动中的缩放 / 适合页面须关闭；按实际尺寸打印。",10,30,128,23,10);
                Ink.Line(g,20,70,120,70);Ink.Line(g,20,70,20,170);for(int i=0;i<=10;i++){Ink.Line(g,20+i*10,67,20+i*10,73);Ink.Line(g,17,70+i*10,23,70+i*10);}Ink.T(g,"100 mm",50,76,60,10,12);Ink.T(g,"纸张左边到横线起点应为：设置边距 + 20 mm + 横向偏移。\n向右 / 下为正偏移，向左 / 上为负。",30,130,100,35,9);Ink.Footer(g,148,210,"QSL 邮务台 · 本页仅提交打印任务，不代表已出纸");
            }}};
        }
        void Rebuild(){pages=Documents.Compose(Originals(),Profile(),(int)repeat.Value);currentPage=0;ShowPage();SaveProfile(Profile());}
        void SafeRebuild(){Ui.Guard(this,Rebuild);}
        void ShowPage()
        {
            if(pages.Count==0)return;
            var sheet=pages[currentPage];preview.ShowPage(sheet);
            previous.Enabled=currentPage>0;next.Enabled=currentPage<pages.Count-1;
            string face=sheet.Side==PageSide.Front?"正面  ":sheet.Side==PageSide.Back?"背面  ":"";
            info.Text=face+(currentPage+1)+" / "+pages.Count+" 页   "+sheet.Width.ToString("0.##")+" × "+sheet.Height.ToString("0.##")+" mm";
        }
        void PlaceFields()
        {
            if(calibration)return;
            int page=currentPage;
            var face=pages.Count>0?pages[page].Face??Template:Template;
            using(var editor=new FormPlacementDialog(face,context))
                if(editor.ShowDialog(this)==DialogResult.OK){Rebuild();currentPage=Math.Min(page,pages.Count-1);ShowPage();}
        }
        void EditFields()
        {
            if(!calibration&&OriginalForms.HasArt(Template))
            {
                OriginalForms.Edit(Template,context,this,SafeRebuild);
                return;
            }
            var upu=Template as UpuTemplate;
            if(!calibration&&upu!=null){UpuForms.Edit(upu,context,this,SafeRebuild);return;}
            var fields=new List<Field>{
                new Field("From","发单／原寄台",context.From),new Field("To","寄达台",context.To),
                new Field("Reference","文书编号",context.Reference),new Field("Date","日期",context.Date),
                new Field("Operator","经办人员",context.Operator),new Field("Tracking","真实物流号码",context.Tracking),
                new Field("Special","特别标记",context.V("Special",context.Bag==null?"":context.Bag.Seal)),
                new Field("EmptyCount","退回空袋件数",context.V("EmptyCount",context.Bag!=null&&context.Bag.MailClass=="空袋"?"1":"")),
                new Field("RelatedCase","答复原单号",context.V("RelatedCase",context.Case==null?"":context.Case.RelatedCase)),
                new Field("ReceivedNo","接收号码",context.V("ReceivedNo","")),
                new Field("Supervisor","主管人员",context.V("Supervisor",context.Case==null?"":context.Case.Supervisor)),
                new Field("ReceivedDate","收到查单日期",context.V("ReceivedDate","")),
                new Field("DeliveredDate","实际投递日期",context.V("DeliveredDate","")),
                new Field("Resend","二次寄发日期／台站",context.V("Resend",""))
            };
            if(Template.Key=="intlquery")fields.AddRange(new[]{
                new Field("DuplicateDate","副份日期",context.V("DuplicateDate","")),
                new Field("SenderFax","原寄台电话／传真",context.V("SenderFax","")),
                new Field("RecipientFax","寄达台电话／传真",context.V("RecipientFax",""))
            });
            if(Template.Key=="receipt")fields.Add(new Field("Actual","实收张数",context.V("Actual","")));
            fields.AddRange(new[]{
                new Field("Notes","查询／查验说明",context.Note).Multi(),
                new Field("Reply","查明答复",context.V("Reply",context.Case==null?"":context.Case.Reply)).Multi(),
                new Field("Attachment","附件说明",context.V("Attachment",context.Case==null?"":context.Case.Attachment)).Multi()
            });
            using(var dialog=new RecordDialog("填写单据","所填内容仅用于本次打印或导出。",
                fields,d=>{foreach(var field in fields)context.Fields[field.Key]=d.Value(field.Key);}))
                if(dialog.ShowDialog(this)==DialogResult.OK)SafeRebuild();
        }
        void ExportPdf()
        {
            Rebuild();using(var d=new SaveFileDialog{Title="导出实际尺寸 PDF",Filter="PDF 文档|*.pdf",FileName=(calibration?"打印校准页":Template.Title.Replace("/","-"))+"-"+Clock.Date+".pdf"})if(d.ShowDialog(this)==DialogResult.OK)
            {UseWaitCursor=true;try{PdfExport.Write(d.FileName,pages);repo.Change(db=>Repository.Log(db,"导出PDF",context.Reference,Template.Title+" / "+pages.Count+" 页",new[]{context.Card==null?"":context.Card.Id,context.Bag==null?"":context.Bag.Id}));MessageBox.Show(this,"PDF 已保存，共 "+pages.Count+" 页。","导出 PDF");}finally{UseWaitCursor=false;}}
        }
        void Print()
        {
            Rebuild();if(PrinterSettings.InstalledPrinters.Count==0)throw new InvalidOperationException("尚未安装打印机驱动。请先安装驱动，或导出 PDF 到其他电脑打印。");
            PrintProfile p=Profile();using(var document=new PrintDocument())
            {
                if(PrinterSettings.InstalledPrinters.Cast<string>().Contains(p.Printer))document.PrinterSettings.PrinterName=p.Printer;
                document.DocumentName="QSL - "+Template.Title;PrintEngine.Configure(document,pages[0]);document.PrinterSettings.MinimumPage=1;document.PrinterSettings.MaximumPage=pages.Count;document.PrinterSettings.FromPage=1;document.PrinterSettings.ToPage=pages.Count;
                using(var dialog=new PrintDialog{Document=document,UseEXDialog=true,AllowSomePages=true,AllowSelection=false,AllowPrintToFile=true})
                {
                    if(dialog.ShowDialog(this)!=DialogResult.OK)return;if(!document.PrinterSettings.IsValid)throw new InvalidOperationException("打印机无效，请重新选择。");
                    int start=document.PrinterSettings.PrintRange==PrintRange.SomePages?document.PrinterSettings.FromPage-1:0,end=document.PrinterSettings.PrintRange==PrintRange.SomePages?document.PrinterSettings.ToPage:pages.Count;
                    if(start<0||end>pages.Count||end<=start)throw new InvalidOperationException("打印页码范围不正确。");int at=start;
                    // Preflight all selected pages, including barcode module-width checks, before touching the spooler.
                    var verifiedSettings=new List<PageSettings>();
                    for(int n=start;n<end;n++)
                    {
                        var settings=(PageSettings)document.DefaultPageSettings.Clone();
                        settings.PrinterSettings=document.PrinterSettings;
                        PrintEngine.ConfigurePage(settings,pages[n]);
                        PrintEngine.ValidatePrintableArea(pages[n],settings);
                        verifiedSettings.Add(settings);
                    }
                    document.DefaultPageSettings=verifiedSettings[0];
                    document.QueryPageSettings+=(s,e)=>e.PageSettings=verifiedSettings[at-start];
                    document.PrintPage+=(s,e)=>{
                        PrintEngine.DrawPage(e,pages[at]);at++;e.HasMorePages=at<end;
                    };
                    document.Print();string printer=document.PrinterSettings.PrinterName;
                    if(printers.Items.Contains(printer))printers.SelectedItem=printer;p.Printer=printer;SaveProfile(p);
                    repo.Change(db=>Repository.Log(db,"已提交打印任务",context.Reference,Template.Title+" / "+(end-start)+" 页 / "+printer+"；仅表示已交给 Windows 打印队列",new[]{context.Card==null?"":context.Card.Id,context.Bag==null?"":context.Bag.Id}));
                    MessageBox.Show(this,"打印任务已提交至「"+printer+"」。","打印",MessageBoxButtons.OK,MessageBoxIcon.Information);
                }
            }
        }
        public void CaptureScreen(string path){Rebuild();UiSnapshot.Save(this,path);}
    }
    public static class PrintEngine
    {
        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr dc,int index);

        const int HorizontalPixels=8,VerticalPixels=10,DpiX=88,DpiY=90;
        const int PhysicalWidth=110,PhysicalHeight=111,OffsetX=112,OffsetY=113;

        static RectangleF DeviceArea(Graphics graphics,DocumentPage page)
        {
            IntPtr dc=graphics.GetHdc();
            try
            {
                int horizontalDpi=GetDeviceCaps(dc,DpiX),verticalDpi=GetDeviceCaps(dc,DpiY);
                int width=GetDeviceCaps(dc,PhysicalWidth),height=GetDeviceCaps(dc,PhysicalHeight);
                if(width<=0||height<=0||horizontalDpi<=0||verticalDpi<=0)
                    throw new InvalidOperationException("打印机未返回成品纸张尺寸。请用该驱动已配置的纸型，或导出原尺寸 PDF。");
                float mmX=25.4f/horizontalDpi,mmY=25.4f/verticalDpi;
                float actualWidth=width*mmX,actualHeight=height*mmY;
                if(Math.Abs(actualWidth-page.Width)>1.1f||Math.Abs(actualHeight-page.Height)>1.1f)
                    throw new InvalidOperationException(String.Format(
                        "当前驱动使用 {0:0.#} × {1:0.#} mm，单据需要 {2:0.##} × {3:0.##} mm。请在打印机中选用对应纸型，或导出原尺寸 PDF。",
                        actualWidth,actualHeight,page.Width,page.Height));
                return new RectangleF(GetDeviceCaps(dc,OffsetX)*mmX,GetDeviceCaps(dc,OffsetY)*mmY,
                                      GetDeviceCaps(dc,HorizontalPixels)*mmX,GetDeviceCaps(dc,VerticalPixels)*mmY);
            }
            finally { graphics.ReleaseHdc(dc); }
        }

        public static void Configure(PrintDocument document,DocumentPage page)
        {
            ConfigurePage(document.DefaultPageSettings,page);document.OriginAtMargins=false;
        }
        public static void ConfigurePage(PageSettings settings,DocumentPage page)
        {
            int w=(int)Math.Round(page.Width/25.4*100),h=(int)Math.Round(page.Height/25.4*100);
            var stocks=settings.PrinterSettings.PaperSizes.Cast<PaperSize>().ToList();
            PaperSize paper=stocks.FirstOrDefault(s=>Math.Abs(s.Width-w)<=2&&Math.Abs(s.Height-h)<=2);
            bool rotate=false;
            if(paper==null)
            {
                paper=stocks.FirstOrDefault(s=>Math.Abs(s.Width-h)<=2&&Math.Abs(s.Height-w)<=2);
                rotate=paper!=null;
            }
            settings.PaperSize=paper??new PaperSize("QSL "+page.Width.ToString("0.##")+" x "+page.Height.ToString("0.##")+" mm",w,h);
            settings.Landscape=rotate;settings.Margins=new Margins(0,0,0,0);
        }
        public static void DrawPage(PrintPageEventArgs e,DocumentPage page)
        {
            try
            {
                RectangleF area=DeviceArea(e.Graphics,page);
                var state=e.Graphics.Save();
                try
                {
                    e.Graphics.PageUnit=GraphicsUnit.Pixel;
                    e.Graphics.ScaleTransform(e.Graphics.DpiX/25.4f,e.Graphics.DpiY/25.4f);
                    e.Graphics.TranslateTransform(-area.Left,-area.Top);
                    page.Paint(e.Graphics);
                }
                finally { e.Graphics.Restore(state); }
            }
            catch { e.Cancel=true;throw; }
        }
        public static void ValidatePrintableArea(DocumentPage page,PageSettings settings)
        {
            RectangleF area;
            using(var graphics=settings.PrinterSettings.CreateMeasurementGraphics(settings))area=DeviceArea(graphics,page);
            using(var image=page.Render(96))
            {
                var bits=image.LockBits(new Rectangle(0,0,image.Width,image.Height),System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                int left=image.Width,top=image.Height,right=-1,bottom=-1;var row=new byte[image.Width*3];
                try{for(int y=0;y<image.Height;y++){System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),row,0,row.Length);for(int x=0;x<image.Width;x++)if(row[x*3]<220||row[x*3+1]<220||row[x*3+2]<220){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=y;}}}
                finally{image.UnlockBits(bits);}
                const float mm=25.4f/96;const float tolerance=.45f;
                if(right>=0&&(left*mm<area.Left-tolerance||top*mm<area.Top-tolerance||(right+1)*mm>area.Right+tolerance||(bottom+1)*mm>area.Bottom+tolerance))
                    throw new InvalidOperationException("当前打印机的不可打印边缘会裁切单据。请增加边距、调整偏移、选择支持的无边距纸张，或明确减小缩放后重新预览。");
            }
        }
    }
}


