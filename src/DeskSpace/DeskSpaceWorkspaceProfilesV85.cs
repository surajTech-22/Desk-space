using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// Named layout/pin configurations. Does not copy notes, reminders or passwords.
// Saving/applying affects only widget appearance and pinned applications.
internal sealed class DeskSpaceProfileV85
{
    public string Name {get;set;}="";
    public string CenterMode {get;set;}="Clock";
    public bool ShowWeather {get;set;}
    public string WeatherCity {get;set;}="";
    public List<string> PinnedApps {get;set;}=new();
}

internal static class DeskSpaceWorkspaceProfilesV85
{
    private static readonly object Gate=new();
    internal static string Filename=>Path.Combine(
        WorkspaceStore.Root,"widget-profiles.json");

    internal static IReadOnlyList<DeskSpaceProfileV85> Load()
    {
        lock(Gate)
        {
            try
            {
                if(!File.Exists(Filename))return Array.Empty<DeskSpaceProfileV85>();
                return JsonSerializer.Deserialize<List<DeskSpaceProfileV85>>(
                    File.ReadAllText(Filename))?.Where(x=>x!=null &&
                    !string.IsNullOrWhiteSpace(x.Name)).Take(24).ToArray() ??
                    Array.Empty<DeskSpaceProfileV85>();
            }
            catch(Exception ex) when(ex is IOException or JsonException or
                UnauthorizedAccessException)
            {System.Diagnostics.Debug.WriteLine("DeskSpace profile: "+ex);
                return Array.Empty<DeskSpaceProfileV85>();}
        }
    }
    internal static void Save(DeskSpaceProfileV85 profile)
    {
        if(string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length>48)
            throw new ArgumentException("Profile name must be 1–48 characters");
        lock(Gate)
        {
            var profiles=Load().ToList();
            profiles.RemoveAll(x=>x.Name.Equals(profile.Name,
                StringComparison.OrdinalIgnoreCase));
            profiles.Insert(0,profile);
            if(profiles.Count>24)profiles.RemoveRange(24,profiles.Count-24);
            Directory.CreateDirectory(WorkspaceStore.Root);
            var temp=Filename+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(temp,JsonSerializer.Serialize(profiles));
                File.Move(temp,Filename,true);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
    // Import profiles without applying a single setting. Existing profile
    // names are retained, and the same data lock as Save() is used.
    internal static int ImportNewProfilesV201(
        IReadOnlyList<DeskSpaceProfileV85> incoming)
    {
        if(incoming==null || incoming.Count>24)
            throw new FormatException("Invalid profile import count.");
        foreach(var profile in incoming)
            DeskSpaceProfilePortabilityV201.Validate(profile);
        if(incoming.Select(x=>x.Name).Distinct(
            StringComparer.OrdinalIgnoreCase).Count()!=incoming.Count)
            throw new FormatException("Duplicate imported profile names.");
        lock(Gate)
        {
            var current=Load().ToList();
            var known=new HashSet<string>(current.Select(x=>x.Name),
                StringComparer.OrdinalIgnoreCase);
            var fresh=incoming.Where(x=>!known.Contains(x.Name)).ToList();
            if(fresh.Count==0)return 0;
            if(current.Count+fresh.Count>24)
                throw new FormatException("Profile limit is 24; no changes made.");
            Directory.CreateDirectory(WorkspaceStore.Root);
            var temp=Filename+"."+Guid.NewGuid().ToString("N")+".partial";
            try
            {
                File.WriteAllText(temp,
                    JsonSerializer.Serialize(current.Concat(fresh).ToList()));
                // Keep a separate recovery copy of the previous profile
                // database. This never touches Notes, Excel or secrets.
                if(File.Exists(Filename))
                {
                    var backup=Filename+".before-import-"+
                        DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+
                        Guid.NewGuid().ToString("N")+".bak";
                    File.Copy(Filename,backup,false);
                }
                File.Move(temp,Filename,true);
                return fresh.Count;
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
    internal static DeskSpaceProfileV85 Capture(string name,WidgetSettingsV3 source)
        =>new(){
            Name=name.Trim(),
            CenterMode=source.CenterMode,
            ShowWeather=source.ShowWeather,
            WeatherCity=source.WeatherCity,
            PinnedApps=source.PinnedApps?.ToList()??new List<string>()
        };
    internal static void Apply(DeskSpaceProfileV85 profile,WidgetSettingsV3 target)
    {
        // Do not touch StartWithWindows, passwords, account info,
        // weather permission, or any workspace.json contents.
        target.CenterMode=profile.CenterMode;
        target.ShowWeather=profile.ShowWeather;
        target.WeatherCity=profile.WeatherCity;
        target.PinnedApps ??= new List<string>();
        target.PinnedApps.Clear();
        foreach(var app in (profile.PinnedApps??new List<string>()).Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(40))
            target.PinnedApps.Add(app);
        target.Save();
    }
    private static string? AskName(Window owner)
    {
        var prompt=new Window{Title="Save DeskSpace layout",
            Width=315,Height=160,ShowInTaskbar=false,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,
            Owner=owner,ResizeMode=ResizeMode.NoResize,Topmost=true};
        var stack=new StackPanel{Margin=new Thickness(14)};
        stack.Children.Add(new TextBlock{Text="Workspace profile name"});
        var input=new TextBox{Text="Accounting",Margin=new Thickness(0,8,0,8)};
        stack.Children.Add(input);
        var save=new Button{Content="Save profile",Width=112,
            HorizontalAlignment=HorizontalAlignment.Right};
        save.Click+=(_,_)=>{
            if(!string.IsNullOrWhiteSpace(input.Text) && input.Text.Length<=48)
            {prompt.DialogResult=true;prompt.Close();}
        };
        stack.Children.Add(save);prompt.Content=stack;
        return prompt.ShowDialog()==true?input.Text.Trim():null;
    }
    internal static void AddMenu(Forms.ContextMenuStrip menu,Window owner,
        WidgetSettingsV3 prefs,Action refresh)
    {
        var root=new Forms.ToolStripMenuItem("Workspace profiles");
        menu.Items.Add(root);
        root.DropDownOpening+=(_,_)=>{
            root.DropDownItems.Clear();
            root.DropDownItems.Add("Save current layout as…",null,(_,_)=>{
                owner.Dispatcher.Invoke(()=>{
                    var name=AskName(owner);
                    if(string.IsNullOrWhiteSpace(name))return;
                    try
                    {
                        bool existing=Load().Any(x=>x.Name.Equals(name,
                            StringComparison.OrdinalIgnoreCase));
                        if(existing && MessageBox.Show(owner,
                            "Replace saved layout '"+name+"'?",
                            "DeskSpace",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)
                            return;
                        Save(Capture(name,prefs));
                    }
                    catch(Exception error)
                    {MessageBox.Show(owner,error.Message,"DeskSpace profiles");}
                });
            });
            foreach(var profile in Load())
            {
                var chosen=profile;
                root.DropDownItems.Add(chosen.Name,null,(_,_)=>{
                    owner.Dispatcher.Invoke(()=>{
                        try{Apply(chosen,prefs);refresh();}
                        catch(Exception error)
                        {MessageBox.Show(owner,error.Message,"DeskSpace profiles");}
                    });
                });
            }
        };
    }
}
