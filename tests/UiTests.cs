using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QslPost;
class UiTests
{
    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);

    static int checks;
    static void Assert(bool condition,string message){checks++;if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static Control Find(Control root,string name){return root.Controls.Find(name,true).First();}
    static Button Button(Control root,string text){foreach(Control child in root.Controls){if(child is Button&&child.Text==text)return (Button)child;try{return Button(child,text);}catch(InvalidOperationException){}}throw new InvalidOperationException(text);}
    static System.Collections.Generic.IEnumerable<DataGridView> Grids(Control root){foreach(Control child in root.Controls){if(child is DataGridView)yield return (DataGridView)child;foreach(var g in Grids(child))yield return g;}}
    static void Capture(Form f,string path){UiSnapshot.Save(f,path);}
    static bool Fits(Control control,Control parent)
    {
        return parent.RectangleToScreen(parent.ClientRectangle).Contains(control.RectangleToScreen(control.ClientRectangle));
    }
    static Exception callbackError;
    static void Modal(Action<Form> action)
    {
        var timer=new Timer{Interval=250};timer.Tick+=(s,e)=>{timer.Stop();try{var f=Application.OpenForms.Cast<Form>().Last();action(f);}catch(Exception ex){callbackError=ex;foreach(Form f in Application.OpenForms.Cast<Form>().Reverse().SkipLastCompat(1).ToArray())f.Close();}finally{timer.Dispose();}};timer.Start();
    }
    static void CheckWindowSize(MainForm main,string output)
    {
        var area=Screen.FromControl(main).WorkingArea;
        if(area.Width<1720||area.Height<1050)return;
        main.Size=new Size(1060,740);main.Navigate("卡片台账");
        var ledger=Grids(main).Single();var register=Button(main,"登记卡片");
        int rowSize=ledger.Rows[0].Height,buttonSize=register.Height;
        float textSize=ledger.Font.Size,buttonText=register.Font.Size,headingSize=ledger.ColumnHeadersDefaultCellStyle.Font.Size;
        main.WindowState=FormWindowState.Maximized;
        Assert(ledger.Font.Size>=textSize*1.24f&&ledger.Rows[0].Height>=rowSize*1.22f&&register.Height>=buttonSize*1.22f,
            "maximizing updates button sizes, table typography and row heights before the next paint");
        float expectedHeader=headingSize*ledger.Font.Size/textSize;
        Assert(Math.Abs(ledger.ColumnHeadersDefaultCellStyle.Font.Size-expectedHeader)<.3f&&Fits(Button(main,"完整溯源"),main),
            "headers scale once and the bottom actions remain on screen");
        Capture(main,Path.Combine(output,"最大化卡片台账.png"));
        main.Navigate("总览");main.Navigate("卡片台账");
        Assert(Grids(main).Single().Font.Size>=textSize*1.24f&&Fits(Button(main,"完整溯源"),main),"pages opened while maximized use the larger layout");
        ledger=Grids(main).Single();
        main.WindowState=FormWindowState.Normal;
        ledger=Grids(main).Single();register=Button(main,"登记卡片");
        Assert(Math.Abs(ledger.Font.Size-textSize)<.2f&&Math.Abs(register.Font.Size-buttonText)<.2f&&
            Math.Abs(ledger.Rows[0].Height-rowSize)<=1&&Math.Abs(register.Height-buttonSize)<=1,"restoring the window returns to the compact layout");
    }
    static void CheckCompactLedger(MainForm main,string output)
    {
        main.Size=main.MinimumSize;
        main.Navigate("卡片台账");
        var navigation=(ScrollableControl)Find(main,"navigationViewport");
        var settings=Button(main,"设置与备份");
        Assert(Fits(settings,navigation),"the minimum main window shows the full settings navigation button");
        var search=Find(main,"cardSearch");
        var title=Find(main,"cardDetailTitle");
        search.Text="NO-SUCH-QSL-CARD";
        Assert(Grids(main).Single().Rows.Count==0&&title.Text=="未选中卡片"&&
            !Button(main,"完整溯源").Enabled&&!Button(main,"打印标签").Enabled,
            "an empty filter clears the previous card details and record actions");
        search.Text="";
        Assert(Grids(main).Single().Rows.Count==1&&title.Text.Contains("DEMO/B")&&Button(main,"完整溯源").Enabled,
            "clearing the filter restores the selected card details and actions");
        Capture(main,Path.Combine(output,"最小窗口.png"));
    }
    static void CheckCompactPrintSettings(Form studio)
    {
        studio.Size=studio.MinimumSize;
        studio.PerformLayout();
        var settings=(ScrollableControl)Find(studio,"printSettings");
        var template=Find(studio,"printTemplate");
        Assert(settings.VerticalScroll.Visible&&!settings.HorizontalScroll.Visible&&Fits(template,settings),
            "the print settings scrollbar leaves each template dropdown fully visible");
        var update=Button(studio,"更新预览");
        settings.ScrollControlIntoView(update);
        Assert(Fits(update,settings)&&Fits(Find(studio,"previewPageInfo"),studio),
            "scrolling print settings reveals the complete preview button and keeps page information visible");
        settings.AutoScrollPosition=Point.Empty;
    }
    static void CheckGridClipping()
    {
        using(var form=new Form{ClientSize=new Size(360,96),StartPosition=FormStartPosition.CenterScreen})
        {
            var grid=Ui.Grid();grid.Columns.Add("reference","卡片编号");
            grid.Rows.Add("测试卡片 1");grid.Rows.Add("测试卡片 2");grid.Rows.Add("测试卡片 3");
            var foreground=new System.Collections.Generic.Dictionary<int,bool>();
            grid.RowPrePaint+=(sender,e)=>foreground[e.RowIndex]=(e.PaintParts&DataGridViewPaintParts.ContentForeground)!=0;
            form.Controls.Add(grid);form.Show();grid.Refresh();
            int visible=grid.GetRowDisplayRectangle(1,true).Height;
            Assert(visible>0&&visible<grid.Rows[1].Height&&foreground[0]&&!foreground[1],
                "a partially visible grid row does not paint cropped text");
            grid.FirstDisplayedScrollingRowIndex=1;grid.Refresh();
            Assert(foreground[1]&&grid.GetRowDisplayRectangle(1,true).Height==grid.Rows[1].Height,
                "scrolling a grid row fully into view restores its text");

            for(int i=3;i<120;i++)grid.Rows.Add("测试卡片 "+(i+1));
            form.ClientSize=new Size(540,190);
            grid.Scale(new SizeF(1.5f,1.5f));
            grid.FirstDisplayedScrollingRowIndex=110;grid.Refresh();
            Assert(grid.Rows[110].Height==grid.RowTemplate.Height&&foreground[110],
                "offscreen rows use the new height when scrolled into view after scaling");
        }
    }
    static void CheckStartupGuide(MainForm main,Repository repo,string output)
    {
        Modal(f=>{
            Assert(repo.Data.Cards.Count==0&&repo.Data.Contacts.Count==0&&repo.Data.Bags.Count==0&&
                repo.Data.Settings.Organization==""&&repo.Data.Settings.Callsign==""&&repo.Data.Settings.OriginCode=="",
                "a fresh profile opens with an empty ledger and empty station information");
            Capture(f,Path.Combine(output,"首次使用.png"));
            ((CheckBox)Find(f,"hideStartupGuide")).Checked=true;
            Modal(manual=>{
                var contents=(ListBox)Find(manual,"manualChapter");
                Assert(contents.Items.Count>=8&&Find(manual,"manualText").Text.Contains("首次启动"),"startup guide opens the integrated manual");
                int example=contents.Items.IndexOf("名址填写示例");
                if(example>=0)
                {
                    contents.SelectedIndex=example;
                    var language=(ComboBox)Find(manual,"manualAddressLanguage");
                    string chinese=Find(manual,"manualText").Text;
                    language.SelectedIndex=1;
                    Assert(language.Visible&&Find(manual,"manualText").Text!=chinese,"the example switches languages in the manual");
                    contents.SelectedIndex=0;
                }
                Capture(manual,Path.Combine(output,"使用手册.png"));
                CheckManualLayout(manual,contents,output);
                manual.Close();
            });
            Button(f,"查看使用手册").PerformClick();
        });
        main.Show();Application.DoEvents();if(callbackError!=null)throw callbackError;
        Assert(repo.Data.Settings.HideStartupGuide&&!repo.Data.Settings.Initialized&&Grids(main).Single().Rows.Count==0,
            "dismissing startup help leaves all records blank and saves only the preference");
        main.Navigate("设置与备份");
        Modal(f=>{
            Assert(Find(f,"org").Text==""&&Find(f,"call").Text==""&&Find(f,"address").Text=="",
                "opening station settings does not fill in the manual example");
            ((ComboBox)Find(f,"profile")).SelectedItem="个人台站";
            Find(f,"org").Text="UI 测试台站";Find(f,"call").Text="DEMO/A";Find(f,"origin").Text="QSLAAA";
            Button(f,"保存").PerformClick();
        });
        Button(main,"台站资料").PerformClick();if(callbackError!=null)throw callbackError;
        Assert(repo.Data.Settings.Initialized&&repo.Data.Settings.Profile=="个人台站","the user can register a profile from settings");
    }

    static void CheckManualLayout(Form manual,ListBox contents,string output)
    {
        float fontSize=contents.Font.Size;
        int rowHeight=contents.ItemHeight;
        int longest=contents.Items.Cast<string>().Max(title=>TextRenderer.MeasureText(title,contents.Font).Width);
        Assert(fontSize>=20&&rowHeight>=44&&longest+28<=contents.ClientSize.Width,
            "the manual directory shows every chapter title with the larger font and row spacing");

        const int lbSetCurSel=0x0186, reflectedCommand=0x2111, lbnSelChange=1;
        SendMessage(contents.Handle,lbSetCurSel,new IntPtr(1),IntPtr.Zero);
        SendMessage(contents.Handle,reflectedCommand,new IntPtr(lbnSelChange<<16),contents.Handle);
        string chapter=Find(manual,"manualText").Text;
        var area=Screen.FromControl(manual).WorkingArea;
        var maximize=(Button)Find(manual,"manualMaximize");
        maximize.Focus();
        maximize.PerformClick();
        Assert(manual.WindowState==FormWindowState.Maximized&&maximize.AccessibleName=="还原"&&
            area.Contains(manual.RectangleToScreen(manual.ClientRectangle))&&Fits(maximize,manual),
            "maximizing the manual keeps its caption controls within the desktop work area");
        Assert(contents.SelectedIndex==1&&Find(manual,"manualText").Text==chapter,
            "maximizing retains the chapter selected through the native list box");
        if(area.Width>=1720&&area.Height>=1050)
            Assert(contents.Font.Size>=fontSize*1.24f&&contents.ItemHeight>=rowHeight*1.22f,
                "the manual scales its directory during maximize");
        Capture(manual,Path.Combine(output,"使用手册最大化.png"));

        maximize.PerformClick();
        Assert(manual.WindowState==FormWindowState.Normal&&maximize.AccessibleName=="最大化"&&
            Math.Abs(contents.Font.Size-fontSize)<.2f&&Math.Abs(contents.ItemHeight-rowHeight)<=1&&
            contents.SelectedIndex==1&&Find(manual,"manualText").Text==chapter,
            "restoring the manual returns to the normal scale and retains the selected chapter");
    }

    static void CheckAddressLanguage(string output)
    {
        string data=Path.Combine(output,"address-demo");
        const string primary="北京市测试信箱",english="P.O. BOX 100023-00, 100023 BEIJING, P.R. CHINA";
        using(var repo=new Repository(data))
        {
            DemoData.Create(repo,new Contact{Callsign="DEMO1",Name="演示收件人",EnglishName="Demo Recipient",Address=primary,EnglishAddress=english,PostalCode="100023",Country="中国"});
            using(var main=new MainForm(repo,true))
            {
                main.Show();main.Navigate("台站名址");
                var grid=Grids(main).Single();var choice=(ComboBox)Find(main,"demoAddressLanguage");
                Assert(choice.SelectedIndex==0&&Equals(grid.Rows[0].Cells[4].Value,primary),"personal demo initially shows the Chinese address alone");
                choice.SelectedIndex=1;
                Assert(Equals(grid.Rows[0].Cells[4].Value,english)&&Equals(grid.Rows[0].Cells[2].Value,"Demo Recipient"),"the language picker replaces the example name and address");
                Modal(f=>{
                    Assert(Find(f,"address").Text==english,"contact details follow the selected language");f.Close();
                });
                Button(main,"修改名址").PerformClick();if(callbackError!=null)throw callbackError;
                Capture(main,Path.Combine(output,"演示英文地址.png"));
                var context=new DocumentContext{Db=repo.Data,Case=repo.Data.Cases[0],Card=repo.Data.Cards[0]};
                using(var studio=new PrintStudio(repo,context,"domquery",true))
                {
                    Modal(f=>{
                        var language=(ComboBox)Find(f,"demoAddressLanguage");
                        Assert(language.SelectedIndex==1&&context.RecipientAddress==english,"print studio starts with the selected English address");
                        language.SelectedIndex=0;
                        Assert(context.RecipientAddress==primary,"print studio replaces the address when switching back to Chinese");
                        language.SelectedIndex=1;
                        Capture(f,Path.Combine(output,"打印英文地址.png"));
                        var area=Screen.FromControl(f).WorkingArea;
                        if(area.Width>=1720&&area.Height>=1050)
                        {
                            var zoom=Find(f,"previewZoom");float font=zoom.Font.Size;
                            f.WindowState=FormWindowState.Maximized;
                            Assert(zoom.Font.Size>=font*1.24f&&Fits(zoom,f)&&Fits(Button(f,"导出 PDF"),f),"the maximized print window keeps its preview and actions accessible");
                            Capture(f,Path.Combine(output,"最大化打印工作室.png"));
                        }
                        f.Close();
                    });
                    studio.ShowDialog(main);if(callbackError!=null)throw callbackError;
                }
                main.Close();
            }
        }
        using(var reopened=new Repository(data))
            Assert(reopened.Data.Settings.EnglishDemoAddress&&reopened.Data.Bags[0].SealedCards[0].RecipientAddress==primary,
                "the language preference survives reopening while the sealed record keeps its original address");
    }
    [STAThread] static void Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);string output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
        using(var locations=StorageLocations.Load(Path.Combine(output,"configuration")))
        using(var repo=new Repository(locations.DataDirectory))using(var main=new MainForm(repo,false,locations))
        {
            CheckStartupGuide(main,repo,output);main.Navigate("卡片台账");Capture(main,Path.Combine(output,"空台账.png"));
            string[] qsoFields={"utc","freq","mode","rs","rr"};
            Modal(f=>{
                Assert(qsoFields.All(field=>Find(f,field).Text=="")&&Find(f,"to").Text=="",
                    "new cards leave contact details blank");
                Assert(Find(f,"from").Text=="DEMO/A","the sender uses the configured station call sign");
                Capture(f,Path.Combine(output,"登记卡片.png"));
                Find(f,"to").Text="DEMO/B";
                Button(f,"保存").PerformClick();
            });
            Button(main,"登记卡片").PerformClick();if(callbackError!=null)throw callbackError;
            var card=repo.Data.Cards.Single();
            Assert(card.ToCall=="DEMO/B"&&card.QsoUtc==""&&card.Frequency==""&&card.Mode==""&&card.RstSent==""&&card.RstReceived=="",
                "card registration saves omitted contact details as empty values");
            Modal(f=>{
                Assert(qsoFields.All(field=>Find(f,field).Text==""),"editing keeps omitted contact details blank");
                Find(f,"utc").Text="2026-11-18 10:30";
                Find(f,"freq").Text="7.074";
                ((ComboBox)Find(f,"mode")).SelectedItem="FT8";
                Find(f,"rs").Text="-10";
                Find(f,"rr").Text="-14";
                Button(f,"保存").PerformClick();
            });
            Button(main,"修改").PerformClick();if(callbackError!=null)throw callbackError;
            Modal(f=>{
                Assert(qsoFields.Select(field=>Find(f,field).Text).SequenceEqual(new[]{"2026-11-18 10:30","7.074","FT8","-10","-14"}),
                    "editing shows the contact details entered and saved by the operator");
                f.Close();
            });
            Button(main,"修改").PerformClick();if(callbackError!=null)throw callbackError;
            main.Navigate("封发作业");
            Modal(f=>{
                Assert(Find(f,"destination").Text==""&&Find(f,"weight").Text=="","new bags leave the destination code and weight blank");
                Find(f,"to").Text="DEMO Bureau";Find(f,"destination").Text="QSLBBB";Find(f,"weight").Text="0.6";
                Capture(f,Path.Combine(output,"建立邮袋.png"));
                Button(f,"保存").PerformClick();
            });
            Button(main,"建立邮袋").PerformClick();if(callbackError!=null)throw callbackError;
            var enteredBag=repo.Data.Bags.Single();
            Assert(enteredBag.DestinationCode=="QSLBBB"&&enteredBag.WeightKg==0.6m,"bag creation saves the entered destination and weight");
            Modal(f=>{var grid=f.Controls.OfType<DataGridView>().First();grid.Rows[0].Cells[0].Value=true;Button(f,"装入所选").PerformClick();});Button(main,"装入卡片").PerformClick();if(callbackError!=null)throw callbackError;Assert(repo.Data.Bags[0].CardIds.Count==1,"bag selection dialog commits checked cards");
            Assert(Grids(main).All(g=>g.Rows.Count==1),"initial bag details display each member exactly once");main.Navigate("封发作业");Assert(Grids(main).All(g=>g.Rows.Count==1),"bag details refresh does not append duplicates");
            string bagId=repo.Data.Bags[0].Id;repo.Transition(bagId,"已封袋","UI 核对");main.Navigate("封发作业");
            Modal(f=>{Find(f,"note").Text="演示接收人";Button(f,"保存").PerformClick();});Button(main,"封袋 / 交接").PerformClick();if(callbackError!=null)throw callbackError;Assert(repo.Data.Bags[0].State=="已交接","sealed bag opens the handover dialog through the main action");
            Modal(f=>{Find(f,"count").Text="1";Find(f,"note").Text="实际核对一张";Button(f,"保存").PerformClick();});Button(main,"封袋 / 交接").PerformClick();if(callbackError!=null)throw callbackError;Assert(repo.Data.Bags[0].State=="已接收","handed-over bag opens the physical receipt dialog through the main action");
            main.Navigate("卡片台账");Modal(f=>{Capture(f,Path.Combine(output,"标签打印工作室.png"));f.Close();});Button(main,"打印标签").PerformClick();if(callbackError!=null)throw callbackError;Assert(repo.Data.Settings.PrintProfiles.Any(x=>x.Template=="cardlabel"),"print studio previews and saves selected-card profile");
            main.Navigate("单据中心");
            ((ComboBox)Find(main,"templateFamily")).SelectedItem="UPU 包裹 CP";Find(main,"templateSearch").Text="CP85";Application.DoEvents();
            Assert(Grids(main).Single().Rows.Count==1&&Convert.ToString(Grids(main).Single().Rows[0].Cells[0].Value)=="cp85large","catalog searches CP85 without spaces and applies the CP family filter");
            Capture(main,Path.Combine(output,"UPU模板搜索.png"));
            Modal(f=>{
                Assert(((ComboBox)Find(f,"printStock")).SelectedIndex==(int)PrintStock.Original&&Find(f,"templateSize").Text.Contains("130 × 90"),"official CP85 opens at the original 130 by 90 mm");
                CheckCompactPrintSettings(f);
                Modal(d=>{Find(d,"Bag").Text=repo.Data.Bags[0].Id;Button(d,"保存").PerformClick();});
                Button(f,"填写单据").PerformClick();
                Capture(f,Path.Combine(output,"原规格CP85.png"));
                var canvas=(ScrollableControl)Find(f,"pagePreview");
                ((ComboBox)Find(f,"previewZoom")).SelectedIndex=1;
                Application.DoEvents();
                Assert(canvas.AutoScrollMinSize.Width>canvas.ClientSize.Width,"preview zoom creates a scrollable enlarged page");
                using(var painted=new Bitmap(canvas.Width,canvas.Height))
                {
                    canvas.DrawToBitmap(painted,new Rectangle(Point.Empty,canvas.Size));
                    var bottomRight=painted.GetPixel(canvas.ClientSize.Width-5,canvas.ClientSize.Height-5);
                    Assert(bottomRight.ToArgb()!=canvas.BackColor.ToArgb(),"zoom draws the enlarged paper into the visible corner");
                }
                Capture(f,Path.Combine(output,"CP85放大.png"));f.Close();
            });
            Button(main,"打开打印工作室").PerformClick();
            if(callbackError!=null)throw callbackError;
            Assert(repo.Data.Settings.PrintProfiles.Any(p=>p.Template=="cp85large"&&p.Stock==PrintStock.Original),"official CP85 template-specific editor returns to the native-size preview");
            CheckCompactLedger(main,output);
            main.Navigate("设置与备份");string data=Path.Combine(output,"新资料"),cache=Path.Combine(output,"新查询缓存");
            Modal(f=>{Find(f,"newDataDirectory").Text=data;Find(f,"newCacheDirectory").Text=cache;Button(f,"保存").PerformClick();});
            Button(main,"更改目录").PerformClick();if(callbackError!=null)throw callbackError;
            Assert(repo.Folder==data&&Find(main,"cacheDirectory").Text==cache,"settings moves the database and uses the selected cache folder");
            Capture(main,Path.Combine(output,"目录设置.png"));
            repo.AddShipment(new Shipment{Number="RR123456789CN",CardId=repo.Data.Cards[0].Id});
            main.Navigate("物流追踪");
            Assert(Button(main,"免费查询").Enabled,"the shipment toolbar offers public tracking");
            Modal(f=>{Find(f,"location").Text="HK";Find(f,"description").Text="已发往寄达局";Button(f,"保存").PerformClick();});
            Button(main,"保存进度").PerformClick();if(callbackError!=null)throw callbackError;
            Assert(repo.Data.Shipments.Single().Events.Single().Description=="已发往寄达局","logistics progress can be saved without an API credential");
            CheckWindowSize(main,output);
            main.Close();
        }
        CheckAddressLanguage(output);
        CheckGridClipping();
        File.WriteAllText(Path.Combine(output,"ui-results.txt"),checks+" / "+checks+" GUI interaction checks passed.");
    }
}
static class Compatibility
{
    public static System.Collections.Generic.IEnumerable<T> SkipLastCompat<T>(this System.Collections.Generic.IEnumerable<T> source,int n){var list=source.ToList();return list.Take(Math.Max(0,list.Count-n));}
}
