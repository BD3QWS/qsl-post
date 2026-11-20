using System;
using System.IO;
using System.Xml;

namespace QslPost
{
    public sealed class StorageLocations : IDisposable
    {
        readonly string preferences;
        readonly FileStream sessionLock;
        public string DataDirectory { get; private set; }
        public string CacheDirectory { get; private set; }

        StorageLocations(string data,string cache,string config,FileStream session)
        {
            DataDirectory=WorkspacePaths.Folder(data);CacheDirectory=WorkspacePaths.Folder(cache);preferences=config;sessionLock=session;
        }

        public static StorageLocations Session(string data,string cache=null)
        {
            string location=WorkspacePaths.Folder(data);
            return new StorageLocations(location,cache??(location+".cache"),null,null);
        }

        public static StorageLocations Load(string appDirectory)
        {
            Directory.CreateDirectory(appDirectory);
            string config=Path.Combine(appDirectory,WorkspacePaths.PreferencesFile);
            FileStream session;
            try
            {
                session=new FileStream(Path.Combine(appDirectory,WorkspacePaths.SessionFile),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            }
            catch(IOException error){throw new InvalidOperationException("QSL 邮务台已打开。",error);}
            try
            {
                if(!File.Exists(config))return new StorageLocations(Path.Combine(appDirectory,"Data"),Path.Combine(appDirectory,"Cache"),config,session);
                var paths=WorkspacePaths.Read(config);
                string data=paths.Data,cache=paths.Cache;
                if(!File.Exists(Path.Combine(data,WorkspacePaths.DatabaseFile)))
                    throw new DirectoryNotFoundException("找不到资料库："+data+"。请接回存放资料的磁盘后重试。");
                return new StorageLocations(data,cache,config,session);
            }
            catch {session.Dispose();throw;}
        }

        public void Change(Repository repository,string data,string cache)
        {
            data=WorkspacePaths.Folder(data);cache=WorkspacePaths.Folder(cache);
            if(!Same(repository.Folder,DataDirectory))throw new InvalidOperationException("资料库目录已变更，请重新打开目录设置。");
            if(Contains(data,cache)||Contains(cache,data))
                throw new InvalidOperationException("请为资料和查询缓存选择两个独立的文件夹。");
            if(Contains(repository.Folder,cache)||Contains(cache,repository.Folder))
                throw new InvalidOperationException("查询缓存请放在原资料目录之外。");
            Directory.CreateDirectory(cache);
            string probe=Path.Combine(cache,".write-"+Guid.NewGuid().ToString("N"));
            using(var file=new FileStream(probe,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.DeleteOnClose))
                file.WriteByte(1);
            if(Same(data,DataDirectory))Save(data,cache);
            else repository.Relocate(data,()=>Save(data,cache));
            DataDirectory=data;CacheDirectory=cache;
        }

        void Save(string data,string cache)
        {
            if(preferences==null)return;
            string temporary=preferences+"."+Guid.NewGuid().ToString("N")+".new";
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    using(var xml=XmlWriter.Create(stream,new XmlWriterSettings{CloseOutput=false,Indent=true}))
                    {
                        xml.WriteStartElement("locations");xml.WriteAttributeString("data",data);xml.WriteAttributeString("cache",cache);xml.WriteEndElement();
                    }
                    stream.Flush(true);
                }
                if(File.Exists(preferences))File.Replace(temporary,preferences,null);
                else File.Move(temporary,preferences);
            }
            finally { if(File.Exists(temporary))File.Delete(temporary); }
        }

        internal static bool Same(string a,string b) { return a.Equals(b,StringComparison.OrdinalIgnoreCase); }
        internal static bool Contains(string parent,string child)
        {
            string prefix=parent.EndsWith(Path.DirectorySeparatorChar.ToString())?parent:parent+Path.DirectorySeparatorChar;
            return Same(parent,child)||child.StartsWith(prefix,StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose() {if(sessionLock!=null)sessionLock.Dispose();}
    }
}
