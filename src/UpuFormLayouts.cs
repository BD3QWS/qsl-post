using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace QslPost
{
    public static partial class UpuForms
    {
        enum RegisterStyle { Register, Despatch, Delivery, SpecialList, Route, Sampling, Weight, Account, Contents }

        // Sources use separate front matter, body and approval panels for these families.
        static readonly Dictionary<string,RegisterStyle> RegisterStyles = new Dictionary<string,RegisterStyle>
        {
            {"CN 31",RegisterStyle.Despatch},{"CN 32",RegisterStyle.Despatch},{"CP 87",RegisterStyle.Despatch},
            {"CN 37",RegisterStyle.Delivery},{"CN 38",RegisterStyle.Delivery},{"CN 41",RegisterStyle.Delivery},
            {"CN 46",RegisterStyle.Delivery},{"CN 47",RegisterStyle.Delivery},
            {"CN 16",RegisterStyle.SpecialList},{"CN 33",RegisterStyle.SpecialList},
            {"CN 27",RegisterStyle.Route},{"CP 81",RegisterStyle.Route},{"CP 82",RegisterStyle.Route},
            {"CN 53",RegisterStyle.Sampling},{"CN 54",RegisterStyle.Sampling},{"CN 54bis",RegisterStyle.Sampling},
            {"CN 55",RegisterStyle.Sampling},{"CN 56",RegisterStyle.Sampling},
            {"CN 65",RegisterStyle.Weight},{"CN 66",RegisterStyle.Weight},{"CN 67",RegisterStyle.Weight},{"CN 69",RegisterStyle.Weight},
            {"CN 12",RegisterStyle.Account},{"CN 19",RegisterStyle.Account},{"CN 20",RegisterStyle.Account},
            {"CN 48",RegisterStyle.Account},{"CN 51",RegisterStyle.Account},{"CN 52",RegisterStyle.Account},
            {"CN 57",RegisterStyle.Account},{"CN 57bis",RegisterStyle.Account},{"CN 60",RegisterStyle.Account},
            {"CN 61",RegisterStyle.Account},{"CN 62",RegisterStyle.Account},{"CN 62bis",RegisterStyle.Account},
            {"CN 64",RegisterStyle.Account},{"CN 64bis",RegisterStyle.Account},{"CN 64ter",RegisterStyle.Account},
            {"CN 71",RegisterStyle.Account},{"CN 71bis",RegisterStyle.Account},{"CN 73",RegisterStyle.Account},
            {"CP 75",RegisterStyle.Account},{"CP 77",RegisterStyle.Account},{"CP 88",RegisterStyle.Account},
            {"CP 94",RegisterStyle.Account},{"CP 94bis",RegisterStyle.Account}
        };

        static bool DespatchBarcode(UpuTemplate form)
        {
            return form.Code=="CN 31"||form.Code=="CN 32"||form.Code=="CP 87";
        }

        sealed class TableGeometry
        {
            public readonly RegisterStyle Style;
            public readonly float Margin,Width,HeaderY,FieldHeight,Prelude,TableY,HeadingHeight,RowHeight,TableBottom;
            public readonly int Columns,Capacity,Shelves;
            public readonly bool NumberRows;
            public TableGeometry(UpuTemplate t)
            {
                RegisterStyle style;
                if(t.Layout=="contents")style=RegisterStyle.Contents;else RegisterStyles.TryGetValue(t.Code,out style);
                Style=style;
                Margin=t.Width<113?3.8f:t.Width<160?6:9;
                Width=t.Width-2*Margin;
                HeaderY=t.Height<120?19:25;
                FieldHeight=t.Height<120?9:t.Height<170?10:12;
                Columns=t.Width<120?2:3;
                Shelves=style==RegisterStyle.SpecialList?2:1;
                NumberRows=style==RegisterStyle.SpecialList||style==RegisterStyle.Despatch||
                           style==RegisterStyle.Delivery||style==RegisterStyle.Weight;
                bool claims=style==RegisterStyle.Despatch&&t.Code!="CP 87";
                Prelude=style==RegisterStyle.Delivery?11:style==RegisterStyle.Sampling?9:
                        style==RegisterStyle.Contents?9:style==RegisterStyle.Despatch?12:0;
                TableY=HeaderY+((t.FillFields.Length+Columns-1)/Columns)*FieldHeight+Prelude+7;
                HeadingHeight=t.Height<120?10.3f:12.1f;
                float footer=style==RegisterStyle.Contents?26:style==RegisterStyle.Despatch?(claims?78:51):
                             style==RegisterStyle.Account&&t.Height>=175?40:28;
                TableBottom=t.Height-footer-9;
                float space=TableBottom-TableY-HeadingHeight;
                int rows=Math.Max(1,(int)Math.Floor(space/(t.Height<120?8.1f:8.8f)));
                if(style==RegisterStyle.SpecialList)rows=Math.Min(22,rows);
                if(style==RegisterStyle.Despatch&&claims)rows=Math.Min(10,rows);
                RowHeight=Math.Max(5.2f,space/rows);
                Capacity=rows*Shelves;
            }
        }

        static void DetailedTable(Graphics g,UpuTemplate t,Dictionary<string,string> data,List<string[]> rows,
                                  string[] totals,int index,int count,TableGeometry grid,string barcode)
        {
            float m=grid.Margin,w=grid.Width,h=t.Height;
            if(DespatchBarcode(t))
            {
                if(barcode.Length>0)Barcode128.Draw(g,barcode,PostalFormInk.Area(m,5,t.Width*.39f,14.4f),true);
                float x=t.Width*.49f;
                PostalType.FormTitle(g,PostalType.Title(t),PostalFormInk.Area(x,4,t.Width-x-m-22,17.8f),false);
                PostalType.Text(g,t.Code,PostalType.Sans,PostalFormInk.Area(t.Width-m-21,4,21,6),9.5f,true,true);
            }
            else
            {
                Header(g,t,3.8f,t.Height<120?13.5f:17.5f,11.9f);
                if(t.Height>=140&&t.Variant.Length>0)
                    PostalType.Caption(g,PostalType.Variant(t),PostalFormInk.Area(t.Width-m-48,10.5f,47,8.0f),6.3f,true);
            }
            Cells(g,t.FillFields,data,m,grid.HeaderY,w,grid.Columns,grid.FieldHeight,8);
            float y=grid.TableY-grid.Prelude-7;
            if(grid.Style==RegisterStyle.Delivery)
            {
                var area=PostalFormInk.Area(m,y,w,10);
                if(t.Code=="CN 37")PostalFormInk.Choices(g,area,false,"Rail","铁路","Ship","海运","Road","汽运");
                else if(t.Code=="CN 47")PostalFormInk.Choices(g,area,false,"Air","航空","Surface","水陆","Empty","退空袋");
                else PostalFormInk.Choices(g,area,false,"Priority","优先","NonPriority","普通","Direct","直接转运");
            }
            else if(grid.Style==RegisterStyle.Despatch)
            {
                PostalFormInk.Field(g,"Expected","发封张数",
                    rows.Count>0?Pick(data,"Qty").Length>0?Pick(data,"Qty"):rows.Count.ToString():"",
                    m,y,w*.21f,11.8f);
                PostalFormInk.Choices(g,PostalFormInk.Area(m+w*.21f,y,w*.79f,10.8f),false,"Ordinary","普通卡件","Registered","挂号卡件","Tracked","可追踪卡件");
            }
            else if(grid.Style==RegisterStyle.Contents)
                PostalFormInk.Choices(g,PostalFormInk.Area(m,y,w,8.7f),false,"QslCards","确认卡","Documents","资料","Returned","退回件");
            else if(grid.Style==RegisterStyle.Sampling)
                PostalFormInk.Choices(g,PostalFormInk.Area(m,y,w,8.7f),false,"Incoming","到达","Outgoing","发出","Whole","全量复核");

            string section=grid.Style==RegisterStyle.Despatch?"PartDetail":grid.Style==RegisterStyle.Delivery?"PartTransfer":
                           grid.Style==RegisterStyle.Route?"PartRoutes":grid.Style==RegisterStyle.Weight?"PartWeight":
                           grid.Style==RegisterStyle.Sampling?"PartSample":grid.Style==RegisterStyle.Account?"PartBasis":
                           grid.Style==RegisterStyle.Contents?"PartDeclare":"PartItems";
            string cn=grid.Style==RegisterStyle.Despatch?"逐件登记":grid.Style==RegisterStyle.Delivery?"交接明细":
                      grid.Style==RegisterStyle.Route?"通路与接收条件":grid.Style==RegisterStyle.Weight?"按运别、级别列报重量":
                      grid.Style==RegisterStyle.Sampling?"抽样及统计明细":grid.Style==RegisterStyle.Account?"账项与凭据":
                      grid.Style==RegisterStyle.Contents?"内件登记":"卡件明细";
            PostalFormInk.Part(g,section,cn,"1",m,grid.TableY-7,w,7);
            for(int shelf=0;shelf<grid.Shelves;shelf++)
            {
                float shelfWidth=(w-(grid.Shelves-1)*1.6f)/grid.Shelves;
                float left=m+shelf*(shelfWidth+1.6f);
                Ledger(g,t,rows,index*grid.Capacity+shelf*(grid.Capacity/grid.Shelves),grid.Capacity/grid.Shelves,left,grid.TableY,
                       shelfWidth,grid.HeadingHeight,grid.RowHeight,grid.NumberRows);
            }
            float bottom=grid.TableBottom;
            if(index==count-1&&rows.Count>0)
                TotalsRow(g,t,totals,m,bottom,w,8.8f);
            else PostalFormInk.Field(g,"Subtotal","本页条数",Math.Min(grid.Capacity,Math.Max(0,rows.Count-index*grid.Capacity)).ToString(),
                                     m,bottom,w,8.8f);
            float footerStart=bottom+8.8f;
            if(grid.Style==RegisterStyle.Despatch)
                DespatchSummary(g,t,data,footerStart,m,w);
            else if(grid.Style==RegisterStyle.Account&&h>=175)
            {
                PostalFormInk.Part(g,"PartAmounts","款项与核签","2",m,footerStart,w,7.7f);
                PostalFormInk.Field(g,"Currency","结算币种",Pick(data,"Currency"),m,footerStart+7.7f,w*.23f,11.4f);
                PostalFormInk.Field(g,"Notes","账项说明","",m+w*.23f,footerStart+7.7f,w*.77f,11.4f);
            }
            else if(grid.Style==RegisterStyle.Contents)
            {
                PostalFormInk.Field(g,"Treatment","无法交付时的处理",Pick(data,"Treatment"),m,footerStart,w*.60f,11);
                PostalFormInk.Field(g,"Sign","声明人",Pick(data,"Operator"),m+w*.60f,footerStart,w*.40f,11);
            }
            int signatures=grid.Style==RegisterStyle.Delivery?3:2;
            PostalFormInk.Signature(g,"FromSign","发单方签章",m,h-14,w*.30f);
            if(signatures==3)PostalFormInk.Signature(g,"CarrierSign","承运方签章",m+w*.35f,h-14,w*.28f);
            PostalFormInk.Signature(g,"ToSign","接收方签章",m+w*.67f,h-14,w*.33f);
            AddFooter(g,t,(index+1)+" / "+count);
        }

        static void Ledger(Graphics g,UpuTemplate t,List<string[]> rows,int start,int length,float x,float y,
                           float width,float heading,float rowHeight,bool numbered)
        {
            if(t.Code=="CN 33"||t.Code=="CN 16")
            {
                SpecialLedger(g,t,rows,start,length,x,y,width,heading,rowHeight);
                return;
            }
            float numberWidth=numbered?7:0;
            var weights=t.Columns.Select(f=>ColumnWeight(f.Key)).ToArray();
            float unit=(width-numberWidth)/weights.Sum();
            if(numbered)
            {
                HeadingCell(g,PostalType.Label("Position","序"),x,y,numberWidth,heading,4.7f);
                for(int n=0;n<length;n++)
                    LedgerCell(g,"Position",(start+n+1).ToString(),x,y+heading+n*rowHeight,numberWidth,rowHeight);
            }
            float left=x+numberWidth;
            for(int col=0;col<t.Columns.Length;col++)
            {
                UpuField field=t.Columns[col];
                float wide=unit*weights[col];
                HeadingCell(g,PostalType.Label(field.Key,field.Label),left,y,wide,heading,wide<23?5.8f:6.8f);
                for(int n=0;n<length;n++)
                    LedgerCell(g,field.Key,start+n<rows.Count?rows[start+n][col]:"",left,y+heading+n*rowHeight,wide,rowHeight);
                left+=wide;
            }
        }

        static void SpecialLedger(Graphics g,UpuTemplate t,List<string[]> rows,int start,int length,float x,float y,float width,float heading,float rowHeight)
        {
            string[] ids={"Position","Card","Station","Notes"};
            string[] names={"序","卡号／物流号","原寄／寄达呼号","通联及备注"};
            float[] portions={.08f,.32f,.24f,.36f};
            for(int col=0;col<ids.Length;col++)
            {
                float wide=width*portions[col];
                HeadingCell(g,PostalType.Label(ids[col],names[col]),x,y,wide,heading,wide<12?4.8f:6.5f);
                for(int n=0;n<length;n++)
                {
                    int at=start+n;
                    string value=col==0?(at+1).ToString():"";
                    if(col>0&&at<rows.Count)
                    {
                        string[] keys=col==1?new[]{"Card","Tracking"}:col==2?new[]{"CallFrom","CallTo"}:new[]{"Qso","Rst","Value","Notes"};
                        var parts=t.Columns.Select((f,i)=>new{Field=f.Key,Value=rows[at][i]}).Where(v=>keys.Contains(v.Field)&&v.Value.Length>0)
                                   .Select(v=>v.Value).ToArray();
                        value=String.Join(col==3?" ":"\n",parts);
                    }
                    LedgerCell(g,ids[col],value,x,y+heading+n*rowHeight,wide,rowHeight);
                }
                x+=wide;
            }
        }

        static void HeadingCell(Graphics g,PostalLegend legend,float x,float y,float w,float h,float size)
        {
            PostalFormInk.Frame(g,PostalFormInk.Area(x,y,w,h),.23f);
            if(w<9)legend=new PostalLegend(legend.Chinese,"Nº","No.");
            PostalType.Caption(g,legend,PostalFormInk.Area(x+.18f,y+.22f,w-.36f,h-.44f),size,true,false,true);
        }

        static void LedgerCell(Graphics g,string key,string value,float x,float y,float w,float h)
        {
            PostalFormInk.Frame(g,PostalFormInk.Area(x,y,w,h),.115f);
            PostalFormInk.Value(g,key,value,PostalFormInk.Area(x+.32f,y+.27f,w-.64f,h-.54f),6.9f,true);
        }

        static void TotalsRow(Graphics g,UpuTemplate t,string[] totals,float x,float y,float w,float h)
        {
            var sumFields=t.Columns.Select((f,i)=>new{Field=f,Text=totals[i]}).Where(a=>a.Field.Sum).ToArray();
            int actual=Math.Max(1,sumFields.Length);
            float left=w*.27f,step=(w-left)/actual;
            PostalFormInk.Field(g,"Total","合计",totals.Length>0?totals[0]:"",x,y,left,h);
            for(int i=0;i<actual;i++)
            {
                string id=i<sumFields.Length?sumFields[i].Field.Key:"Notes";
                string name=i<sumFields.Length?sumFields[i].Field.Label:"备注";
                string text=i<sumFields.Length?sumFields[i].Text??"":"";
                PostalFormInk.Field(g,id,name,text,x+left+i*step,y,step,h);
            }
        }

        static void DespatchSummary(Graphics g,UpuTemplate t,Dictionary<string,string> data,float y,float x,float w)
        {
            PostalFormInk.Part(g,"PartSummary","发封汇总","2",x,y,w,7.8f);
            float col=w/3;
            PostalFormInk.Field(g,"Bag","关联邮袋",Pick(data,"Bag"),x,y+7.8f,col,12.6f);
            PostalFormInk.Field(g,"Qty","本单卡片数",Pick(data,"Qty"),x+col,y+7.8f,col,12.6f);
            PostalFormInk.Field(g,"Weight","总重量(kg)",Pick(data,"Weight"),x+2*col,y+7.8f,col,12.6f);
            if(t.Code=="CP 87")return;
            PostalFormInk.Part(g,"PartHandover","编目、随附单据与交接","3",x,y+21,w,8.2f);
            PostalFormInk.Field(g,"Documents","随附单据（CN 16／CN 33 等）",Pick(data,"Documents"),x,y+29.2f,w*.55f,13);
            PostalFormInk.Field(g,"Tracking","全封物流编号",Pick(data,"Tracking"),x+w*.55f,y+29.2f,w*.45f,13);
            PostalFormInk.Choices(g,PostalFormInk.Area(x,y+43,w,9),false,"Ordinary","普通","Registered","挂号","Insured","保价","Tracked","追踪");
        }

        static void DetailedNotice(Graphics g,UpuTemplate t,Dictionary<string,string> data)
        {
            if(t.Code=="CN 11"){FoldedPayment(g,t,data);return;}
            if(t.Key=="upu-cn08"){Inquiry(g,t,data,false);return;}
            if(t.Key=="upu-cn08-back"){Inquiry(g,t,data,true);return;}
            float w=t.Width,h=t.Height,m=w<110?4.2f:8.2f,span=w-2*m;
            Header(g,t,4,h>120?17.5f:13.5f,10);
            if(t.Variant.Length>0)
                PostalType.Caption(g,PostalType.Variant(t),PostalFormInk.Area(w-m-40,11,39,8.0f),6.2f,true);
            float y=h>120?25:21;
            string section=t.Code=="CN 43"||t.Code=="CP 78"?"PartDiscrepancy":t.Code=="CN 08"?"PartIntake":
                           t.Code=="CN 22"||t.Code=="CN 23"||t.Code=="CP 72"?"PartRules":
                           t.Code=="CN 17"||t.Code=="CN 21"?"PartHandling":"PartReport";
            string caption=t.Code=="CN 43"||t.Code=="CP 78"?"查验与差错":t.Code=="CN 08"?"交回受理回执":
                           section=="PartRules"?"随附说明":section=="PartHandling"?"办理事项":"登记与声明";
            PostalFormInk.Part(g,section,caption,"A",m,y,span,8);
            y+=8;
            var fields=t.FillFields.Where(f=>!f.Multiline).ToArray();
            var contexts=t.FillFields.Where(f=>f.Multiline).ToArray();
            int columns=w>=165?3:2;
            float fieldHeight=h<121?10:13.2f;
            Cells(g,fields,data,m,y,span,columns,fieldHeight,8.4f);
            y+=((fields.Length+columns-1)/columns)*fieldHeight;
            if(t.Code=="CN 43"||t.Code=="CP 78")
            {
                PostalFormInk.Part(g,"PartDiscrepancy","差错分类","B",m,y,span,8.4f);
                y+=8.4f;
                PostalFormInk.Choices(g,PostalFormInk.Area(m,y,span,9.8f),false,"MissingDocs","单据缺失","MissingBag","邮袋短少","ExtraBag","邮袋溢到","SealMismatch","封志不符");
                y+=9.8f;
                PostalFormInk.Choices(g,PostalFormInk.Area(m,y,span,9.8f),false,"MissingCard","卡片短少","ExtraCard","卡片溢到","Damaged","破损","Other","其他");
                y+=9.8f;
            }
            else if(t.Code=="CN 18"||t.Code=="CN 17"||t.Code=="CN 24")
            {
                string[] symbols=t.Code=="CN 18"?new[]{"Delivered","已收","ReasonUndelivered","未收","Resend","请补寄"}:
                                 t.Code=="CN 17"?new[]{"Withdraw","撤回","NewAddress","改址","Fee","更正费用"}:
                                 new[]{"MissingCard","短少","Damaged","破损","Returned","退回"};
                PostalFormInk.Choices(g,PostalFormInk.Area(m,y,span,9.8f),false,symbols);
                y+=9.8f;
            }
            if(contexts.Length>0)
            {
                PostalFormInk.Part(g,"PartHandling","处置说明与附件",t.Code=="CN 43"||t.Code=="CP 78"?"C":"B",m,y,span,8.2f);
                y+=8.2f;
                float block=(h-23-y)/contexts.Length;
                foreach(var field in contexts)
                {
                    Labelled(g,field,Pick(data,field.Key),m,y,span,block,8.5f);
                    y+=block;
                }
            }
            PostalFormInk.Signature(g,"FromSign","发出方／申请人",m,h-14,span*.36f);
            PostalFormInk.Signature(g,"ToSign","接收方签章",m+span*.61f,h-14,span*.39f);
            AddFooter(g,t,"");
        }

        static void Inquiry(Graphics g,UpuTemplate t,Dictionary<string,string> data,bool reverse)
        {
            float h=t.Height,x=9,w=t.Width-18,half=w/2,y=26;
            Header(g,t,4.5f,17.5f,13);
            PostalType.Caption(g,PostalType.Variant(t),PostalFormInk.Area(t.Width-49,11,40,7.2f),6.3f,true);
            if(!reverse)
            {
                PostalFormInk.Field(g,"From","发查局／原寄台",Pick(data,"From"),x,y,half,15);
                PostalFormInk.Field(g,"Case","查单号",Pick(data,"Case"),x+half,y,half*.54f,15);
                PostalFormInk.Field(g,"Date","发查日期",Pick(data,"Date"),x+half*1.54f,y,half*.46f,15);
                y+=15;
                PostalFormInk.Field(g,"To","寄达局／寄达台",Pick(data,"To"),x,y,half,13);
                PostalFormInk.Field(g,"Subject","查询事由",Pick(data,"Subject"),x+half,y,half,13);
                y+=13;
                PostalFormInk.Part(g,"PartOrigin","由原寄台填写","A",x,y,w,8.8f);y+=8.8f;
                PostalFormInk.Choices(g,PostalFormInk.Area(x,y,w,9.6f),false,"Priority","优先","NonPriority","普通","Tracked","追踪","Registered","挂号","Insured","保价");
                y+=10;
                PostalFormInk.Field(g,"Card","卡片编号",Pick(data,"Card"),x,y,half,12.5f);
                PostalFormInk.Field(g,"Tracking","邮件物流号码",Pick(data,"Tracking"),x+half,y,half,12.5f);
                y+=12.5f;
                PostalFormInk.Field(g,"AddressFrom","寄件台名址",Pick(data,"AddressFrom"),x,y,half,24);
                PostalFormInk.Field(g,"AddressTo","寄达台名址",Pick(data,"AddressTo"),x+half,y,half,24);
                y+=24;
                PostalFormInk.Part(g,"PartRecording","通联或短波收听资料","B",x,y,w,8.8f);y+=8.8f;
                Cells(g,t.FillFields.Where(f=>new[]{"CallFrom","CallTo","Qso","MHz","Mode","Rst","Equipment","Power","Antenna"}.Contains(f.Key)),
                    data,x,y,w,3,12.3f,8);y+=36.9f;
                PostalFormInk.Part(g,"PartHandover","由封发卡片局复核","C",x,y,w,8.8f);y+=8.8f;
                PostalFormInk.Field(g,"Notes","待核查事项",Pick(data,"Notes"),x,y,w,67);
                PostalFormInk.Field(g,"Attachment","随附资料",Pick(data,"Attachment"),x,y+67,w,18);
            }
            else
            {
                PostalFormInk.Field(g,"Case","原查单号",Pick(data,"Case"),x,y,half,13);
                PostalFormInk.Field(g,"Date","答复日期",Pick(data,"Date"),x+half,y,half,13);y+=13;
                PostalFormInk.Part(g,"PartTransit","由中转卡片局填写","A",x,y,w,8.8f);y+=8.8f;
                PostalFormInk.Choices(g,PostalFormInk.Area(x,y,w,10),false,"Air","航空","Sal","空运水陆路","Surface","水陆");y+=10;
                Cells(g,t.FillFields.Where(f=>new[]{"From","Via","To","Dispatch","Bag","Tracking"}.Contains(f.Key)),
                    data,x,y,w,3,12.5f,8);y+=25;
                PostalFormInk.Field(g,"TransitReply","中转局意见",Pick(data,"TransitReply"),x,y,w,27);y+=27;
                PostalFormInk.Part(g,"PartDestination","由寄达台填写","B",x,y,w,8.8f);y+=8.8f;
                PostalFormInk.Choices(g,PostalFormInk.Area(x,y,w,10),false,"Delivered","已交付","ReasonUndelivered","尚未到达","Returned","已退回","Incorrect","通联资料有误");y+=10;
                PostalFormInk.Field(g,"Received","实收时间",Pick(data,"Received"),x,y,half,12.4f);
                PostalFormInk.Field(g,"CaseState","文书状态",Pick(data,"CaseState"),x+half,y,half,12.4f);y+=12.4f;
                PostalFormInk.Field(g,"Reply","寄达台答复",Pick(data,"Reply"),x,y,w,42);y+=42;
                PostalFormInk.Part(g,"PartRemedy","最终答复与补寄","C",x,y,w,8.8f);y+=8.8f;
                PostalFormInk.Field(g,"Remedy","补寄及结案处理",Pick(data,"Remedy"),x,y,half,43);
                PostalFormInk.Field(g,"Attachment","附件凭证",Pick(data,"Attachment"),x+half,y,half,43);
            }
            PostalFormInk.Signature(g,"FromSign","发单方签章",x,h-15,w*.36f);
            PostalFormInk.Signature(g,"ToSign","寄达台签章",x+w*.64f,h-15,w*.36f);
            AddFooter(g,t,"");
        }

        static void FoldedPayment(Graphics g,UpuTemplate t,Dictionary<string,string> data)
        {
            float w=t.Width-14,x=7;
            for(int side=0;side<2;side++)
            {
                float y=side*105;
                Header(g,t,y+5,15,9.8f);
                PostalFormInk.Part(g,side==0?"PartOrigin":"PartDestination",side==0?"交款台留存":"收取台确认",
                                   side==0?"A":"B",x,y+24,w,8);
                Cells(g,t.FillFields.Where(f=>!f.Multiline),data,x,y+32,w,3,11.5f,7.8f);
                string k=side==0?"Notes":"Reply";
                PostalFormInk.Field(g,k,side==0?"交款说明":"收取确认",Pick(data,k),x,y+78,w,17);
            }
            Dashed(g,4,105,t.Width-4,105);
            PostalType.Caption(g,PostalType.Label("Fold","对折线"),PostalFormInk.Area(t.Width-35,99,31,5.8f),5.3f,true);
            AddFooter(g,t,"");
        }

        static void Dashed(Graphics g,float x,float y,float u,float v)
        {
            using(var p=new Pen(Color.FromArgb(95,95,95),.14f)){p.DashStyle=DashStyle.Dash;g.DrawLine(p,x,y,u,v);}
        }

        static void PostalEnvelope(Graphics g,UpuTemplate t,Dictionary<string,string> data)
        {
            float w=t.Width,h=t.Height;
            PostalFormInk.Frame(g,PostalFormInk.Area(3.5f,3.5f,w-7,h-7),.24f);
            if(t.Code=="CP 91"||t.Code=="CP 92")
            {
                float seam=w*.77f;
                PostalFormInk.Rule(g,seam,3.5f,seam,h-3.5f,.17f);
                Dashed(g,w-9,3.5f,w-9,h-3.5f);
                PostalType.FormTitle(g,PostalType.Title(t),PostalFormInk.Area(8,h-33,w-53,17),false);
                PostalType.Text(g,t.Code,PostalType.Sans,PostalFormInk.Area(seam-21,h-18,19,7),10.2f,true,true);
                Cells(g,t.FillFields,data,9,h*.40f,seam-17,2,12.3f,8);
            }
            else if(t.Code=="CN 45")
            {
                PostalType.Text(g,t.Code,PostalType.Sans,PostalFormInk.Area(w-26,9,20,7),10.5f,true,true);
                PostalFormInk.Stamp(g,w-35,24,18,"DayStamp","日戳",false);
                PostalType.FormTitle(g,PostalType.Title(t),PostalFormInk.Area(14,40,w-58,20),false);
                Cells(g,t.FillFields,data,14,70,w*.65f,2,14,8.5f);
            }
            else
            {
                Header(g,t,9,17.6f,12);
                float span=w-17,half=span*(t.Code=="CN 14"?.39f:.50f);
                PostalFormInk.Part(g,"PartRules","封套寄递信息","A",8.5f,38,half,8.8f);
                PostalFormInk.Part(g,"PartAddress","寄达地址","B",8.5f+half,38,span-half,8.8f);
                var left=t.FillFields.Where(f=>!new[]{"To","AddressTo","Country"}.Contains(f.Key)).ToArray();
                var right=t.FillFields.Where(f=>new[]{"To","AddressTo","Country"}.Contains(f.Key)).ToArray();
                Cells(g,left,data,8.5f,47,half,1,Math.Min(15,(h-68)/Math.Max(1,left.Length)),8);
                Cells(g,right,data,8.5f+half,47,span-half,1,Math.Min(24,(h-68)/Math.Max(1,right.Length)),9);
            }
            PostalFormInk.Footnote(g,w,h-3.4f,t.Code,"");
        }

        static void DirectTransfer(Graphics g,UpuTemplate t,Dictionary<string,string> data)
        {
            PostalFormInk.TagOutline(g,t.Width,t.Height,14.5f);
            PostalType.Text(g,t.Code,PostalType.Sans,PostalFormInk.Area(t.Width-24,5,20,7),10.5f,true,true);
            PostalType.FormTitle(g,PostalType.Label("Direct","航空直接转运"),PostalFormInk.Area(29,13,t.Width-41,25.5f),false);
            Cells(g,t.FillFields,data,22,41,t.Width-30,2,10.2f,8);
            PostalFormInk.Footnote(g,t.Width,t.Height-3.8f,t.Code,"");
        }

        static void AdviceOfReceipt(Graphics g,UpuTemplate t,Dictionary<string,string> data)
        {
            float x=5,w=t.Width-10,h=t.Height,split=w*.54f;
            PostalFormInk.Frame(g,PostalFormInk.Area(x,4,w,h-10),.23f);
            Header(g,t,5,14,10);
            PostalFormInk.Rule(g,x+split,20,x+split,h-8,.27f);
            PostalFormInk.Part(g,"Acknowledge","交付及实收证明","1",x,21,split,7.7f);
            Cells(g,t.FillFields.Where(f=>!new[]{"To","AddressTo","CallTo"}.Contains(f.Key)),data,x,28.7f,split,2,11.1f,8);
            PostalType.Text(g,"A.R.",PostalType.Sans,PostalFormInk.Area(x+split+1,22,w-split-2,13),18,true,true);
            PostalFormInk.Part(g,"ReturnTo","回执寄回","2",x+split,37,w-split,7.7f);
            Cells(g,t.FillFields.Where(f=>new[]{"To","CallTo","AddressTo"}.Contains(f.Key)),data,x+split,44.7f,w-split,1,13,8.4f);
            PostalFormInk.Signature(g,"ToSign","寄达台签章",x+split+5,h-15,w-split-10);
            AddFooter(g,t,"");
        }
    }
}
