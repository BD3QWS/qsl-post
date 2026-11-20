using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

namespace QslPost
{
    internal static class ChinesePostalForms
    {
        public static void CompactBag(Graphics g,DocumentContext c,FormTemplate t)
        {
            // The attached compact tag has an open border and a three-part routing foot.
            const float x=1.55f,divider=21.02f,edge=61.55f,top=1.15f;
            float[] lines={top,7.5f,12.2f,17.03f,21.5f,26f,31f,36f};
            for(int i=1;i<lines.Length;i++)PostalFormInk.Rule(g,x,lines[i],i==4||i==5||i==7?edge:divider,lines[i],.26f);
            PostalFormInk.Rule(g,divider,top,divider,36,.26f);
            PostalFormInk.Rule(g,33.5f,26,33.5f,36,.26f);
            PostalFormInk.Rule(g,50.5f,26,50.5f,36,.26f);
            string code=t.Key.ToUpperInvariant();
            var route=t.Key=="cp85"?PostalType.Label("Sal","空运水陆路"):
                       t.Key=="cp84"?PostalType.Label("Air","航空"):PostalType.Label("Surface","水陆路");
            CompactCaption(g,route,divider+1,top+.25f,32,5.95f,false);
            PostalType.Text(g,code,PostalType.Narrow,PostalFormInk.Area(54,top,10.7f,3.8f),6.6f,true,true);
            CompactField(g,"From","寄自",c.From,x,top,divider-x,6.35f);
            CompactField(g,"Dispatch","总封号码",c.Bag==null?c.Reference:c.Bag.DispatchId,x,7.5f,divider-x,4.7f);
            CompactField(g,"Date","寄发日期",c.Date,x,12.2f,divider-x,4.83f);
            CompactField(g,"BagNo","容器号码",c.Bag==null?"":c.Bag.Id.Substring(20,3),x,17.03f,divider-x,4.47f);
            CompactField(g,"Qty","卡片件数",c.Bag==null&&c.Card==null?"":c.Cards.Count.ToString(),x,21.5f,divider-x,4.5f);
            CompactField(g,"Class","类型",c.Bag==null?"QSL":c.Bag.MailClass,x,26,divider-x,5);
            CompactField(g,"Weight","重量",c.Bag==null?"":c.Bag.WeightKg.ToString("0.0##",CultureInfo.InvariantCulture)+" kg",x,31,divider-x,5);
            CompactField(g,"Special","特别标记",c.V("Special",c.Bag==null?"":c.Bag.Seal),divider,21.5f,edge-divider,4.5f);
            CompactCaption(g,PostalType.Label("To","寄往"),divider+1,7.75f,edge-divider-2,4.8f,false);
            PostalFormInk.Value(g,"To",c.To,PostalFormInk.Area(divider+1.1f,12.6f,edge-divider-2.2f,8.4f),10.4f,false,true);
            CompactField(g,"Origin","启运地",c.Bag==null?"":c.Bag.OriginCode,divider,26,33.5f-divider,10);
            CompactField(g,"Via","中转卡片局",c.Bag==null?"":c.Bag.Via,33.5f,26,17,10);
            CompactField(g,"Destination","寄达地",c.Bag==null?"":c.Bag.DestinationCode,50.5f,26,edge-50.5f,10);
            PostalFormInk.Footnote(g,t.Width,t.Height,"","",true);
        }

        static void CompactCaption(Graphics g,PostalLegend label,float x,float y,float w,float h,bool center)
        {
            float half=h*.51f;
            Ink.PrintedText(g,label.Chinese,PostalType.Song,x,y,w,half,5.8f,false,center,2.5f);
            string terms=label.French==label.English?label.French:label.French+" / "+label.English;
            Ink.PrintedText(g,terms,PostalType.Narrow,x,y+half,w,h-half,4.9f,false,center,2.5f);
        }

        static void CompactField(Graphics g,string key,string label,string value,float x,float y,float w,float h)
        {
            float head=h<5?2.24f:h<7?2.65f:3.8f;
            if(h<7)
            {
                var tr=PostalType.Label(key,label);
                Ink.PrintedText(g,tr.Chinese+"  "+tr.French+" / "+tr.English,PostalType.Narrow,
                    x+.16f,y+.11f,w-.32f,head,4.5f,false,false,2.6f);
            }
            else CompactCaption(g,PostalType.Label(key,label),x+.22f,y+.19f,w-.44f,head,false);
            if(value.Length>0)
                Ink.PrintedText(g,value,value.All(ch=>ch<128)?PostalType.Narrow:PostalType.Song,
                    x+.21f,y+head+.07f,w-.42f,h-head-.20f,h<6?5.5f:6.6f,false,false,3.1f);
        }

