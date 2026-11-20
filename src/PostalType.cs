using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace QslPost
{
    internal sealed class PostalLegend
    {
        public readonly string Chinese, French, English;
        public PostalLegend(string chinese,string french,string english)
        { Chinese=chinese; French=french; English=english; }
    }

    internal static class PostalType
    {
        static readonly HashSet<string> Fonts = new HashSet<string>(FontFamily.Families.Select(f=>f.Name),StringComparer.OrdinalIgnoreCase);
        public static readonly string Song=Face("SimSun","宋体","Microsoft YaHei");
        public static readonly string Hei=Face("SimHei","黑体",Song);
        public static readonly string Fang=Face("FangSong","仿宋",Song);
        public static readonly string Narrow=Face("FZYaoTi","方正姚体","FZYaoTi GBK","Arial Narrow","Arial");
        public static readonly string Serif=Face("Times New Roman","Times");
        public static readonly string Sans=Face("Arial","Liberation Sans");
        public static readonly string Number=Face("Courier New","Consolas",Serif);

        static readonly Dictionary<string,string[]> Lexicon=Read();

        static string Face(params string[] names)
        {
            return names.FirstOrDefault(Fonts.Contains)??names.Last();
        }

        static Dictionary<string,string[]> Read()
        {
            var result=new Dictionary<string,string[]>(StringComparer.Ordinal);
            using(var input=typeof(PostalType).Assembly.GetManifestResourceStream("QslPost.PostalLanguage.tsv"))
            using(var reader=new StreamReader(input,Encoding.UTF8))
            {
                string line;
                while((line=reader.ReadLine())!=null)
                {
                    if(line.Length==0||line.StartsWith("#"))continue;
                    var columns=line.Split('\t');
                    if(columns.Length!=3||columns.Any(String.IsNullOrWhiteSpace))
                        throw new InvalidDataException("邮务单据的三语索引缺项："+line);
                    result.Add(columns[0],new[]{columns[1],columns[2]});
                }
            }
            return result;
        }

        public static PostalLegend Label(string id,string chinese)
        {
            string[] pair;
            if(!Lexicon.TryGetValue(id+"@"+chinese,out pair)&&!Lexicon.TryGetValue(id,out pair))
                throw new InvalidDataException("邮务单据的三语索引缺少："+id+" / "+chinese);
            return new PostalLegend(chinese,pair[0],pair[1]);
        }

        public static PostalLegend Title(UpuTemplate form)
        {
            string id=form.Key;
            if(!Lexicon.ContainsKey(id))
                id=form.Key.StartsWith("upu-cn25-t",StringComparison.Ordinal)?"CN 25 transit":form.Code;
            return Label(id,form.Purpose);
        }

        public static PostalLegend Variant(UpuTemplate form)
        {
            return form.Variant.Length==0?null:Label(":"+form.Variant,form.Variant);
        }

        public static void Text(Graphics g,string text,string family,RectangleF area,float pointSize,bool bold=false,bool center=false,bool topAlign=false)
        {
            Ink.PrintedText(g,text,family,area.X,area.Y,area.Width,area.Height,pointSize,bold,center,0,topAlign);
        }

        static void LegendLine(Graphics g,string text,string family,RectangleF area,float pointSize,bool bold,bool center)
        {
            Ink.PrintedText(g,text,family,area.X,area.Y,area.Width,area.Height,pointSize,bold,center,2.7f);
        }

        public static void Caption(Graphics g,PostalLegend label,RectangleF area,float pointSize=6.4f,bool center=false,bool chineseStyle=false,bool bold=false)
        {
            if(label==null)return;
            string chinese=bold?Hei:Song;
            string latin=chineseStyle?Serif:Sans;
            if(area.Height<6.0f)
            {
                // Small labels share two baselines. Keep the French legend before the English equivalent.
                float head=area.Height*.53f;
                LegendLine(g,label.Chinese,chinese,new RectangleF(area.X,area.Y,area.Width,head),pointSize,bold,center);
                string terms=label.French==label.English?label.French:label.French+" · "+label.English;
                LegendLine(g,terms,latin,new RectangleF(area.X,area.Y+head,area.Width,area.Height-head),
                    Math.Min(pointSize-.7f,6.2f),false,center);
                return;
            }
            float zh=area.Height*.41f,fr=area.Height*.31f;
            LegendLine(g,label.Chinese,chinese,new RectangleF(area.X,area.Y,area.Width,zh),pointSize,bold,center);
            LegendLine(g,label.French,latin,new RectangleF(area.X,area.Y+zh,area.Width,fr),Math.Max(3.2f,pointSize-1),bold,center);
            LegendLine(g,label.English,latin,new RectangleF(area.X,area.Y+zh+fr,area.Width,area.Height-zh-fr),Math.Max(3.2f,pointSize-1.8f),false,center);
        }

        public static void FormTitle(Graphics g,PostalLegend title,RectangleF area,bool chineseStyle,bool center=false)
        {
            string zh=chineseStyle?Song:Hei;
            string latin=chineseStyle?Serif:Sans;
            float first=area.Height*.47f;
            LegendLine(g,title.Chinese,zh,new RectangleF(area.X,area.Y,area.Width,first),chineseStyle?16:13,!chineseStyle,center);
            LegendLine(g,title.French,latin,new RectangleF(area.X,area.Y+first,area.Width,area.Height*.29f),9,true,center);
            LegendLine(g,title.English,latin,new RectangleF(area.X,area.Y+area.Height*.76f,area.Width,area.Height*.24f),7,false,center);
        }
    }
}
