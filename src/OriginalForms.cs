using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Windows.Forms;

namespace QslPost
{
    internal sealed class FormSlot
    {
        internal string Key, Label;
        internal RectangleF Area;
        internal float PointSize, Pitch;
        internal int Rows, Offset;
        internal bool Relative, Barcode, Center;

        internal RectangleF Bounds(RectangleF art,int row)
        {
            var bounds=Area;
            bounds.Y+=row*Pitch;
            if(!Relative)return bounds;
            return new RectangleF(art.X+bounds.X*art.Width,art.Y+bounds.Y*art.Height,
                                  bounds.Width*art.Width,bounds.Height*art.Height);
        }
    }

    internal static class OriginalForms
    {
        static readonly ResourceManager Art=new ResourceManager("QslPost.FormArt",typeof(OriginalForms).Assembly);
        static readonly Dictionary<string,List<FormSlot>> Presets=LoadSlots();

        static Dictionary<string,List<FormSlot>> LoadSlots()
        {
            var slots=new Dictionary<string,List<FormSlot>>(StringComparer.Ordinal);
            using(var input=typeof(OriginalForms).Assembly.GetManifestResourceStream("QslPost.FormSlots.tsv"))
            using(var reader=new StreamReader(input))
            {
                string line;
                while((line=reader.ReadLine())!=null)
                {
                    if(line.Length==0||line[0]=='#')continue;
                    var parts=line.Split('\t');
                    var slot=new FormSlot{Key=parts[1],Label=parts[2],Area=new RectangleF(Number(parts[3]),Number(parts[4]),Number(parts[5]),Number(parts[6])),
                        PointSize=Number(parts[7]),Relative=parts[8].Contains("ratio"),Barcode=parts[8].Contains("bar"),Center=parts[8].Contains("center"),
                        Rows=parts.Length>9?(int)Number(parts[9]):0,Pitch=parts.Length>10?Number(parts[10]):0,Offset=parts.Length>11?(int)Number(parts[11]):0};
                    List<FormSlot> entries;
                    if(!slots.TryGetValue(parts[0],out entries))slots.Add(parts[0],entries=new List<FormSlot>());
                    entries.Add(slot);
                }
            }
            return slots;
        }

        static float Number(string value){return Single.Parse(value,CultureInfo.InvariantCulture);}
        static byte[] Artwork(FormTemplate face){return (byte[])Art.GetObject(face.Key,CultureInfo.InvariantCulture);}
        internal static bool HasArt(FormTemplate face){return face!=null&&Artwork(face)!=null;}

        internal static List<FormSlot> Slots(FormTemplate face,DocumentContext context)
        {
            List<FormSlot> presets,entered;
            var result=Presets.TryGetValue(face.Key,out presets)?new List<FormSlot>(presets):new List<FormSlot>();
            if(context.Placements.TryGetValue(face.Key,out entered))
            {
                foreach(var slot in entered)
                {
                    result.RemoveAll(old=>old.Key==slot.Key&&old.Rows==0&&!old.Barcode);
                    if(slot.Area.Width>0&&slot.Area.Height>0)result.Add(slot);
                }
            }
            return result;
        }

        static RectangleF ArtworkArea(FormTemplate face,Image image)
        {
            // The manual sometimes reproduces a specimen at a different aspect ratio
            // from the stock size. Preserve the specimen's aspect ratio on that stock.
            if(!(face is UpuTemplate))return new RectangleF(0,0,face.Width,face.Height);
            float scale=Math.Min(face.Width/image.Width,face.Height/image.Height);
            float width=image.Width*scale,height=image.Height*scale;
            return new RectangleF((face.Width-width)/2,face.Key=="upu-cn17-back"?0:(face.Height-height)/2,width,height);
        }

        internal static RectangleF ArtworkArea(FormTemplate face)
        {
            using(var stream=new MemoryStream(Artwork(face),false))
            using(var image=Image.FromStream(stream))return ArtworkArea(face,image);
        }