        public static void DomesticBag(Graphics g,DocumentContext c,FormTemplate form)
        {
            string kind=form.Key=="domplain"?"Ordinary":form.Key=="domregistered"?"Registered":form.Key=="domcombined"?"Combined":"Empty";
            string cn=kind=="Ordinary"?"平信":kind=="Registered"?"挂信":kind=="Combined"?"合封":"空袋";
            PostalFormInk.Rule(g,1.2f,10.5f,10.5f,1.2f,.26f);
            PostalFormInk.Rule(g,40,1.2f,48.8f,10,.26f);
            using(var p=new Pen(Color.Black,.24f))g.DrawEllipse(p,20.5f,10.99f,9,9);
            PostalType.Text(g,cn,PostalType.Hei,PostalFormInk.Area(4,22,42,7.6f),18,true,true);
            var transport=PostalType.Label(kind,cn);
            PostalType.Text(g,transport.French+" / "+transport.English,PostalType.Serif,PostalFormInk.Area(4,30,42,4.6f),7.1f,false,true);
            PostalFormInk.Value(g,"To",c.To,PostalFormInk.Area(4.5f,35.2f,31,10.6f),9.4f,false,true);
            CompactCaption(g,PostalType.Label("To","台收"),24,45.6f,21,5.5f,true);
            string number=kind=="Ordinary"?"":c.V("Reference",c.Bag==null?c.Reference:c.Bag.DispatchId.Substring(15));
            CompactCaption(g,PostalType.Label(kind=="Ordinary"?"Untracked":"Reference",kind=="Ordinary"?"无单":"号码"),4.6f,53,20,5.7f,false);
            PostalFormInk.Value(g,"Reference",number,PostalFormInk.Area(25,53,20,5.7f),7.8f,true);
            PostalFormInk.Value(g,"From",c.From,PostalFormInk.Area(4.5f,62,31,11),9.4f,false,true);
            CompactCaption(g,PostalType.Label("From","台发"),24,74,21,5.1f,true);
            string count=kind=="Empty"?c.V("EmptyCount",c.Bag!=null&&c.Bag.MailClass=="空袋"?"1":""):c.Bag==null&&c.Card==null?"":c.Cards.Count.ToString();
            if(kind=="Empty"&&count.Length>0)
            {
                int pieces;
                if(!Int32.TryParse(count,NumberStyles.None,CultureInfo.InvariantCulture,out pieces)||pieces<1||pieces>9999)
                    throw new InvalidOperationException("空袋件数须为 1-9999 的整数。");
            }
            CompactField(g,"Qty","件数",count,36.2f,35.5f,9.1f,8.3f);
            PostalType.Text(g,c.Date,PostalType.Serif,PostalFormInk.Area(4.5f,77,19,5.4f),5.4f);
        }

