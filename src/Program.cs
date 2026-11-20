using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

[assembly:AssemblyTitle("QSL 邮务台")]
[assembly:AssemblyProduct("QSL 邮务台")]
[assembly:AssemblyDescription("QSL 卡片收发、封发与追溯管理系统")]
[assembly:AssemblyVersion("1.4.4.0")]
[assembly:AssemblyFileVersion("1.4.4.0")]
[assembly:AssemblyInformationalVersion("1.4.4")]
namespace QslPost
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException+=(s,e)=>MessageBox.Show(e.Exception.Message,"QSL 邮务台 · 操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Information);
            if(args.Contains("--manual")||(args.Contains("--demo")&&!args.Contains("--data")))
            {
                using(var guide=new ManualDialog{StartPosition=FormStartPosition.CenterScreen})Application.Run(guide);
                return;
            }
            bool demo=args.Contains("--demo")||args.Contains("--qa");
            string appDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QslPost");
            int at=Array.IndexOf(args,"--data");bool isolated=at>=0&&at+1<args.Length;
            try
            {
                string folder=isolated?args[at+1]:Path.Combine(appDirectory,demo?"Demo":"Data");
                using(var locations=demo||isolated?StorageLocations.Session(folder):StorageLocations.Load(appDirectory))
                {
                    folder=locations.DataDirectory;
                    using(var repo=new Repository(folder))
                    {
                    if(demo&&repo.Data.Cards.Count==0)DemoData.Create(repo);
                    using(var main=new MainForm(repo,demo,locations))
                    {
                        int qa=Array.IndexOf(args,"--qa");if(qa>=0&&qa+1<args.Length)
                        {
                            string output=Path.GetFullPath(args[qa+1]);
                            bool capture=!args.Contains("--qa-no-screens");
                            if(capture){main.Show();Application.DoEvents();main.CaptureScreens(Path.Combine(output,"screens"));}
                            Directory.CreateDirectory(Path.Combine(output,"forms"));var all=new System.Collections.Generic.List<DocumentPage>();
                            foreach(var t in Documents.Templates)
                            {
                                var context=new DocumentContext{Db=repo.Data,Bag=repo.Data.Bags.First()};
                                context.Case=t.Group=="查验与答复"?repo.Data.Cases.First():null;context.Card=t.Key=="cardlabel"?repo.Data.Cards.First():context.Case==null?null:repo.Data.Cards.FirstOrDefault(c=>c.Id==context.Case.CardId);
                                var upu=t as UpuTemplate;
                                if(upu!=null)
                                {
                                    if(upu.Layout=="notice"){context.Case=repo.Data.Cases.First();context.Card=repo.Data.Cards.First();}
                                    if(upu.Layout=="item"){context.Bag=null;context.Card=repo.Data.Cards.First();}
                                    if(upu.RowSource=="cards"||upu.RowSource=="contents")context.Bag=repo.Data.Bags.Last();
                                }
                                if(t.Key=="domempty")context.Fields["EmptyCount"]="1";
                                var pages=Documents.Build(t,context);PdfExport.Write(Path.Combine(output,"forms",t.Key+".pdf"),pages);using(var image=pages[0].Render(140))image.Save(Path.Combine(output,"forms",t.Key+".png"));
                                all.AddRange(Documents.Compose(pages,new PrintProfile(),1));
                            }
                            PdfExport.Write(Path.Combine(output,"QSL邮务台-内置单据样张.pdf"),all);
                            if(capture)
                                using(var studio=new PrintStudio(repo,new DocumentContext{Db=repo.Data,Bag=repo.Data.Bags.First()},"cp85large")){studio.Show();Application.DoEvents();studio.CaptureScreen(Path.Combine(output,"screens","打印工作室.png"));studio.Close();}
                            File.WriteAllText(Path.Combine(output,"qa-complete.txt"),"Rendered "+Documents.Templates.Count+" templates; "+all.Count+" native-size pages from the demo database.");main.Close();return;
                        }
                        Application.Run(main);
                    }
                    }
                }
            }
            catch(Exception ex)
            {
                int qa=Array.IndexOf(args,"--qa");
                if(qa>=0&&qa+1<args.Length)
                {
                    Directory.CreateDirectory(args[qa+1]);File.WriteAllText(Path.Combine(args[qa+1],"qa-error.txt"),ex.ToString());
                }
                else MessageBox.Show(ex.Message,"QSL 邮务台",MessageBoxButtons.OK,MessageBoxIcon.Error);
                Environment.ExitCode=1;
            }
        }
    }
}

