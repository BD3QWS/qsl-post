using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace QslPost
{
    public partial class MainForm:GlassForm
    {
        readonly Repository repo;
        readonly bool demo;
        readonly StorageLocations locations;
        Panel content;
        Label pageTitle,pageSubtitle,status,stationSummary;
        TextBox scanner;
        readonly List<GlassButton> navigation=new List<GlassButton>();
        string page="总览",cardFilter="",bagFilter="";
        DataGridView cardsGrid,bagsGrid,shipGrid,caseGrid,contactGrid;
        protected override bool FitWindow { get { return true; } }
        public MainForm(Repository repository,bool demonstration,StorageLocations storage=null)
        {
            repo=repository;demo=demonstration;locations=storage??StorageLocations.Session(repo.Folder);
            Text=demo?"QSL 邮务台（演示版）":"QSL 邮务台";Width=1440;Height=910;MinimumSize=new Size(1060,700);StartPosition=FormStartPosition.CenterScreen;
            var shell=new SurfaceTablePanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Padding=new Padding(18,18,18,10),BackColor=Color.Transparent};shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,206));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,30));Controls.Add(shell);
            var sidebar=new GlassPanel{Dock=DockStyle.Fill,Margin=new Padding(0,0,18,0),Padding=new Padding(14,20,14,15)};shell.Controls.Add(sidebar,0,0);
            var logo=Ui.Label("QSL 邮务台",20,true);logo.Dock=DockStyle.Top;logo.Height=42;var hint=Ui.Note("卡片收发与封发系统");hint.Dock=DockStyle.Top;hint.Height=33;
            string[] names={"总览","卡片台账","封发作业","物流追踪","单据中心","查验与查单","台站名址","设置与备份"};string[] icons={"home","cards","bags","track","print","cases","contacts","settings"};
            const int navRowHeight=50,navTopInset=8;
            var navHost=new SurfacePanel{Name="navigationViewport",Dock=DockStyle.Fill,AutoScroll=true,BackColor=Color.Transparent};
            var nav=new SurfaceTablePanel{Dock=DockStyle.Top,ColumnCount=1,RowCount=names.Length,
                Height=names.Length*navRowHeight+navTopInset,BackColor=Color.Transparent,Padding=new Padding(0,navTopInset,0,0)};
            nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            for(int i=0;i<names.Length;i++)
            {
                string target=names[i];
                var button=new GlassButton{Text=target,IconKind=icons[i],Dock=DockStyle.Fill,Margin=new Padding(0,0,0,5)};
                button.Click+=(s,e)=>Navigate(target);
                navigation.Add(button);
                nav.RowStyles.Add(new RowStyle(SizeType.Absolute,navRowHeight));
                nav.Controls.Add(button,0,i);
            }
            navHost.Controls.Add(nav);
            stationSummary=Ui.Label("",9,false);
            stationSummary.Dock=DockStyle.Bottom;stationSummary.Height=48;stationSummary.ForeColor=Ui.Quiet;
            sidebar.Controls.Add(navHost);sidebar.Controls.Add(hint);sidebar.Controls.Add(logo);sidebar.Controls.Add(stationSummary);
            var main=new SurfacePanel{Dock=DockStyle.Fill,BackColor=Color.Transparent};shell.Controls.Add(main,1,0);
            var top=new SurfacePanel{Dock=DockStyle.Top,Height=100,BackColor=Color.Transparent};
            var heading=new SurfacePanel{Dock=DockStyle.Fill,Padding=new Padding(0,3,20,0),BackColor=Color.Transparent};
            pageTitle=Ui.Label("",26,true);pageTitle.Dock=DockStyle.Top;pageTitle.Height=50;
            pageSubtitle=Ui.Label("",9.5f,false);pageSubtitle.ForeColor=Ui.Quiet;pageSubtitle.Dock=DockStyle.Top;pageSubtitle.Height=31;
            heading.Controls.Add(pageSubtitle);heading.Controls.Add(pageTitle);
            var searchWrap=new SurfacePanel{Dock=DockStyle.Right,Width=320,Padding=new Padding(0,12,20,0),BackColor=Color.Transparent};
            scanner=Ui.Input("");scanner.AccessibleName="扫码或搜索编号";scanner.Dock=DockStyle.Top;
            var scanHint=Ui.Note("扫码 / 输入卡号、袋号或物流号后回车");scanHint.Dock=DockStyle.Top;scanHint.Height=42;scanHint.Padding=new Padding(0,6,0,0);
            searchWrap.Controls.Add(scanHint);searchWrap.Controls.Add(scanner);
            top.Controls.Add(heading);top.Controls.Add(searchWrap);
            scanner.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Ui.Guard(this,()=>FindRecord(scanner.Text.Trim()));}};
            main.Controls.Add(top);content=new SurfacePanel{Dock=DockStyle.Fill,BackColor=Color.Transparent};main.Controls.Add(content);content.BringToFront();
            status=Ui.Label(demo?"演示资料":"",8.5f,false);status.ForeColor=Ui.Quiet;status.Dock=DockStyle.Fill;shell.Controls.Add(status,0,1);shell.SetColumnSpan(status,2);
            Navigate(!demo&&!repo.Data.Settings.Initialized&&repo.Data.Cards.Count==0?"卡片台账":"总览");
            KeyPreview=true;KeyDown+=(s,e)=>{if(e.Control&&e.KeyCode==Keys.F){scanner.Focus();scanner.SelectAll();e.SuppressKeyPress=true;}if(e.KeyCode==Keys.F5){Navigate(page);e.SuppressKeyPress=true;}if(e.KeyCode==Keys.F1){ShowManual();e.SuppressKeyPress=true;}};
            Shown+=(s,e)=>{if(!demo&&!repo.Data.Settings.Initialized&&!repo.Data.Settings.HideStartupGuide)ShowStartupGuide();};
        }

        void ShowStartupGuide()
        {
            using(var guide=new StartupGuide())
            {
                var result=guide.ShowDialog(this);
                if(guide.HideNextTime)repo.Change(db=>db.Settings.HideStartupGuide=true);
                if(result==DialogResult.OK)ShowManual();
            }
        }
        public void Navigate(string target)
        {
            page=target;stationSummary.Text=(demo?"独立演示资料\n":repo.Data.Settings.Profile+"\n")+(repo.Data.Settings.Callsign.Length>0?repo.Data.Settings.Callsign:"尚未设置本台呼号");foreach(var b in navigation){b.Selected=b.Text==target;b.Invalidate();}
            content.SuspendLayout();foreach(Control child in content.Controls.Cast<Control>().ToArray()){content.Controls.Remove(child);child.Dispose();}content.ResumeLayout();pageTitle.Text=target=="总览"?"收发工作台":target;
            var stationName=repo.Data.Settings.Organization;
            pageSubtitle.Text=demo?"演示资料":(stationName.Length==0?"":stationName+"  ·  ")+DateTime.Now.ToString("yyyy年M月d日");
            switch(target){case "总览":Overview();break;case "卡片台账":CardsPage();break;case "封发作业":BagsPage();break;case "物流追踪":TrackingPage();break;case "单据中心":PrintPage();break;case "查验与查单":CasesPage();break;case "台站名址":ContactsPage();break;default:SettingsPage();break;}
            foreach(Control child in content.Controls)WindowLayout.Scale(child,LayoutScale);
        }
        GlassPanel Surface(){var panel=new GlassPanel{Dock=DockStyle.Fill,Padding=new Padding(18),Margin=new Padding(0)};content.Controls.Add(panel);return panel;}
        void Overview()
        {
            var surface=Surface();var actions=Ui.Toolbar();actions.Controls.Add(Ui.Button("登记卡片",()=>EditCard(null),true));actions.Controls.Add(Ui.Button("建立邮袋",CreateBag,false));actions.Controls.Add(Ui.Button("生成单据",()=>Navigate("单据中心"),false));surface.Controls.Add(actions);
            var summary=new SurfaceTablePanel{Dock=DockStyle.Top,Height=104,ColumnCount=4,RowCount=1,BackColor=Color.Transparent,Padding=new Padding(0,15,0,10)};
            summary.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            string[] labels={"待分拣 / 待封发","待交接邮袋","运输中物流","待处理查验"};int[] values={repo.Data.Cards.Count(c=>c.Status=="待封发"||c.Status=="待分拣"),repo.Data.Bags.Count(b=>b.State=="已封袋"),repo.Data.Shipments.Count(s=>s.Status!="物流已妥投"&&s.Status!="已妥投"&&s.Status!="已退回"),repo.Data.Cases.Count(c=>c.State!="已结案")};
            for(int i=0;i<4;i++){summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));var p=new SurfacePanel{Dock=DockStyle.Fill,BackColor=Color.Transparent};var l=Ui.Label(labels[i],9.5f,false);l.Dock=DockStyle.Bottom;l.Height=26;l.ForeColor=Ui.Quiet;var v=Ui.Label(values[i].ToString(),27,true);v.Dock=DockStyle.Fill;p.Controls.Add(v);p.Controls.Add(l);summary.Controls.Add(p,i,0);}surface.Controls.Add(summary);summary.BringToFront();actions.BringToFront();
            var body=new SurfaceTablePanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(0,12,0,0),BackColor=Color.Transparent};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,67));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));surface.Controls.Add(body);body.BringToFront();
            var left=new SurfacePanel{Dock=DockStyle.Fill,Padding=new Padding(0,0,20,0),BackColor=Color.Transparent};var caption=Ui.Label("最近流转",13,true);caption.Dock=DockStyle.Top;caption.Height=40;var grid=Ui.Grid();Ui.Columns(grid,"id","卡片编号","原寄 → 寄达","当前状态");Ui.FitColumns(grid,"QSL-00000000-000000","DEMO/A → DEMO/B","已装袋");foreach(var c in repo.Data.Cards.AsEnumerable().Reverse().Take(12))grid.Rows.Add(c.Id,c.Id,c.FromCall+" → "+c.ToCall,c.Status);grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)ShowTrace(Ui.Selected(grid));};left.Controls.Add(grid);left.Controls.Add(caption);body.Controls.Add(left,0,0);
            var right=new SurfacePanel{Dock=DockStyle.Fill,BackColor=Color.Transparent};var rh=Ui.Label("资料与操作记录",13,true);rh.Dock=DockStyle.Top;rh.Height=40;var foot=new SurfaceFlowPanel{Dock=DockStyle.Top,Height=105,FlowDirection=FlowDirection.TopDown,BackColor=Color.Transparent};foot.Controls.Add(Ui.Button("查看操作记录",()=>ShowHistory(null),false));foot.Controls.Add(Ui.Button("使用手册",ShowManual,false));right.Controls.Add(foot);right.Controls.Add(rh);body.Controls.Add(right,1,0);
            if(repo.Data.Cards.Count==0){var empty=Ui.Note("还没有卡片。可登记或导入 CSV / ADIF。");empty.Dock=DockStyle.Bottom;empty.Height=45;left.Controls.Add(empty);}
        }
        void CardsPage()
        {
            var panel=Surface();var toolbar=Ui.Toolbar();
            var edit=Ui.Button("修改",()=>EditCard(RequireCard()),false);
            var assign=Ui.Button("装入邮袋",()=>AssignCards(Ui.SelectedIds(cardsGrid)),false);
            var label=Ui.Button("打印标签",()=>OpenPrint("cardlabel",null,RequireCard(),null),false);
            toolbar.Controls.AddRange(new Control[]{Ui.Button("登记卡片",()=>EditCard(null),true),edit,
                Ui.Button("导入",ImportCards,false),Ui.Button("导出 CSV",ExportCards,false),assign,label});
            panel.Controls.Add(toolbar);
            var filters=Ui.Toolbar();filters.Height=72;var search=Ui.Input(cardFilter);search.Name="cardSearch";search.Width=220;search.AccessibleName="筛选卡片";var direction=Ui.Combo(new[]{"全部方向","出口","进口"});direction.Width=114;var state=Ui.Combo(new[]{"全部状态","待分拣","待封发","已装袋","已封发","已交接","已接收","已交付","已作废"});state.Width=124;filters.Controls.Add(FilterControl("卡号 / 呼号 / 袋号",search,220));filters.Controls.Add(FilterControl("进出口方向",direction,114));filters.Controls.Add(FilterControl("卡片状态",state,124));var count=Ui.Label("",9,false);count.Width=160;count.Height=30;filters.Controls.Add(count);panel.Controls.Add(filters);filters.BringToFront();toolbar.BringToFront();
            var split=new SurfaceTablePanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=Color.Transparent};split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,74));split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,26));split.RowStyles.Add(new RowStyle(SizeType.Percent,100));panel.Controls.Add(split);split.BringToFront();
            cardsGrid=Ui.Grid();Ui.Columns(cardsGrid,"id","卡片编号","原寄呼号","寄达呼号","UTC","状态");
            split.Controls.Add(cardsGrid,0,0);
            Ui.FitColumns(cardsGrid,"QSL-00000000-000000","DEMO/A","DEMO/B","0000-00-00 00:00","待封发");
            var detail=new SurfacePanel{Dock=DockStyle.Fill,Padding=new Padding(20,8,0,0),BackColor=Color.Transparent};var title=Ui.Label("",15,true);title.Name="cardDetailTitle";title.Dock=DockStyle.Top;title.Height=55;var meta=Ui.Label("",9.5f,false);meta.ForeColor=Ui.Quiet;meta.Dock=DockStyle.Top;meta.Height=90;var timeline=new Timeline{Dock=DockStyle.Fill};var bottom=new SurfaceTablePanel{Dock=DockStyle.Bottom,Height=88,ColumnCount=2,RowCount=2,BackColor=Color.Transparent};bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));bottom.RowStyles.Add(new RowStyle(SizeType.Percent,50));bottom.RowStyles.Add(new RowStyle(SizeType.Percent,50));var cardActions=new[]{Ui.Button("完整溯源",()=>ShowTrace(RequireCard().Id),false),Ui.Button("确认交付",()=>MarkCard("已交付"),false),Ui.Button("继续转寄",ForwardCard,false),Ui.Button("作废留档",()=>MarkCard("已作废"),false)};for(int n=0;n<cardActions.Length;n++){cardActions[n].Dock=DockStyle.Fill;cardActions[n].Margin=new Padding(2,3,2,3);bottom.Controls.Add(cardActions[n],n%2,n/2);}detail.Controls.Add(timeline);detail.Controls.Add(meta);detail.Controls.Add(title);detail.Controls.Add(bottom);split.Controls.Add(detail,1,0);
            Action refreshDetails=()=>{
                Card card=repo.Data.Cards.FirstOrDefault(c=>c.Id==Ui.Selected(cardsGrid));
                bool selected=card!=null;
                edit.Enabled=assign.Enabled=label.Enabled=selected;
                foreach(var button in cardActions)button.Enabled=selected;
                if(selected)
                {
                    title.Text=card.FromCall+" → "+card.ToCall;
                    string tracking=String.Join(" / ",repo.CardShipments(card).Select(x=>x.Number));
                    meta.Text=card.Id+"\n"+card.Direction+" · "+card.Status+" · "+card.Kind+"\n"+
                        card.Frequency+" MHz / "+card.Mode+"\n物流号码  "+(tracking.Length==0?"未绑定":tracking);
                    timeline.AuditEvents=repo.History(card.Id).Take(6).ToList();
                }
                else
                {
                    title.Text="未选中卡片";
                    meta.Text=repo.Data.Cards.Count==0?"登记卡片或导入 CSV / ADIF。":
                        cardsGrid.Rows.Count==0?"没有符合条件的卡片。":"选择一张卡片查看流转记录。";
                    timeline.AuditEvents.Clear();
                }
                timeline.Invalidate();
            };
            Action populate=()=>{
                string term=search.Text.Trim();cardsGrid.Rows.Clear();
                var found=repo.Data.Cards.Where(c=>(direction.SelectedIndex==0||c.Direction==direction.Text)&&
                    (state.SelectedIndex==0||c.Status==state.Text)&&(term.Length==0||
                    (c.Id+" "+c.FromCall+" "+c.ToCall+" "+c.BagId+" "+c.Country+" "+c.Via).IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0)).Reverse();
                foreach(var c in found)cardsGrid.Rows.Add(c.Id,c.Id,c.FromCall,c.ToCall,c.QsoUtc,c.Status);
                count.Text=cardsGrid.Rows.Count+" 张卡片";
                refreshDetails();
            };
            cardsGrid.CurrentCellChanged+=(s,e)=>refreshDetails();
            cardsGrid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)ShowTrace(Ui.Selected(cardsGrid));};
            search.TextChanged+=(s,e)=>populate();direction.SelectedIndexChanged+=(s,e)=>populate();state.SelectedIndexChanged+=(s,e)=>populate();populate();cardFilter="";
        }
        Card RequireCard(){string id=cardsGrid==null?"":Ui.Selected(cardsGrid);var card=repo.Data.Cards.FirstOrDefault(x=>x.Id==id);if(card==null)throw new InvalidOperationException("请先选择一张卡片。");return card;}
        Control FilterControl(string caption,Control input,int width)
        {
            var p=new SurfacePanel{Width=width,Height=56,Margin=new Padding(0,0,9,0),BackColor=Color.Transparent};var label=Ui.Label(caption,8.5f,false);label.Dock=DockStyle.Top;label.Height=23;label.ForeColor=Ui.Quiet;
            var row=new SurfacePanel{Dock=DockStyle.Bottom,Height=31,BackColor=Color.Transparent};input.Dock=DockStyle.Top;row.Controls.Add(input);
            p.Controls.Add(row);p.Controls.Add(label);return p;
        }
        Bag RequireBag(){string id=bagsGrid==null?"":Ui.Selected(bagsGrid);var bag=repo.Data.Bags.FirstOrDefault(x=>x.Id==id);if(bag==null)throw new InvalidOperationException("请先选择一个邮袋。");return bag;}
        Shipment RequireShipment(){string id=shipGrid==null?"":Ui.Selected(shipGrid);var shipment=repo.Data.Shipments.FirstOrDefault(x=>x.Id==id);if(shipment==null)throw new InvalidOperationException("请先选择一条物流记录。");return shipment;}
        void BagsPage()
        {
            var panel=Surface();var toolbar=Ui.Toolbar();toolbar.Controls.Add(Ui.Button("建立邮袋",CreateBag,true));toolbar.Controls.Add(Ui.Button("装入卡片",()=>SelectBagCards(RequireBag()),false));toolbar.Controls.Add(Ui.Button("封袋 / 交接",()=>BagAction(RequireBag()),false));toolbar.Controls.Add(Ui.Button("绑定物流",()=>EditShipment(null,RequireBag().Id,""),false));toolbar.Controls.Add(Ui.Button("打印随袋单",()=>OpenPrint("manifest",RequireBag(),null,null),false));toolbar.Controls.Add(Ui.Button("重开",ReopenBag,false));panel.Controls.Add(toolbar);
            var split=new SurfaceTablePanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Color.Transparent};split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));split.RowStyles.Add(new RowStyle(SizeType.Percent,52));split.RowStyles.Add(new RowStyle(SizeType.Percent,48));panel.Controls.Add(split);split.BringToFront();bagsGrid=Ui.Grid();Ui.Columns(bagsGrid,"id","总封编号 / S8","封发台 → 寄达台","状态","张数","重量 kg");bagsGrid.Columns[1].FillWeight=180;bagsGrid.Columns[2].FillWeight=150;
            foreach(var b in repo.Data.Bags.AsEnumerable().Reverse().Where(b=>bagFilter.Length==0||b.Id==bagFilter||b.DispatchId==bagFilter))bagsGrid.Rows.Add(b.Id,b.DispatchId,b.From+" → "+b.To,b.State,b.CardIds.Count,b.WeightKg);split.Controls.Add(bagsGrid,0,0);bagFilter="";
            var lower=new SurfacePanel{Dock=DockStyle.Fill,Padding=new Padding(0,12,0,0),BackColor=Color.Transparent};var title=Ui.Label("袋内卡片",11,true);title.Dock=DockStyle.Top;title.Height=34;var contents=Ui.Grid();Ui.Columns(contents,"id","永久卡号","原寄呼号","寄达呼号","通联UTC","模式");var line=Ui.Toolbar();line.Dock=DockStyle.Bottom;line.Height=48;line.Controls.Add(Ui.Button("移出所选卡片",()=>{var b=RequireBag();string id=Ui.Selected(contents);if(id.Length==0)throw new InvalidOperationException("请选择袋内卡片。");repo.RemoveFromBag(b.Id,id);Navigate(page);},false));line.Controls.Add(Ui.Button("查看袋历史",()=>ShowHistory(RequireBag().Id),false));line.Controls.Add(Ui.Button("确认接收",()=>ReceiveBag(RequireBag()),false));lower.Controls.Add(contents);lower.Controls.Add(title);lower.Controls.Add(line);split.Controls.Add(lower,0,1);
            Ui.FitColumns(contents,"QSL-00000000-000000","DEMO/A","DEMO/B","0000-00-00 00:00","SSB");
            Action showBag=()=>{contents.Rows.Clear();var b=repo.Data.Bags.FirstOrDefault(x=>x.Id==Ui.Selected(bagsGrid));title.Text="袋内卡片";if(b==null)return;title.Text="袋内卡片 · "+b.CardIds.Count+" 张   /   "+b.Id;foreach(var c in repo.Data.Cards.Where(c=>b.CardIds.Contains(c.Id)))contents.Rows.Add(c.Id,c.Id,c.FromCall,c.ToCall,c.QsoUtc,c.Mode);};bagsGrid.CurrentCellChanged+=(s,e)=>showBag();if(bagsGrid.Rows.Count>0){bagsGrid.CurrentCell=bagsGrid.Rows[0].Cells[1];bagsGrid.ClearSelection();bagsGrid.Rows[0].Selected=true;}showBag();
        }
        void TrackingPage()
        {
            var panel=Surface();var bar=Ui.Toolbar();
            bar.Controls.Add(Ui.Button("绑定物流",()=>EditShipment(null,"",""),false));
            bar.Controls.Add(Ui.Button("免费查询",OpenTrackingWeb,true));
            bar.Controls.Add(Ui.Button("保存进度",()=>RecordProgress(RequireShipment()),false));
            bar.Controls.Add(Ui.Button("修改运单",()=>EditShipment(RequireShipment(),"",""),false));
            panel.Controls.Add(bar);
            var split=new SurfaceTablePanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Color.Transparent};split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));split.RowStyles.Add(new RowStyle(SizeType.Percent,45));split.RowStyles.Add(new RowStyle(SizeType.Percent,55));panel.Controls.Add(split);split.BringToFront();shipGrid=Ui.Grid();Ui.Columns(shipGrid,"id","物流单号","承运商 / 分段","关联对象","物流状态","最后更新");shipGrid.Columns[1].FillWeight=125;shipGrid.Columns[3].FillWeight=160;
            foreach(var s in repo.Data.Shipments.AsEnumerable().Reverse())shipGrid.Rows.Add(s.Id,s.Number,s.CarrierName+" / "+s.Leg,s.CardId.Length>0?s.CardId:s.BagId,s.Status,Clock.Display(s.UpdatedUtc));split.Controls.Add(shipGrid,0,0);
            var wrap=new SurfacePanel{Dock=DockStyle.Fill,Padding=new Padding(0,14,0,0),BackColor=Color.Transparent};var h=Ui.Label("物流进度",11,true);h.Dock=DockStyle.Top;h.Height=35;var events=Ui.Grid();Ui.Columns(events,"id","时间","地点","进度","来源");events.Columns[3].FillWeight=210;wrap.Controls.Add(events);wrap.Controls.Add(h);split.Controls.Add(wrap,0,1);
            Action show=()=>{events.Rows.Clear();var s=repo.Data.Shipments.FirstOrDefault(x=>x.Id==Ui.Selected(shipGrid));if(s==null){h.Text="物流进度";return;}int n=0;foreach(var ev in s.Events)events.Rows.Add((++n).ToString(),Clock.Display(ev.Time),ev.Location,ev.Description,ev.Provider);h.Text=s.Events.Count==0?"暂无已保存的进度":"物流进度（"+s.Events.Count+" 条）";};shipGrid.CurrentCellChanged+=(s,e)=>show();show();
        }
        void PrintPage()
        {
            var panel=Surface();
            var grid=Ui.Grid();Ui.Columns(grid,"id","单据模板","类别","成品尺寸","用途与尺寸依据");
            grid.Columns[1].FillWeight=190;grid.Columns[2].FillWeight=82;grid.Columns[3].FillWeight=94;grid.Columns[4].FillWeight=244;
            Action open=()=>{string key=Ui.Selected(grid);if(key.Length>0)OpenPrint(key,null,null,null);};
            var bar=Ui.Toolbar();
            var openStudio=Ui.Button("打开打印工作室",open,true);
            bar.Controls.Add(openStudio);
            bar.Controls.Add(Ui.Button("打印校准页",()=>{using(var f=new PrintStudio(repo,new DocumentContext{Db=repo.Data},"calibration"))f.ShowDialog(this);},false));
            var family=Ui.Combo(new[]{"全部单据","附件与辅助单据","UPU 函件 CN","UPU 包裹 CP"});family.Name="templateFamily";family.Width=156;family.Margin=new Padding(0,3,8,0);family.AccessibleName="表单分类";bar.Controls.Add(family);
            var hint=Ui.Note("搜索模板");hint.Width=67;hint.Height=36;hint.TextAlign=ContentAlignment.MiddleLeft;bar.Controls.Add(hint);
            var find=Ui.Input("");find.Name="templateSearch";find.Width=188;find.Margin=new Padding(0,4,8,0);find.AccessibleName="按编号或名称查找单据";bar.Controls.Add(find);
            panel.Controls.Add(bar);
            var note=Ui.Note("");note.Dock=DockStyle.Top;note.Height=30;panel.Controls.Add(note);note.BringToFront();
            panel.Controls.Add(grid);grid.BringToFront();
            Action refresh=()=>{
                string term=find.Text.Trim().Replace(" ","");
                grid.Rows.Clear();
                foreach(var t in Documents.Templates)
                {
                    if((family.SelectedIndex==1&&t is UpuTemplate)||(family.SelectedIndex>1&&family.Text!=t.Group))continue;
                    string match=t.Title+" "+t.Key+" "+t.Description;
                    if(match.Replace(" ","").IndexOf(term,StringComparison.OrdinalIgnoreCase)<0)continue;
                    grid.Rows.Add(t.Key,t.Title,t.Group,t.SizeText,t.Description);
                }
                openStudio.Enabled=grid.Rows.Count>0;
                note.Text=grid.Rows.Count+" / "+Documents.Templates.Count+" 种单据";
            };
            family.SelectedIndexChanged+=(s,e)=>refresh();find.TextChanged+=(s,e)=>refresh();refresh();
            grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)open();};
            find.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter&&grid.Rows.Count>0){e.SuppressKeyPress=true;open();}};
        }
        void CasesPage()
        {
            var p=Surface();var bar=Ui.Toolbar();bar.Controls.Add(Ui.Button("新建查验单",()=>EditCase(null),true));bar.Controls.Add(Ui.Button("填写 / 答复",()=>EditCase(SelectedCase()),false));bar.Controls.Add(Ui.Button("生成文书",()=>{var c=SelectedCase();OpenPrint(CaseTemplate(c),FindBag(c.BagId),FindCard(c.CardId),c);},false));bar.Controls.Add(Ui.Button("查看历史",()=>ShowHistory(SelectedCase().Id),false));p.Controls.Add(bar);caseGrid=Ui.Grid();Ui.Columns(caseGrid,"id","文书编号","类型","发单台 → 寄达台","主题","处理状态");caseGrid.Columns[4].FillWeight=180;foreach(var c in repo.Data.Cases.AsEnumerable().Reverse())caseGrid.Rows.Add(c.Id,c.Id,c.Type,c.From+" → "+c.To,c.Subject,c.State);p.Controls.Add(caseGrid);caseGrid.BringToFront();
        }
        CaseRecord SelectedCase(){var c=repo.Data.Cases.FirstOrDefault(x=>x.Id==Ui.Selected(caseGrid));if(c==null)throw new InvalidOperationException("请先选择一份查验单。");return c;}
        Card FindCard(string id){return repo.Data.Cards.FirstOrDefault(c=>c.Id==id);}Bag FindBag(string id){return repo.Data.Bags.FirstOrDefault(c=>c.Id==id);}
        string CaseTemplate(CaseRecord c){switch(c.Type){case "复验":return "reply";case "国内查单":return "domquery";case "国内回单":return "domreply";case "国际查单":return "intlquery";case "国际答复":return "intlreply";case "未收声明":return "rd10";default:return "verify";}}
        void ContactsPage()
        {
            var p=Surface();var bar=Ui.Toolbar();
            bar.Controls.Add(Ui.Button("新增台站",()=>EditContact(null),true));
            bar.Controls.Add(Ui.Button("修改名址",()=>{
                var contact=repo.Data.Contacts.FirstOrDefault(x=>x.Id==Ui.Selected(contactGrid));
                if(contact==null)throw new InvalidOperationException("请先选择台站。");EditContact(contact);
            },false));
            if(demo&&DemoData.HasAddressOptions(repo.Data))bar.Controls.Add(Ui.DemoAddressLanguage(repo,RefreshContacts));
            p.Controls.Add(bar);
            contactGrid=Ui.Grid();Ui.Columns(contactGrid,"id","呼号 / 卡片局","名称","国家地区","名址","邮编");
            contactGrid.Columns[4].FillWeight=250;
            RefreshContacts();p.Controls.Add(contactGrid);contactGrid.BringToFront();
        }
        void RefreshContacts()
        {
            string selected=Ui.Selected(contactGrid);contactGrid.Rows.Clear();
            foreach(var contact in repo.Data.Contacts)
            {
                bool english=demo&&DemoData.DisplayEnglish(repo.Data,contact);
                string name=english&&contact.EnglishName.Length>0?contact.EnglishName:contact.Name;
                contactGrid.Rows.Add(contact.Id,contact.Callsign,name,english&&contact.Country=="中国"?"China":contact.Country,
                    english?contact.EnglishAddress:contact.Address,contact.PostalCode);
            }
            SelectRow(contactGrid,selected);
        }
        void SettingsPage()
        {
            var p=Surface();
            var bar=Ui.Toolbar();
            bar.Controls.Add(Ui.Button("台站资料",EditSettings,false));
            bar.Controls.Add(Ui.Button("更改目录",EditDirectories,true));
            bar.Controls.Add(Ui.Button("备份",Backup,false));
            bar.Controls.Add(Ui.Button("恢复备份",Restore,false));
            bar.Controls.Add(Ui.Button("打开资料目录",()=>Process.Start(new ProcessStartInfo("explorer.exe","\""+repo.Folder+"\""){UseShellExecute=true}),false));
            bar.Controls.Add(Ui.Button("操作记录",()=>ShowHistory(null),false));
            p.Controls.Add(bar);
            var scroll=new SurfacePanel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Color.Transparent};
            var content=new SurfaceTablePanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,BackColor=Color.Transparent,Padding=new Padding(0,10,8,10)};
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            AddDirectoryRow(content,0,"资料目录",locations.DataDirectory,"dataDirectory");
            AddDirectoryRow(content,2,"查询缓存目录",locations.CacheDirectory,"cacheDirectory");
            var settings=repo.Data.Settings;
            var station=Ui.Label(settings.Organization+"    "+settings.Callsign+"\n经办人  "+settings.Operator+"    节点代码  "+settings.OriginCode,10,false);
            station.Dock=DockStyle.Top;station.Height=70;station.Margin=new Padding(0,25,0,0);content.Controls.Add(station,0,4);
            var help=Ui.Button("使用手册",ShowManual,false);
            content.Controls.Add(help,0,5);
            scroll.Controls.Add(content);p.Controls.Add(scroll);scroll.BringToFront();
        }
        void ShowManual(){using(var manual=new ManualDialog())manual.ShowDialog(this);}
        void AddDirectoryRow(TableLayoutPanel table,int row,string label,string path,string name)
        {
            var title=Ui.Label(label,11,true);title.Height=30;title.Dock=DockStyle.Top;table.Controls.Add(title,0,row);
            var field=Ui.Input(path);field.ReadOnly=true;field.Name=name;field.AccessibleName=label;field.Dock=DockStyle.Top;field.Margin=new Padding(0,0,0,15);
            table.Controls.Add(field,0,row+1);
        }
        void EditDirectories()
        {
            using(var dialog=new DirectorySettings(repo,locations))
                if(dialog.ShowDialog(this)==DialogResult.OK){Navigate(page);status.Text="目录已保存。";}
        }
        void OpenPrint(string template,Bag bag,Card card,CaseRecord record){using(var f=new PrintStudio(repo,new DocumentContext{Db=repo.Data,Bag=bag,Card=card,Case=record},template,demo))f.ShowDialog(this);}
        void FindRecord(string term)
        {
            if(term.Length==0)return;Card c=repo.Data.Cards.FirstOrDefault(x=>x.Id.Equals(term,StringComparison.OrdinalIgnoreCase));if(c!=null){ShowTrace(c.Id);return;}
            var b=repo.Data.Bags.FirstOrDefault(x=>x.Id==term||x.DispatchId==term);if(b!=null){bagFilter=b.Id;Navigate("封发作业");return;}
            var s=repo.Data.Shipments.FirstOrDefault(x=>x.Number.Equals(term,StringComparison.OrdinalIgnoreCase));if(s!=null){Navigate("物流追踪");SelectRow(shipGrid,s.Id);return;}cardFilter=term;Navigate("卡片台账");
        }
        void SelectRow(DataGridView g,string id){foreach(DataGridViewRow row in g.Rows)if(Convert.ToString(row.Cells[0].Value)==id){g.ClearSelection();g.CurrentCell=row.Cells[1];row.Selected=true;g.FirstDisplayedScrollingRowIndex=row.Index;break;}}
        public void CaptureScreens(string directory)
        {
            Directory.CreateDirectory(directory);foreach(string name in new[]{"总览","卡片台账","封发作业","物流追踪","单据中心","查验与查单","设置与备份"}){Navigate(name);UiSnapshot.Save(this,Path.Combine(directory,name+".png"));}
        }
    }
}