        public static void Manifest(Graphics g,DocumentContext c,int page,int total)
        {
            float left=14.5f,top=50.0f,rows=6.45f,head=8.2f,inner=121;
            PostalType.FormTitle(g,PostalType.Label("Manifest","封 发 卡 片 清 单"),PostalFormInk.Area(41,11,68,14.2f),true,true);
            PostalFormInk.Stamp(g,20,29,13,"DispatchStamp","封发日戳",true);
            PostalFormInk.Stamp(g,117,29,13,"ReceiveStamp","接收日戳",true);
            CompactField(g,"Reference","清单号",c.Reference,42,26,48,6);
            CompactField(g,"Page","页次",(page+1)+" / "+total,91,26,18,6);
            PostalFormInk.Field(g,"From","自台",c.From,42,33,33,12,true,false);
            PostalFormInk.Field(g,"To","至台",c.To,75,33,33,12,true,false);
            float[] widths={5.0f,18.5f,21.7f,15.3f};
            string[] keys={"Position","Card","Station","Notes"};
            string[] labels={"格","号码","收寄／寄达台","备注"};
            var cards=c.Cards;
            for(int shelf=0;shelf<2;shelf++)
            {
                float x=left+shelf*inner/2;
                for(int col=0;col<widths.Length;col++)
                {
                    GridCaption(g,x,top,widths[col],head,keys[col],labels[col]);
                    for(int i=0;i<20;i++)
                    {
                        int at=page*40+shelf*20+i;
                        Card card=at<cards.Count?cards[at]:null;
                        string value=col==0?(shelf*20+i+1).ToString():card==null?"":col==1?card.Id:col==2?
                                    card.FromCall+" / "+card.ToCall:card.MailClass+(card.Via.Length==0?"":" "+card.Via);
                        GridValue(g,x,top+head+i*rows,widths[col],rows,keys[col],value,col==1?5.4f:6.4f);
                    }
                    x+=widths[col];
                }
            }
            var selected=cards.Skip(page*40).Take(40).ToList();
            float foot=top+head+20*rows;
            PostalFormInk.Field(g,"Total","总件数",cards.Count.ToString(),left,foot,43,12.1f,true,true,true);
            ManifestTally(g,"Tracked","条码平信",selected.Count(x=>x.MailClass=="条码平信"),left+43,foot,25.5f);
            ManifestTally(g,"Untracked","无单平信",selected.Count(x=>x.MailClass=="平信"||x.MailClass=="无单平信"),left+68.5f,foot,28);
            ManifestTally(g,"Registered","挂号卡片",selected.Count(x=>x.MailClass=="挂信"),left+43,foot+6.05f,53.5f);
            PostalFormInk.Field(g,"Subtotal","本页件数",selected.Count.ToString(),left+96.5f,foot,24.5f,12.1f,true);
            PostalFormInk.Signature(g,"FromSign","封发人员签章",20,205,45,true);
            PostalFormInk.Signature(g,"ToSign","接收人员签章",83,205,50,true);
        }

        static void ManifestTally(Graphics g,string key,string caption,int value,float x,float y,float w)
        {
            PostalFormInk.Frame(g,PostalFormInk.Area(x,y,w,6.05f),.22f);
            CompactField(g,key,caption,value.ToString(),x,y,w,6.05f);
        }

        public static void Waybill(Graphics g,DocumentContext c,int page,int total)
        {
            float left=14,top=43.5f,head=7.1f,step=6.64f;
            PostalType.FormTitle(g,PostalType.Label("Waybill","收 发 卡 片 路 单"),PostalFormInk.Area(37,7,72,15.6f),true,true);
            CalendarCircle(g,14.2f,17,23,"DispatchStamp","发日",true);
            CalendarCircle(g,114,15,19.5f,"ReceiveStamp","收日",false);
            CompactField(g,"Reference","编号",c.Reference,40,22,65,6);
            CompactField(g,"From","由台",c.From,40,29,34,5.8f);
            CompactField(g,"To","交台",c.To,74,29,34,5.8f);
            CompactField(g,"Page","共页／第页",total+" / "+(page+1),75,36,34,6);
            float[] widths={8,18,23,21,9.0f,9.0f,9.0f,22};
            string[] ids={"Position","Card","CallFrom","CallTo","Registered","Ordinary","PrintedMatter","Notes"};
            string[] names={"格数","号码","原寄电台","接收台名","挂","平","刷","备注"};
            var cards=c.Cards.Skip(page*20).Take(20).ToList();
            float x=left;
            for(int col=0;col<widths.Length;col++)
            {
                GridCaption(g,x,top,widths[col],head,ids[col],names[col]);
                for(int row=0;row<20;row++)
                {
                    Card card=row<cards.Count?cards[row]:null;
                    string text=col==0?(row+1).ToString():card==null?"":col==1?card.Id:col==2?card.FromCall:col==3?card.ToCall:
                        col==4&&card.MailClass=="挂信"||col==5&&card.MailClass=="平信"||col==6&&card.MailClass=="印刷品"?"1":"";
                    GridValue(g,x,top+head+row*step,widths[col],step,ids[col],text,col==1?5.4f:6.2f);
                }
                x+=widths[col];
            }
            float bottom=top+head+20*step;
            PostalFormInk.Field(g,"Departure","发完时分",c.Bag==null?"":Clock.Display(c.Bag.HandoverUtc),14,bottom,48,11,true,false);
            PostalFormInk.Field(g,"Subtotal","本页小计",cards.Count.ToString(),63,bottom,26,8.15f,true);
            PostalFormInk.Field(g,"Total","共计",c.Cards.Count.ToString(),89,bottom,44,8.15f,true);
            CompactField(g,"Handover","交接时分","",63,193,70,8);
            PostalFormInk.Signature(g,"FromSign","寄发人员盖章",25,205,43,true);
            PostalFormInk.Signature(g,"ToSign","接收人员盖章",84,205,46,true);
        }

