using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace QslPost
{
    public sealed partial class ManualDialog : GlassForm
    {
        readonly List<string> chapters=new List<string>();
        readonly TextBox article;
        readonly ListBox contents;
        readonly ComboBox language;
        readonly Contact example;
        int exampleIndex=-1;

        public ManualDialog()
        {
            Text="使用手册 · QSL 邮务台";
            Size=new Size(1160,810);
            MinimumSize=new Size(850,650);
            Padding=new Padding(22,86,22,22);
            StartPosition=FormStartPosition.CenterParent;
            ConfigureCaption();

            var body=new SurfaceTablePanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=Color.Transparent};
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,324));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            body.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            Controls.Add(body);
            body.BringToFront();

            var directory=new GlassPanel{Dock=DockStyle.Fill,Padding=new Padding(10,12,10,12),Margin=new Padding(0)};
            contents=new ListBox{Name="manualChapter",AccessibleName="手册目录",Dock=DockStyle.Fill,
                Font=Ui.Font(15,false),BorderStyle=BorderStyle.None,IntegralHeight=false,
                BackColor=Color.FromArgb(247,251,255),DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=44};
            contents.DrawItem+=DrawChapter;
            directory.Controls.Add(contents);
            body.Controls.Add(directory,0,0);
            var reading=new GlassPanel{Dock=DockStyle.Fill,Padding=new Padding(20),Margin=new Padding(16,0,0,0)};
            body.Controls.Add(reading,1,0);

            language=Ui.Combo(new[]{"中文地址","English address"});
            language.Name="manualAddressLanguage";
            language.AccessibleName="示例地址语言";
            language.Dock=DockStyle.Top;
            language.Visible=false;
            reading.Controls.Add(language);
            article=new TextBox{Name="manualText",AccessibleName="手册正文",Dock=DockStyle.Fill,ReadOnly=true,
                BorderStyle=BorderStyle.None,Multiline=true,ScrollBars=ScrollBars.Vertical,WordWrap=true,
                Font=Ui.Font(13,false),ForeColor=Ui.Ink,BackColor=Color.FromArgb(251,253,255)};
            reading.Controls.Add(article);
            article.BringToFront();

            var assembly=typeof(ManualDialog).Assembly;
            using(var input=assembly.GetManifestResourceStream("QslPost.UserGuide.txt"))
            using(var reader=new StreamReader(input))
            {
                var text=reader.ReadToEnd();
                var headings=Regex.Matches(text,@"^(?:[一二三四五六七八九十]+、[^\r\n]+|官方参考)\r?$",RegexOptions.Multiline);
                for(int i=0;i<headings.Count;i++)
                {
                    var start=headings[i];
                    int end=i+1==headings.Count?text.Length:headings[i+1].Index;
                    contents.Items.Add(start.Value.Trim());
                    chapters.Add(text.Substring(start.Index,end-start.Index).Trim());
                }
            }
            using(var input=assembly.GetManifestResourceStream("QslPost.ManualExample.xml"))
            {
                if(input!=null)
                {
                    var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16384};
                    using(var reader=XmlReader.Create(input,settings))
                        example=(Contact)new XmlSerializer(typeof(Contact)).Deserialize(reader);
                    exampleIndex=contents.Items.Count;
                    contents.Items.Add("名址填写示例");
                }
            }

            contents.SelectedIndexChanged+=(s,e)=>ShowChapter();
            language.SelectedIndexChanged+=(s,e)=>ShowChapter();
            contents.SelectedIndex=0;
            ActiveControl=contents;
        }

        void DrawChapter(object sender,DrawItemEventArgs e)
        {
            if(e.Index<0)return;
            using(var background=new SolidBrush(contents.BackColor))e.Graphics.FillRectangle(background,e.Bounds);
            if(e.Bounds.Bottom>contents.ClientSize.Height)return;

            bool selected=(e.State&DrawItemState.Selected)!=0;
            var selection=Rectangle.Inflate(e.Bounds,-WindowLayout.Pixels(4,LayoutScale),-WindowLayout.Pixels(3,LayoutScale));
            if(selected)
            {
                var state=e.Graphics.Save();
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using(var pill=Ui.Round(selection,12*LayoutScale))
                using(var fill=new SolidBrush(Ui.Tint))
                using(var edge=new Pen(Color.FromArgb(115,151,194)))
                {
                    e.Graphics.FillPath(fill,pill);
                    if((e.State&DrawItemState.Focus)!=0)e.Graphics.DrawPath(edge,pill);
                }
                e.Graphics.Restore(state);
            }
            TextRenderer.DrawText(e.Graphics,Convert.ToString(contents.Items[e.Index]),contents.Font,
                Rectangle.Inflate(e.Bounds,-WindowLayout.Pixels(14,LayoutScale),0),selected?Ui.Blue:Ui.Ink,
                TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        }

        void ShowChapter()
        {
            int selected=contents.SelectedIndex;
            if(selected<0)return;
            language.Visible=selected==exampleIndex;
            string text;
            if(selected==exampleIndex)
            {
                bool english=language.SelectedIndex==1;
                text="名址填写示例 · 收件地址一\r\n\r\n"+
                    "在「台站名址」点「新增名址」，按下方各项填写。实际登记时请使用对方提供的资料。\r\n\r\n"+
                    "呼号  "+example.Callsign+"\r\n"+
                    "收件人  "+(english?example.EnglishName:example.Name)+"\r\n"+
                    "地址  "+(english?example.EnglishAddress:example.Address)+"\r\n"+
                    "邮编  "+example.PostalCode+"\r\n"+
                    "电话  "+example.Phone+"\r\n"+
                    "电子邮件  "+example.Email+"\r\n\r\n"+
                    "要把实际资料印在单据上，请在打印工作室选择记录，或单击「填写单据」录入相应栏目。";
            }
            else text=Regex.Replace(chapters[selected],@"(?:\r?\n)+","\r\n\r\n");
            if(article.Text==text)return;
            article.Text=text;
            article.Select(0,0);
            article.ScrollToCaret();
        }
    }

    internal sealed class StartupGuide : GlassForm
    {
        readonly CheckBox hide;
        public bool HideNextTime { get { return hide.Checked; } }

        public StartupGuide()
        {
            Text="首次使用 · QSL 邮务台";
            ClientSize=new Size(465,216);
            FormBorderStyle=FormBorderStyle.FixedDialog;
            MaximizeBox=false;
            MinimizeBox=false;
            ShowInTaskbar=false;
            StartPosition=FormStartPosition.CenterParent;
            Padding=new Padding(20);

            var title=Ui.Label("首次使用",18,true);
            title.Dock=DockStyle.Top;
            title.Height=42;
            var text=Ui.Note("使用手册载有台站登记、卡片录入及单据打印步骤。");
            text.Dock=DockStyle.Top;
            text.Height=58;
            hide=new CheckBox{Name="hideStartupGuide",Text="下次不再提示",AutoSize=false,Dock=DockStyle.Top,Height=29,
                Font=Ui.Font(10,false),ForeColor=Ui.Ink,BackColor=Color.Transparent};

            var actions=Ui.Toolbar();
            actions.Dock=DockStyle.Bottom;
            var open=Ui.Button("查看使用手册",()=>{DialogResult=DialogResult.OK;Close();},true);
            var later=Ui.Button("暂不查看",()=>{DialogResult=DialogResult.Cancel;Close();},false);
            actions.Controls.Add(open);
            actions.Controls.Add(later);
            AcceptButton=open;
            CancelButton=later;
            Controls.Add(hide);
            Controls.Add(text);
            Controls.Add(title);
            Controls.Add(actions);
        }
    }
}