        static string Value(DocumentContext c,string key,Card card=null)
        {
            string value;
            if(c.Fields.TryGetValue(key,out value))return value;
            var current=card??c.Card;
            switch(key)
            {
                case "RefSuffix":
                    string reference=c.Reference;
                    int split=reference.LastIndexOf('-');
                    return split<0?reference:reference.Substring(split+1);
                case "DispatchSerial": return c.Bag!=null&&c.Bag.DispatchId.Length>=20?c.Bag.DispatchId.Substring(15):"";
                case "CardSuffix":
                    return current==null?"":current.Id;
                case "RouteCall": return current==null?"":current.Direction=="进口"?current.FromCall:current.ToCall;
                case "SendRst": return current==null?"":current.RstSent;
                case "RecvRst": return current==null?"":current.RstReceived;
                case "QsoDate": return QsoPart(current,0,10);
                case "QsoTime": return QsoPart(current,11,5);
                case "DateYear": return DateComponent(c.Date,0,4);
                case "DateMonth": return DateComponent(c.Date,5,2);
                case "DateDay": return DateComponent(c.Date,8,2);
                case "QslSummary":
                    if(current==null)return "";
                    string signal=current.RstSent+"/"+current.RstReceived;
                    return String.Join("  ",new[]{current.Id,current.FromCall+" → "+current.ToCall,
                        current.QsoUtc.Length==0?"":current.QsoUtc+" UTC",current.Frequency.Length==0?"":current.Frequency+" MHz",
                        current.Mode,signal=="/"?"":signal}.Where(text=>text.Length>0));
                case "Hour": return QsoPart(current,11,2);
                case "Minute": return QsoPart(current,14,2);
                case "Registered": return current!=null&&current.MailClass=="挂信"?"1":"";
                case "Ordinary": return current!=null&&current.MailClass=="平信"?"1":"";
                case "Printed": return current!=null&&current.MailClass=="印刷品"?"1":"";
                case "ClassRegistered": return c.Cards.Count==0?"":c.Cards.Count(x=>x.MailClass=="挂信").ToString();
                case "ClassOrdinary": return c.Cards.Count==0?"":c.Cards.Count(x=>x.MailClass=="平信").ToString();
                case "ClassPrinted": return c.Cards.Count==0?"":c.Cards.Count(x=>x.MailClass=="印刷品").ToString();
                case "Count": return c.Cards.Count==0?"":c.Cards.Count.ToString();
                case "ReceivedNo": return "";
                case "Special": return c.Bag==null?"":c.Bag.Seal;
                case "BagNumber": return c.Bag==null?"":c.Bag.Id;
                case "ItemId":
                    string tracking=UpuForms.Value(c,key,current,null);
                    return tracking.Length>0?tracking:current==null?"":current.Id;
                case "Sent": return c.Bag==null?"":Clock.Display(c.Bag.HandoverUtc);
                case "EmptyCount": return c.Bag!=null&&c.Bag.MailClass=="空袋"?"1":"";
                case "OperatorDate": return String.Join("  ",new[]{c.Date,c.Operator}.Where(x=>x.Length>0));
                case "SenderContact": return String.Join("  ",new[]{c.From,c.SenderAddress,c.V("SenderFax","")}.Where(x=>x.Length>0));
                case "OriginContact": return String.Join("  ",new[]{c.From,c.V("SenderFax","")}.Where(x=>x.Length>0));
                case "DestinationContact": return String.Join("  ",new[]{c.To,c.V("RecipientFax","")}.Where(x=>x.Length>0));
                case "RowNotes": return current==null?"":current.Notes;
            }
            return UpuForms.Value(c,key,card,null);
        }

        static string QsoPart(Card card,int start,int length)
        {
            string text=card==null?"":card.QsoUtc;
            return text.Length>=start+length?text.Substring(start,length):"";
        }

        static string DateComponent(string date,int start,int length)
        {
            return date.Length>=start+length?date.Substring(start,length):"";
        }

        internal static string ReadValue(DocumentContext context,string key){return Value(context,key);}

        internal static List<Field> Fields(FormTemplate face,DocumentContext context)
        {
            var upu=face as UpuTemplate;
            var slots=Slots(face,context);
            var fields=new List<Field>();
            if(upu!=null)
                fields.AddRange(upu.FillFields.Select(x=>new Field(x.Key,x.Label,Value(context,x.Key)){Multiline=x.Multiline}));
            foreach(var slot in slots.Where(s=>s.Rows==0&&!s.Key.StartsWith("Page",StringComparison.Ordinal)))
            {
                if(fields.Any(f=>f.Key==slot.Key))continue;
                fields.Add(new Field(slot.Key,slot.Label,Value(context,slot.Key)){Multiline=slot.Area.Height>=12&&!slot.Relative||slot.Relative&&slot.Area.Height>=.075f});
            }
            return fields;
        }