        static void GridCaption(Graphics g,float x,float y,float w,float h,string id,string caption)
        {
            PostalFormInk.Frame(g,PostalFormInk.Area(x,y,w,h),.26f);
            var label=PostalType.Label(id,caption);
            if(w<10)
            {
                string[] words=id=="Registered"?new[]{"R","R"}:id=="Ordinary"?new[]{"O","O"}:
                               id=="PrintedMatter"?new[]{"I","P"}:new[]{"Nº","No."};
                label=new PostalLegend(caption,words[0],words[1]);
            }
            PostalType.Caption(g,label,PostalFormInk.Area(x+.2f,y+.16f,w-.4f,h-.32f),w<10?5.0f:6.3f,true,true,true);
        }

        static void GridValue(Graphics g,float x,float y,float w,float h,string key,string value,float size)
        {
            PostalFormInk.Frame(g,PostalFormInk.Area(x,y,w,h),.16f);
            string face=value.All(ch=>ch<128)?PostalType.Serif:PostalType.Song;
            Ink.PrintedText(g,value,face,x+.18f,y+.15f,w-.36f,h-.30f,size,false,true,3.3f);
        }

        static void CalendarCircle(Graphics g,float x,float y,float diameter,string key,string cn,bool divided)
        {
            using(var pen=new Pen(Color.Black,.26f))g.DrawEllipse(pen,x,y,diameter,diameter);
            if(divided)
            {
                PostalFormInk.Rule(g,x+.5f,y+diameter*.37f,x+diameter-.5f,y+diameter*.37f,.23f);
                PostalFormInk.Rule(g,x+.5f,y+diameter*.62f,x+diameter-.5f,y+diameter*.62f,.23f);
            }
            CompactCaption(g,PostalType.Label(key,cn),x-3,y-7,diameter+6,6,true);
        }

        public static void Verification(Graphics g,DocumentContext c,bool reply,string message)
        {
            const float x=11.4f,w=108.3f,y=29;
            PostalType.FormTitle(g,PostalType.Label(reply?"ReplyTitle":"Verify",reply?"复     验":"验     单"),PostalFormInk.Area(27,7,78,14.1f),true,true);
            CompactField(g,"Case",reply?"复验字第":"验单号码",c.Reference,77,21.6f,42.7f,6.7f);
            PostalFormInk.Frame(g,PostalFormInk.Area(x,y,w,140.7f),.25f);
            PostalFormInk.Rule(g,x,y+10.4f,x+w,y+10.4f,.24f);
            if(reply)
            {
                CompactField(g,"To","答复台",c.To,x+.6f,y+.25f,42,9.6f);
                CompactField(g,"RelatedCase","原验单字第",c.V("RelatedCase",c.Case==null?"":c.Case.RelatedCase),x+43,y+.25f,64,9.6f);
            }
            else
            {
                CompactField(g,"From","由台发",c.From,x+.6f,y+.25f,37.4f,9.6f);
                CompactField(g,"To","至台",c.To,x+39,y+.25f,37,9.6f);
                CompactField(g,"ReceivedNo","接收号码",c.V("ReceivedNo",""),x+77,y+.25f,30.4f,9.6f);
            }
            PostalType.Caption(g,PostalType.Label(reply?"Reply":"PartDiscrepancy",reply?"对于上列验单查明答复如下":"验明差错和不合事项如下"),
                PostalFormInk.Area(x+1,40.2f,w-2,8.5f),8.3f,false,true);
            PostalType.Text(g,message,PostalType.Fang,PostalFormInk.Area(x+1.5f,49,w-3,67.8f),9.5f,false,false,true);
            CompactField(g,"Card","关联卡片／袋号",c.Card==null?Bag(c):c.Card.Id,x+.6f,117.8f,w-1.2f,8.4f);
            PostalFormInk.Rule(g,x,127.4f,x+w,127.4f,.24f);
            PostalFormInk.Rule(g,x,139.1f,x+w,139.1f,.24f);
            PostalFormInk.Rule(g,x+20,127.4f,x+20,139.1f,.22f);
            PostalType.Caption(g,PostalType.Label("Attachment","附 件"),PostalFormInk.Area(x+.4f,127.9f,19.2f,10.6f),7.2f,true,true);
            PostalType.Text(g,c.V("Attachment",c.Case==null?"":c.Case.Attachment),PostalType.Fang,PostalFormInk.Area(x+20.7f,128,w-21.5f,10.2f),8.2f);
            PostalFormInk.Rule(g,x+w/2,139.1f,x+w/2,169.7f,.22f);
            CompactField(g,"Operator","经手人员",c.Operator,x+.6f,139.8f,w/2-1.2f,8.1f);
            if(reply)CompactField(g,"Supervisor","主管人员",c.V("Supervisor",c.Case==null?"":c.Case.Supervisor),x+.6f,148,w/2-1.2f,8.1f);
            CompactField(g,"Date","日期",c.Date,x+.6f,157.3f,w/2-1.2f,11.2f);
            PostalFormInk.Stamp(g,x+w*.75f-7,144.5f,14,"DayStamp",reply?"复验台日戳":"发验台日戳",true);
            PostalFormInk.Footnote(g,130,180,"","",true);
        }

