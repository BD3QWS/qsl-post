using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using QslPost;

class DomainTests
{
    static int checks;
    static void Assert(bool condition,string name){checks++;if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS "+name);}
    static void Reject(Action action,string name){bool rejected=false;try{action();}catch(Exception){rejected=true;}Assert(rejected,name);}
    static Card New(string target){return new Card{FromCall="DEMO/A",ToCall=target,QsoUtc="2026-11-18 10:00",Frequency="14.250",Notes="测试资料"};}
    [STAThread]
    static int Main(string[] args)
    {
        try { Run(args); return 0; }
        catch(Exception ex) { Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message); return 1; }
    }
    static void Run(string[] args)
    {
        string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);string firstId,bagId,shipId;
        using(var repo=new Repository(Path.Combine(root,"database")))
        {
            firstId=repo.AddCard(New("DEMO/B"));string other=repo.AddCard(New("DEMO/C"));Assert(firstId!=other,"per-card permanent unique IDs");Reject(()=>new Repository(repo.Folder),"workspace rejects concurrent writers");
            int before=repo.Data.Cards.Count;Reject(()=>repo.AddCard(new Card()),"invalid card rejected");Assert(repo.Data.Cards.Count==before,"failed add leaves database unchanged");
            var bag=repo.CreateBag("DEMO/A","DEMO/B","QSLAAA","QSLBBB","SAL","挂信","出口",1.21m,"DEMO-SEAL","","测试");bagId=bag.Id;Assert(bag.DispatchId.Length==20&&bag.Id.Length==29,"S8 and S9 lengths");Assert(bag.Id.EndsWith("0013"),"S9 weight rounds up to 0.1kg");
            Reject(()=>repo.AddToBag(bag.Id,new[]{firstId,"missing"}),"batch membership atomic on missing card");Assert(repo.Data.Cards.Single(x=>x.Id==firstId).BagId=="","rollback partial bag membership");repo.AddToBag(bag.Id,new[]{firstId,other});Reject(()=>repo.AddToBag(bag.Id,new[]{firstId}),"no duplicate bag membership");
            repo.Transition(bag.Id,"已封袋","test");Reject(()=>repo.RemoveFromBag(bag.Id,firstId),"sealed contents locked");var edited=repo.Clone(repo.Data).Cards.Single(x=>x.Id==firstId);edited.ToCall="DEMO/D";Reject(()=>repo.EditCard(edited),"sealed card edits rejected");Reject(()=>repo.Transition(bag.Id,"草稿",""),"reopen needs reason");repo.Transition(bag.Id,"草稿","测试重开");repo.RemoveFromBag(bag.Id,other);repo.AddToBag(bag.Id,new[]{other});repo.Transition(bag.Id,"已封袋","复核");
            Reject(()=>repo.Transition(bag.Id,"已接收",""),"custody cannot skip handover");Reject(()=>repo.Transition(bag.Id,"已交接",""),"handover requires custodian");repo.Transition(bag.Id,"已交接","DEMO 接收人");
            shipId=repo.AddShipment(new Shipment{Number="0000123456789",BagId=bag.Id,CarrierName="DEMO"});Assert(repo.CardShipments(repo.Data.Cards.First()).Single().Number.StartsWith("0000"),"tracking retains leading zeros and inherits from bag");Reject(()=>repo.AddShipment(new Shipment{Number="0000123456789",BagId=bag.Id}),"duplicate linked tracking rejected");
            Reject(()=>repo.ForwardCard(firstId,"转寄"),"forward requires physical receipt");repo.Transition(bag.Id,"已接收","实物核对");repo.ForwardCard(firstId,"转给下一卡片局");Assert(repo.Data.Cards.Single(x=>x.Id==firstId).BagId==""&&repo.Data.Bags.Single(x=>x.Id==bagId).CardIds.Contains(firstId),"forward preserves immutable old manifest");Assert(repo.CardShipments(repo.Data.Cards.Single(x=>x.Id==firstId)).Any(s=>s.Id==shipId),"forward preserves previous shipment legs");
            var forwarded=repo.Clone(repo.Data).Cards.Single(x=>x.Id==firstId);forwarded.ToCall="DEMO/NEW";repo.EditCard(forwarded);
            var oldManifest=new DocumentContext{Db=repo.Data,Bag=repo.Data.Bags.Single(x=>x.Id==bagId)};
            Assert(oldManifest.Cards.Single(x=>x.Id==firstId).ToCall=="DEMO/B","reprinted sealed manifest preserves original recipient after card edit");
            var next=repo.CreateBag("DEMO/B","DEMO/Z","QSLBBB","QSLCCC","航空","挂信","出口",.4m,"","","二段");repo.AddToBag(next.Id,new[]{firstId});Assert(repo.History(firstId).Any(x=>x.Action=="已交接"),"forwarded card traces earlier custody");
            Tracker.SaveProgress(repo,shipId,"2026-11-17T14:00:00Z","DEMO","Fixture pickup","已揽收");
            Tracker.SaveProgress(repo,shipId,"2026-11-18T09:00:00+08:00","DEMO","Fixture delivered","已妥投");
            Assert(repo.Data.Shipments.Single(s=>s.Id==shipId).Events.Count==2,"manually saved events form a complete timeline");
            Assert(repo.Data.Cards.Single(c=>c.Id==other).Status=="已接收","carrier delivered does not change physical custody");
            string snapshot=Repository.Serialize(repo.Data);
            Reject(()=>Tracker.SaveProgress(repo,shipId,"invalid","","Invalid event",""),"invalid event time is rejected");
            Assert(snapshot==Repository.Serialize(repo.Data),"invalid progress does not erase saved history");
            var db=repo.Clone(repo.Data);db.Audit[0].Details="edited externally";Reject(()=>Repository.Validate(db),"audit tampering detected");
            string backup=Path.Combine(root,"backup.xml");repo.Backup(backup);repo.AddCard(New("DEMO/EXTRA"));long nextCard=repo.Data.NextCard;
            var laterBag=repo.CreateBag("DEMO/A","DEMO/B","QSLAAA","QSLBBB","SAL","空袋","出口",.1m,"","","");
            Reject(()=>repo.AddToBag(laterBag.Id,new[]{repo.Data.Cards.Last().Id}),"return-of-empty-bags cannot accept cards");
            repo.Restore(backup);Assert(repo.Data.Cards.Count==2,"backup restore verified");Assert(Directory.GetFiles(Path.Combine(repo.Folder,"backups"),"before-restore-*").Length==1,"restore retains pre-restore snapshot");
            Assert(repo.Data.NextCard==nextCard,"restoring older data cannot recycle issued card numbers");
            var replacement=repo.CreateBag("DEMO/A","DEMO/B","QSLAAA","QSLBBB","SAL","空袋","出口",.1m,"","","");
            Assert(replacement.DispatchId!=laterBag.DispatchId,"restoring older data cannot recycle issued despatch codes");
            string csv=Path.Combine(root,"export.csv");Exchange.ExportCards(repo,csv,repo.Data.Cards);var imported=Exchange.ReadCsv(csv);Assert(imported.Count==2&&imported.Any(c=>c.ImportTracking.Contains("0000123456789")),"CSV roundtrip preserves tracking numbers");
            using(var target=new Repository(Path.Combine(root,"import"))){int skip;Assert(Exchange.Import(target,imported,out skip)==2,"CSV import creates independent cards");Assert(target.Data.Shipments.Count==2,"CSV tracking imported as searchable shipment links");Assert(Exchange.Import(target,imported,out skip)==0&&skip==2,"repeat import is idempotent");}
            using(var physical=new Repository(Path.Combine(root,"physical-import"))){int skip;var a=New("DEMO/SAME");a.Id="SOURCE-A";a.ImportFormat="CSV";var b=New("DEMO/SAME");b.Id="SOURCE-B";b.ImportFormat="CSV";Assert(Exchange.Import(physical,new List<Card>{a,b},out skip)==2,"distinct physical CSV cards with same QSO are retained");Assert(physical.Data.Cards.Select(c=>c.SourceId).Distinct().Count()==2,"original import card IDs remain traceable");}
            string adi=Path.Combine(root,"test.adi");File.WriteAllText(adi,"<ADIF_VER:5>3.1.4<EOH><CALL:6>DEMO/B<QSO_DATE:8>20261118<TIME_ON:6>103000<FREQ:6>14.250<MODE:3>SSB<EOR>");Assert(Exchange.ReadAdif(adi,"DEMO/A").Single().QsoUtc=="2026-11-18 10:30","ADIF UTC parsing");File.AppendAllText(adi,"<CALL:6>DEMO/C");Reject(()=>Exchange.ReadAdif(adi,"DEMO/A"),"ADIF missing EOR rejected atomically");
            Assert(Exchange.ParseCsv("a,b\r\n\"line\nnext\",\"a\"\"b\"\r\n")[1][1]=="a\"b","RFC4180 multiline and escaped quote CSV");Assert(Exchange.Cell("=1+2").Contains("'=1+2"),"CSV formula injection escaped");
            Assert(Tracker.WebUrl("0000123456789").EndsWith("0000123456789"),"17TRACK official website query retains number");Reject(()=>Tracker.WebUrl("<script>"),"invalid tracking URL input rejected");
            var fake=repo.Clone(repo.Data);fake.Cards.Clear();fake.Bags.Clear();var printBag=new Bag{Id=bagId,DispatchId=bagId.Substring(0,20),From="DEMO/A",To="DEMO/B",WeightKg=1.3m};fake.Bags.Add(printBag);for(int i=0;i<41;i++){var c=New("DEMO/B");c.Id="QSL-20261118-"+i.ToString("000000");c.BagId=bagId;fake.Cards.Add(c);printBag.CardIds.Add(c.Id);}var ctx=new DocumentContext{Db=fake,Bag=printBag};
            Assert(Documents.Build(Documents.Templates.Single(t=>t.Key=="manifest"),ctx).Count==2,"41-card manifest paginates at 40");Assert(Documents.Build(Documents.Templates.Single(t=>t.Key=="waybill"),ctx).Count==3,"41-card waybill paginates at 20");Assert(Documents.Build(Documents.Templates.Single(t=>t.Key=="cp85large"),ctx).Count==1,"one bag tag per bag regardless of card count");
            foreach(var t in Documents.Templates)
            {
                ctx.Card=t.Key=="cardlabel"||t is UpuTemplate&&((UpuTemplate)t).Layout=="item"?fake.Cards[0]:null;
                var blank=new DocumentContext{Db=fake};
                try
                {
                    foreach(var p in Documents.Build(t,ctx).Concat(Documents.Build(t,blank)))
                        using(var image=p.Render(96)){}
                }
                catch(Exception e){throw new Exception(t.Key+" "+e.Message);}
            }
            Assert(Documents.Templates.Select(t=>t.Key).Distinct().Count()==Documents.Templates.Count,"all template keys are unique; every template renders with blank and bound data");
            Assert(UpuForms.Catalog.Select(t=>t.Code).Distinct().Count()==86,"all 86 distinct Convention Manual 2026-09 CN/CP codes are represented");
            var blankForm=new DocumentContext{Db=fake,BlankSource=true};
            Assert(blankForm.From==""&&blankForm.Date==""&&blankForm.SenderAddress==""&&blankForm.Operator=="",
                "blank source leaves the issuing station, date and contact fields unfilled");
            foreach(var duplex in Documents.Templates.Where(t=>t.DoubleSided))
            {
                var sides=Documents.Build(duplex,blankForm);
                Assert(sides.Count==2&&sides[0].Side==PageSide.Front&&sides[1].Side==PageSide.Back&&
                    sides[0].Width==sides[1].Width&&sides[0].Height==sides[1].Height&&
                    Documents.FindTemplate(duplex.Following.Key)==duplex,"paired source and old reverse key resolve together: "+duplex.Key);
            }
            var inquiry=Documents.Build(Documents.FindTemplate("domquery"),blankForm);
            var onPaper=Documents.Compose(inquiry,new PrintProfile{Stock=PrintStock.A4,Margin=0,Scale=50},2);
            Assert(onPaper.Count==4&&onPaper.Select(p=>p.Side).SequenceEqual(new[]{PageSide.Front,PageSide.Back,PageSide.Front,PageSide.Back}),
                "two copies output the front and back on successive pages even when A4 could fit both");
            var small=Documents.Build(Documents.Templates[0],ctx);
            var official=Documents.Build(Documents.Templates.Single(t=>t.Key=="cp85large"),ctx);
            Assert(small[0].Width==68&&small[0].Height==46&&official[0].Width==130&&official[0].Height==90,"compact supplied and official CP85 sizes stay independent");
            var native=Documents.Compose(small.Concat(official).ToList(),new PrintProfile{Margin=8,Scale=50},2);
            Assert(native.Count==4&&native[0].Width==68&&native[0].Height==46&&native[1].Width==130&&native[1].Height==90&&native[2]==native[0],"default printing preserves native dimensions and repeats in order");
            var legacy=new System.Xml.Serialization.XmlSerializer(typeof(PrintProfile));
            using(var oldA4=new StringReader("<PrintProfile><Printer>Saved device</Printer><A4>true</A4></PrintProfile>"))
            using(var oldCustom=new StringReader("<PrintProfile><A4>false</A4><Width>68</Width><Height>46</Height></PrintProfile>"))
            {
                var migrated=(PrintProfile)legacy.Deserialize(oldA4);
                var custom=(PrintProfile)legacy.Deserialize(oldCustom);
                Assert(migrated.Stock==PrintStock.Original&&migrated.Printer=="Saved device"&&custom.Stock==PrintStock.Custom&&custom.Width==68,"v1 default A4 migrates to native; device and intentional custom stock are kept");
            }
            Assert(Documents.Compose(small,new PrintProfile{Stock=PrintStock.A4,Margin=8},8).Count==1,"A4 optional mode packs eight 68x46 tags");Reject(()=>Documents.Compose(small,new PrintProfile{Stock=PrintStock.Custom,Width=50,Height=30,Margin=0},1),"undersized label stock rejected before output");Reject(()=>Documents.Compose(small,new PrintProfile{Stock=PrintStock.A4,OffsetX=-20},1),"out-of-paper calibration offsets rejected");
            ctx.Card=fake.Cards[0];var barcodePages=Documents.Build(Documents.Templates.Single(t=>t.Key=="cardlabel"),ctx);Reject(()=>Documents.Compose(barcodePages,new PrintProfile{Stock=PrintStock.A4,Scale=20},1),"scaled barcode module width enforced in physical output");ctx.Fields["Reply"]=new String('长',3000);Assert(Documents.Build(Documents.Templates.Single(t=>t.Key=="reply"),ctx).Count>=4,"long manually filled reply keeps complete continuation pages");ctx.Fields.Clear();
            var report=UpuForms.Catalog.Single(t=>t.Key=="upu-cn03");
            ctx.Fields["DetailRows"]=String.Join("\n",Enumerable.Repeat("QSL-001|DEMO|1|0|1|测试",48).ToArray());
            var statement=Documents.Build(report,ctx);
            Assert(statement.Count>=2&&statement.All(p=>p.Width==210&&p.Height==297),"UPU manual detail rows produce native-size continuation sheets");
            foreach(var p in statement)using(var image=p.Render(96)){}
            ctx.Fields["DetailRows"]="1|2|3|4|5|6|7";Reject(()=>Documents.Build(report,ctx),"UPU detail entry rejects more values than the selected form's columns");ctx.Fields.Clear();
            PdfExport.Write(Path.Combine(root,"output.pdf"),native);Assert(new FileInfo(Path.Combine(root,"output.pdf")).Length>1000,"native mixed-size built-in PDF export writes complete artifact");
            Assert(Barcode128.Codes("AB123").Last()==106,"Code 128 checksum and stop generated");using(var image=new Bitmap(200,100))using(var g=Graphics.FromImage(image))Reject(()=>Barcode128.Draw(g,bagId,new RectangleF(0,0,10,10),true),"barcode minimum module width enforced");
        }
        using(var reopened=new Repository(Path.Combine(root,"database"))){Assert(reopened.Data.Cards.Any(c=>c.Id==firstId),"database persists across process sessions");Repository.Validate(reopened.Data);}
        string configuration=Path.Combine(root,"locations"),moved=Path.Combine(root,"moved"),cache=Path.Combine(root,"new-cache");
        using(var saved=StorageLocations.Load(configuration))
        using(var repo=new Repository(saved.DataDirectory))
        {
            string previous=repo.Folder;
            string id=repo.AddCard(New("MOVE/B"));
            string mail=repo.AddShipment(new Shipment{CardId=id,Number="RR123456789CN"});
            byte[] legacyFile={20,17,7,6,4};
            File.WriteAllBytes(Path.Combine(repo.Folder,"17track-token.dat"),legacyFile);
            repo.Change(db=>db.Shipments.Single().Events.Add(new TrackEvent{Time="2026-11-17T11:00:00+08:00",Location="Fixture",Description="Legacy pickup",Provider="17TRACK"}));
            Tracker.SaveProgress(repo,mail,"2026-11-18T12:00:00+08:00","Hong Kong","已离开处理中心","运输中");
            Tracker.SaveProgress(repo,mail,"2026-11-18T12:00:00+08:00","Hong Kong","已离开处理中心","运输中");
            Assert(repo.Data.Shipments.Single().Events.Count(e=>e.Manual)==1,"saving the same progress twice deduplicates the event");
            Assert(repo.Data.Cards.Single().Status=="待封发","manual carrier progress does not confirm physical custody");
            Assert(repo.Data.Shipments.Single().Events.Any(e=>e.Provider=="17TRACK")&&repo.Data.Shipments.Single().Events.Count==2,
                "manual progress preserves cached events from earlier versions");
            string database=File.ReadAllText(repo.DataPath);
            Reject(()=>saved.Change(repo,Path.Combine(previous,"child"),cache),"nested data folder cannot take over the live database");
            using(var other=new Repository(Path.Combine(root,"occupied")))
                Reject(()=>saved.Change(repo,other.Folder,cache),"relocation refuses a directory with a live repository");
            saved.Change(repo,moved,cache);
            Assert(repo.Folder==moved&&File.ReadAllText(repo.DataPath)==database,"relocation copies the complete live database and switches the session");
            Assert(File.ReadAllText(Path.Combine(previous,"qsl-post.xml"))==database&&Directory.GetFiles(Path.Combine(moved,"backups")).Length>0,
                "relocation keeps the source and historical backups");
            Assert(File.ReadAllBytes(Path.Combine(moved,"17track-token.dat")).SequenceEqual(legacyFile),"relocation preserves the legacy credential file byte for byte");
            Reject(()=>new Repository(moved),"the relocated repository remains exclusively locked");
            repo.AddCard(New("MOVE/C"));
        }
        using(var saved=StorageLocations.Load(configuration))
        using(var repo=new Repository(saved.DataDirectory))
            Assert(saved.DataDirectory==moved&&saved.CacheDirectory==cache&&repo.Data.Cards.Count==2&&repo.Data.Shipments.Single().Events.Count==2,
                "new directory preferences persist after reopening and retain subsequent writes");
        Assert(PdfExport.Resolution(new DocumentPage{Width=130,Height=90})==600&&
            PdfExport.Resolution(new DocumentPage{Width=1000,Height=1000})<300,"PDF keeps native forms high-resolution and bounds large jobs");
        File.WriteAllText(Path.Combine(root,"test-results.txt"),checks+" / "+checks+" checks passed.");Console.WriteLine(checks+" checks passed.");
    }
}