        internal static void Edit(FormTemplate template,DocumentContext context,IWin32Window owner,Action rebuild)
        {
            var primary=Fields(template,context);
            if(template.Following!=null)
                foreach(var field in Fields(template.Following,context))
                    if(primary.All(f=>f.Key!=field.Key))primary.Add(field);
            var details=new List<UpuTemplate>();
            foreach(var t in new[]{template,template.Following}.OfType<UpuTemplate>().Where(t=>t.Columns.Length>0))
            {
                details.Add(t);
                primary.Add(new Field("DetailRows."+t.Key,"明细 · "+String.Join(" | ",t.Columns.Select(c=>c.Label)),context.V("DetailRows."+t.Key,"")).Multi());
            }
            if(primary.Count==0)
            {
                using(var place=new FormPlacementDialog(template,context))
                    if(place.ShowDialog(owner)==DialogResult.OK)rebuild();
                return;
            }
            string help="填写内容仅用于本次输出。";
            if(details.Count>0)help+="明细每行一条，各栏以竖线（|）分隔。";
            using(var dialog=new RecordDialog("填写单据",help,primary,d=>{
                foreach(var t in details)UpuForms.ParseRows(d.Value("DetailRows."+t.Key),t.Columns.Length);
                foreach(var field in primary)
                {
                    string entered=d.Value(field.Key);
                    if(entered!=field.Value||context.Fields.ContainsKey(field.Key))context.Fields[field.Key]=entered;
                }
            }))
                if(dialog.ShowDialog(owner)==DialogResult.OK)rebuild();
        }