        public static void DomesticInquiry(Graphics g,DocumentContext c,int page,int total)
        {
            const float x=5.9f,w=118.2f;
            PostalType.Text(g,"业无1601",PostalType.Song,PostalFormInk.Area(x,12,22,7),8.1f);
            PostalType.FormTitle(g,PostalType.Label("DomesticQuery","查        单"),PostalFormInk.Area(26,15.5f,78,15.6f),true,true);
            CompactField(g,"From","发单台",c.From,x,32,65,5.6f);
            CompactField(g,"To","查单寄达台",c.To,x,37.6f,65,6.1f);
            CompactField(g,"Case","查单号码",c.Reference,72,31.9f,51.2f,5.7f);
            CompactField(g,"ReceivedNo","接收号码",c.V("ReceivedNo",""),72,37.6f,51.2f,6.1f);
            PostalFormInk.Field(g,"Class","邮件种类",c.Bag!=null?c.Bag.MailClass:c.Card==null?"":c.Card.MailClass,x,44,w*.48f,8.8f,true);
            PostalFormInk.Field(g,"Tracking","邮件号码",c.Tracking,x+w*.48f,44,w*.52f,8.8f,true);
            PostalFormInk.Field(g,"Office","收寄局",c.From,x,52.8f,w*.48f,8.8f,true);
            PostalFormInk.Field(g,"Date","收寄日期",c.Date,x+w*.48f,52.8f,w*.52f,8.8f,true);
            PostalFormInk.Field(g,"Special","相关特别标记",c.V("Special",""),x,61.6f,w,8.8f,true);
            PostalFormInk.Field(g,"AddressFrom","寄件人名址",c.SenderAddress,x,70.4f,w,9.4f,true);
            PostalFormInk.Field(g,"AddressTo","收件人名址",c.RecipientAddress,x,79.8f,w,9.4f,true);
            PostalFormInk.Field(g,"Resend","二次发出卡片",c.V("Resend",""),x,89.2f,w,8.8f,true);
            CompactField(g,"Operator","经办人",c.Operator,x,101,45,8.6f);
            CompactField(g,"Date","查单发出日期",c.Date,58,101,40,8.6f);
            PostalFormInk.Stamp(g,102,100,13,"DayStamp","发查日戳",true);
            Dotted(g,x,119,x+w,119);
            PostalType.Caption(g,PostalType.Label("PartRecording","通 联 相 关 节 目"),PostalFormInk.Area(x,119.5f,w,8),7.9f,true,true);
            float[] widths={10,16.7f,24,10,10,8,14,13,12.5f};
            string[] ids={"Mode","CallTo","Qso","Rst","Rst","Power","Equipment","Antenna","MHz"};
            string[] names={"模式","通联／收听台","日期·时间 UTC","RST收","RST发","功率","设备","天线","MHz"};
            float tip=x;var cards=c.Cards;
            for(int col=0;col<widths.Length;col++)
            {
                float wide=widths[col]*w/118.2f;
                var label=col==3?new PostalLegend(names[col],"Reçu","Rx"):col==4?new PostalLegend(names[col],"Émis","Tx"):PostalType.Label(ids[col],names[col]);
                PostalFormInk.Frame(g,PostalFormInk.Area(tip,128,wide,13),.24f);
                PostalType.Caption(g,label,PostalFormInk.Area(tip+.2f,128.2f,wide-.4f,12.6f),wide<12?4.8f:6.2f,true,true);
                for(int row=0;row<7;row++)
                {
                    int at=page*7+row;Card card=at<cards.Count?cards[at]:null;
                    string data=card==null?"":(new[]{card.Mode,card.ToCall,card.QsoUtc,card.RstReceived,card.RstSent,card.Power,card.Equipment,card.Antenna,card.Frequency})[col];
                    GridValue(g,tip,141+row*8.25f,wide,8.25f,ids[col],data,6.5f);
                }
                tip+=wide;
            }
            PostalFormInk.Footnote(g,130,205,"",(page+1)+" / "+total,true);
        }

