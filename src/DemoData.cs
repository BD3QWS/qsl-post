using System;
using System.Linq;

namespace QslPost
{
    public static class DemoData
    {
        internal static bool HasAddressOptions(Database db)
        {
            var station=FindStation(db,db.Settings.Callsign);
            return station!=null&&station.EnglishAddress.Length>0;
        }

        internal static bool DisplayEnglish(Database db,Contact contact)
        {
            return db.Settings.EnglishDemoAddress&&contact.Callsign.Equals(db.Settings.Callsign,StringComparison.OrdinalIgnoreCase)&&contact.EnglishAddress.Length>0;
        }

        internal static string Address(Database db,string callsign,string primary)
        {
            if(!db.Settings.EnglishDemoAddress)return primary;
            var station=FindStation(db,callsign);
            return station!=null&&DisplayEnglish(db,station)&&primary==station.Address?station.EnglishAddress:primary;
        }

        static Contact FindStation(Database db,string callsign)
        {
            if(callsign.Length==0||!callsign.Equals(db.Settings.Callsign,StringComparison.OrdinalIgnoreCase))return null;
            return db.Contacts.FirstOrDefault(c=>c.Callsign.Equals(callsign,StringComparison.OrdinalIgnoreCase));
        }

        public static void Create(Repository repo,Contact station=null)
        {
            bool personal=station!=null;
            string call=personal?station.Callsign:"DEMO",receiver=personal?station.Name:"演示经办人";
            string address=personal?station.Address:"示例地址 · 不用于实际邮寄";
            repo.Change(db=>{
                db.Settings.Initialized=true;
                db.Settings.Profile=personal?"个人台站":"协会 / 卡片局";
                db.Settings.Organization=personal?call+" 演示台站":"QSL 演示卡片局";
                db.Settings.Callsign=call;
                db.Settings.Address=address;
                db.Settings.Operator=receiver;
                db.Settings.OriginCode="QSLAAA";
                if(personal)
                {
                    station.Id="CONTACT-"+(db.NextContact++).ToString("0000");
                    db.Contacts.Add(station);
                }
            });
            for(int i=0;i<47;i++)
            {
                bool inbound=personal&&i<41;
                string from=i%3==0?"DEMO/A":"DEMO/B",to=i%2==0?"DEMO/C":"DEMO/D";
                if(personal){if(inbound)to=call;else from=call;}
                repo.AddCard(new Card{
                    Direction=inbound?"进口":"出口",FromCall=from,ToCall=to,
                    QsoUtc="2026-11-17 "+(i%24).ToString("00")+":30",Frequency=i%2==0?"14.250":"7.050",Mode=i%2==0?"SSB":"CW",
                    RstSent="59",RstReceived="59",
                    Country=i%2==0?"日本（示例）":"德国（示例）",MailClass=i%3==0?"平信":"挂信",
                    SenderAddress=personal&&!inbound?address:"演示原寄台",RecipientAddress=inbound?address:"演示寄达台",
                    Notes="演示卡片，通联数据为示例。"
                });
            }
            var bag=repo.CreateBag("DEMO QSL Bureau",personal?call:"DEMO Japan Bureau",
                personal?"QSLBBB":"QSLAAA",personal?"QSLAAA":"QSLBBB","SAL","挂信",personal?"进口":"出口",1.8m,
                "DEMO-SEAL-001","DEMO Transit","演示邮袋");
            repo.AddToBag(bag.Id,repo.Data.Cards.Take(41).Select(c=>c.Id));
            repo.Transition(bag.Id,"已封袋","演示核对 41 张");
            repo.Transition(bag.Id,"已交接","演示交接人 / 中转点");
            var second=repo.CreateBag(personal?call:"DEMO QSL Bureau","DEMO Europe Bureau","QSLAAA","QSLCCC",
                "航空","合封","出口",.6m,"DEMO-SEAL-002","","演示草稿袋");
            repo.AddToBag(second.Id,repo.Data.Cards.Skip(41).Take(3).Select(c=>c.Id));
            string mail=repo.AddShipment(new Shipment{BagId=bag.Id,Number="DEMO202611180001",CarrierName="演示承运商",Leg="国际运输"});
            repo.Change(db=>{
                var shipment=db.Shipments.Single(x=>x.Id==mail);
                shipment.Status="运输中（演示）";shipment.UpdatedUtc=Clock.Utc;
                shipment.Events.Add(new TrackEvent{Time="2026-11-18T08:30:00+08:00",Location="演示交换点",Description="已离开中转站",Provider="演示"});
                shipment.Events.Add(new TrackEvent{Time="2026-11-17T15:00:00+08:00",Location="演示收寄点",Description="收寄扫描",Provider="演示"});
                var card=db.Cards[0];
                var record=new CaseRecord{Id="CASE-DEMO-0001",Type="国际查单",From=card.FromCall,To=card.ToCall,BagId=bag.Id,CardId=card.Id,
                    Subject="演示：核对 QSL 通联信息",Details="请核对清单中的卡号、通联时间及 RST。此文书的通联和流转均为示例。",
                    Reply="已核对通联记录，待收到实体卡片后确认。",Operator=receiver,CreatedUtc=Clock.Utc};
                db.Cases.Add(record);db.NextCase++;
                Repository.Log(db,"演示查单",record.Id,record.Subject,new[]{record.CardId,record.BagId});
            });
        }
    }
}
