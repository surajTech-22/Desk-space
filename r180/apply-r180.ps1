$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceWindowLayoutsV179.cs'
$nl=[string][char]10;$crlf=[string][char]13+$nl
$s=[IO.File]::ReadAllText($p).Replace($crlf,$nl)
function Swap([string]$before,[string]$after,[string]$why){
 $before=$before.Replace($crlf,$nl);$after=$after.Replace($crlf,$nl)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw "R180 $why: expected one anchor, got $n"}
 $script:s=$script:s.Replace($before,$after)
}
$methods=@'
    // Portable export is opt-in and intentionally excludes real window titles,
    // document paths and workspace content. Import is validated before any save.
    internal static string ExportProfile(Profile profile)
    {
        if(!ValidProfile(profile))
            throw new ArgumentException("Invalid layout profile.");
        return JsonSerializer.Serialize(profile,new JsonSerializerOptions{
            WriteIndented=true});
    }
    internal static Profile ImportProfile(string json)
    {
        if(string.IsNullOrWhiteSpace(json)||json.Length>131072)
            throw new ArgumentException("Invalid profile size.");
        Profile? profile;
        try{profile=JsonSerializer.Deserialize<Profile>(json);}
        catch(JsonException){throw new ArgumentException("Invalid profile JSON.");}
        if(profile==null||!ValidProfile(profile))
            throw new ArgumentException("Profile coordinates or labels are invalid.");
        return profile;
    }
    internal static bool ValidProfile(Profile? profile)
    {
        if(profile==null||!ValidName(profile.Name)||
           profile.Windows==null||profile.Windows.Count>32)return false;
        return profile.Windows.All(e=>
            e!=null && e.Process.Length is >=1 and <=128 &&
            e.Process.All(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'||c=='.') &&
            e.TitleHash.Length==64 &&
            e.TitleHash.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f') &&
            e.Instance is >=0 and <=31 && e.Monitor.Length is >=1 and <=128 &&
            double.IsFinite(e.X)&&double.IsFinite(e.Y) &&
            double.IsFinite(e.W)&&double.IsFinite(e.H) &&
            e.X is >=-10 and <=10 && e.Y is >=-10 and <=10 &&
            e.W is >0 and <=10 && e.H is >0 and <=10);
    }
    internal static bool DeleteProfile(string name)
    {
        if(!ValidName(name))return false;
        var current=Load();
        if(current.RemoveAll(p=>p.Name.Equals(name,
            StringComparison.OrdinalIgnoreCase))==0)return false;
        Persist(current);
        return true;
    }
    private static void Persist(List<Profile> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Storage)!);
        var tmp=Storage+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(tmp,JsonSerializer.Serialize(profiles),
                new UTF8Encoding(false));
            File.Move(tmp,Storage,true);
        }
        finally
        {
            try{if(File.Exists(tmp))File.Delete(tmp);}
            catch(IOException){}
        }
    }
'@
Swap '    internal static Window Create(Window owner)' ($methods+'    internal static Window Create(Window owner)') 'portable profile validation and atomic persistence'
Swap @'
        var buttons=new StackPanel{Orientation=Orientation.Horizontal};
        buttons.Children.Add(capture);buttons.Children.Add(restore);
'@ @'
        var export=new Button{Content="Export selected...",
            Padding=new Thickness(8,6,8,6),
            Margin=new Thickness(6,0,0,0)};
        export.Click+=(_,_)=>{
            if(pick.SelectedItem is not string selected)return;
            var profile=Load().FirstOrDefault(p=>p.Name==selected);
            if(profile==null)return;
            var dialog=new Microsoft.Win32.SaveFileDialog{
                Title="Export non-sensitive window layout",
                FileName="DeskSpace-layout-"+selected+".json",
                Filter="JSON layout (*.json)|*.json",DefaultExt=".json",
                AddExtension=true,OverwritePrompt=true,
                InitialDirectory=Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),"Downloads")
            };
            if(dialog.ShowDialog()==true)
                try{
                    File.WriteAllText(dialog.FileName,ExportProfile(profile),
                        new UTF8Encoding(false));
                    status.Text="Exported positions and hashed titles only.";
                }
                catch(Exception ex)when(ex is IOException or UnauthorizedAccessException)
                {status.Text="Export failed ("+ex.GetType().Name+").";}
        };
        var import=new Button{Content="Import...",
            Padding=new Thickness(8,6,8,6),
            Margin=new Thickness(6,0,0,0)};
        import.Click+=(_,_)=>{
            var dialog=new Microsoft.Win32.OpenFileDialog{
                Title="Import window layout",Filter="JSON layout (*.json)|*.json",
                CheckFileExists=true,Multiselect=false};
            if(dialog.ShowDialog()!=true)return;
            try
            {
                if(new FileInfo(dialog.FileName).Length>131072)
                    throw new ArgumentException("File too large.");
                var profile=ImportProfile(File.ReadAllText(dialog.FileName));
                if(Load().Any(p=>p.Name.Equals(profile.Name,
                    StringComparison.OrdinalIgnoreCase)) &&
                    MessageBox.Show("Replace the saved layout "+profile.Name+"?",
                        "DeskSpace layouts",MessageBoxButton.YesNo,
                        MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
                Save(profile);Refresh();pick.SelectedItem=profile.Name;
                status.Text="Imported "+profile.Windows.Count+
                    " window positions; none were moved.";
            }
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
                or ArgumentException)
            {status.Text="Import rejected ("+ex.GetType().Name+").";}
        };
        var remove=new Button{Content="Delete",
            Padding=new Thickness(8,6,8,6),
            Margin=new Thickness(6,0,0,0)};
        remove.Click+=(_,_)=>{
            if(pick.SelectedItem is not string selected)return;
            if(MessageBox.Show("Delete the saved layout "+selected+"?",
                "DeskSpace layouts",MessageBoxButton.YesNo,
                MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            DeleteProfile(selected);Refresh();
            status.Text="Saved layout removed; application windows unchanged.";
        };
        var buttons=new WrapPanel{Orientation=Orientation.Horizontal};
        buttons.Children.Add(capture);buttons.Children.Add(restore);
        buttons.Children.Add(export);buttons.Children.Add(import);
        buttons.Children.Add(remove);
'@ 'safe import export delete controls'
Swap '            Width=485,Height=370,MinWidth=395,MinHeight=330,' '            Width=600,Height=415,MinWidth=475,MinHeight=340,' 'profile panel width for new controls'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R180: safe portable window layouts, schema validation, explicit import/export and deletion.'