        public static void DomesticReply(Graphics g,DocumentContext c,string replyText,string notes)
        {
            const float x=8.2f,w=114;
            PostalType.FormTitle(g,PostalType.Label("DomesticReply","查 询 通 联 回 单"),PostalFormInk.Area(x,18,w,17),true,true);
            PostalType.Caption(g,PostalType.Label("PartRecording","记录相关答复事项"),PostalFormInk.Area(x,42,w,8),8,false,true);
            PostalFormInk.Choices(g,PostalFormInk.Area(x,53,w,10),true,"Confirmed","记录已确认","Partial","记录部分确认","Pending","记录未确认");
            PostalType.Text(g,replyText,PostalType.Fang,PostalFormInk.Area(x+1,66,w-2,24),9.5f,false,false,true);
            PostalFormInk.Signature(g,"ToSign","寄达电台签章",72,94,42,true);
            PostalFormInk.Rule(g,7.5f,107,122.5f,107,.26f);
            PostalType.Caption(g,PostalType.Label("PartItems","卡片相关答复事项"),PostalFormInk.Area(x,110,w,8),8,false,true);
            PostalFormInk.Choices(g,PostalFormInk.Area(x,121,w,10),true,"ReasonUndelivered","尚未收到","Delivered","已投递","Returned","已退回");
            PostalFormInk.Choices(g,PostalFormInk.Area(x,132,w,9),true,"Incorrect","资料有误","Resend","请补寄","Other","其他");
            PostalType.Text(g,notes,PostalType.Fang,PostalFormInk.Area(x+1,143,w-2,30),9.5f,false,false,true);
            CompactField(g,"ReceivedDate","收到查单日期",c.V("ReceivedDate",""),x+7,178,w*.40f,12.4f);
            CompactField(g,"Date","退回查单日期",c.Date,x+59,178,w*.43f,12.4f);
            PostalFormInk.Footnote(g,130,205,"","",true);
        }

