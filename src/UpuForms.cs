using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace QslPost
{
    public sealed class UpuTemplate : FormTemplate
    {
        public readonly string Code, SourcePages, PaperBasis, Layout, Purpose, Variant, RowSource;
        public readonly UpuField[] FillFields, Columns;

        internal UpuTemplate(string[] row)
            : base(row[0], row[7] + " · " + row[1] + (row[8].Length == 0 ? "" : " / " + row[8]),
                   row[1].StartsWith("CN ") ? "UPU 函件 CN" : "UPU 包裹 CP",
                   Single.Parse(row[2], CultureInfo.InvariantCulture), Single.Parse(row[3], CultureInfo.InvariantCulture),
                   row[12] + " · " + row[5])
        {
            Code = row[1]; SourcePages = row[4]; PaperBasis = row[5]; Layout = row[6]; Purpose = row[7];
            Variant = row[8]; FillFields = UpuField.Parse(row[9]); RowSource = row[10]; Columns = UpuField.Parse(row[11]);
        }
    }

    public sealed class UpuField
    {
        public readonly string Key, Label;
        public readonly bool Multiline, Sum;
        UpuField(string key, string label)
        {
            Multiline = key.EndsWith("*"); Sum = key.EndsWith("+");
            Key = key.TrimEnd('*', '+'); Label = label;
        }
        internal static UpuField[] Parse(string text)
        {
            return text.Split(new[]{';'}, StringSplitOptions.RemoveEmptyEntries).Select(pair =>
            {
                var pieces = pair.Split(new[]{'='}, 2);
                return new UpuField(pieces[0], pieces[1]);
            }).ToArray();
        }
    }

    public static partial class UpuForms
    {
        public static readonly IList<UpuTemplate> Catalog = Load().AsReadOnly();
        const string DetailKey = "DetailRows";
        const string RowsHelp = "一行一条，栏位用 | 或 Tab 隔开。留空则带入所选记录。";
        const float ItemModule = .25f;

        static List<UpuTemplate> Load()
        {
            var forms = new List<UpuTemplate>();
            using (var input = typeof(UpuForms).Assembly.GetManifestResourceStream("QslPost.UpuForms.tsv"))
            using (var reader = new StreamReader(input, Encoding.UTF8))
            {
                reader.ReadLine();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (String.IsNullOrWhiteSpace(line)) continue;
                    var row = line.Split('\t');
                    if (row.Length != 13) throw new InvalidDataException("UPU 表单目录字段数错误：" + row[0]);
                    forms.Add(new UpuTemplate(row));
                }
            }
            return forms;
        }

        public static void Edit(UpuTemplate template, DocumentContext context, System.Windows.Forms.IWin32Window owner, Action rebuild)
        {
            var fields = template.FillFields.Select(entry =>
            {
                var field = new Field(entry.Key, entry.Label, Value(context, entry.Key, null, null));
                return entry.Multiline ? field.Multi() : field;
            }).ToList();
            if (template.Columns.Length > 0)
                fields.Add(new Field(DetailKey, "明细顺序 · " + String.Join(" | ", template.Columns.Select(c => c.Label).ToArray()), context.V(DetailKey, "")).Multi());
            string help = template.Columns.Length > 0 ? RowsHelp : "这里改动的内容只用于本次出单。";
            using (var dialog = new RecordDialog("填写 " + template.Code, help, fields, d =>
            {
                var values = fields.ToDictionary(f => f.Key, f => d.Value(f.Key));
                if (template.Columns.Length > 0) ParseRows(values[DetailKey], template.Columns.Length);
                foreach (var field in fields)
                {
                    string value=values[field.Key];
                    bool unchangedAddress=(field.Key=="AddressFrom"||field.Key=="AddressTo")&&value==field.Value.Trim()&&!context.Fields.ContainsKey(field.Key);
                    if(!unchangedAddress)context.Fields[field.Key]=value;
                }
            }))
                if (dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK) rebuild();
        }

        internal static string Value(DocumentContext c, string key, Card card, Bag bag)
        {
            string value;
            if (c.Fields.TryGetValue(key, out value)) return value;
            card = card ?? c.Card ?? (c.Case != null ? c.Db.Cards.FirstOrDefault(v => v.Id == c.Case.CardId) : null);
            bag = bag ?? c.Bag;
            switch (key)
            {
                case "From": return c.From;
                case "To": return c.To;
                case "Reference": return c.Reference;
                case "Date": return c.Date;
                case "Operator": return c.Operator;
                case "Notes": return c.Note;
                case "Subject": return c.Case == null ? "" : c.Case.Subject;
                case "Case": return c.Case == null ? "" : c.Case.Id;
                case "RelatedCase": return c.Case == null ? "" : c.Case.RelatedCase;
                case "CaseState": return c.Case == null ? "" : c.Case.State;
                case "Reply": return c.Case == null ? "" : c.Case.Reply;
                case "Attachment": return c.Case == null ? "" : c.Case.Attachment;
                case "Supervisor": return c.Case == null ? "" : c.Case.Supervisor;
                case "Card": return card == null ? "" : card.Id;
                case "CallFrom": return card == null ? "" : card.FromCall;
                case "CallTo": return card == null ? "" : card.ToCall;
                case "Qso": return card == null ? "" : card.QsoUtc;
                case "MHz": return card == null ? "" : card.Frequency;
                case "Mode": return card == null ? "" : card.Mode;
                case "Rst": return card == null ? "" : card.RstSent + "/" + card.RstReceived;
                case "Equipment": return card == null ? "" : card.Equipment;
                case "Power": return card == null ? "" : card.Power;
                case "Antenna": return card == null ? "" : card.Antenna;
                case "Kind": return card == null ? "" : card.Kind;
                case "CardState": return card == null ? "" : card.Status;
                case "Country": return card == null ? "" : card.Country;
                case "Grid": return card == null ? "" : card.Grid;
                case "AddressFrom": return card == null || card == c.Card ? c.SenderAddress : DemoData.Address(c.Db,card.FromCall,card.SenderAddress);
                case "AddressTo": return card == null || card == c.Card ? c.RecipientAddress : DemoData.Address(c.Db,card.ToCall,card.RecipientAddress);
                case "Bag": return bag == null ? "" : bag.Id;
                case "Dispatch": return bag == null ? "" : bag.DispatchId;
                case "DispatchType": return bag == null || bag.DispatchId.Length < 15 ? "" : bag.DispatchId.Substring(12,3);
                case "BagNo": return bag == null || bag.Id.Length < 23 ? "" : bag.Id.Substring(20,3);
                case "BagType": return bag == null ? "" : "QSL";
                case "Origin": return bag == null ? "" : bag.OriginCode;
                case "Destination": return bag == null ? "" : bag.DestinationCode;
                case "Via": return bag != null ? bag.Via : card == null ? "" : card.Via;
                case "Transport": return bag == null ? "" : bag.Transport;
                case "Class": return bag != null ? bag.MailClass : card == null ? "" : card.MailClass;
                case "Direction": return bag != null ? bag.Direction : card == null ? "" : card.Direction;
                case "Seal": return bag == null ? "" : bag.Seal;
                case "Weight": return bag == null ? "" : bag.WeightKg.ToString("0.###", CultureInfo.InvariantCulture);
                case "BagState": return bag == null ? "" : bag.State;
                case "Handover": return bag == null ? "" : Clock.Display(bag.HandoverUtc);
                case "Departure": return bag == null ? "" : Clock.Display(bag.HandoverUtc);
                case "Received": return bag == null ? "" : Clock.Display(bag.ReceivedUtc);
                case "Qty": return card != null ? "1" : bag != null ? bag.CardIds.Count.ToString() : "";
                case "EmptyQty": return bag != null && bag.MailClass == "空袋" ? "1" : "";
                case "Material": return card == null && bag == null ? "" : "纸质 QSL 卡片";
                case "ItemId":
                    var numbers = c.Db.Shipments.Where(s => card != null ? s.CardId == card.Id : bag != null && s.BagId == bag.Id)
                                   .Select(s => s.Number).Distinct().Take(2).ToArray();
                    return numbers.Length == 1 ? numbers[0] : "";
                case "Tracking":
                    if (card != null && card != c.Card) return new DocumentContext{Db=c.Db, Card=card}.Tracking;
                    return c.Tracking;
            }
            return "";
        }

        internal static List<string[]> ParseRows(string text, int columns)
        {
            var rows = new List<string[]>();
            int line = 0;
            foreach (string entry in text.Replace("\r", "").Split('\n'))
            {
                line++;
                if (String.IsNullOrWhiteSpace(entry)) continue;
                var parts = entry.Split(entry.Contains("\t") ? '\t' : '|');
                if (parts.Length > columns)
                    throw new InvalidOperationException("明细第 " + line + " 行超过 " + columns + " 栏。请按照模板列顺序填写。");
                Array.Resize(ref parts, columns);
                rows.Add(parts.Select(s => (s ?? "").Trim()).ToArray());
            }
            return rows;
        }

        internal static List<string[]> Rows(UpuTemplate t, DocumentContext c)
        {
            string manual = c.V(DetailKey+"."+t.Key, c.V(DetailKey, ""));
            if(t.Columns.Length==0)return new List<string[]>();
            if (manual.Length > 0) return ParseRows(manual, t.Columns.Length);
            if (t.RowSource == "cards" || t.RowSource == "contents")
                return c.Cards.Select(card => t.Columns.Select(e => Value(c, e.Key, card, null)).ToArray()).ToList();
            if (t.RowSource == "bags" && c.Bag != null)
                return new List<string[]> { t.Columns.Select(e => Value(c, e.Key, null, c.Bag)).ToArray() };
            return new List<string[]>();
        }

        static string[] Total(UpuTemplate t, List<string[]> rows)
        {
            var total = new string[t.Columns.Length];
            if (total.Length == 0 || rows.Count == 0) return total;
            total[0] = "合计 " + rows.Count + " 条";
            for (int i = 1; i < total.Length; i++)
            {
                if (!t.Columns[i].Sum) continue;
                decimal amount = 0;
                bool any = false, valid = true;
                foreach (var row in rows)
                {
                    if (String.IsNullOrWhiteSpace(row[i])) continue;
                    decimal number;
                    if (!Decimal.TryParse(row[i], NumberStyles.Number, CultureInfo.InvariantCulture, out number)) { valid = false; break; }
                    amount += number; any = true;
                }
                if (any && valid) total[i] = amount.ToString("0.###", CultureInfo.InvariantCulture);
            }
            return total;
        }

        public static List<DocumentPage> Build(UpuTemplate t, DocumentContext context)
        {
            var data = t.FillFields.ToDictionary(e => e.Key, e => Value(context, e.Key, null, null));
            if(DespatchBarcode(t))foreach(string key in new[]{"Qty","Weight","Tracking"})
                if(!data.ContainsKey(key))data.Add(key,Value(context,key,null,null));
            var rows = Rows(t, context);
            string bar = Barcode(t, data);
            float minimumScale=.2f;
            if(bar.Length>0)
            {
                float barWidth=t.Layout=="item"?Barcode128.MinimumWidth(bar,ItemModule):t.Layout=="bag"?t.Width-51.2f:t.Width*.39f;
                minimumScale=Math.Max(minimumScale,Barcode128.MinimumWidth(bar)/barWidth);
            }
            var result = new List<DocumentPage>();
            var geometry=t.Columns.Length==0?null:new TableGeometry(t);
            int capacity=geometry==null?1:geometry.Capacity;
            int count = Math.Max(1, (rows.Count + capacity - 1) / capacity);
            var totals = Total(t, rows);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                result.Add(new DocumentPage {
                    Width = t.Width, Height = t.Height, Name = t.Title,
                    MinimumScale = minimumScale,
                    Paint = g => {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        switch (t.Layout)
                        {
                            case "item": Item(g, t, data, bar); break;
                            case "mark": Mark(g, t, data); break;
                            case "bag": Receptacle(g, t, data, bar); break;
                            case "envelope": Envelope(g, t, data); break;
                            case "coupon": case "bundle": case "slip": Slip(g, t, data); break;
                            case "table": case "contents": DetailedTable(g,t,data,rows,totals,index,count,geometry,bar);break;
                            default: Notice(g, t, data); break;
                        }
                    }
                });
            }
            return result;
        }

        static string Pick(Dictionary<string,string> data, string key)
        {
            string value;
            return data.TryGetValue(key, out value) ? value : "";
        }

        static string Barcode(UpuTemplate t, Dictionary<string,string> data)
        {
            if(t.Layout!="item"&&t.Layout!="bag"&&!DespatchBarcode(t))return "";
            string s=t.Layout=="bag"?Pick(data,"Bag"):t.Layout=="item"?Pick(data,"ItemId"):Pick(data,"Dispatch");
            if (s.Length == 0 && t.Layout == "item") s = Pick(data, "Card");
            if(s.Length==0)return "";
            float requiredWidth=t.Layout=="item"?Barcode128.MinimumWidth(s,ItemModule):Barcode128.MinimumWidth(s);
            float available=t.Layout=="bag"?t.Width-51.2f:t.Layout=="item"?t.Width-10f:t.Width*.39f;
            if (requiredWidth > available)
                throw new InvalidOperationException("此标签的条码需要能放入纸面的英文或数字编号。请在“编辑填空”填写适合的短编号。");
            return s;
        }

        static void Header(Graphics g, UpuTemplate t, float y, float height, float size)
        {
            float w = t.Width;
            var title=PostalType.Title(t);
            if(height>=15)PostalType.FormTitle(g,title,PostalFormInk.Area(4,y,w-33,height),false);
            else PostalType.Caption(g,title,PostalFormInk.Area(4,y,w-33,height),size,false,false,true);
            PostalType.Text(g,t.Code,PostalType.Sans,PostalFormInk.Area(w-31,y,27,6),Math.Min(size,10),true,true);
        }

        static void Signature(Graphics g, float x, float y, float width, string text)
        {
            PostalFormInk.Signature(g,"Sign",text,x,y,width);
        }

        static void AddFooter(Graphics g, UpuTemplate t, string ending)
        {
            PostalFormInk.Footnote(g,t.Width,t.Height,t.Code,ending);
        }

        static void Cells(Graphics g, IEnumerable<UpuField> fields, Dictionary<string,string> data,
                          float x, float y, float width, int columns, float height, float size)
        {
            var array = fields.ToArray();
            for (int n = 0; n < array.Length; n++)
            {
                float cellWidth = width / columns;
                int column = n % columns;
                if (column == 0 && n == array.Length - 1) cellWidth = width;
                Labelled(g, array[n], Pick(data, array[n].Key),
                         x + column * width / columns, y + n / columns * height, cellWidth, height, size);
            }
        }

        static void Labelled(Graphics g, UpuField field, string value, float x, float y, float w, float h, float size)
        {
            PostalFormInk.Field(g,field.Key,field.Label,value,x,y,w,h);
        }

        static void Item(Graphics g, UpuTemplate t, Dictionary<string,string> values, string bar)
        {
            float w = t.Width, h = t.Height;
            using(var outline=Ui.Round(PostalFormInk.Area(2.5f,6.1f,w-5,h-8f),2.2f))
            using(var pen=new Pen(Color.Black,.18f))g.DrawPath(pen,outline);
            Header(g,t,1.25f,4.7f,5.8f);
            string reference = Pick(values, "ItemId");
            if (reference.Length == 0) reference = Pick(values, "Card");
            string mark=t.Code=="CN 04"?"R":t.Code=="CN 06"||t.Code=="CP 74"?"V":t.Code.StartsWith("CP")?"CP":"QSL";
            PostalType.Text(g,mark,PostalType.Sans,PostalFormInk.Area(4,6.6f,10,5.6f),10,true,true);
            PostalFormInk.Value(g,"ItemId",reference.Length>0?reference:"__________________",PostalFormInk.Area(14,6.7f,w-19,5.2f),8.6f,true);
            PostalFormInk.Rule(g,4.4f,12.4f,w-4.4f,12.4f,.25f);
            if(bar.Length>0)Barcode128.Draw(g,bar,PostalFormInk.Area(5,13.4f,w-10,7.2f),false,ItemModule);
            string extraText="";
            string card=Pick(values,"Card");
            if(card.Length>0&&!card.Equals(reference,StringComparison.Ordinal))extraText="QSL "+card;
            var extra=t.FillFields.FirstOrDefault(f=>f.Key!="ItemId"&&f.Key!="Card");
            if(extra!=null&&Pick(values,extra.Key).Length>0)
            {
                var legend=PostalType.Label(extra.Key,extra.Label);
                string label=extra.Key=="CallTo"?"收 / À / To":legend.Chinese+" · "+legend.French+" · "+legend.English;
                extraText+=(extraText.Length>0?"    ":"")+label+"  "+Pick(values,extra.Key);
            }
            PostalType.Text(g,extraText,PostalType.Serif,PostalFormInk.Area(4,21f,w-8,2.6f),4.6f,false,true);
        }

        static void Mark(Graphics g, UpuTemplate t, Dictionary<string,string> values)
        {
            float w = t.Width, h = t.Height;
            if (t.Code == "CN 29")
            {
                Ink.Line(g, 1.5f, h - 1.5f, w - 1.5f, h - 1.5f);
                Ink.Line(g, 1.5f, h - 1.5f, w / 2, 1.4f);
                Ink.Line(g, w / 2, 1.4f, w - 1.5f, h - 1.5f);
                PostalType.Caption(g,PostalType.Title(t),PostalFormInk.Area(7,9,w-14,6.1f),5.3f,true,false,true);
                return;
            }
            Ink.Box(g, 1.7f, 1.7f, w - 3.4f, h - 3.4f);
            PostalType.Caption(g,PostalType.Title(t),PostalFormInk.Area(2.2f,2.2f,w-4.4f,Math.Min(13,h-5.3f)),h<25?6:9,true,false,true);
            if (h > 25)
            {
                if(t.Code=="CN 15")PostalFormInk.Choices(g,PostalFormInk.Area(3,15,w-6,8),false,"ReasonUndelivered","未收","Incorrect","地址有误");
                Cells(g,t.FillFields,values,3,24,w-6,2,12,7.2f);
            }
            PostalType.Text(g,t.Code,PostalType.Sans,PostalFormInk.Area(2.2f,h-5,w-4.4f,3.2f),h<25?4.7f:6,false,true);
        }

        static void Receptacle(Graphics g, UpuTemplate t, Dictionary<string,string> values, string bar)
        {
            const float left=15.5f,split=47.4f,right=127.5f,top=5;
            float w=t.Width,h=t.Height,small=split-left,large=right-split;
            PostalFormInk.TagOutline(g,w,h,14.5f);
            PostalFormInk.Rule(g,left,top,left,81.3f,.27f);
            PostalFormInk.Rule(g,split,top,split,81.3f,.30f);
            PostalType.Caption(g,new PostalLegend(t.Code.StartsWith("CP")?"QSL 包件":"QSL 函件",t.Code.StartsWith("CP")?"COLIS QSL":"LETTRES QSL",t.Code.StartsWith("CP")?"QSL PARCELS":"QSL LETTERS"),
                PostalFormInk.Area(left+1,top,small-2,7.4f),7.1f,false,false,true);
            PostalType.Caption(g,PostalType.Variant(t),PostalFormInk.Area(split+1.1f,4.1f,large-19,9),9.1f,false,false,true);
            PostalType.Text(g,t.Code,PostalType.Sans,PostalFormInk.Area(w-20,4.2f,17,5.7f),9.9f,true,true);
            float y=13;
            PostalFormInk.Field(g,"From","原寄卡片局",Pick(values,"From"),left,y,small,10.3f);
            y+=10.3f;
            string dispatch=Pick(values,"Dispatch");
            PostalFormInk.Field(g,"DispatchType","总封类别",Pick(values,"DispatchType"),left,y,small*.44f,8f);
            PostalFormInk.Field(g,"IndexNumber","总封序号",dispatch.Length>=20?dispatch.Substring(16,4):dispatch,left+small*.44f,y,small*.56f,8f);
            y+=8f;
            PostalFormInk.Field(g,"Date","封发日",Pick(values,"Date"),left,y,small,7.9f);
            y+=7.9f;
            PostalFormInk.Field(g,"BagType","容器类型",Pick(values,"BagType"),left,y,small*.53f,8f);
            PostalFormInk.Field(g,"BagNo","袋序",Pick(values,"BagNo"),left+small*.53f,y,small*.47f,8f);
            y+=8f;
            PostalFormInk.Field(g,"Seal","封志",Pick(values,"Seal"),left,y,small,8f);
            y+=8f;
            PostalFormInk.Field(g,"Class","袋类别",Pick(values,"Class"),left,y,small*.53f,8);
            PostalFormInk.Field(g,"Qty","卡片数",Pick(values,"Qty"),left+small*.53f,y,small*.47f,8);
            y+=8f;
            PostalFormInk.Field(g,"Weight","毛重(kg)",Pick(values,"Weight"),left,y,small,8);
            PostalFormInk.Field(g,"Origin","起始节点",Pick(values,"Origin"),split,13,large*.5f,7.4f,false,false);
            PostalFormInk.Field(g,"Destination","目的节点",Pick(values,"Destination"),split+large*.5f,13,large*.5f,7.4f,false,false);
            PostalFormInk.Rule(g,split,20.4f,right,20.4f,.28f);
            PostalType.Caption(g,PostalType.Label("To","寄达卡片局"),PostalFormInk.Area(split+1.1f,20.9f,large-2.2f,6.1f),7.2f);
            PostalFormInk.Value(g,"To",Pick(values,"To"),PostalFormInk.Area(split+1.2f,27.2f,large-2.4f,9.8f),13.1f,false,true);
            PostalFormInk.Rule(g,split,37.7f,right,37.7f,.28f);
            PostalType.Caption(g,PostalType.Label("Bag","袋号 S9"),PostalFormInk.Area(split+1.2f,38.1f,large-2.5f,4.8f),5.7f);
            if(bar.Length>0)
            {
                var area=PostalFormInk.Area(split+1,43,large-1.3f,15.4f);
                Barcode128.Draw(g,bar,area,true,Barcode128.MinimumWidth(bar,.25f)<=area.Width?.25f:0);
            }
            else PostalFormInk.Value(g,"Bag","________________________________________________",PostalFormInk.Area(split+1,45,large-2,11.8f),7,true);
            PostalFormInk.Rule(g,split,59.3f,right,59.3f,.28f);
            float route=large*.65f;
            PostalFormInk.Field(g,"Departure","发运日期",Pick(values,"Departure"),split,59.3f,route*.50f,9.5f,false,false);
            string routeDetail=Pick(values,"Vehicle").Length>0?Pick(values,"Vehicle"):Pick(values,"Transport");
            PostalFormInk.Field(g,"Vehicle","车辆／班次",routeDetail,split+route*.5f,59.3f,route*.5f,9.5f,false,false);
            PostalFormInk.Field(g,"Unload","卸载台",Pick(values,"Unload"),split+route,59.3f,large-route,9.5f,false,false);
            PostalFormInk.Rule(g,split+route,59.3f,split+route,81.3f,.18f);
            PostalFormInk.Field(g,"Via","中转局",Pick(values,"Via"),split,69,route,10,false,false);
            PostalFormInk.Field(g,"Tracking","物流号码",Pick(values,"Tracking"),split+route,69,large-route,10,false,false);
            PostalFormInk.Field(g,"Dispatch","总封号 S8",dispatch,left,72,small,9.3f,false,false);
            PostalFormInk.Footnote(g,w,h-3.8f,t.Code,"");
        }

        static void Envelope(Graphics g, UpuTemplate t, Dictionary<string,string> data)
        {
            PostalEnvelope(g,t,data);
        }

        static void Slip(Graphics g, UpuTemplate t, Dictionary<string,string> data)
        {
            float w = t.Width, h = t.Height;
            Ink.Box(g, 3, 3, w - 6, h - 8);
            Header(g, t, 4, 9, 11);
            if(t.Code=="CN 42"){DirectTransfer(g,t,data);return;}
            if(t.Code=="CN 07"){AdviceOfReceipt(g,t,data);return;}
            if(t.Layout=="bundle"&&(t.Variant.StartsWith("普通")||t.Variant.StartsWith("挂号")))
            {
                Color tint=t.Variant.StartsWith("挂号")?Color.FromArgb(249,227,235):Color.FromArgb(222,240,249);
                using(var ink=new SolidBrush(tint))g.FillRectangle(ink,3.2f,13.4f,w-6.4f,7.6f);
            }
            PostalType.Caption(g,PostalType.Variant(t),PostalFormInk.Area(4,15,w-8,5.4f),6.7f,true);
            int columns = w < 130 ? 2 : 3;
            float rows = Math.Max(1, (t.FillFields.Length + columns - 1) / columns);
            Cells(g, t.FillFields, data, 5, 22, w - 10, columns, Math.Min(14, (h - 39) / rows), 7.5f);
            if (t.Layout == "coupon")
            {
                PostalFormInk.Signature(g,"CouponIssue","发券局日戳",7,h-16,w*.37f);
                PostalFormInk.Signature(g,"CouponExchange","兑付局日戳",w*.59f,h-16,w*.35f);
            }
            else Signature(g, w - 45, h - 14, 38, t.Layout == "bundle" ? "复核 / 扎捆" : "交接 / 确认");
            AddFooter(g, t, "");
        }

        static void Notice(Graphics g, UpuTemplate t, Dictionary<string,string> data)
        {
            DetailedNotice(g,t,data);
        }
        static float ColumnWeight(string key)
        {
            switch (key)
            {
                case "Qty": case "Weight": case "MHz": case "Mode": case "Rst": return .73f;
                case "Notes": case "Bag": case "Dispatch": case "Tracking": case "Reference": return 1.75f;
                case "Qso": case "Card": case "Material": return 1.4f;
                default: return 1;
            }
        }

    }
}
