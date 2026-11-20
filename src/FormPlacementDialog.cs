using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace QslPost
{
    internal sealed class FormPlacementDialog : GlassForm
    {
        readonly DocumentContext context,working;
        readonly FormTemplate face;
        readonly PagePreview preview;
        readonly ComboBox choices;
        readonly TextBox value;
        readonly List<Field> fields;
        Point anchor;
        bool dragging,changing;

        public FormPlacementDialog(FormTemplate face,DocumentContext context)
        {
            this.face=face;
            this.context=context;
            working=new DocumentContext{Db=context.Db,Card=context.Card,Bag=context.Bag,Case=context.Case,BlankSource=context.BlankSource,
                Fields=new Dictionary<string,string>(context.Fields),
                Placements=context.Placements.ToDictionary(pair=>pair.Key,pair=>new List<FormSlot>(pair.Value))};
            fields=OriginalForms.Fields(face,working);
            string extra="FreeText."+face.Key;
            if(fields.All(f=>f.Key!=extra))fields.Add(new Field(extra,"附加文字",OriginalForms.ReadValue(working,extra)));

            Text="设置填报位置 · "+face.Title;
            Size=new Size(1120,780);
            MinimumSize=new Size(800,600);
            Padding=new Padding(18);
            StartPosition=FormStartPosition.CenterParent;
            var left=new GlassPanel{Dock=DockStyle.Left,Width=300,Padding=new Padding(15)};
            Controls.Add(left);
            var title=Ui.Label("字段定位",17,true);title.Dock=DockStyle.Top;title.Height=35;
            var help=Ui.Note("选择字段并录入内容。在预览内的空白栏拖出一个矩形，软件会将文字填入该栏。");
            help.Dock=DockStyle.Top;help.Height=78;
            choices=Ui.Combo(fields.Select(f=>f.Label));choices.Dock=DockStyle.Top;choices.Name="placementField";choices.AccessibleName="待定位字段";
            value=Ui.Input("");value.Name="placementValue";value.AccessibleName="字段内容";value.Multiline=true;value.ScrollBars=ScrollBars.Vertical;
            value.Dock=DockStyle.Top;value.Height=100;
            var apply=Ui.Button("应用位置",Apply,true);apply.Dock=DockStyle.Bottom;apply.DialogResult=DialogResult.None;
            var cancel=Ui.Button("取消",()=>{DialogResult=DialogResult.Cancel;Close();},false);cancel.Dock=DockStyle.Bottom;
            left.Controls.Add(value);left.Controls.Add(choices);left.Controls.Add(help);left.Controls.Add(title);
            left.Controls.Add(cancel);left.Controls.Add(apply);
            CancelButton=cancel;
            preview=new PagePreview{Dock=DockStyle.Fill,Cursor=Cursors.Cross};
            Controls.Add(preview);preview.BringToFront();
            choices.SelectedIndexChanged+=(s,e)=>ReadField();
            value.TextChanged+=(s,e)=>{
                if(!changing&&choices.SelectedIndex>=0)working.Fields[fields[choices.SelectedIndex].Key]=value.Text.Trim();
            };
            preview.MouseDown+=(s,e)=>{
                if(e.Button!=MouseButtons.Left||!preview.PaperBounds.Contains(e.Location))return;
                anchor=e.Location;dragging=true;preview.Capture=true;
            };
            preview.MouseMove+=(s,e)=>{if(dragging)Select(e.Location);};
            preview.MouseUp+=(s,e)=>{if(dragging){Select(e.Location);Commit();}};
            ReadField();
            Shown+=(s,e)=>RefreshPage();
        }

        void ReadField()
        {
            if(choices.SelectedIndex<0)return;
            changing=true;
            value.Text=OriginalForms.ReadValue(working,fields[choices.SelectedIndex].Key);
            changing=false;
        }

        void Select(Point end)
        {
            var p=preview.PaperBounds;
            int x=Math.Max(p.Left,Math.Min(end.X,p.Right)),y=Math.Max(p.Top,Math.Min(end.Y,p.Bottom));
            preview.SelectionArea=Rectangle.FromLTRB(Math.Min(x,anchor.X),Math.Min(y,anchor.Y),Math.Max(x,anchor.X),Math.Max(y,anchor.Y));
            preview.Invalidate();
        }

        void Commit()
        {
            dragging=false;preview.Capture=false;
            var selected=preview.SelectionArea;
            preview.SelectionArea=Rectangle.Empty;
            if(selected.Width<7||selected.Height<7){preview.Invalidate();return;}
            var paper=preview.PaperBounds;
            float mmX=face.Width/paper.Width,mmY=face.Height/paper.Height;
            var field=fields[choices.SelectedIndex];
            var slot=new FormSlot{Key=field.Key,Label=field.Label,PointSize=9,
                Area=new RectangleF((selected.X-paper.X)*mmX,(selected.Y-paper.Y)*mmY,selected.Width*mmX,selected.Height*mmY)};
            List<FormSlot> positions;
            if(!working.Placements.TryGetValue(face.Key,out positions))working.Placements.Add(face.Key,positions=new List<FormSlot>());
            positions.RemoveAll(p=>p.Key==field.Key);
            positions.Add(slot);
            RefreshPage();
        }

        void RefreshPage()
        {
            List<DocumentPage> pages;
            if(OriginalForms.TryBuild(face,working,out pages))preview.ShowPage(pages[0]);
        }

        void Apply()
        {
            context.Fields=working.Fields;
            context.Placements=working.Placements;
            DialogResult=DialogResult.OK;
            Close();
        }
    }
}
