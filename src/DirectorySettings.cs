using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace QslPost
{
    internal sealed class DirectorySettings : GlassForm
    {
        public DirectorySettings(Repository repository,StorageLocations locations)
        {
            Text="目录设置";Width=720;Height=365;MinimumSize=new Size(560,355);
            StartPosition=FormStartPosition.CenterParent;MinimizeBox=false;ShowInTaskbar=false;Padding=new Padding(24);
            var heading=Ui.Label("目录设置",18,true);heading.Dock=DockStyle.Top;heading.Height=44;Controls.Add(heading);
            var main=new SurfaceTablePanel{Dock=DockStyle.Fill,RowCount=5,ColumnCount=2,BackColor=Color.Transparent};
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,107));
            foreach(int height in new[]{25,37,36,25,37})main.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            var data=DirectoryField(main,0,"资料目录","newDataDirectory",locations.DataDirectory);
            var cache=DirectoryField(main,3,"查询缓存目录","newCacheDirectory",locations.CacheDirectory);
            var hint=Ui.Note("更改资料目录时会复制资料和备份，旧目录可留作备用。");
            hint.Dock=DockStyle.Fill;main.Controls.Add(hint,0,2);main.SetColumnSpan(hint,2);
            Controls.Add(main);main.BringToFront();
            var buttons=new SurfaceFlowPanel{Dock=DockStyle.Bottom,Height=45,FlowDirection=FlowDirection.RightToLeft,BackColor=Color.Transparent};
            var save=Ui.Button("保存",()=>{
                UseWaitCursor=true;
                try {locations.Change(repository,data.Text,cache.Text);DialogResult=DialogResult.OK;Close();}
                finally {UseWaitCursor=false;}
            },true);
            var cancel=Ui.Button("取消",()=>{DialogResult=DialogResult.Cancel;Close();},false);
            buttons.Controls.Add(save);buttons.Controls.Add(cancel);Controls.Add(buttons);main.BringToFront();
            AcceptButton=save;CancelButton=cancel;
        }

        TextBox DirectoryField(TableLayoutPanel table,int row,string label,string name,string current)
        {
            var title=Ui.Label(label,10,true);title.Dock=DockStyle.Fill;table.Controls.Add(title,0,row);
            var input=Ui.Input(current);input.Name=name;input.AccessibleName=label;input.Dock=DockStyle.Fill;
            input.Margin=new Padding(0,5,9,0);table.Controls.Add(input,0,row+1);
            var select=Ui.Button("浏览…",()=>{
                using(var picker=new FolderBrowserDialog{Description=label,ShowNewFolderButton=true})
                {
                    if(Directory.Exists(input.Text))picker.SelectedPath=input.Text;
                    if(picker.ShowDialog(this)==DialogResult.OK)input.Text=picker.SelectedPath;
                }
            },false);
            select.Height=30;table.Controls.Add(select,1,row+1);
            return input;
        }
    }
}
