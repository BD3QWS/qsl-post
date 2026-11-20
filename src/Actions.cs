using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace QslPost
{
    public partial class MainForm
    {
        void EditCard(Card original)
        {
            Card c=original==null?new Card{FromCall=repo.Data.Settings.Callsign,SenderAddress=repo.Data.Settings.Address}:repo.Clone(repo.Data).Cards.Single(x=>x.Id==original.Id);
            string sender=DemoData.Address(repo.Data,c.FromCall,c.SenderAddress),recipient=DemoData.Address(repo.Data,c.ToCall,c.RecipientAddress);
            var fields=new List<Field>{new Field("direction","业务方向",c.Direction).Choices("出口","进口"),new Field("kind","卡片内容",c.Kind).Choices("QSO","SWL"),new Field("from","原寄电台呼号 *",c.FromCall),new Field("to","寄达电台呼号 *",c.ToCall),new Field("via","QSL 经理人 / Via",c.Via),new Field("country","国家 / 地区",c.Country),new Field("grid","梅登海德网格",c.Grid),new Field("utc","通联 UTC · yyyy-MM-dd HH:mm",c.QsoUtc),new Field("freq","频率 MHz",c.Frequency),new Field("mode","通联模式",c.Mode).Choices("SSB","CW","FM","AM","FT8","FT4","RTTY","SSTV","DIGITAL","OTHER"),new Field("rs","发出 RST",c.RstSent),new Field("rr","收到 RST",c.RstReceived),new Field("equipment","设备",c.Equipment),new Field("antenna","天线",c.Antenna),new Field("power","功率 W",c.Power),new Field("mail","邮类",c.MailClass).Choices("平信","条码平信","无单平信","挂信","印刷品","快递","合封"),new Field("sender","寄件人名址",sender).Multi(),new Field("address","收件人名址",recipient).Multi(),new Field("notes","备注",c.Notes).Multi()};
            using(var f=new RecordDialog(original==null?"登记 QSL 卡片":"修改卡片 · "+c.Id,"通联时间填 UTC，卡号自动生成。",fields,d=>{
                c.Direction=d.Value("direction");c.Kind=d.Value("kind");c.FromCall=d.Value("from");c.ToCall=d.Value("to");c.Via=d.Value("via");c.Country=d.Value("country");c.Grid=d.Value("grid");c.QsoUtc=d.Value("utc");c.Frequency=d.Value("freq");c.Mode=d.Value("mode");c.RstSent=d.Value("rs");c.RstReceived=d.Value("rr");c.Equipment=d.Value("equipment");c.Antenna=d.Value("antenna");c.Power=d.Value("power");c.MailClass=d.Value("mail");
                if(d.Value("sender")!=sender)c.SenderAddress=d.Value("sender");
                if(d.Value("address")!=recipient)c.RecipientAddress=d.Value("address");
                c.Notes=d.Value("notes");if(original==null)repo.AddCard(c);else repo.EditCard(c);
            }))if(f.ShowDialog(this)==DialogResult.OK){cardFilter=c.Id;Navigate("卡片台账");}
        }
        void CreateBag()
        {
            var settings=repo.Data.Settings;
            using(var f=new RecordDialog("建立封发邮袋","节点代码填 6 位字母。袋号按路向和毛重生成。",new[]{
                new Field("from","封发台 / 原寄卡片局 *",settings.Callsign.Length>0?settings.Callsign:settings.Organization),new Field("to","寄达台 / 卡片局 *",""),new Field("origin","原寄内部节点代码 · 6 位字母",settings.OriginCode),new Field("destination","寄达内部节点代码 · 6 位字母","QSLBBB"),new Field("direction","业务方向","出口").Choices("出口","进口"),new Field("transport","运输方式","航空").Choices("航空","SAL","水陆"),new Field("class","邮袋类型","挂信").Choices("平信","挂信","合封","印刷品","快递","空袋"),new Field("weight","毛重 kg · 0-999.8","0.5"),new Field("seal","封志 / 特别标记",""),new Field("via","中转卡片局 / Via",""),new Field("notes","封发说明","").Multi()
            },d=>{decimal weight;if(!Decimal.TryParse(d.Value("weight"),NumberStyles.Number,CultureInfo.InvariantCulture,out weight))throw new InvalidOperationException("重量须为数字，使用英文小数点。");var b=repo.CreateBag(d.Value("from"),d.Value("to"),d.Value("origin"),d.Value("destination"),d.Value("transport"),d.Value("class"),d.Value("direction"),weight,d.Value("seal"),d.Value("via"),d.Value("notes"));bagFilter=b.Id;}))if(f.ShowDialog(this)==DialogResult.OK)Navigate("封发作业");
        }
        void AssignCards(string[] ids)
        {
            if(ids.Length==0)throw new InvalidOperationException("请选择要装入的卡片，可用 Ctrl / Shift 多选。");var bags=repo.Data.Bags.Where(b=>b.State=="草稿").ToList();if(bags.Count==0)throw new InvalidOperationException("请先建立一个草稿邮袋。");
            string[] choices=bags.Select(b=>b.DispatchId+" · "+b.To+" · "+b.Direction).ToArray();using(var f=new RecordDialog("装入邮袋","待装 "+ids.Length+" 张。请选择同方向的草稿袋。",new[]{new Field("bag","选择草稿邮袋",choices[0]).Choices(choices)},d=>{int n=Array.IndexOf(choices,d.Value("bag"));repo.AddToBag(bags[n].Id,ids);} ))if(f.ShowDialog(this)==DialogResult.OK)Navigate("封发作业");
        }
        void SelectBagCards(Bag b)
        {
            if(b.State!="草稿")throw new InvalidOperationException("此邮袋已封袋。请按流程重开后再调整内容。");var cards=repo.Data.Cards.Where(c=>c.BagId.Length==0&&c.Direction==b.Direction&&c.Status!="已交付"&&c.Status!="已作废").ToList();if(cards.Count==0)throw new InvalidOperationException("没有符合方向且尚未装袋的卡片。请先登记卡片。");
            using(var f=new SelectionDialog("装入卡片 · "+b.To,"勾选实体卡片，核对编号后装入。可筛选呼号或卡号。",cards))if(f.ShowDialog(this)==DialogResult.OK){repo.AddToBag(b.Id,f.Selected);Navigate(page);}
        }
        void BagAction(Bag b)
        {
            if(b.State=="草稿")
            {
                if(MessageBox.Show(this,"即将封袋并锁定清单，共 "+b.CardIds.Count+" 张卡片。\n\n总封："+b.DispatchId+"\n寄达："+b.To+"\n重量："+b.WeightKg+" kg\n封志："+b.Seal+"\n\n请核对实物后封袋。","封袋核对",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
                repo.Transition(b.Id,"已封袋",b.Seal);Navigate(page);
            }
            else if(b.State=="已封袋")
            {
                using(var f=new RecordDialog("交接邮袋","请填接收人和交接地点。",new[]{new Field("note","接收人 / 地点 / 交接说明 *","").Multi()},d=>repo.Transition(b.Id,"已交接",d.Value("note"))))
                    if(f.ShowDialog(this)==DialogResult.OK)Navigate(page);
            }
            else if(b.State=="已交接")ReceiveBag(b);else throw new InvalidOperationException("此邮袋已接收，可在卡片台账中逐张确认交付。");
        }
        void ReceiveBag(Bag b)
        {
            if(b.State!="已交接")throw new InvalidOperationException("请先完成封袋与交接流程。");
            using(var f=new RecordDialog("接收邮袋","应收 "+b.CardIds.Count+" 张。少卡或多卡时请开验单。",new[]{new Field("count","核对后的实收件数 *",""),new Field("note","接收人 / 实物核对说明 *","").Multi()},d=>{int n;if(!Int32.TryParse(d.Value("count"),out n)||n!=b.CardIds.Count)throw new InvalidOperationException("实收件数与清单不一致，请建立验单记录差异。");if(d.Value("note").Length==0)throw new InvalidOperationException("请填写接收人及核对说明。");repo.Transition(b.Id,"已接收",d.Value("note"));}))if(f.ShowDialog(this)==DialogResult.OK)Navigate(page);
        }
        void ReopenBag()
        {
            var b=RequireBag();if(b.State!="已封袋")throw new InvalidOperationException("只有已封袋且尚未交接的邮袋可以重开。交接后的差错请建立验单。");using(var f=new RecordDialog("重开邮袋","调整清单后，请重新封袋。",new[]{new Field("reason","重开原因 *","").Multi()},d=>repo.Transition(b.Id,"草稿",d.Value("reason"))))if(f.ShowDialog(this)==DialogResult.OK)Navigate(page);
        }
        void MarkCard(string state)
        {
            Card c=RequireCard();using(var f=new RecordDialog(state+" · "+c.Id,state=="已作废"?"作废后仍能按卡号查到。":"确认收卡台站已收到此张实体卡片。",new[]{new Field("note",state=="已作废"?"作废原因 *":"签收人 / 交付说明 *","").Multi()},d=>repo.MarkCard(c.Id,state,d.Value("note"))))if(f.ShowDialog(this)==DialogResult.OK)Navigate(page);
        }
        void ForwardCard()
        {
            Card c=RequireCard();using(var f=new RecordDialog("接收后转寄 · "+c.Id,"转寄后回到出口待封发，沿用原卡号。",new[]{new Field("note","转寄原因 / 去向说明 *","").Multi()},d=>repo.ForwardCard(c.Id,d.Value("note"))))if(f.ShowDialog(this)==DialogResult.OK)Navigate(page);
        }
        void ImportCards()
        {
            using(var dialog=new OpenFileDialog{Title="导入 QSL 卡片记录",Filter="CSV / ADIF 文件|*.csv;*.adi;*.adif|CSV 文件|*.csv|ADIF 文件|*.adi;*.adif"})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;if(new FileInfo(dialog.FileName).Length>32*1024*1024)throw new InvalidOperationException("导入文件过大，请分成 32 MB 以下的批次。");
                var cards=Path.GetExtension(dialog.FileName).Equals(".csv",StringComparison.OrdinalIgnoreCase)?Exchange.ReadCsv(dialog.FileName):Exchange.ReadAdif(dialog.FileName,repo.Data.Settings.Callsign);
                if(cards.Count==0)throw new InvalidOperationException("文件没有可导入的卡片记录。");
                using(var preview=new SelectionDialog("导入预览",cards.Count+" 条解析成功。默认全选；CSV 按来源卡号、ADIF 按通联去重；不同编号的实体卡即使通联相同也分别登记。重新编号，不导入装袋状态。",cards,true))
                {
                    if(preview.ShowDialog(this)!=DialogResult.OK)return;var selected=preview.Selected.Select(x=>cards[Int32.Parse(x)]).ToList();int skipped;int added=Exchange.Import(repo,selected,out skipped);MessageBox.Show(this,"已导入 "+added+" 张；跳过重复记录 "+skipped+" 条。","导入完成");Navigate("卡片台账");
                }
            }
        }
        void ExportCards()
        {
            using(var d=new SaveFileDialog{Title="导出当前筛选结果 / CSV 样表",Filter="CSV 文件|*.csv",FileName="QSL卡片台账-"+Clock.Date+".csv"})if(d.ShowDialog(this)==DialogResult.OK){var ids=new HashSet<string>(cardsGrid.Rows.Cast<DataGridViewRow>().Select(r=>Convert.ToString(r.Cells[0].Value)));Exchange.ExportCards(repo,d.FileName,repo.Data.Cards.Where(c=>ids.Contains(c.Id)));status.Text="已导出 "+ids.Count+" 张卡片到 "+d.FileName;}
        }
        void EditShipment(Shipment original,string bagId,string cardId)
        {
            Shipment value=original==null?new Shipment{BagId=bagId,CardId=cardId}:repo.Clone(repo.Data).Shipments.Single(x=>x.Id==original.Id);
            var targets=new Dictionary<string,string>();foreach(var b in repo.Data.Bags)targets["邮袋 · "+b.DispatchId+" · "+b.To]=b.Id;foreach(var c in repo.Data.Cards)targets["卡片 · "+c.Id+" · "+c.ToCall]=c.Id;
            if(targets.Count==0)throw new InvalidOperationException("请先建立卡片或邮袋，再绑定物流。");string current=targets.FirstOrDefault(x=>x.Value==(value.BagId.Length>0?value.BagId:value.CardId)).Key??targets.Keys.First();
            var fields=new[]{new Field("target","关联对象 *",current).Choices(targets.Keys.ToArray()),new Field("number","物流单号 *（保留前导零）",value.Number),
                new Field("carrier","承运商名称",value.CarrierName),new Field("leg","运输分段",value.Leg).Choices("收寄","国内转寄","干线运输","国际运输","末端派送","退回","其他")};
            using(var f=new RecordDialog(original==null?"绑定物流":"修改运单","每段运输填一个单号。换单号时请新增一笔。",fields,d=>{
                string target=targets[d.Value("target")];string number=Identifiers.Tracking(d.Value("number"));
                if(original!=null&&(target!=(value.CardId.Length>0?value.CardId:value.BagId)||number!=value.Number))throw new InvalidOperationException("修改号码或关联对象请新建物流记录，旧记录继续保留。");
                value.Number=number;value.CarrierName=d.Value("carrier");value.Leg=d.Value("leg");
                if(original==null){if(target.StartsWith("QSL-"))value.CardId=target;else value.BagId=target;repo.AddShipment(value);}else repo.Change(db=>{int n=db.Shipments.FindIndex(x=>x.Id==value.Id);db.Shipments[n]=value;Repository.Log(db,"补充物流信息",value.Id,value.Number,new[]{value.BagId,value.CardId});});
            }))if(f.ShowDialog(this)==DialogResult.OK)Navigate("物流追踪");
        }
        void OpenTrackingWeb()
        {
            if(demo)throw new InvalidOperationException("请切换到正式资料库查询运单。");
            Shipment shipment=RequireShipment();
            using(var viewer=new TrackingDialog(shipment,locations.CacheDirectory,()=>RecordProgress(shipment)))viewer.ShowDialog(this);
        }
        void RecordProgress(Shipment shipment)
        {
            var fields=new[]{
                new Field("time","物流时间（年-月-日 时:分）",DateTime.Now.ToString("yyyy-MM-dd HH:mm")),
                new Field("location","地点",""),
                new Field("state","物流状态","沿用当前状态").Choices("沿用当前状态","已揽收","运输中","派送中","已妥投","运输异常","已退回"),
                new Field("description","进度","").Multi()
            };
            using(var dialog=new RecordDialog("保存物流进度",shipment.Number,fields,d=>{
                string state=d.Value("state");
                Tracker.SaveProgress(repo,shipment.Id,d.Value("time"),d.Value("location"),d.Value("description"),state=="沿用当前状态"?"":state);
            }))
            {
                var owner=ActiveForm??this;
                if(dialog.ShowDialog(owner)!=DialogResult.OK)return;
                if(page=="物流追踪"){Navigate(page);SelectRow(shipGrid,shipment.Id);}
                status.Text="物流进度已保存。";
            }
        }
        void EditCase(CaseRecord original)
        {
            var c=original==null?new CaseRecord{From=repo.Data.Settings.Callsign,Operator=repo.Data.Settings.Operator}:repo.Clone(repo.Data).Cases.Single(x=>x.Id==original.Id);
            var cardChoices=new[]{"不关联"}.Concat(repo.Data.Cards.Select(x=>x.Id)).ToArray();var bagChoices=new[]{"不关联"}.Concat(repo.Data.Bags.Select(x=>x.Id)).ToArray();var priorChoices=new[]{"不关联"}.Concat(repo.Data.Cases.Where(x=>x.Id!=c.Id).Select(x=>x.Id)).ToArray();
            using(var f=new RecordDialog(original==null?"新建查验与查询文书":"填写文书 · "+c.Id,"保存后可出单。",new[]{new Field("type","文书类型",c.Type).Choices("验单","复验","国内查单","国内回单","国际查单","国际答复","未收声明"),new Field("state","办理状态",c.State).Choices("待处理","已发出","待答复","已答复","已结案"),new Field("from","发单电台",c.From),new Field("to","寄达电台",c.To),new Field("card","关联永久卡号",c.CardId.Length>0?c.CardId:"不关联").Choices(cardChoices),new Field("bag","关联邮袋",c.BagId.Length>0?c.BagId:"不关联").Choices(bagChoices),new Field("related","答复原单编号",c.RelatedCase.Length>0?c.RelatedCase:"不关联").Choices(priorChoices),new Field("operator","经办人",c.Operator),new Field("subject","主题 *",c.Subject),new Field("supervisor","主管人员",c.Supervisor),new Field("details","差错 / 查询事项 *",c.Details).Multi(),new Field("reply","查明情况 / 答复",c.Reply).Multi(),new Field("attachment","附件说明",c.Attachment).Multi()},d=>{
                if(d.Value("subject").Length==0||d.Value("details").Length==0)throw new InvalidOperationException("请填写主题和具体事项。");c.Type=d.Value("type");c.State=d.Value("state");c.From=d.Value("from");c.To=d.Value("to");c.CardId=d.Value("card")=="不关联"?"":d.Value("card");c.BagId=d.Value("bag")=="不关联"?"":d.Value("bag");c.RelatedCase=d.Value("related")=="不关联"?"":d.Value("related");c.Operator=d.Value("operator");c.Subject=d.Value("subject");c.Supervisor=d.Value("supervisor");c.Details=d.Value("details");c.Reply=d.Value("reply");c.Attachment=d.Value("attachment");
                if(c.Type=="复验"&&c.RelatedCase.Length==0)throw new InvalidOperationException("复验请关联原验单。");if(c.State=="已结案"&&c.Reply.Length==0)throw new InvalidOperationException("请填写答复后结案。");
                repo.Change(db=>{if(original==null){c.Id="CASE-"+Clock.Date+"-"+(db.NextCase++).ToString("0000");c.CreatedUtc=Clock.Utc;db.Cases.Add(c);}else db.Cases[db.Cases.FindIndex(x=>x.Id==c.Id)]=c;c.ClosedUtc=c.State=="已结案"?Clock.Utc:"";Repository.Log(db,original==null?"建立查验单":"办理查验单",c.Id,c.Type+" / "+c.Subject+" / "+c.State+"\n事项："+c.Details+"\n答复："+c.Reply,new[]{c.CardId,c.BagId,c.RelatedCase});});
            }))if(f.ShowDialog(this)==DialogResult.OK)Navigate("查验与查单");
        }
        void EditContact(Contact original)
        {
            Contact c=original==null?new Contact():repo.Clone(repo.Data).Contacts.Single(x=>x.Id==original.Id);
            bool english=demo&&DemoData.DisplayEnglish(repo.Data,c);
            string name=english&&c.EnglishName.Length>0?c.EnglishName:c.Name;
            var fields=new[]{new Field("call","呼号 / 卡片局代码 *",c.Callsign),new Field("name","名称",name),new Field("country","国家地区",c.Country),
                new Field("zip","邮政编码",c.PostalCode),new Field("phone","联系电话",c.Phone),new Field("email","电子邮箱",c.Email),
                new Field("address",english?"收件地址（英文）":"收件地址",english?c.EnglishAddress:c.Address).Multi(),new Field("notes","说明",c.Notes).Multi()};
            using(var f=new RecordDialog(original==null?"新增台站":"修改台站名址","",fields,d=>{
                if(d.Value("call").Length==0)throw new InvalidOperationException("请填写呼号或卡片局代码。");
                c.Callsign=d.Value("call").ToUpperInvariant();c.Country=d.Value("country");
                c.PostalCode=d.Value("zip");c.Phone=d.Value("phone");c.Email=d.Value("email");c.Notes=d.Value("notes");
                if(english){c.EnglishName=d.Value("name");c.EnglishAddress=d.Value("address");}
                else{c.Name=d.Value("name");c.Address=d.Value("address");}
                repo.Change(db=>{
                    if(original==null){c.Id="CONTACT-"+(db.NextContact++).ToString("0000");db.Contacts.Add(c);}
                    else db.Contacts[db.Contacts.FindIndex(x=>x.Id==c.Id)]=c;
                    Repository.Log(db,"保存台站名址",c.Id,c.Callsign,null);
                });
            }))if(f.ShowDialog(this)==DialogResult.OK)Navigate("台站名址");
        }
        void EditSettings()
        {
            var s=repo.Data.Settings;
            using(var f=new RecordDialog("台站资料","",new[]{new Field("profile","使用方式 *",s.Profile).Choices("","协会 / 卡片局","个人台站"),new Field("org","台站或卡片局名称 *",s.Organization),new Field("call","本台呼号",s.Callsign),new Field("operator","经办人",s.Operator),new Field("origin","内部节点代码（6 位英文字母）*",s.OriginCode),new Field("address","本台寄件名址",s.Address).Multi()},d=>{
                if(d.Value("profile").Length==0)throw new InvalidOperationException("请选择个人台站或卡片局。");
                if(d.Value("org").Length==0)throw new InvalidOperationException("请填写台站或卡片局名称。");
                Identifiers.Dispatch(d.Value("origin"),"QSLBBB","航空",DateTime.Now,1);
                repo.Change(db=>{db.Settings.Profile=d.Value("profile");db.Settings.Organization=d.Value("org");db.Settings.Callsign=d.Value("call").ToUpperInvariant();db.Settings.Operator=d.Value("operator");db.Settings.OriginCode=d.Value("origin").ToUpperInvariant();db.Settings.Address=d.Value("address");db.Settings.Initialized=true;Repository.Log(db,"保存台站设置","设置",db.Settings.Organization+" / "+db.Settings.Profile,null);});
            }))if(f.ShowDialog(this)==DialogResult.OK){Navigate(page);status.Text="台站资料已保存。";}
        }
        void Backup(){using(var d=new SaveFileDialog{Title="备份完整资料库",Filter="QSL 资料库 XML|*.xml",FileName="QSL邮务台备份-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".xml"})if(d.ShowDialog(this)==DialogResult.OK){repo.Backup(d.FileName);status.Text="备份已保存。";}}
        void Restore()
        {
            using(var d=new OpenFileDialog{Title="选择 QSL 邮务台备份",Filter="QSL 资料库 XML|*.xml"})if(d.ShowDialog(this)==DialogResult.OK){var candidate=repo.Read(d.FileName);Repository.Validate(candidate);if(MessageBox.Show(this,"将恢复为备份中的 "+candidate.Cards.Count+" 张卡片、"+candidate.Bags.Count+" 个邮袋。\n当前资料会在本地另存为恢复前备份。\n\n恢复后不会自动合并当前记录。","恢复资料库",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;repo.Restore(d.FileName);Navigate(page);status.Text="恢复完成。原资料已备份。";}
        }
        void ShowHistory(string id)
        {
            using(var f=new GlassForm{Text=id==null?"完整操作历史":"操作历史 · "+id,Width=1080,Height=710,MinimumSize=new Size(720,500),StartPosition=FormStartPosition.CenterParent,Padding=new Padding(18)})
            {
                var grid=Ui.Grid();Ui.Columns(grid,"id","序号","时间","经办人","操作","关联编号","说明");grid.Columns[6].FillWeight=240;var events=(id==null?repo.Data.Audit.OrderByDescending(x=>x.Sequence):repo.History(id)).ToList();foreach(var a in events)grid.Rows.Add(a.Sequence.ToString(),a.Sequence,Clock.Display(a.TimeUtc),a.Actor,a.Action,a.Entity,a.Details);
                var details=Ui.Input("");details.ReadOnly=true;details.Multiline=true;details.ScrollBars=ScrollBars.Vertical;details.Dock=DockStyle.Bottom;details.Height=130;grid.CurrentCellChanged+=(s,e)=>{long seq;var a=Int64.TryParse(Ui.Selected(grid),out seq)?events.FirstOrDefault(x=>x.Sequence==seq):null;details.Text=a==null?"":a.Details+"\r\n关联："+a.Links+"\r\n完整性哈希："+a.Hash;};f.Controls.Add(grid);f.Controls.Add(details);f.ShowDialog(this);
            }
        }
        void ShowTrace(string id)
        {
            var c=FindCard(id);if(c==null)return;using(var f=new GlassForm{Text="逐张溯源 · "+c.Id,Width=1100,Height=780,MinimumSize=new Size(760,530),StartPosition=FormStartPosition.CenterParent,Padding=new Padding(24)})
            {
                var top=Ui.Label(c.FromCall+" → "+c.ToCall+"   /   "+c.Status,21,true);top.Dock=DockStyle.Top;top.Height=50;var info=Ui.Label(c.Id+"    "+c.Direction+"    "+c.QsoUtc+" UTC    "+c.Frequency+" MHz / "+c.Mode+"\n所属邮袋："+(c.BagId.Length>0?c.BagId:"尚未装袋")+"\n物流："+String.Join(" / ",repo.CardShipments(c).Select(s=>s.Number+"（"+s.Leg+" / "+s.Status+"）").ToArray()),10,false);info.Dock=DockStyle.Top;info.Height=108;
                var bar=Ui.Toolbar();bar.Dock=DockStyle.Bottom;bar.Controls.Add(Ui.Button("打印卡号标签",()=>OpenPrint("cardlabel",FindBag(c.BagId),c,null),true));bar.Controls.Add(Ui.Button("查看相关物流",()=>{f.Close();Navigate("物流追踪");var s=repo.CardShipments(c).FirstOrDefault();if(s!=null)SelectRow(shipGrid,s.Id);},false));
                var history=Ui.Grid();Ui.Columns(history,"id","时间","经办人","流转操作","说明");history.Columns[4].FillWeight=280;foreach(var a in repo.History(c.Id))history.Rows.Add(a.Sequence.ToString(),Clock.Display(a.TimeUtc),a.Actor,a.Action,a.Details);f.Controls.Add(history);f.Controls.Add(info);f.Controls.Add(top);f.Controls.Add(bar);f.ShowDialog(this);
            }
        }
    }
    public class SelectionDialog:GlassForm
    {
        readonly DataGridView grid;readonly HashSet<string> checkedIds=new HashSet<string>();
        public string[] Selected{get{return checkedIds.ToArray();}}
        public SelectionDialog(string title,string explanation,List<Card> cards,bool import=false)
        {
            Text=title;Width=1000;Height=720;MinimumSize=new Size(800,520);StartPosition=FormStartPosition.CenterParent;Padding=new Padding(22);var label=Ui.Note(explanation);label.Dock=DockStyle.Top;label.Height=55;var bar=Ui.Toolbar();var filter=Ui.Input("");filter.Width=280;bar.Controls.Add(filter);var count=Ui.Label("",9,false);count.Height=32;count.Width=150;
            grid=Ui.Grid();grid.ReadOnly=false;grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="选择",HeaderText="选",Width=42,AutoSizeMode=DataGridViewAutoSizeColumnMode.None});Ui.Columns(grid,"id","卡片编号","原寄","寄达","UTC","MHz","模式");grid.Columns[0].Visible=true;grid.Columns[1].Visible=false;for(int i=2;i<grid.Columns.Count;i++)grid.Columns[i].ReadOnly=true;
            var records=cards.Select((c,i)=>new {Card=c,Id=import?i.ToString():c.Id}).ToList();if(import)foreach(var r in records)checkedIds.Add(r.Id);
            Action populate=()=>{grid.Rows.Clear();foreach(var r in records.Where(r=>(r.Card.Id+" "+r.Card.FromCall+" "+r.Card.ToCall).IndexOf(filter.Text,StringComparison.OrdinalIgnoreCase)>=0))grid.Rows.Add(checkedIds.Contains(r.Id),r.Id,import?"保存时生成":r.Card.Id,r.Card.FromCall,r.Card.ToCall,r.Card.QsoUtc,r.Card.Frequency,r.Card.Mode);count.Text="已选 "+checkedIds.Count+" 张";};
            grid.CurrentCellDirtyStateChanged+=(s,e)=>{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};grid.CellValueChanged+=(s,e)=>{if(e.RowIndex<0||e.ColumnIndex!=0)return;var row=grid.Rows[e.RowIndex];string id=Convert.ToString(row.Cells[1].Value);if(Convert.ToBoolean(row.Cells[0].Value))checkedIds.Add(id);else checkedIds.Remove(id);count.Text="已选 "+checkedIds.Count+" 张";};filter.TextChanged+=(s,e)=>populate();bar.Controls.Add(Ui.Button("全选当前结果",()=>{foreach(DataGridViewRow r in grid.Rows)r.Cells[0].Value=true;},false));bar.Controls.Add(Ui.Button("清空选择",()=>{checkedIds.Clear();populate();},false));bar.Controls.Add(count);
            var bottom=Ui.Toolbar();bottom.Dock=DockStyle.Bottom;bottom.Controls.Add(Ui.Button(import?"导入所选":"装入所选",()=>{if(checkedIds.Count==0)throw new InvalidOperationException("请先勾选卡片。");DialogResult=DialogResult.OK;Close();},true));bottom.Controls.Add(Ui.Button("取消",()=>{DialogResult=DialogResult.Cancel;Close();},false));Controls.Add(grid);Controls.Add(bar);Controls.Add(label);Controls.Add(bottom);populate();
        }
    }
}



