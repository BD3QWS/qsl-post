using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using QslPost;

static class ClearUserData
{
    const uint ReadAttributes=0x80,OpenExisting=3,BackupSemantics=0x02000000,OpenReparsePoint=0x00200000;
    [StructLayout(LayoutKind.Sequential)]
    struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;
        public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern SafeFileHandle CreateFile(string path,uint access,FileShare sharing,IntPtr security,uint creation,uint flags,IntPtr template);
    [DllImport("kernel32.dll",SetLastError=true)]
    static extern bool GetFileInformationByHandle(SafeFileHandle handle,out FileInformation info);

    static readonly Regex AutomaticBackup=new Regex(@"\A(?:qsl-post-\d{8}|before-restore-\d{8}-\d{6}-\d{3})\.xml\z",
        RegexOptions.CultureInvariant|RegexOptions.IgnoreCase);
    static readonly Regex PendingPreferences=new Regex(@"\Alocations\.xml\.[0-9a-f]{32}\.new\z",
        RegexOptions.CultureInvariant|RegexOptions.IgnoreCase);

    static int Main(string[] args)
    {
        if(args.Length!=1)return 2;
        try { Remove(WorkspacePaths.Folder(args[0]));return 0; }
        catch(Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    static void Remove(string root)
    {
        if(Path.GetDirectoryName(root)==null)throw new InvalidOperationException("资料设置目录不能是驱动器根目录。");
        if(!DirectoryAvailable(root))return;

        var data=new HashSet<string>(StringComparer.OrdinalIgnoreCase){Path.Combine(root,"Data"),Path.Combine(root,"Demo")};
        var caches=new HashSet<string>(StringComparer.OrdinalIgnoreCase){Path.Combine(root,"Cache"),Path.Combine(root,"Demo.cache")};
        var managedFolders=new List<string>(data);
        managedFolders.AddRange(caches);
        string preferences=Path.Combine(root,WorkspacePaths.PreferencesFile);
        string sessionFile=Path.Combine(root,WorkspacePaths.SessionFile);
        using(AcquireLock(sessionFile))
        {
            if(RegularFile(preferences))
            {
                var paths=WorkspacePaths.Read(preferences);
                data.Add(paths.Data);
                caches.Add(paths.Cache);
            }
            foreach(string path in data)CheckDrive(path);
            foreach(string path in caches)CheckDrive(path);

            var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var backupFolders=new List<string>();
            var cacheFolders=new List<string>();
            var locks=new List<FileStream>();
            try
            {
                foreach(string folder in data)
                {
                    if(!DirectoryAvailable(folder))continue;
                    var owned=new List<string>();
                    AddFile(owned,Path.Combine(folder,WorkspacePaths.DatabaseFile));
                    AddFile(owned,Path.Combine(folder,WorkspacePaths.DatabaseFile+".bak"));
                    AddFile(owned,Path.Combine(folder,WorkspacePaths.DatabaseFile+".new"));
                    AddFile(owned,Path.Combine(folder,WorkspacePaths.LegacyTrackingFile));

                    string backups=Path.Combine(folder,"backups");
                    if(DirectoryAvailable(backups))
                    {
                        foreach(string file in Directory.GetFiles(backups))
                            if(AutomaticBackup.IsMatch(Path.GetFileName(file)))AddFile(owned,file);
                        backupFolders.Add(backups);
                    }
                    string databaseLock=Path.Combine(folder,WorkspacePaths.DatabaseLock);
                    if(owned.Count>0||RegularFile(databaseLock))locks.Add(AcquireLock(databaseLock));
                    files.UnionWith(owned);
                }
                foreach(string cache in caches)
                    CollectCache(Path.Combine(cache,"17track"),files,cacheFolders);
                foreach(string file in Directory.GetFiles(root))
                    if(PendingPreferences.IsMatch(Path.GetFileName(file)))AddFile(files,file);

                // All known data locations and active writers are checked before deleting any records.
                foreach(string file in files)DeleteFile(file);
                foreach(string folder in cacheFolders)DeleteDirectory(folder,false);
                foreach(string folder in backupFolders)DeleteEmptyDirectory(folder);
            }
            finally { foreach(var lease in locks)lease.Dispose(); }
            foreach(var lease in locks)DeleteFile(lease.Name);
            DeleteFile(preferences);
        }
        DeleteFile(sessionFile);
        foreach(string folder in managedFolders)DeleteEmptyDirectory(folder);
        DeleteEmptyDirectory(root);
    }

    static void CollectCache(string directory,ICollection<string> files,ICollection<string> folders)
    {
        if(!DirectoryAvailable(directory))return;
        foreach(string file in Directory.GetFiles(directory))AddFile(files,file);
        foreach(string child in Directory.GetDirectories(directory))CollectCache(child,files,folders);
        folders.Add(directory);
    }

    static void CheckDrive(string path)
    {
        if(!DirectoryAvailable(Path.GetPathRoot(path)))
            throw new IOException("无法访问资料盘："+path);
    }

    static FileStream AcquireLock(string path)
    {
        FileStream lease=null;
        WithDirectory(Path.GetDirectoryName(path),()=>{
            RegularFile(path);
            lease=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        });
        if(lease==null)throw new DirectoryNotFoundException(path);
        return lease;
    }

    static void AddFile(ICollection<string> files,string path)
    {
        if(RegularFile(path))files.Add(path);
    }

    static void DeleteFile(string path)
    {
        WithDirectory(Path.GetDirectoryName(path),()=>{if(RegularFile(path))File.Delete(path);});
    }

    static void DeleteEmptyDirectory(string path)
    {
        DeleteDirectory(path,true);
    }

    static void DeleteDirectory(string path,bool onlyIfEmpty)
    {
        WithDirectory(Path.GetDirectoryName(path),()=>{
            if(DirectoryAvailable(path)&&(!onlyIfEmpty||Directory.GetFileSystemEntries(path).Length==0))Directory.Delete(path);
        });
    }

    static void WithDirectory(string path,Action action)
    {
        string parent=Path.GetDirectoryName(path);
        if(parent==null)
        {
            if(DirectoryAvailable(path))action();
            return;
        }
        WithDirectory(parent,()=>{
            // Keep the checked ancestors open without write or delete sharing throughout the operation.
            using(var handle=CreateFile(path,ReadAttributes,FileShare.Read,IntPtr.Zero,OpenExisting,BackupSemantics|OpenReparsePoint,IntPtr.Zero))
            {
                if(handle.IsInvalid)
                {
                    int error=Marshal.GetLastWin32Error();
                    if(error==2||error==3)return;
                    throw new Win32Exception(error,path);
                }
                FileInformation info;
                if(!GetFileInformationByHandle(handle,out info))throw new Win32Exception(Marshal.GetLastWin32Error(),path);
                var attributes=(FileAttributes)info.Attributes;
                if((attributes&FileAttributes.Directory)==0||(attributes&FileAttributes.ReparsePoint)!=0)
                    throw new IOException("无法清除链接目录中的资料："+path);
                action();
            }
        });
    }

    static bool DirectoryAvailable(string path)
    {
        string parent=Path.GetDirectoryName(path);
        if(parent!=null&&!DirectoryAvailable(parent))return false;
        var attributes=Attributes(path);
        if(!attributes.HasValue)return false;
        if((attributes.Value&FileAttributes.Directory)==0)throw new IOException("资料目录不是文件夹："+path);
        return true;
    }

    static bool RegularFile(string path)
    {
        var attributes=Attributes(path);
        if(!attributes.HasValue)return false;
        if((attributes.Value&FileAttributes.Directory)!=0)throw new IOException("资料文件的路径指向文件夹："+path);
        return true;
    }

    static FileAttributes? Attributes(string path)
    {
        FileAttributes attributes;
        try { attributes=File.GetAttributes(path); }
        catch(FileNotFoundException) { return null; }
        catch(DirectoryNotFoundException) { return null; }
        if((attributes&FileAttributes.ReparsePoint)!=0)
            throw new IOException("无法清除链接目录中的资料："+path);
        return attributes;
    }
}
