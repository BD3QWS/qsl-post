using System;
using System.IO;
using System.Xml;

namespace QslPost
{
    internal sealed class WorkspacePaths
    {
        internal const string PreferencesFile="locations.xml",SessionFile="locations.lock";
        internal const string DatabaseFile="qsl-post.xml",DatabaseLock="workspace.lock",LegacyTrackingFile="17track-token.dat";
        internal readonly string Data,Cache;

        WorkspacePaths(string data,string cache)
        {
            Data=Folder(data);
            Cache=Folder(cache);
        }

        internal static WorkspacePaths Read(string preferences)
        {
            var document=new XmlDocument{XmlResolver=null};
            var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=32768};
            using(var input=XmlReader.Create(preferences,settings))document.Load(input);
            var root=document.DocumentElement;
            if(root==null||root.Name!="locations")throw new InvalidDataException("目录设置文件无法读取。");
            return new WorkspacePaths(root.GetAttribute("data"),root.GetAttribute("cache"));
        }

        internal static string Folder(string value)
        {
            if(String.IsNullOrWhiteSpace(value)||!Path.IsPathRooted(value.Trim()))
                throw new InvalidOperationException("请选择完整的文件夹路径。");
            string full=Path.GetFullPath(value.Trim()),root=Path.GetPathRoot(full);
            return full.Length==root.Length?root:full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        }
    }
}
