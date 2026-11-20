using System;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using QslPost;
class PrintDriverTest
{
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Print(args);
            if(args.Contains("--expect-size-rejection"))throw new Exception("The test expected the driver to substitute unsupported stock.");
            return 0;
        }
        catch(InvalidOperationException error)
        {
            Console.Error.WriteLine(error.Message);
            return args.Contains("--expect-size-rejection")&&error.Message.StartsWith("当前驱动使用")&&!File.Exists(args[1])?0:1;
        }
        catch(Exception error) { Console.Error.WriteLine(error);return 1; }
    }
    static void Print(string[] args)
    {
        using(var repo=new Repository(args[0]))
        using(var doc=new PrintDocument())
        {
            doc.PrinterSettings.PrinterName="Microsoft Print to PDF";
            if(!doc.PrinterSettings.IsValid)throw new Exception("Microsoft Print to PDF is unavailable; no other printer will be used.");
            doc.PrinterSettings.PrintToFile=true;doc.PrinterSettings.PrintFileName=Path.GetFullPath(args[1]);doc.PrinterSettings.Copies=1;doc.PrintController=new StandardPrintController();doc.DocumentName="QSL virtual driver verification";
            string key=args.Skip(2).FirstOrDefault(arg=>!arg.StartsWith("--"))??"cp85large";
            var template=Documents.Templates.Single(t=>t.Key==key);
            var context=new DocumentContext{Db=repo.Data,Bag=repo.Data.Bags.First()};
            var profile=new PrintProfile{Stock=args.Contains("--a4")?PrintStock.A4:PrintStock.Original,Margin=8};
            var pages=Documents.Compose(Documents.Build(template,context),profile,1);int index=0;
            PrintEngine.Configure(doc,pages[0]);
            foreach(var page in pages)PrintEngine.ValidatePrintableArea(page,doc.DefaultPageSettings);doc.PrintPage+=(s,e)=>{PrintEngine.DrawPage(e,pages[index++]);e.HasMorePages=index<pages.Count;};doc.Print();Console.WriteLine("Submitted only to Microsoft Print to PDF: "+doc.PrinterSettings.PrintFileName);
        }
    }
}
