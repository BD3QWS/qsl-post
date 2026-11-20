using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace QslPost
{
    public static class Exchange
    {
        public static readonly string[] Headers = { "卡片编号", "方向", "原寄呼号", "寄达呼号", "经理人", "国家地区", "网格", "通联UTC", "频率MHz", "模式", "RST发", "RST收", "QSO或SWL", "邮类", "寄件地址", "收件地址", "备注", "状态", "邮袋编号", "物流号码", "设备", "天线", "功率" };
        public static string Cell(string s)
        {
            s = s ?? "";
            // Spreadsheet applications interpret these prefixes as formulas, even in quoted CSV cells.
            if (s.Length > 0 && "=+-@\t\r".IndexOf(s[0]) >= 0) s = "'" + s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        public static void ExportCards(Repository repo, string path, IEnumerable<Card> cards)
        {
            using (var sw = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                sw.WriteLine(String.Join(",", Headers.Select(Cell).ToArray()));
                foreach (Card c in cards)
                    sw.WriteLine(String.Join(",", new[] { c.Id, c.Direction, c.FromCall, c.ToCall, c.Via, c.Country, c.Grid, c.QsoUtc, c.Frequency, c.Mode, c.RstSent, c.RstReceived,
                        c.Kind, c.MailClass, c.SenderAddress, c.RecipientAddress, c.Notes, c.Status, c.BagId, String.Join(";",repo.CardShipments(c).Select(x => x.Number).ToArray()),c.Equipment,c.Antenna,c.Power }.Select(Cell).ToArray()));
            }
        }
        public static List<List<string>> ParseCsv(string content)
        {
            var rows = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder(); bool quoted = false;
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < content.Length && content[i + 1] == '"') { field.Append('"'); i++; }
                    else if (!quoted && field.Length != 0) throw new FormatException("CSV 引号位置不正确。");
                    else quoted = !quoted;
                }
                else if (c == ',' && !quoted) { row.Add(field.ToString()); field.Length = 0; }
                else if ((c == '\n' || c == '\r') && !quoted)
                {
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                    row.Add(field.ToString()); field.Length = 0;
                    if (row.Any(x => x.Length > 0)) rows.Add(row); row = new List<string>();
                }
                else field.Append(c);
            }
            if (quoted) throw new FormatException("CSV 含有未闭合的引号。");
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
            return rows;
        }
        public static List<Card> ReadCsv(string path)
        {
            var rows = ParseCsv(File.ReadAllText(path, Encoding.UTF8));
            if (rows.Count == 0) throw new InvalidOperationException("CSV 没有数据。请先导出 CSV 样表，按同一列名填写。");
            var headers = rows[0];
            if (headers.Count != headers.Distinct().Count()) throw new InvalidOperationException("CSV 中存在重复列名。");
            if (!headers.Contains("原寄呼号") || !headers.Contains("寄达呼号")) throw new InvalidOperationException("CSV 需要“原寄呼号”和“寄达呼号”两列。请使用导出的 CSV 样表。");
            var cards = new List<Card>();
            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i]; Func<string, string> get = key => { int n = headers.IndexOf(key); string v = n >= 0 && n < row.Count ? row[n] : ""; return v.StartsWith("'") ? v.Substring(1) : v; };
                Card c = new Card { Id=get("卡片编号"), Direction = get("方向") == "进口" ? "进口" : "出口", FromCall = get("原寄呼号"), ToCall = get("寄达呼号"), Via = get("经理人"), Country = get("国家地区"),
                    Grid = get("网格"), QsoUtc = get("通联UTC"), Frequency = get("频率MHz"), Mode = get("模式"), RstSent = get("RST发"), RstReceived = get("RST收"), Kind = get("QSO或SWL"),
                    MailClass = get("邮类"), SenderAddress = get("寄件地址"), RecipientAddress = get("收件地址"), Notes = get("备注"), Equipment=get("设备"), Antenna=get("天线"), Power=get("功率") };
                // Tracking survives a CSV round trip, but membership / custody is established only through the bag workflow.
                c.ImportTracking=get("物流号码");c.ImportFormat="CSV";
                try { Repository.CheckCard(c); } catch (Exception ex) { throw new InvalidOperationException("CSV 第 " + (i + 1) + " 行：" + ex.Message); }
                cards.Add(c);
            }
            return cards;
        }
        public static List<Card> ReadAdif(string path, string station)
        {
            string content = File.ReadAllText(path, Encoding.UTF8); int pos = 0; var fields = new Dictionary<string, string>(); var result = new List<Card>();
            while (pos < content.Length)
            {
                int start = content.IndexOf('<', pos); if (start < 0) break;
                int end = content.IndexOf('>', start); if (end < 0) throw new FormatException("ADIF 字段未闭合。");
                string tag = content.Substring(start + 1, end - start - 1); pos = end + 1;
                if (tag.Equals("EOH", StringComparison.OrdinalIgnoreCase)) { fields.Clear(); continue; }
                if (tag.Equals("EOR", StringComparison.OrdinalIgnoreCase))
                {
                    Func<string, string> get = k => fields.ContainsKey(k) ? fields[k] : "";
                    if (fields.Count > 0)
                    {
                        string date = get("QSO_DATE"), time = get("TIME_ON"); DateTime dt;
                        string utc = "";
                        if (date.Length > 0 || time.Length > 0)
                        {
                            if (time.Length < 4 || !DateTime.TryParseExact(date + time.Substring(0, 4), "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) throw new FormatException("ADIF 第 " + (result.Count+1) + " 条记录的 UTC 日期或时间无效。");
                            utc = dt.ToString("yyyy-MM-dd HH:mm");
                        }
                        Card c = new Card { FromCall = get("STATION_CALLSIGN").Length > 0 ? get("STATION_CALLSIGN") : station, ToCall = get("CALL"), Via = get("QSL_VIA"), Country = get("COUNTRY"),
                            Grid = get("GRIDSQUARE"), Frequency = get("FREQ"), Mode = get("SUBMODE").Length > 0 ? get("SUBMODE") : get("MODE"), RstSent = get("RST_SENT"), RstReceived = get("RST_RCVD"), QsoUtc = utc, Notes = get("COMMENT"), Kind = "QSO" };
                        c.ImportFormat="ADIF";Repository.CheckCard(c); result.Add(c);
                    }
                    fields.Clear(); continue;
                }
                string[] parts = tag.Split(':'); int length;
                if (parts.Length >= 2 && Int32.TryParse(parts[1], out length))
                {
                    if (length < 0 || length > 100000 || length > content.Length - pos) throw new FormatException("ADIF 字段长度不正确。");
                    fields[parts[0].ToUpperInvariant()] = content.Substring(pos, length).Trim(); pos += length;
                }
            }
            if (fields.Count > 0) throw new FormatException("ADIF 最后一条记录缺少 <EOR>，导入已取消。");
            return result;
        }
        public static int Import(Repository repo, List<Card> cards, out int skipped)
        {
            int added = 0, duplicates = 0;
            repo.Change(db => {
                foreach (Card c in cards)
                {
                    Repository.CheckCard(c);
                    string sourceId=c.Id;
                    Card existing=sourceId.Length==0?null:db.Cards.FirstOrDefault(x=>x.Id==sourceId||x.SourceId==sourceId);
                    Func<Card,bool> sameQso=x=>x.FromCall==c.FromCall&&x.ToCall==c.ToCall&&x.Direction==c.Direction&&x.QsoUtc==c.QsoUtc&&x.Frequency==c.Frequency&&x.Mode==c.Mode;
                    if(existing!=null&&!sameQso(existing))throw new InvalidOperationException("来源卡号 "+sourceId+" 与资料库中另一张卡片编号冲突。请核对来源；需要独立登记实体卡片时，清空 CSV 的卡片编号列后重新导入。");
                    bool duplicate=existing!=null||c.ImportFormat=="ADIF"&&c.QsoUtc.Length>0&&db.Cards.Any(sameQso);
                    if (duplicate) { duplicates++; continue; }
                    c.SourceId=sourceId;c.Id = "QSL-" + Clock.Date + "-" + db.NextCard++.ToString("000000"); c.CreatedUtc = Clock.Utc; c.BagId = ""; c.Status = c.Direction == "进口" ? "待分拣" : "待封发";
                    db.Cards.Add(c); Repository.Log(db, "导入卡片", c.Id, c.FromCall + " → " + c.ToCall+"；来源卡号："+sourceId, null);
                    foreach(string number in c.ImportTracking.Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries).Distinct())
                    {
                        var s=new Shipment{Id="MAIL-"+(db.NextShipment++).ToString("000000"),CardId=c.Id,Number=Identifiers.Tracking(number),Leg="导入记录",CreatedUtc=Clock.Utc};db.Shipments.Add(s);Repository.Log(db,"导入物流号码",s.Id,s.Number,new[]{c.Id});
                    }
                    added++;
                }
            }); skipped = duplicates; return added;
        }
    }
    public static class Tracker
    {
        public static string WebUrl(string number) { return "https://t.17track.net/zh-cn#nums=" + Uri.EscapeDataString(Identifiers.Tracking(number)); }
        public static void SaveProgress(Repository repo,string shipmentId,string time,string location,string description,string status)
        {
            DateTimeOffset when;
            if(!DateTimeOffset.TryParse(time,CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out when))
                throw new InvalidOperationException("请按“年-月-日 时:分”填写物流时间。");
            if(String.IsNullOrWhiteSpace(description))throw new InvalidOperationException("请填一条物流进度。");
            string stamp=when.ToString("o",CultureInfo.InvariantCulture);
            repo.Change(db=>{
                var shipment=db.Shipments.Single(x=>x.Id==shipmentId);
                bool latest=shipment.Events.All(x=>EventInstant(x)<=when);
                var entry=new TrackEvent{Time=stamp,Location=location.Trim(),Description=description.Trim(),Provider="手工记录",Manual=true};
                shipment.Events=MergeEvents(shipment.Events.Concat(new[]{entry}));
                if(latest&&!String.IsNullOrWhiteSpace(status))shipment.Status=status.Trim();
                shipment.UpdatedUtc=Clock.Utc;
                Repository.Log(db,"记录物流进度",shipment.Id,entry.Description,new[]{shipment.BagId,shipment.CardId});
            });
        }
        static DateTimeOffset EventInstant(TrackEvent row)
        {
            DateTimeOffset value;
            return DateTimeOffset.TryParse(row.Time,out value)?value:DateTimeOffset.MinValue;
        }
        static List<TrackEvent> MergeEvents(IEnumerable<TrackEvent> events)
        {
            return events.GroupBy(e=>{
                    DateTimeOffset time=EventInstant(e);
                    return Tuple.Create(time,time==DateTimeOffset.MinValue?e.Time:"",e.Location,e.Description);
                })
                .Select(g=>g.OrderByDescending(x=>x.Manual).First()).OrderByDescending(EventInstant).ToList();
        }
    }
}
