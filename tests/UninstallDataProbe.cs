using System;
using System.IO;
using QslPost;

class UninstallDataProbe
{
    static int Main(string[] args)
    {
        using(var storage=StorageLocations.Load(args[1]))
        using(var repository=new Repository(storage.DataDirectory))
        {
            if(args[0]=="empty")
            {
                var settings=repository.Data.Settings;
                return repository.Data.Cards.Count==0&&repository.Data.Contacts.Count==0&&repository.Data.Bags.Count==0&&
                    settings.Organization.Length==0&&!settings.Initialized&&settings.PrintProfiles.Count==0?0:1;
            }

            repository.AddCard(new Card{FromCall="TEST/A",ToCall="TEST/B",QsoUtc="2026-11-20 08:00"});
            repository.Change(database=>{
                database.Settings.Organization="Installer test station";
                database.Settings.Initialized=true;
                database.Settings.PrintProfiles.Add(new PrintProfile{Template="cp85large"});
            });
            repository.Backup(args[2]);
            repository.Restore(args[2]);
            if(args.Length==5)storage.Change(repository,args[3],args[4]);
            Directory.CreateDirectory(Path.Combine(storage.CacheDirectory,"17track","Profile"));
            File.WriteAllText(Path.Combine(storage.CacheDirectory,"17track","Profile","query-cache.txt"),"Synthetic query cache.");
            return 0;
        }
    }
}
