using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace QslPost
{
    public class FormTemplate
    {
        public string Key, Title, Group, Description;
        public float Width, Height;
        public FormTemplate Following { get; internal set; }
        public bool DoubleSided { get; internal set; }
        public FormTemplate(string key,string title,string group,float width,float height,string description) { Key=key;Title=title;Group=group;Width=width;Height=height;Description=description; }
        public string SizeText { get { return Width.ToString("0.##",CultureInfo.InvariantCulture)+" × "+Height.ToString("0.##",CultureInfo.InvariantCulture)+" mm"; } }
        public override string ToString() { return Title+"  ·  "+SizeText; }
    }
    public class DocumentContext
    {
        public Database Db;
        public Bag Bag;
        public Card Card;
        public CaseRecord Case;
        public bool BlankSource;
        public Dictionary<string,string> Fields=new Dictionary<string,string>();
        internal Dictionary<string,List<FormSlot>> Placements=new Dictionary<string,List<FormSlot>>();
        public string V(string key,string fallback) { string value; return Fields.TryGetValue(key,out value)?value:fallback??""; }
        public string From { get { return V("From", Case!=null?Case.From:Bag!=null?Bag.From:Card!=null?Card.FromCall:BlankSource?"":Db.Settings.Callsign); } }
        public string To { get { return V("To", Case!=null?Case.To:Bag!=null?Bag.To:Card!=null?Card.ToCall:""); } }
        public string SenderAddress
        {
            get
            {
                if(BlankSource)return V("AddressFrom", "");
                if(Card!=null&&Card.SenderAddress.Length>0)return V("AddressFrom",DemoData.Address(Db,Card.FromCall,Card.SenderAddress));
                string sender=From;
                if(sender.Length==0||String.Equals(sender,Db.Settings.Organization,StringComparison.OrdinalIgnoreCase))sender=Db.Settings.Callsign;
                var value=String.Equals(sender,Db.Settings.Callsign,StringComparison.OrdinalIgnoreCase)?Db.Settings.Address:ContactAddress(sender);
                return V("AddressFrom",DemoData.Address(Db,sender,value));
            }
        }
        public string RecipientAddress
        {
            get
            {
                if(BlankSource)return V("AddressTo", "");
                var address=Card!=null&&Card.RecipientAddress.Length>0?DemoData.Address(Db,Card.ToCall,Card.RecipientAddress):DemoData.Address(Db,To,ContactAddress(To));
                return V("AddressTo",address);
            }
        }
        string ContactAddress(string callsign)
        {
            var contact=Db.Contacts.FirstOrDefault(c=>String.Equals(c.Callsign,callsign,StringComparison.OrdinalIgnoreCase));
            return contact==null?"":contact.Address;
        }
        public string Reference { get { return V("Reference",Case!=null?Case.Id:Bag!=null?Bag.DispatchId:Card!=null?Card.Id:""); } }
        public string Date
        {
            get
            {
                if(BlankSource)return V("Date", "");
                string time = Case != null ? Case.CreatedUtc : Bag != null ? (Bag.SealedUtc.Length > 0 && Bag.State != "草稿" ? Bag.SealedUtc : Bag.CreatedUtc) : "";
                DateTimeOffset issued;
                string date = DateTimeOffset.TryParse(time, out issued) ? issued.ToLocalTime().ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd");
                return V("Date", date);
            }
        }
        public string Operator { get { return V("Operator",Case!=null?Case.Operator:BlankSource?"":Db.Settings.Operator); } }
        public string Note { get { return V("Notes",Case!=null?Case.Details:Bag!=null?Bag.Notes:Card!=null?Card.Notes:""); } }
        public List<Card> Cards
        {
            get
            {
                if (Card != null) return new List<Card> { Card };
                if (Bag == null) return new List<Card>();
                if (Bag.State != "草稿" && Bag.SealedCards.Count > 0) return Bag.SealedCards;
                var ids = new HashSet<string>(Bag.CardIds, StringComparer.Ordinal);
                return Db.Cards.Where(c => ids.Contains(c.Id)).ToList();
            }
        }
        public string Tracking
        {
            get { return V("Tracking",String.Join(" / ",Db.Shipments.Where(s=>Bag!=null&&s.BagId==Bag.Id||Card!=null&&(s.CardId==Card.Id||Card.BagId.Length>0&&s.BagId==Card.BagId||Card.PreviousBagIds.Contains(s.BagId))).Select(s=>s.Number).Distinct().ToArray())); }
        }
    }
    public enum PageSide { Single, Front, Back }

    public class DocumentPage
    {
        public float Width,Height;
        public string Name;
        public PageSide Side;
        internal FormTemplate Face;
        public float MinimumScale=.2f;
        public Action<Graphics> Paint;
        public Bitmap Render(int dpi) { return RenderAtScale(dpi/25.4f); }
        internal Bitmap RenderAtScale(float pixelsPerMillimetre)
        {
            var image=new Bitmap((int)Math.Ceiling(Width*pixelsPerMillimetre),(int)Math.Ceiling(Height*pixelsPerMillimetre),PixelFormat.Format24bppRgb);
            try
            {
                float dpi=pixelsPerMillimetre*25.4f;image.SetResolution(dpi,dpi);
                using(var g=Graphics.FromImage(image)) {g.Clear(Color.White);g.PageUnit=GraphicsUnit.Pixel;g.ScaleTransform(pixelsPerMillimetre,pixelsPerMillimetre);Paint(g);}
                return image;
            }
            catch { image.Dispose(); throw; }
        }
    }
    public static class Ink
    {
        public static void Text(Graphics g,string value,float x,float y,float w,float h,float size,bool bold,bool center)
        {
            PrintedText(g,value,"SimSun",x,y,w,h,size,bold,center);
        }
        public static void PrintedText(Graphics g,string value,string family,float x,float y,float w,float h,float size,bool bold,bool center,float minimumPoint=0,bool topAlign=false)
        {
            if(!FitText(g,value,family,new RectangleF(x,y,w,h),size,bold,center,minimumPoint,topAlign,true))
                throw new InvalidOperationException("字段内容超出可印范围。请缩短内容或使用附页。\n\n"+value.Substring(0,Math.Min(35,value.Length)));
        }

        internal static bool FitText(Graphics g,string value,string family,RectangleF area,float size,bool bold,bool center,float minimumPoint,bool topAlign,bool paint)
        {
            if(String.IsNullOrEmpty(value))return true;
            using(var format=new StringFormat { Alignment=center?StringAlignment.Center:StringAlignment.Near,LineAlignment=topAlign?StringAlignment.Near:StringAlignment.Center,Trimming=StringTrimming.None })
            {
                Font font=null;
                try
                {
                    // Graphics coordinates are millimetres. A pixel-unit font follows that world transform.
                    float em=size*25.4f/72f,minEm=Math.Min(size,minimumPoint>0?minimumPoint:size>=7?6:3.2f)*25.4f/72f;bool fits=false;
                    for(int i=0;i<14;i++)
                    {
                        if(font!=null)font.Dispose(); font=new Font(family,em,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);
                        SizeF measured=g.MeasureString(value,font,new SizeF(Math.Max(.5f,area.Width-.8f),100000),format);
                        if(measured.Height<=area.Height-.4f){fits=true;break;}
                        if(em<=minEm+.001f)break;
                        em=Math.Max(minEm,em*.9f);
                    }
                    if(fits&&paint)g.DrawString(value,font,Brushes.Black,new RectangleF(area.X+.4f,area.Y+.2f,area.Width-.8f,area.Height-.4f),format);
                    return fits;
                }
                finally { if(font!=null)font.Dispose(); }
            }
        }
        public static void T(Graphics g,string text,float x,float y,float w,float h,float size) { Text(g,text,x,y,w,h,size,false,false); }
        public static void C(Graphics g,string text,float x,float y,float w,float h,float size) { Text(g,text,x,y,w,h,size,false,true); }
        public static void Line(Graphics g,float x,float y,float x2,float y2) { using(var p=new Pen(Color.Black,.18f))g.DrawLine(p,x,y,x2,y2); }
        public static void Box(Graphics g,float x,float y,float w,float h) { using(var p=new Pen(Color.Black,.18f))g.DrawRectangle(p,x,y,w,h); }
        public static void Cell(Graphics g,string text,float x,float y,float w,float h,float size) { Box(g,x,y,w,h);T(g,text,x+.4f,y+.2f,w-.8f,h-.4f,size); }
        public static void Footer(Graphics g,float width,float height,string text) { C(g,text,3,height-5,width-6,3,4.7f); }
        public static void Stamp(Graphics g,float x,float y,float d,string label)
        {
            using(var p=new Pen(Color.Gray,.15f)){p.DashStyle=DashStyle.Dash;g.DrawEllipse(p,x,y,d,d);} C(g,label,x-3,y+d+1,d+6,4,5);
        }
    }
    public static class Barcode128
    {
        const float MinimumModule = .18f;
        // ISO/IEC 15417 Code 128 patterns, checksum and stop are encoded in the symbol.
        static readonly string[] P=("212222 222122 222221 121223 121322 131222 122213 122312 132212 221213 221312 231212 112232 122132 122231 113222 123122 123221 223211 221132 221231 213212 223112 312131 311222 321122 321221 312212 322112 322211 212123 212321 232121 111323 131123 131321 112313 132113 132311 211313 231113 231311 112133 112331 132131 113123 113321 133121 313121 211331 231131 213113 213311 213131 311123 311321 331121 312113 312311 332111 314111 221411 431111 111224 111422 121124 121421 141122 141221 112214 112412 122114 122411 142112 142211 241211 221114 413111 241112 134111 111242 121142 121241 114212 124112 124211 411212 421112 421211 212141 214121 412121 111143 111341 131141 114113 114311 411113 411311 113141 114131 311141 411131 211412 211214 211232 2331112").Split(' ');
        public static List<int> Codes(string value)
        {
            if(String.IsNullOrEmpty(value)||value.Any(c=>c<32||c>126))throw new InvalidOperationException("Code 128 条码需要非空的 ASCII 字母、数字或符号。");
            bool numeric=DigitRun(value,0)==value.Length&&value.Length%2==0;
            var codes=new List<int>{numeric?105:104};
            int at=0;
            while(at<value.Length)
            {
                if(numeric)
                {
                    if(at+1<value.Length&&IsDigit(value[at])&&IsDigit(value[at+1]))
                    {
                        codes.Add((value[at]-'0')*10+value[at+1]-'0');
                        at+=2;
                        continue;
                    }
                    codes.Add(100);numeric=false;
                }
                int digits=DigitRun(value,at);
                if(digits%2==0&&(digits>=6||(digits>=4&&at+digits==value.Length)))
                {
                    codes.Add(99);numeric=true;continue;
                }
                codes.Add(value[at++]-32);
            }
            int sum=codes[0];for(int i=1;i<codes.Count;i++)sum+=i*codes[i];codes.Add(sum%103);codes.Add(106);return codes;
        }
        static bool IsDigit(char value) { return value>='0'&&value<='9'; }
        static int DigitRun(string value,int start)
        {
            int end=start;
            while(end<value.Length&&IsDigit(value[end]))end++;
            return end-start;
        }
        static int Modules(List<int> codes) { return (codes.Count-1)*11+13+20; }
        public static float MinimumWidth(string value,float module=MinimumModule) { return module*Modules(Codes(value)); }
        public static void Draw(Graphics g,string value,RectangleF rect,bool human,float fixedModule=0)
        {
            var codes=Codes(value);int modules=Modules(codes);
            float unit=fixedModule>0?fixedModule:rect.Width/modules;
            if(unit<MinimumModule||unit*modules>rect.Width+.001f)throw new InvalidOperationException("条码区域太窄，请使用较短的扫描编号或独立条码模板。");
            float step=0,leftOffset=0;
            if(fixedModule>0)
            {
                using(var transform=g.Transform)
                {
                    var parts=transform.Elements;
                    if(parts[0]>0&&parts[1]==0&&parts[2]==0){step=parts[0];leftOffset=parts[4];}
                }
                if(step>0)
                {
                    float snapped=(float)Math.Round(unit*step)/step;
                    if(snapped>=MinimumModule&&snapped*modules<=rect.Width)unit=snapped;
                }
            }
            float x=rect.X+(rect.Width-modules*unit)/2+10*unit;
            if(step>0&&Math.Abs(unit*step-Math.Round(unit*step))<.002)
                x=((float)Math.Round(x*step+leftOffset)-leftOffset)/step;
            var saved=g.Save();g.SmoothingMode=SmoothingMode.None;g.PixelOffsetMode=PixelOffsetMode.None;
            g.FillRectangle(Brushes.White,rect);float height=rect.Height-(human?4:0);
            foreach(int code in codes){bool black=true;foreach(char n in P[code]){float w=(n-'0')*unit;if(black)g.FillRectangle(Brushes.Black,x,rect.Y,w,height);x+=w;black=!black;}}
            g.Restore(saved);if(human)Ink.C(g,value,rect.X,rect.Bottom-4,rect.Width,4,5.8f);
        }
    }
    public static class Documents
    {
        public static readonly List<FormTemplate> Templates=new List<FormTemplate>{
            new FormTemplate("cp85","国际袋牌 · SAL / CP85","袋牌",68,46,"对应提供的袋牌第 1 页，原尺寸。独立 S9 条码可另行打印。"),
            new FormTemplate("cp84","国际袋牌 · 航空 / CP84","袋牌",68,46,"对应提供的袋牌第 2 页。"),
            new FormTemplate("cp83","国际袋牌 · 水陆 / CP83","袋牌",68,46,"对应提供的袋牌第 3 页。"),
            new FormTemplate("domplain","国内袋牌 · 平信","袋牌",50,85,"对应国内袋牌第 1 页，无单平信。"),
            new FormTemplate("domregistered","国内袋牌 · 挂信","袋牌",50,85,"对应国内袋牌第 2 页，保留号码栏。"),
            new FormTemplate("domcombined","国内袋牌 · 合封","袋牌",50,85,"对应国内袋牌第 3 页。"),
            new FormTemplate("domempty","国内袋牌 · 空袋","袋牌",50,85,"对应国内袋牌第 4 页，号码与件数栏。"),
            new FormTemplate("manifest","封发卡片清单 · 邮1301式","清单与路单",150,215,"双列 40 格，自动续页、页计和总计。"),
            new FormTemplate("waybill","收发卡片路单","清单与路单",145,215,"20 格交接路单，带挂 / 平 / 刷分类、交接签章。"),
            new FormTemplate("verify","验单","查验与答复",130,180,"差错与不符事项，关联卡片、袋号和原物流记录。"),
            new FormTemplate("reply","复验","查验与答复",130,180,"答复原验单，保留原验单编号、附件、经办及主管。"),
            new FormTemplate("domquery","国内查单","查验与答复",130,205,"业无1601式，含查询通联回单；通联记录自动续页。"),
            new FormTemplate("domreply","查询通联回单 · 背面","查验与答复",130,205,"记录答复和实物卡片答复分栏。"),
            new FormTemplate("intlquery","国际查单","查验与答复",210,297,"含原件正反面，填写邮件、名址、通联／收听与查复。"),
            new FormTemplate("intlreply","国际查单 · 背面","查验与答复",210,297,"中法主排、英文对照；中转意见、实收及补发答复。"),
            new FormTemplate("s8","S8 格式总封条码","条码与回执",148,40,"20 位总封编号；内部节点代码，不作为运营商签发的邮件号码。"),
            new FormTemplate("s9","S9 格式邮袋条码","条码与回执",148,45,"29 位袋号；带 Code 128 校验与静区。"),
            new FormTemplate("cardlabel","单张卡片编号标签","条码与回执",100,40,"永久卡片编号及条码，扫描后直接溯源。"),
            new FormTemplate("receipt","QSL 接收交付回执","条码与回执",148,105,"接收台、实收件数、差异和签收栏。"),
            new FormTemplate("rd10","RD10 卡片未收声明","条码与回执",148,210,"与国际查单中的未收声明配套的 QSL 内部文书。")
        };
        static readonly Dictionary<string,FormTemplate> PreviousKeys=new Dictionary<string,FormTemplate>(StringComparer.OrdinalIgnoreCase);

        static Documents()
        {
            Templates.AddRange(UpuForms.Catalog);
            ConnectPages("domquery", "domreply", true);
            ConnectPages("intlquery", "intlreply", true);
            ConnectPages("upu-cn08", "upu-cn08-back", true);
            ConnectPages("upu-cn17", "upu-cn17-back", true);
            ConnectPages("upu-cn22", "upu-cn22-back", true);
            ConnectPages("upu-cn22-max", "upu-cn22-max-back", true);
            ConnectPages("upu-cn23", "upu-cn23-back", true);
            ConnectPages("upu-cn24", "upu-cn24-back", true);
            ConnectPages("upu-cn43", "upu-cn43-back", true);
            ConnectPages("upu-cn54bis", "upu-cn54bis-back", false);
            ConnectPages("upu-cp72", "upu-cp72-back", true);
            ConnectPages("upu-cp72-label", "upu-cp72-label-back", true);
            ConnectPages("upu-cp72-customs", "upu-cp72-customs-back", true);
            ConnectPages("upu-cp78", "upu-cp78-back", true);
            ConnectPages("upu-cp82", "upu-cp82-back", false);
            PreviousKeys.Add("upu-cn01-back", Templates.Single(t=>t.Key=="upu-cn01"));
            Templates.RemoveAll(t=>t.Key=="upu-cn01-back");
        }

        static void ConnectPages(string first, string second, bool duplex)
        {
            var template=Templates.Single(t=>t.Key==first);
            var following=Templates.Single(t=>t.Key==second);
            template.Following=following;
            template.DoubleSided=duplex;
            if(duplex)
            {
                template.Title=template.Title.Replace(" / 正面", "").Replace(" / 申请面", "").Replace("交运联正面", "交运联");
                template.Description+=" · 含正反面";
            }
            else template.Description+=" · 含接续页";
            Templates.Remove(following);
            PreviousKeys.Add(second,template);
        }

        public static FormTemplate FindTemplate(string key)
        {
            FormTemplate previous;
            return PreviousKeys.TryGetValue(key??"",out previous)?previous:Templates.FirstOrDefault(t=>t.Key==key);
        }

        public static List<DocumentPage> Build(FormTemplate template,DocumentContext ctx)
        {
            var pages=BuildFace(template,ctx);
            if(template.Following==null)return pages;

            var following=BuildFace(template.Following,ctx);
            if(template.DoubleSided)
            {
                pages[0].Side=PageSide.Front;
                following[0].Side=PageSide.Back;
                pages.Insert(1,following[0]);
                pages.AddRange(following.Skip(1));
            }
            else pages.AddRange(following);
            return pages;
        }

        static List<DocumentPage> BuildFace(FormTemplate template,DocumentContext ctx)
        {
            List<DocumentPage> original;
            if(OriginalForms.TryBuild(template,ctx,out original))return original;
            var upu=template as UpuTemplate;
            if(upu!=null)return UpuForms.Build(upu,ctx);
            int per=template.Key=="manifest"?40:template.Key=="waybill"?20:template.Key=="domquery"?7:1;
            List<Card> cards=ctx.Cards;bool paginate=template.Key=="manifest"||template.Key=="waybill"||template.Key=="domquery";int count=paginate?Math.Max(1,(cards.Count+per-1)/per):1;
            var pages=new List<DocumentPage>();
            for(int i=0;i<count;i++)
            {
                int index=i;
                string barCode=template.Key=="cardlabel"?(ctx.Card==null?"":ctx.Card.Id):ctx.Bag==null?"":template.Key=="s8"?ctx.Bag.DispatchId:ctx.Bag.Id;
                bool hasBarcode=template.Key=="s8"||template.Key=="s9"||template.Key=="cardlabel";
                float minimumScale=hasBarcode&&barCode.Length>0?Barcode128.MinimumWidth(barCode)/(template.Width-8):.2f;
                pages.Add(new DocumentPage {Name=template.Title,Width=template.Width,Height=template.Height,MinimumScale=minimumScale,Paint=g=>{
                    g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    switch(template.Key)
                    {
                        case "manifest":ChinesePostalForms.Manifest(g,ctx,index,count);break;
                        case "waybill":ChinesePostalForms.Waybill(g,ctx,index,count);break;
                        case "verify":case "reply":
                            bool reply=template.Key=="reply";
                            ChinesePostalForms.Verification(g,ctx,reply,Short(reply?ctx.V("Reply",ctx.Case==null?ctx.Note:ctx.Case.Reply):ctx.Note));break;
                        case "domquery":ChinesePostalForms.DomesticInquiry(g,ctx,index,count);break;
                        case "domreply":ChinesePostalForms.DomesticReply(g,ctx,Short(ctx.V("Reply",ctx.Case==null?"":ctx.Case.Reply),185),Short(ctx.Note,185));break;
                        case "intlquery":case "intlreply":
                            ChinesePostalForms.InternationalInquiry(g,ctx,template.Key=="intlreply",Short(ctx.Note,330),Short(ctx.V("Reply",ctx.Case==null?"":ctx.Case.Reply),390));break;
                        case "s8":case "s9":case "cardlabel":BarcodePage(g,ctx,template);break;
                        case "receipt":Receipt(g,ctx);break;case "rd10":Declaration(g,ctx);break;
                        default:if(template.Key.StartsWith("cp"))ChinesePostalForms.CompactBag(g,ctx,template);else ChinesePostalForms.DomesticBag(g,ctx,template);break;
                    }
                }});
            }
            // Preserve full long narratives on numbered continuation sheets, rather than truncating a form cell.
            string narrative=ctx.Note;
            string fullReply=ctx.V("Reply",ctx.Case==null?"":ctx.Case.Reply);
            if(fullReply.Length>0)narrative+="\n答复：\n"+fullReply;
            int annexLimit=template.Key=="domreply"?185:template.Key=="intlquery"||template.Key=="intlreply"?330:template.Key=="receipt"?190:template.Key=="rd10"?400:650;
            if(narrative.Length>annexLimit && (template.Group=="查验与答复"||template.Key=="rd10"||template.Key=="receipt"))
            {
                for(int p=0;p<(narrative.Length+1399)/1400;p++)
                {
                    int start=p*1400;string part=narrative.Substring(start,Math.Min(1400,narrative.Length-start));int n=p+1;
                    pages.Add(new DocumentPage {Name="续页",Width=148,Height=210,Paint=g=>{
                        PostalType.FormTitle(g,PostalType.Label("Annex","单 据 续 页"),PostalFormInk.Area(9,6,130,16),true,true);
                        PostalFormInk.Field(g,"Reference","关联编号 "+n,ctx.Reference,9,24,130,12,true);
                        PostalFormInk.Frame(g,PostalFormInk.Area(9,36,130,162),.2f);
                        PostalType.Text(g,part,PostalType.Fang,PostalFormInk.Area(11,38,126,156),8.8f,false,false,true);
                        PostalFormInk.Footnote(g,148,210,"","",true);
                    }});
                }
            }
            return pages;
        }
        static string Short(string text,int max=650)
        {
            if(text.Length<=max)return text;
            int stop=max-50;
            if(Char.IsHighSurrogate(text[stop-1]))stop--;
            return text.Substring(0,stop)+"…\n见续页 / Voir annexe / See annex";
        }
        static string BagId(DocumentContext c) {return c.Bag==null?"":c.Bag.Id;}
        static void BarcodePage(Graphics g,DocumentContext c,FormTemplate t)
        {
            bool card=t.Key=="cardlabel";string code=card?(c.Card==null?"":c.Card.Id):c.Bag==null?"":t.Key=="s8"?c.Bag.DispatchId:c.Bag.Id;
            var heading=PostalType.Label(card?"LocalCardBarcode":t.Key=="s8"?"LocalDispatchBarcode":"LocalBagBarcode",
                                        card?"QSL 卡片编号签":t.Key=="s8"?"S8 总封条码":"S9 邮袋条码");
            PostalType.Caption(g,heading,PostalFormInk.Area(4,1.6f,t.Width-8,7.8f),7.4f,true,true,true);
            PostalType.Text(g,c.From+" → "+c.To,PostalType.Serif,PostalFormInk.Area(4,10,t.Width-8,4),7.1f,false,true);
            if(code.Length>0)Barcode128.Draw(g,code,new RectangleF(4,16,t.Width-8,t.Height-23),true);
            else PostalType.Caption(g,PostalType.Label("LabelCode","编号"),PostalFormInk.Area(4,16,t.Width-8,t.Height-23),7.7f,true,true);
            PostalFormInk.Footnote(g,t.Width,t.Height,"","",true);
        }
        static void Receipt(Graphics g,DocumentContext c)
        {
            PostalType.FormTitle(g,PostalType.Label("LocalReceipt","QSL 接 收 / 交 付 回 执"),PostalFormInk.Area(9,6,130,16.6f),true,true);
            PostalFormInk.Part(g,"PartHandover","卡片交付凭据","1",8,25,132,8,true);
            PostalFormInk.Field(g,"ReferenceBag","邮袋／卡片",c.Card==null?BagId(c):c.Card.Id,8,33,67,12,true);
            PostalFormInk.Field(g,"Tracking","物流号码",c.Tracking,75,33,65,12,true);
            PostalFormInk.Field(g,"Expected","应收",c.Cards.Count.ToString(),8,45,26,11,true);
            PostalFormInk.Field(g,"Actual","实收",c.V("Actual",""),34,45,26,11,true);
            PostalFormInk.Field(g,"To","接收台",c.To,60,45,80,11,true);
            PostalFormInk.Field(g,"Notes","差异及说明",Short(c.Note,190),8,56,132,22,true);
            PostalFormInk.Field(g,"Date","日期",c.Date,8,78,49,14,true);
            PostalFormInk.Signature(g,"ToSign","接收方签章",83,90,57,true);
            PostalFormInk.Footnote(g,148,105,"","",true);
        }
        static void Declaration(Graphics g,DocumentContext c)
        {
            PostalType.FormTitle(g,PostalType.Label("LocalDeclaration","卡 片 未 收 声 明 / RD10"),PostalFormInk.Area(10,7,128,18),true,true);
            PostalFormInk.Field(g,"Case","文书编号",c.Reference,10,28,78,15,true);
            PostalFormInk.Field(g,"Date","登记日期",c.Date,88,28,50,15,true);
            PostalFormInk.Part(g,"PartDeclare","所查卡片与声明","A",10,44,128,9,true);
            PostalFormInk.Field(g,"CallFrom","原寄呼号",c.From,10,53,64,14,true);
            PostalFormInk.Field(g,"CallTo","寄达呼号",c.To,74,53,64,14,true);
            PostalFormInk.Field(g,"ReferenceBag","卡片／邮袋编号",c.Card==null?BagId(c):c.Card.Id,10,67,128,14,true);
            PostalFormInk.Field(g,"Tracking","物流号码",c.Tracking,10,81,128,14,true);
            PostalType.Caption(g,new PostalLegend("截至上列日期，本台尚未收到所查卡片。",
                "À la date indiquée, notre station n'a pas reçu les cartes QSL.","On the stated date our station had not received the QSL cards."),
                PostalFormInk.Area(10,100,128,15),8.4f,false,true);
            PostalFormInk.Choices(g,PostalFormInk.Area(10,118,128,12),true,"Resend","请求补寄","Eqsl","接受 eQSL","Registered","挂号重寄");
            PostalFormInk.Field(g,"Notes","说明",Short(c.Note,400),10,134,128,42,true);
            PostalFormInk.Signature(g,"BodySignature","声明人签字及日期",60,193,78,true);
            PostalFormInk.Footnote(g,148,210,"","",true);
        }
        public static List<DocumentPage> Compose(List<DocumentPage> originals,PrintProfile profile,int repeat)
        {
            if(repeat<1||repeat>500)throw new InvalidOperationException("份数须在 1-500 之间。");
            bool hasPair=originals.Any(p=>p.Side!=PageSide.Single);
            var expanded=new List<DocumentPage>();
            for(int i=0;i<repeat;i++)
            {
                if(i>0&&hasPair&&expanded.Count%2!=0)
                {
                    var preceding=expanded.Last();
                    expanded.Add(new DocumentPage{Name="翻面留白",Width=preceding.Width,Height=preceding.Height,Paint=g=>{}});
                }
                expanded.AddRange(originals);
            }
            if(profile.Stock==PrintStock.Original)return expanded;
            if(profile.Scale<20||profile.Scale>200)throw new InvalidOperationException("缩放比例须在 20%-200% 之间。");
            var output=new List<DocumentPage>();int index=0;float scale=(float)profile.Scale/100f;
            while(index<expanded.Count)
            {
                DocumentPage first=expanded[index];if(scale<first.MinimumScale)throw new InvalidOperationException("当前缩放会使条码过窄，无法可靠扫描。请提高缩放或使用更宽的纸张。");
                bool a4=profile.Stock==PrintStock.A4;
                float pw=a4?(profile.Landscape?297:210):(float)profile.Width,ph=a4?(profile.Landscape?210:297):(float)profile.Height;
                float m=(float)profile.Margin;float w=first.Width*scale,h=first.Height*scale;
                int cols=(int)Math.Floor((pw-2*m+.01f)/w),rows=(int)Math.Floor((ph-2*m+.01f)/h);
                if(cols<1||rows<1)throw new InvalidOperationException("单据大于当前纸张可用区域。请调大自定义纸张、改横向、减小边距或明确调整缩放。");
                if(hasPair){cols=1;rows=1;}
                var placements=new List<Tuple<DocumentPage,RectangleF>>();
                for(int r=0;r<rows&&index<expanded.Count;r++)for(int c=0;c<cols&&index<expanded.Count;c++)
                {
                    DocumentPage p=expanded[index];if(Math.Abs(p.Width-first.Width)>.1||Math.Abs(p.Height-first.Height)>.1)break;
                    if(scale<p.MinimumScale)throw new InvalidOperationException("排版后的条码过窄，无法可靠扫描。请提高缩放比例。");
                    float x=m+c*w+(float)profile.OffsetX,y=m+r*h+(float)profile.OffsetY;
                    if(x<-.01||y<-.01||x+w>pw+.01||y+h>ph+.01)throw new InvalidOperationException("校准偏移使单据超出纸张边缘，请调整偏移或边距。");
                    placements.Add(Tuple.Create(p,new RectangleF(x,y,w,h)));index++;
                }
                var placed=placements;
                output.Add(new DocumentPage{Name=first.Name,Width=pw,Height=ph,Side=first.Side,Face=first.Face,Paint=g=>{foreach(var p in placed){var s=g.Save();g.TranslateTransform(p.Item2.X,p.Item2.Y);g.ScaleTransform(scale,scale);p.Item1.Paint(g);g.Restore(s);}}});
            }
            return output;
        }
    }
    public static class PdfExport
    {
        static byte[] ZipRgb(Bitmap bitmap)
        {
            Rectangle area=new Rectangle(0,0,bitmap.Width,bitmap.Height);var data=bitmap.LockBits(area,ImageLockMode.ReadOnly,PixelFormat.Format24bppRgb);
            try
            {
                using(var result=new MemoryStream())
                {
                    result.WriteByte(0x78);result.WriteByte(0x9C);uint a=1,b=0;
                    using(var deflate=new DeflateStream(result,CompressionMode.Compress,true))
                    {
                        byte[] row=new byte[bitmap.Width*3];
                        for(int y=0;y<bitmap.Height;y++)
                        {
                            System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);
                            for(int x=0;x<row.Length;x+=3){byte t=row[x];row[x]=row[x+2];row[x+2]=t;}
                            for(int start=0;start<row.Length;start+=5552)
                            {
                                int end=Math.Min(row.Length,start+5552);
                                for(int x=start;x<end;x++){a+=row[x];b+=a;}
                                a%=65521;b%=65521;
                            }
                            deflate.Write(row,0,row.Length);
                        }
                    }
                    uint adler=(b<<16)|a;result.WriteByte((byte)(adler>>24));result.WriteByte((byte)(adler>>16));result.WriteByte((byte)(adler>>8));result.WriteByte((byte)adler);return result.ToArray();
                }
            }
            finally { bitmap.UnlockBits(data); }
        }
        public static int Resolution(DocumentPage page)
        {
            const double pixels=15000000d;
            return Math.Min(600,(int)Math.Floor(25.4*Math.Sqrt(pixels/(page.Width*page.Height))));
        }
        public static void Write(string destination,List<DocumentPage> pages)
        {
            string temp=destination+".new";var offsets=new List<long>{0};
            try
            {
                using(var fs=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
                {
                    Action<string> write=text=>{byte[] bytes=Encoding.ASCII.GetBytes(text);fs.Write(bytes,0,bytes.Length);};
                    Action<int,string,byte[]> obj=(id,header,stream)=>{offsets.Add(fs.Position);write(id+" 0 obj\n"+header);if(stream!=null){write("\nstream\n");fs.Write(stream,0,stream.Length);write("\nendstream");}write("\nendobj\n");};
                    write("%PDF-1.4\n");obj(1,"<< /Type /Catalog /Pages 2 0 R >>",null);
                    obj(2,"<< /Type /Pages /Count "+pages.Count+" /Kids ["+String.Join(" ",Enumerable.Range(0,pages.Count).Select(i=>(3+i*3)+" 0 R").ToArray())+"] >>",null);
                    for(int i=0;i<pages.Count;i++)
                    {
                        DocumentPage p=pages[i];int id=3+i*3;string w=(p.Width*72/25.4f).ToString("0.###",CultureInfo.InvariantCulture),h=(p.Height*72/25.4f).ToString("0.###",CultureInfo.InvariantCulture);
                        obj(id,"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 "+w+" "+h+"] /Resources << /XObject << /Im "+(id+1)+" 0 R >> >> /Contents "+(id+2)+" 0 R >>",null);
                        using(Bitmap bitmap=p.Render(Resolution(p))) {byte[] rgb=ZipRgb(bitmap);obj(id+1,"<< /Type /XObject /Subtype /Image /Width "+bitmap.Width+" /Height "+bitmap.Height+" /ColorSpace /DeviceRGB /BitsPerComponent 8 /Interpolate false /Filter /FlateDecode /Length "+rgb.Length+" >>",rgb);}
                        byte[] instructions=Encoding.ASCII.GetBytes("q "+w+" 0 0 "+h+" 0 0 cm /Im Do Q");obj(id+2,"<< /Length "+instructions.Length+" >>",instructions);
                    }
                    long xref=fs.Position;write("xref\n0 "+offsets.Count+"\n0000000000 65535 f \n");for(int i=1;i<offsets.Count;i++)write(offsets[i].ToString("0000000000")+" 00000 n \n");write("trailer\n<< /Size "+offsets.Count+" /Root 1 0 R >>\nstartxref\n"+xref+"\n%%EOF\n");fs.Flush(true);
                }
                if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);
            }
            catch{if(File.Exists(temp))File.Delete(temp);throw;}
        }
    }
}