        public static void InternationalInquiry(Graphics g,DocumentContext c,bool reverse,string notes,string reply)
        {
            const float x=9.5f,w=191;
            if(!reverse)
            {
                Card a=c.Card??c.Cards.FirstOrDefault();
                PostalType.Caption(g,PostalType.Label("Station","业余无线电台／卡片局"),PostalFormInk.Area(x,6,62,10),8,false,true);
                PostalType.Text(g,c.Db.Settings.Organization,PostalType.Fang,PostalFormInk.Area(x,16,69,10),9.4f);
                PostalType.FormTitle(g,PostalType.Label("InternationalQuery","查        单"),PostalFormInk.Area(78,7,84,17),true);
                CompactField(g,"IndexNumber","序号",c.Reference,168,6,32.5f,13);
                PostalFormInk.ChoicesWithSelection(g,PostalFormInk.Area(130,24,69,9),true,a==null?"":a.Kind=="SWL"?"Listening":"Contact",
                                                   "Contact","通联","Listening","收听");
                PostalFormInk.Field(g,"From","原寄电台（查单退回台）",c.From,x,35,w*.52f,17,true);
                PostalFormInk.Field(g,"DraftCopy","副份日期",c.V("DuplicateDate",""),x+w*.52f,35,w*.23f,17,true);
                PostalFormInk.Field(g,"Reference","文号",c.Reference,x+w*.75f,35,w*.25f,17,true);
                PostalFormInk.Field(g,"Fax","原寄台联系方式",c.V("SenderFax",""),x,52,w*.52f,21,true);
                PostalFormInk.Field(g,"To","寄达电台及联系方式",c.To+" "+c.V("RecipientFax",""),x+w*.52f,52,w*.48f,21,true);
                PostalFormInk.Part(g,"PartOrigin","由原寄电台填写的内容","A",x,74,w,8.4f,true);
                SideCaption(g,"PartItems","所查邮件",x,82.4f,27,27);
                PostalFormInk.Choices(g,PostalFormInk.Area(x+28,84,86,10),true,"Priority","优先","NonPriority","非优先","Parcel","包裹");
                PostalFormInk.Choices(g,PostalFormInk.Area(x+28,95,86,10),true,"Letter","信函","PrintedMatter","印刷品","SmallPacket","小包");
                PostalFormInk.Field(g,"Tracking","邮件号码",c.Tracking,x+116,82.4f,75,14,true);
                PostalFormInk.Field(g,"Weight","重量",c.Bag==null?"":c.Bag.WeightKg.ToString("0.###",CultureInfo.InvariantCulture)+" kg",x+116,96.4f,75,13,true);
                PostalFormInk.Frame(g,PostalFormInk.Area(x+27,82.4f,89,27));
                SideCaption(g,"Special","特别标记",x,109.4f,27,23.5f);
                PostalFormInk.Frame(g,PostalFormInk.Area(x+27,109.4f,w-27,23.5f));
                PostalFormInk.Choices(g,PostalFormInk.Area(x+28,110.6f,w-29,8),true,"Ordinary","平常","Registered","挂号","Insured","保价","Delivered","确认投递","Acknowledge","回执","Express","快递");
                PostalFormInk.Choices(g,PostalFormInk.Area(x+28,119,w-29,8),true,"Air","航空","Sal","空运水陆路","Surface","水陆");
                PostalType.Text(g,c.V("Special",""),PostalType.Song,PostalFormInk.Area(x+29,127.2f,w-31,5.2f),6.6f);
                PostalFormInk.Field(g,"Date","收寄日期",c.Date,x+27,132.9f,(w-27)*.45f,11,true);
                PostalFormInk.Field(g,"Office","收寄局",c.From,x+27+(w-27)*.45f,132.9f,(w-27)*.55f,11,true);
                SideCaption(g,"Mailing","交寄情况",x,132.9f,27,11);
                SideCaption(g,"From","寄件人",x,143.9f,27,21.1f);
                PostalFormInk.Field(g,"AddressFrom","姓名及详细地址",c.SenderAddress,x+27,143.9f,w-27,21.1f,true);
                SideCaption(g,"Content","内装物品",x,165,27,21);
                PostalFormInk.Field(g,"Card","卡片编号及件数",c.Cards.Count+" QSL    "+(c.Card==null?Bag(c):c.Card.Id),x+27,165,w-27,21,true);
                PostalFormInk.Part(g,"PartRecording","通联或收听相关节目","B",x,188,w,9,true);
                float y=197;
                ThreeFields(g,x,y,w,13,new[]{"Qso","MHz","Mode"},new[]{"通联日期·时间 UTC","频率 MHz","模式"},new[]{a==null?"":a.QsoUtc,a==null?"":a.Frequency,a==null?"":a.Mode});
                string mode=a==null?"":a.Mode.ToUpperInvariant();
                if(mode.Length>0&&!new[]{"FM","AM","SSB","CW"}.Contains(mode))mode="Other";
                PostalFormInk.ChoicesWithSelection(g,PostalFormInk.Area(x,210,w,9),true,mode,
                    "FM","调频","AM","调幅","SSB","单边带","CW","等幅电报","Other","其他");
                ThreeFields(g,x,219,w,13.2f,new[]{"Equipment","Power","Antenna"},new[]{"设备","功率","天线"},new[]{a==null?"":a.Equipment,a==null?"":a.Power,a==null?"":a.Antenna});
                PostalFormInk.Field(g,"Rst","信号报告",a==null?"":a.SignalReport(" / "),x,232.2f,w*.30f,12,true);
                PostalFormInk.Field(g,"Grid","网格",a==null?"":a.Grid,x+w*.30f,232.2f,w*.7f,12,true);
                PostalFormInk.Field(g,"Notes","其他说明",notes,x,244.2f,w,28.6f,true);
                PostalFormInk.Field(g,"BodySignature","日期和签字",c.Date+"    "+c.Operator,x,272.8f,w,13,true);
            }
            else
            {
                PostalType.FormTitle(g,PostalType.Label("InternationalReply","查 单 答 复"),PostalFormInk.Area(x,5,w-45,16),true);
                CompactField(g,"IndexNumber","原查单序号",c.Reference,w-31,5,49.5f,14.5f);
                PostalFormInk.Part(g,"PartTransit","由中转卡片局填写","A",x,25,w,9,true);
                PostalFormInk.Choices(g,PostalFormInk.Area(x+26,35,w-26,10),true,"Priority","优先／航空","Sal","空水路","Surface","非优先／水陆");
                SideCaption(g,"PartTransfer","转寄封发",x,34,25,59);
                ThreeFields(g,x+25,45,w-25,12,new[]{"Date","Dispatch","Bag"},new[]{"寄发日期","总封号码","邮袋识别码"},new[]{c.Date,c.Bag==null?"":c.Bag.DispatchId,Bag(c)});
                ThreeFields(g,x+25,57,w-25,12,new[]{"From","To","Via"},new[]{"原寄台","寄达台","中转卡片局"},new[]{c.From,c.To,c.Bag==null?"":c.Bag.Via});
                PostalFormInk.Field(g,"Tracking","物流追踪号",c.Tracking,x+25,69,w-25,12,true);
                PostalFormInk.Field(g,"BodySignature","日期和签字",c.Date+"   "+c.Operator,x+25,81,w-25,12,true);
                PostalFormInk.Part(g,"PartDestination","由寄达电台填写","B",x,96,w,9,true);
                SideCaption(g,"Delivered","如已收到",x,105,28,24);
                PostalFormInk.Choices(g,PostalFormInk.Area(x+29,106,w-30,10),true,"Delivered","已妥投","Acknowledge","随附签收回执");
                PostalFormInk.Field(g,"DeliveredDate","实际投递日期",c.V("DeliveredDate",""),x+28,116,w-28,13,true);
                SideCaption(g,"ReasonUndelivered","如尚未收到",x,129,28,22);
                PostalFormInk.Frame(g,PostalFormInk.Area(x+28,129,w-28,22));
                PostalFormInk.Choices(g,PostalFormInk.Area(x+29,130,w-30,10),true,"ReasonUndelivered","寄达台尚未收到","Declaration","随附 RD10 未收声明");
                SideCaption(g,"Incorrect","如信息有误",x,151,28,27);
                PostalFormInk.Field(g,"Notes","请在此说明",notes,x+28,151,w-28,27,true);
                PostalFormInk.Part(g,"PartRemedy","最后答复和补发方式","C",x,183,w,9,true);
                SideCaption(g,"Reply","寄达台填写",x,192,28,93);
                PostalFormInk.Field(g,"Reply","查明答复",reply,x+28,192,w-28,52,true);
                PostalFormInk.Frame(g,PostalFormInk.Area(x+28,244,w-28,17));
                PostalFormInk.Choices(g,PostalFormInk.Area(x+29,246,w-30,10),true,"Resend","补寄卡片","Registered","用挂号寄发","Eqsl","使用 eQSL");
                PostalFormInk.Field(g,"BodySignature","寄达电台、日期和签字",c.To+"    "+c.Date+"    "+c.Operator,x+28,261,w-28,24,true);
            }
            PostalFormInk.Footnote(g,210,297,"","",true);
        }

        static void SideCaption(Graphics g,string id,string cn,float x,float y,float w,float h)
        {
            PostalFormInk.Frame(g,PostalFormInk.Area(x,y,w,h),.22f);
            PostalType.Caption(g,PostalType.Label(id,cn),PostalFormInk.Area(x+.6f,y+.5f,w-1.2f,Math.Min(h-.9f,11)),w<31?6.2f:7.2f,true,true);
        }

        static void ThreeFields(Graphics g,float x,float y,float width,float h,string[] id,string[] caption,string[] values)
        {
            float w=width/3;
            for(int col=0;col<3;col++)PostalFormInk.Field(g,id[col],caption[col],values[col],x+w*col,y,w,h,true);
        }

        static void Dotted(Graphics g,float x,float y,float u,float v)
        {
            using(var pen=new Pen(Color.Black,.13f)){pen.DashStyle=DashStyle.Dash;g.DrawLine(pen,x,y,u,v);}
        }

        static string Bag(DocumentContext c) { return c.Bag==null?"":c.Bag.Id; }
    }
}