        internal static bool TryBuild(FormTemplate face,DocumentContext context,out List<DocumentPage> pages)
        {
            var bytes=Artwork(face);
            pages=null;
            if(bytes==null)return false;
            var slots=Slots(face,context);
            RectangleF art;
            using(var stream=new MemoryStream(bytes,false))
            using(var image=Image.FromStream(stream))art=ArtworkArea(face,image);

            var upu=face as UpuTemplate;
            var cards=context.Cards;
            var rows=upu==null?null:UpuForms.Rows(upu,context);
            var columns=upu==null?new string[0]:upu.Columns.Select(c=>c.Key).ToArray();
            int capacity=slots.Where(s=>s.Rows>0).Select(s=>s.Offset+s.Rows).DefaultIfEmpty(0).Max();
            int count=rows==null?cards.Count:rows.Count;
            int sheetCount=capacity>0?Math.Max(1,(count+capacity-1)/capacity):1;
            pages=new List<DocumentPage>();
            var additional=new List<string>();
            using(var proof=new Bitmap(8,8))
            using(var measure=Graphics.FromImage(proof))
            {
            measure.PageUnit=GraphicsUnit.Pixel;
            measure.ScaleTransform(96/25.4f,96/25.4f);
            for(int number=0;number<sheetCount;number++)
            {
                var marks=new List<Tuple<FormSlot,RectangleF,string>>();
                foreach(var slot in slots)
                {
                    if(slot.Rows==0)
                    {
                        string value=slot.Key=="PageNumber"?PageNumber(count,context,number+1):slot.Key=="PageCount"?PageNumber(count,context,sheetCount):Value(context,slot.Key);
                        if(value.Length>0)marks.Add(Tuple.Create(slot,slot.Bounds(art,0),value));
                    }
                    else
                    {
                        int index=Array.IndexOf(columns,slot.Key);
                        for(int r=0;r<slot.Rows;r++)
                        {
                            int item=number*capacity+slot.Offset+r;
                            if(item>=count)break;
                            string value=rows==null?Value(context,slot.Key,cards[item]):index<0?"":rows[item][index];
                            if(value.Length>0)marks.Add(Tuple.Create(slot,slot.Bounds(art,r),value));
                        }
                    }
                }
                for(int i=marks.Count-1;i>=0;i--)
                {
                    var mark=marks[i];
                    if(mark.Item1.Barcode||Fit(measure,mark,false))continue;
                    additional.Add(mark.Item1.Label+"："+mark.Item3);
                    var pointer=Tuple.Create(mark.Item1,mark.Item2,"见附页");
                    if(Fit(measure,pointer,false))marks[i]=pointer;
                    else marks.RemoveAt(i);
                }
                float min=.2f;
                foreach(var mark in marks.Where(m=>m.Item1.Barcode))
                    min=Math.Max(min,Barcode128.MinimumWidth(mark.Item3)/mark.Item2.Width);
                if(min>1.001f)throw new InvalidOperationException("编号超出原式条码栏的宽度。请填写较短的扫描编号，或使用独立条码。");
                pages.Add(new DocumentPage{Width=face.Width,Height=face.Height,Name=face.Title,Face=face,MinimumScale=min,Paint=g=>{
                    using(var stream=new MemoryStream(bytes,false))
                    using(var image=Image.FromStream(stream))
                        g.DrawImage(image,art,new RectangleF(0,0,image.Width,image.Height),GraphicsUnit.Pixel);
                    foreach(var mark in marks)
                    {
                        var slot=mark.Item1;
                        var r=mark.Item2;
                        if(slot.Barcode)Barcode128.Draw(g,mark.Item3,r,false);
                        else Fit(g,mark,true);
                    }
                }});
            }
            }

            if(rows!=null&&rows.Count>0)
            {
                var unmapped=Enumerable.Range(0,columns.Length)
                    .Where(i=>!slots.Any(s=>s.Rows>0&&s.Key==columns[i])&&rows.Any(r=>r[i].Length>0)).ToArray();
                if(unmapped.Length>0)
                {
                    additional.Add("序号 | "+String.Join(" | ",unmapped.Select(i=>upu.Columns[i].Label)));
                    additional.AddRange(rows.Select((row,i)=>(i+1)+" | "+String.Join(" | ",unmapped.Select(j=>row[j]))));
                }
            }
            foreach(var item in Fields(face,context).Where(f=>context.Fields.ContainsKey(f.Key)&&f.Value.Length>0))
            {
                var placed=slots.FirstOrDefault(s=>s.Rows==0&&s.Key==item.Key);
                if(placed==null)additional.Add(item.Label+"："+item.Value);
            }
            AddAnnex(pages,face,context.Reference,additional);
            return true;
        }

        static bool Fit(Graphics graphics,Tuple<FormSlot,RectangleF,string> mark,bool paint)
        {
            var slot=mark.Item1;
            return Ink.FitText(graphics,mark.Item3,"SimSun",mark.Item2,slot.PointSize,false,slot.Center,
                               Math.Min(4.3f,slot.PointSize),true,paint);
        }

        static string PageNumber(int count,DocumentContext context,int value)
        {
            return count==0&&context.BlankSource?"":value.ToString(CultureInfo.InvariantCulture);
        }

        static void AddAnnex(List<DocumentPage> pages,FormTemplate face,string reference,List<string> content)
        {
            if(content.Count==0)return;
            bool hasSpace=face.Width>=120&&face.Height>=130;
            float width=hasSpace?face.Width:148,height=hasSpace?face.Height:210;
            string all=String.Join("\n",content);
            var lines=new List<string>();
            foreach(var item in all.Replace("\r","").Split('\n'))
            {
                if(item.Length==0)lines.Add("");
                for(int i=0;i<item.Length;i+=50)lines.Add(item.Substring(i,Math.Min(50,item.Length-i)));
            }
            for(int i=0;i<lines.Count;i+=28)
            {
                string text=String.Join("\n",lines.Skip(i).Take(28));
                int number=i/28+1;
                pages.Add(new DocumentPage{Name="填报附页",Width=width,Height=height,Face=face,Paint=g=>{
                    Ink.PrintedText(g,"QSL 填报附页  "+number,"SimSun",5,5,width-10,7,12,true,false);
                    Ink.T(g,face.Title+"  "+reference,5,13,width-10,8,8);
                    Ink.PrintedText(g,text,"SimSun",5,22,width-10,height-27,8.5f,false,false,4,true);
                }});
            }
        }
    }
}
