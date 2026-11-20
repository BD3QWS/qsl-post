using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace QslPost
{
    public class StationSettings
    {
        public string Profile = "";
        public string Organization = "";
        public string Callsign = "";
        public string Address = "";
        public string Operator = "";
        public string OriginCode = "";
        public bool Initialized;
        public bool HideStartupGuide;
        public bool EnglishDemoAddress;
        public List<PrintProfile> PrintProfiles = new List<PrintProfile>();
    }
    public enum PrintStock { Original, A4, Custom }
    public class PrintProfile
    {
        public string Template = "";
        public string Printer = "";
        public PrintStock Stock = PrintStock.Original;
        [XmlElement("A4")]
        public bool LegacyA4 { get { return Stock == PrintStock.A4; } set { Stock = value ? PrintStock.Original : PrintStock.Custom; } }
        public bool ShouldSerializeLegacyA4() { return false; }
        public bool Landscape;
        public decimal Width = 100, Height = 150, OffsetX, OffsetY;
        public decimal Margin = 8, Scale = 100;
    }
    public class Card
    {
        public string Id = "", FromCall = "", ToCall = "", Via = "", Country = "", Grid = "";
        public string Direction = "出口", QsoUtc = "", Frequency = "", Mode = "";
        public string RstSent = "", RstReceived = "", Kind = "QSO", Equipment = "", Antenna = "", Power = "";
        public string SenderAddress = "", RecipientAddress = "", MailClass = "平信", Notes = "";
        public string Status = "待封发", BagId = "", CreatedUtc = "";
        public string SourceId = "";
        public List<string> PreviousBagIds = new List<string>();
        [XmlIgnore] public string ImportTracking = "";
        [XmlIgnore] public string ImportFormat = "";

        internal string FrequencyWithUnit { get { return Frequency.Length==0?"":Frequency+" MHz"; } }
        internal string SignalReport(string separator)
        {
            return RstSent.Length==0&&RstReceived.Length==0?"":RstSent+separator+RstReceived;
        }

        internal Card Snapshot()
        {
            var copy = (Card)MemberwiseClone();
            copy.PreviousBagIds = new List<string>(PreviousBagIds);
            return copy;
        }
    }
    public class Bag
    {
        public string Id = "", DispatchId = "", From = "", To = "", OriginCode = "", DestinationCode = "";
        public string Direction = "出口", Transport = "航空", MailClass = "挂信", Via = "", Seal = "", Notes = "";
        public decimal WeightKg;
        public string State = "草稿", CreatedUtc = "", SealedUtc = "", HandoverUtc = "", ReceivedUtc = "";
        public List<string> CardIds = new List<string>();
        public List<Card> SealedCards = new List<Card>();
    }
    public class Shipment
    {
        public string Id = "", Number = "", CarrierName = "", BagId = "", CardId = "", Leg = "干线运输";
        // Read and write the tracking fields found in earlier backups.
        public int CarrierCode;
        public string DestinationPostalCode = "", OriginCountry = "", DestinationCountry = "", ShipDate = "", Param = "";
        public string Status = "尚未查询", UpdatedUtc = "", RegisteredUtc = "", CreatedUtc = "";
        public List<TrackEvent> Events = new List<TrackEvent>();
    }
    public class TrackEvent
    {
        public string Time = "", Location = "", Description = "", Provider = "";
        public bool Manual;
    }
    public class CaseRecord
    {
        public string Id = "", Type = "验单", CardId = "", BagId = "", ShipmentId = "", RelatedCase = "";
        public string From = "", To = "", Subject = "", Details = "", Reply = "", Attachment = "";
        public string Operator = "", Supervisor = "", State = "待处理", CreatedUtc = "", ClosedUtc = "";
    }
    public class Contact
    {
        public string Id = "", Callsign = "", Name = "", Country = "", Address = "", PostalCode = "", Email = "", Notes = "", Phone = "";
        public string EnglishName = "", EnglishAddress = "";
    }
    public class AuditEntry
    {
        public long Sequence;
        public string TimeUtc = "", Actor = "", Action = "", Entity = "", Links = "", Details = "";
        public string PreviousHash = "", Hash = "";
    }
    public class Database
    {
        public int Version = 1;
        public long NextCard = 1, NextBag = 1, NextShipment = 1, NextCase = 1, NextContact = 1;
        public StationSettings Settings = new StationSettings();
        public List<Card> Cards = new List<Card>();
        public List<Bag> Bags = new List<Bag>();
        public List<Shipment> Shipments = new List<Shipment>();
        public List<CaseRecord> Cases = new List<CaseRecord>();
        public List<Contact> Contacts = new List<Contact>();
        public List<AuditEntry> Audit = new List<AuditEntry>();
        public List<string> RetiredDispatchIds = new List<string>();
    }
    public static class Clock
    {
        public static string Utc { get { return DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture); } }
        public static string Date { get { return DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture); } }
        public static string Display(string value)
        {
            DateTimeOffset dt;
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt)
                ? dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : value;
        }
    }
    public static class Identifiers
    {
        public static string Dispatch(string origin, string destination, string transport, DateTime date, int serial)
        {
            origin = origin.Trim().ToUpperInvariant(); destination = destination.Trim().ToUpperInvariant();
            if (!Regex.IsMatch(origin, "^[A-Z]{6}$") || !Regex.IsMatch(destination, "^[A-Z]{6}$"))
                throw new InvalidOperationException("内部节点代码须为 6 位英文字母。");
            if (serial < 1 || serial > 9999) throw new InvalidOperationException("该年度起讫路向的封发序号已满，请变更内部节点代码。");
            string category = transport == "航空" ? "A" : transport == "SAL" ? "B" : "C";
            return origin + destination + category + "QL" + (date.Year % 10).ToString() + serial.ToString("0000");
        }
        public static string Receptacle(string dispatch, int serial, bool last, bool registered, decimal kg)
        {
            if (!Regex.IsMatch(dispatch, "^[A-Z]{15}[0-9]{5}$")) throw new InvalidOperationException("S8 格式的总封编号应为 20 位。");
            if (serial < 1 || serial > 999 || kg < 0 || kg > 999.8m) throw new InvalidOperationException("袋序号须为 1-999，重量须为 0-999.8 kg。");
            int units = (int)Math.Ceiling(kg * 10);
            return dispatch + serial.ToString("000") + (last ? "1" : "0") + (registered ? "1" : "0") + units.ToString("0000");
        }
        public static string Tracking(string value)
        {
            value = (value ?? "").Trim();
            if (!Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9_.-]{2,79}$")) throw new InvalidOperationException("物流号码应为 3-80 位字母、数字或 . _ -，请保持前导零。");
            return value;
        }
    }
    public sealed class Repository : IDisposable
    {
        private const string LegacyTrackingFile = WorkspacePaths.LegacyTrackingFile;
        public string Folder { get; private set; }
        public Database Data { get; private set; }
        private FileStream lease;
        private readonly XmlSerializer serializer = new XmlSerializer(typeof(Database));
        public string DataPath { get { return Path.Combine(Folder, WorkspacePaths.DatabaseFile); } }
        public Repository(string folder)
        {
            Folder = Path.GetFullPath(folder); Directory.CreateDirectory(Folder);
            try { lease = new FileStream(Path.Combine(Folder, WorkspacePaths.DatabaseLock), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { throw new InvalidOperationException("此资料库已由另一个窗口打开。请回到现有窗口，或使用独立的资料库目录。"); }
            try
            {
                if (File.Exists(DataPath)) { Data = Read(DataPath); Validate(Data); }
                else { Data = new Database(); Persist(Data); }
            }
            catch { lease.Dispose(); throw; }
        }
        public Database Read(string path)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 256L * 1024 * 1024 };
            using (var reader = XmlReader.Create(path, settings)) return (Database)serializer.Deserialize(reader);
        }
        internal void Relocate(string destination,Action publish)
        {
            if(StorageLocations.Contains(Folder,destination)||StorageLocations.Contains(destination,Folder))
                throw new InvalidOperationException("新资料目录请选择原目录之外的文件夹。");
            Directory.CreateDirectory(destination);
            string target=Path.Combine(destination,"qsl-post.xml");
            string backupFolder=Path.Combine(Folder,"backups"),backupTarget=Path.Combine(destination,"backups");
            var copies=new List<Tuple<string,string>>();
            foreach(string name in new[]{"qsl-post.xml","qsl-post.xml.bak",LegacyTrackingFile})
            {
                string source=Path.Combine(Folder,name);
                if(File.Exists(source))copies.Add(Tuple.Create(source,Path.Combine(destination,name)));
            }
            if(Directory.Exists(backupFolder))
                foreach(string source in Directory.GetFiles(backupFolder,"*.xml"))
                    copies.Add(Tuple.Create(source,Path.Combine(backupTarget,Path.GetFileName(source))));

            FileStream newLease=null;
            var created=new List<string>();
            try
            {
                try {newLease=new FileStream(Path.Combine(destination,"workspace.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
                catch(IOException error){throw new InvalidOperationException("目标资料库正在使用，请换一个文件夹。",error);}
                if(File.Exists(target)||File.Exists(target+".bak")||File.Exists(Path.Combine(destination,LegacyTrackingFile))||
                    (Directory.Exists(backupTarget)&&Directory.GetFiles(backupTarget,"*.xml").Length>0))
                    throw new InvalidOperationException("这个文件夹已存有 QSL 资料。请选择新的资料文件夹。");
                foreach(var copy in copies)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(copy.Item2));
                    using(var output=new FileStream(copy.Item2,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    {
                        created.Add(copy.Item2);
                        using(var input=File.OpenRead(copy.Item1))input.CopyTo(output);
                        output.Flush(true);
                    }
                }
                Validate(Read(target));
                publish();
                FileStream old=lease;
                lease=newLease;newLease=null;Folder=destination;
                old.Dispose();
            }
            catch
            {
                if(newLease!=null)
                    foreach(string path in created)
                        try {File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}
                throw;
            }
            finally { if(newLease!=null)newLease.Dispose(); }
        }
        public Database Clone(Database db)
        {
            using (var stream = new MemoryStream()) { serializer.Serialize(stream, db); stream.Position = 0; return (Database)serializer.Deserialize(stream); }
        }
        public void Change(Action<Database> action)
        {
            Database candidate = Clone(Data); action(candidate); Validate(candidate); Persist(candidate); Data = candidate;
        }
        public static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
        private static string AuditHash(AuditEntry a)
        {
            // Length-prefix every component, including user input, so delimiters cannot change the record boundary.
            string[] fields = { a.Sequence.ToString(CultureInfo.InvariantCulture), a.TimeUtc, a.Actor, a.Action, a.Entity, a.Links, a.Details, a.PreviousHash };
            return Hash(String.Concat(fields.Select(x => (x ?? "").Length.ToString(CultureInfo.InvariantCulture) + ":" + (x ?? ""))));
        }
        public static void Log(Database db, string action, string entity, string details, IEnumerable<string> related)
        {
            // XML readers normalize line endings. Hash the same text that will be read back from disk.
            Func<string,string> normalize = value => (value ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            var e = new AuditEntry { Sequence = db.Audit.Count + 1, TimeUtc = Clock.Utc, Actor = normalize(db.Settings.Operator),
                Action = normalize(action), Entity = normalize(entity), Details = normalize(details), Links = String.Join("|", (related ?? new string[0]).Where(x => !String.IsNullOrEmpty(x)).Distinct().ToArray()),
                PreviousHash = db.Audit.Count == 0 ? "" : db.Audit[db.Audit.Count - 1].Hash };
            e.Hash = AuditHash(e); db.Audit.Add(e);
        }
        public static void Validate(Database db)
        {
            if (db == null || db.Version != 1) throw new InvalidDataException("资料库版本不受支持。");
            if (db.Cards.GroupBy(x => x.Id).Any(g => g.Count() > 1) || db.Bags.GroupBy(x => x.Id).Any(g => g.Count() > 1)
                || db.Shipments.GroupBy(x => x.Id).Any(g => g.Count() > 1) || db.Cases.GroupBy(x => x.Id).Any(g => g.Count() > 1))
                throw new InvalidDataException("资料库中有重复编号。");
            var cards = db.Cards.ToDictionary(x => x.Id); var bags = db.Bags.ToDictionary(x => x.Id);
            foreach (Card c in db.Cards)
            {
                if (String.IsNullOrEmpty(c.Id)) throw new InvalidDataException("卡片缺少永久编号。");
                if (c.BagId.Length > 0 && (!bags.ContainsKey(c.BagId) || !bags[c.BagId].CardIds.Contains(c.Id))) throw new InvalidDataException("卡片和邮袋的装袋关系不一致。");
            }
            foreach (Bag b in db.Bags)
            {
                if (b.CardIds.Distinct().Count() != b.CardIds.Count) throw new InvalidDataException("邮袋中有重复卡片。");
                foreach (string id in b.CardIds) if (!cards.ContainsKey(id) || cards[id].BagId != b.Id && !cards[id].PreviousBagIds.Contains(b.Id)) throw new InvalidDataException("邮袋引用了不存在或缺少历史装袋关系的卡片。");
                if (b.State != "草稿" && b.SealedCards.Count > 0 && !b.CardIds.OrderBy(id => id).SequenceEqual(b.SealedCards.Select(c => c.Id).OrderBy(id => id)))
                    throw new InvalidDataException("封袋快照和袋内卡片编号不一致。");
            }
            foreach (Shipment s in db.Shipments)
            {
                if ((s.BagId.Length == 0) == (s.CardId.Length == 0)) throw new InvalidDataException("物流记录必须关联一张卡片或一个邮袋。");
                if (s.BagId.Length > 0 && !bags.ContainsKey(s.BagId) || s.CardId.Length > 0 && !cards.ContainsKey(s.CardId)) throw new InvalidDataException("物流记录的关联对象不存在。");
            }
            string previous = ""; long sequence = 1;
            foreach (AuditEntry e in db.Audit)
            {
                if (e.Sequence != sequence++ || e.PreviousHash != previous || e.Hash != AuditHash(e)) throw new InvalidDataException("操作历史完整性检查失败。请保留当前文件并从备份恢复。");
                previous = e.Hash;
            }
            long maxCard = db.Cards.Select(c => { long n; return Int64.TryParse(c.Id.Split('-').Last(), out n) ? n : 0; }).DefaultIfEmpty().Max();
            if (db.NextCard <= maxCard) throw new InvalidDataException("卡片编号计数器落后于现有记录。");
        }
        private void Persist(Database candidate)
        {
            string temp = DataPath + ".new";
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                serializer.Serialize(fs, candidate); fs.Flush(true);
            }
            if (File.Exists(DataPath)) File.Replace(temp, DataPath, DataPath + ".bak", true);
            else File.Move(temp, DataPath);
            string dir = Path.Combine(Folder, "backups");
            string day = Path.Combine(dir, "qsl-post-" + Clock.Date + ".xml");
            try { Directory.CreateDirectory(dir); if (!File.Exists(day)) File.Copy(DataPath, day); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        public void Backup(string destination) { File.Copy(DataPath, destination, true); }
        public void Restore(string source)
        {
            Database candidate = Read(source); Validate(candidate);
            string archive = Directory.CreateDirectory(Path.Combine(Folder, "backups")).FullName;
            string before = Path.Combine(archive, "before-restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".xml");
            File.Copy(DataPath, before);
            // Restoring older content cannot make identifiers already issued on this workstation available again.
            candidate.NextCard = Math.Max(candidate.NextCard, Data.NextCard);
            candidate.NextBag = Math.Max(candidate.NextBag, Data.NextBag);
            candidate.NextShipment = Math.Max(candidate.NextShipment, Data.NextShipment);
            candidate.NextCase = Math.Max(candidate.NextCase, Data.NextCase);
            candidate.NextContact = Math.Max(candidate.NextContact, Data.NextContact);
            var restored = new HashSet<string>(candidate.Bags.Select(b => b.DispatchId), StringComparer.Ordinal);
            candidate.RetiredDispatchIds = candidate.RetiredDispatchIds.Concat(Data.RetiredDispatchIds).Concat(Data.Bags.Select(b => b.DispatchId))
                .Where(id => !restored.Contains(id)).Distinct(StringComparer.Ordinal).ToList();
            Log(candidate, "恢复备份", "资料库", "恢复前资料已另存到 " + Path.GetFileName(before), null);
            Validate(candidate);
            Persist(candidate); Data = candidate;
        }
        public string AddCard(Card value)
        {
            string id = "";
            Change(db => { value.Id = id = "QSL-" + Clock.Date + "-" + db.NextCard++.ToString("000000"); value.CreatedUtc = Clock.Utc;
                value.BagId = ""; value.Status = value.Direction == "进口" ? "待分拣" : "待封发"; CheckCard(value); db.Cards.Add(value);
                Log(db, "登记卡片", id, value.FromCall + " → " + value.ToCall + " / " + value.Direction, null); }); return id;
        }
        public static void CheckCard(Card c)
        {
            if (String.IsNullOrWhiteSpace(c.FromCall) || String.IsNullOrWhiteSpace(c.ToCall)) throw new InvalidOperationException("请填写原寄和寄达呼号。");
            if (c.FromCall.Length > 40 || c.ToCall.Length > 40) throw new InvalidOperationException("呼号不得超过 40 个字符。");
            c.FromCall = c.FromCall.Trim().ToUpperInvariant(); c.ToCall = c.ToCall.Trim().ToUpperInvariant();
            if (c.QsoUtc.Length > 0) { DateTime dt; if (!DateTime.TryParseExact(c.QsoUtc, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) throw new InvalidOperationException("通联时间格式须为 yyyy-MM-dd HH:mm，使用 UTC。"); }
            decimal f; if (c.Frequency.Length > 0 && (!Decimal.TryParse(c.Frequency, NumberStyles.Number, CultureInfo.InvariantCulture, out f) || f <= 0 || f > 100000)) throw new InvalidOperationException("频率应为正数，单位 MHz，使用英文小数点。");
        }
        public void EditCard(Card edited)
        {
            Change(db => { Card old = db.Cards.Single(x => x.Id == edited.Id); CheckCard(edited);
                if (old.BagId.Length > 0 && db.Bags.Single(x => x.Id == old.BagId).State != "草稿") throw new InvalidOperationException("邮袋已封袋。请先按流程重开邮袋后修改卡片，或建立查验单。");
                edited.Id = old.Id; edited.CreatedUtc = old.CreatedUtc; edited.BagId = old.BagId; edited.Status = old.Status;edited.SourceId=old.SourceId;edited.PreviousBagIds=new List<string>(old.PreviousBagIds);
                if (old.BagId.Length > 0 && old.Direction != edited.Direction) throw new InvalidOperationException("已装袋卡片不能改变进出口方向，请先移出邮袋。");
                db.Cards[db.Cards.IndexOf(old)] = edited; Log(db, "修改卡片", edited.Id, "修改前：\n" + Serialize(old) + "\n修改后：\n" + Serialize(edited), new[] { old.BagId }); });
        }
        public static string Serialize<T>(T value)
        {
            using (var sw = new StringWriter(CultureInfo.InvariantCulture)) { new XmlSerializer(typeof(T)).Serialize(sw, value); return sw.ToString(); }
        }
        public Bag CreateBag(string from, string to, string origin, string destination, string transport, string mailClass, string direction, decimal weight, string seal, string via, string notes)
        {
            Bag result = null;
            Change(db => {
                if (String.IsNullOrWhiteSpace(from) || String.IsNullOrWhiteSpace(to)) throw new InvalidOperationException("请填写封发台和寄达台。");
                int serial = 1; string dispatch;
                var issued = new HashSet<string>(db.Bags.Select(b => b.DispatchId).Concat(db.RetiredDispatchIds), StringComparer.Ordinal);
                do { dispatch = Identifiers.Dispatch(origin, destination, transport, DateTime.Now, serial++); } while (issued.Contains(dispatch));
                result = new Bag { DispatchId = dispatch, Id = Identifiers.Receptacle(dispatch, 1, true, mailClass != "平信", weight),
                    From = from, To = to, OriginCode = origin.ToUpperInvariant(), DestinationCode = destination.ToUpperInvariant(),
                    Direction = direction, Transport = transport, MailClass = mailClass, WeightKg = weight, Seal = seal, Via = via, Notes = notes, CreatedUtc = Clock.Utc };
                db.NextBag++; db.Bags.Add(result); Log(db, "建立邮袋", result.Id, from + " → " + to, null);
            }); return result;
        }
        public void AddToBag(string bagId, IEnumerable<string> ids)
        {
            string[] selected = ids.Distinct().ToArray();
            Change(db => { Bag b = db.Bags.Single(x => x.Id == bagId); if (b.State != "草稿") throw new InvalidOperationException("只有草稿邮袋可以装入或移出卡片。");
                if (b.MailClass == "空袋") throw new InvalidOperationException("空袋用于退回空容器，请另建卡片邮袋来装入卡片。");
                if (selected.Length == 0) throw new InvalidOperationException("请先选择卡片。");
                foreach (string id in selected)
                {
                    Card c = db.Cards.Single(x => x.Id == id);
                    if (c.BagId.Length > 0 || c.Status == "已作废" || c.Status == "已交付") throw new InvalidOperationException("卡片 " + id + " 已装袋、交付或作废，本次没有装入任何卡片。");
                    if (c.Direction != b.Direction) throw new InvalidOperationException("卡片进出口方向与邮袋不一致，本次没有装入任何卡片。");
                    c.BagId = b.Id; c.Status = "已装袋"; b.CardIds.Add(id);
                }
                Log(db, "装入邮袋", b.Id, selected.Length + " 张卡片", selected);
            });
        }
        public void RemoveFromBag(string bagId, string cardId)
        {
            Change(db => { Bag b = db.Bags.Single(x => x.Id == bagId); Card c = db.Cards.Single(x => x.Id == cardId);
                if (b.State != "草稿" || c.BagId != b.Id) throw new InvalidOperationException("只有草稿邮袋中实际存在的卡片才能移出。");
                b.CardIds.Remove(cardId); c.BagId = ""; c.Status = c.Direction == "进口" ? "待分拣" : "待封发";
                Log(db, "移出邮袋", b.Id, cardId, new[] { cardId }); });
        }
        public void Transition(string bagId, string state, string note)
        {
            Change(db => { Bag b = db.Bags.Single(x => x.Id == bagId); string old = b.State;
                bool allowed = old == "草稿" && state == "已封袋" || old == "已封袋" && state == "已交接" || old == "已交接" && state == "已接收" || old == "已封袋" && state == "草稿";
                if (!allowed) throw new InvalidOperationException("不允许从“" + old + "”直接变更为“" + state + "”。");
                if (b.CardIds.Count == 0 && b.MailClass != "空袋") throw new InvalidOperationException("空的卡片邮袋不能封袋。寄送空袋请使用“空袋”类型。");
                if (state == "草稿" && String.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("重开邮袋必须填写原因。");
                if (state == "已交接" && String.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("请填写接收人 / 交接地点。");
                b.State = state;
                if (state == "已封袋")
                {
                    b.SealedUtc = Clock.Utc;
                    var members = new HashSet<string>(b.CardIds, StringComparer.Ordinal);
                    b.SealedCards = db.Cards.Where(card => members.Contains(card.Id)).Select(card => card.Snapshot()).ToList();
                }
                if (state == "已交接") b.HandoverUtc = Clock.Utc;
                if (state == "已接收") b.ReceivedUtc = Clock.Utc;
                foreach (Card c in db.Cards.Where(x => x.BagId == b.Id)) c.Status = state == "草稿" ? "已装袋" : state == "已封袋" ? "已封发" : state;
                Log(db, state == "草稿" ? "重开邮袋" : state, b.Id, old + " → " + state + "；" + note, b.CardIds);
            });
        }
        public string AddShipment(Shipment s)
        {
            string id = ""; Change(db => { s.Number = Identifiers.Tracking(s.Number);
                if (db.Shipments.Any(x => x.Number.Equals(s.Number, StringComparison.OrdinalIgnoreCase) && x.BagId == s.BagId && x.CardId == s.CardId)) throw new InvalidOperationException("该对象已绑定此物流号码。");
                s.Id = id = "MAIL-" + db.NextShipment++.ToString("000000"); s.CreatedUtc = Clock.Utc; db.Shipments.Add(s);
                Log(db, "绑定物流", s.Id, s.Number + " / " + s.CarrierName + " / " + s.Leg, new[] { s.BagId, s.CardId }); }); return id;
        }
        public void MarkCard(string cardId, string state, string note)
        {
            Change(db => { Card c = db.Cards.Single(x => x.Id == cardId);
                if (c.Status == "已作废") throw new InvalidOperationException("已作废的卡片保留历史，不能继续流转。");
                if (state == "已作废" && c.BagId.Length > 0) throw new InvalidOperationException("请先从草稿邮袋中移出卡片。");
                if (state == "已交付" && c.BagId.Length > 0 && db.Bags.Single(x => x.Id == c.BagId).State != "已接收") throw new InvalidOperationException("请先完成邮袋接收，再确认交付给台站。");
                if (String.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("请填写经办说明 / 签收人。");
                c.Status = state; Log(db, state, c.Id, note, new[] { c.BagId }); });
        }
        public IEnumerable<Shipment> CardShipments(Card c)
        {
            return Data.Shipments.Where(s => s.CardId == c.Id || c.BagId.Length > 0 && s.BagId == c.BagId || c.PreviousBagIds.Contains(s.BagId));
        }
        public void ForwardCard(string cardId,string note)
        {
            Change(db=>{Card c=db.Cards.Single(x=>x.Id==cardId);
                if(c.BagId.Length==0||db.Bags.Single(b=>b.Id==c.BagId).State!="已接收")throw new InvalidOperationException("只有完成邮袋接收的卡片可以重新转寄。");
                if(c.Status=="已作废"||c.Status=="已交付")throw new InvalidOperationException("已作废或已交付卡片不能继续转寄。");
                if(String.IsNullOrWhiteSpace(note))throw new InvalidOperationException("请填写转寄说明。");
                string old=c.BagId;c.PreviousBagIds.Add(old);c.BagId="";c.Direction="出口";c.Status="待封发";
                Log(db,"接收后转寄",c.Id,note,new[]{old});
            });
        }
        public IEnumerable<AuditEntry> History(string id)
        {
            var related = new HashSet<string>(new[] { id }); Card c = Data.Cards.FirstOrDefault(x => x.Id == id);
            if (c != null)
            {
                if (c.BagId.Length > 0) related.Add(c.BagId);
                foreach(string oldBag in c.PreviousBagIds)related.Add(oldBag);
                foreach (Shipment s in CardShipments(c)) related.Add(s.Id);
                foreach (CaseRecord v in Data.Cases.Where(x => x.CardId == id || c.BagId.Length > 0 && x.BagId == c.BagId)) related.Add(v.Id);
            }
            return Data.Audit.Where(e => related.Contains(e.Entity) || e.Links.Split('|').Any(related.Contains)).OrderByDescending(x => x.Sequence);
        }
        public void Dispose() { lease.Dispose(); }
    }
}
