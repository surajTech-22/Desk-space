$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
function ReadSource([string]$p){[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)}
function WriteSource([string]$p,[string]$s){[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))}
function Swap([string]$a,[string]$b,[string]$label)
{
 $a=$a.Replace([string][char]13+[char]10,[string][char]10)
 $b=$b.Replace([string][char]13+[char]10,[string][char]10)
 $n=([regex]::Matches($script:source,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R85 $label anchor $n"}
 $script:source=$script:source.Replace($a,$b)
}
Copy-Item 'r85/DeskSpaceWorkspaceProfilesV85.cs' "$r/DeskSpaceWorkspaceProfilesV85.cs" -Force
Copy-Item 'r85/DeskSpaceFilePreviewV85.cs' "$r/DeskSpaceFilePreviewV85.cs" -Force
Copy-Item 'r85/DeskSpaceCommandShortcutV85.cs' "$r/DeskSpaceCommandShortcutV85.cs" -Force
$p="$r/WidgetHost.cs";$source=ReadSource $p
Swap '    private DeskSpaceDueShelfV74? dueShelf;' @'
    private DeskSpaceDueShelfV74? dueShelf;
    private DeskSpaceCommandShortcutV85? commandShortcut;
'@ 'shortcut field'
Swap '            DeskSpaceStartupV82.EnableOnFirstLaunch(prefs);' @'
            DeskSpaceStartupV82.EnableOnFirstLaunch(prefs);
            // Ctrl+Alt+Space opens the EXISTING universal inline Search.
            commandShortcut ??= new DeskSpaceCommandShortcutV85(
                this,()=>OpenInWidget("Search"));
'@ 'shortcut startup'
Swap @'
        menu.Items.Add("Watch folder for saved files...",null,(_,_)=>Dispatcher.Invoke(
            DeskSpaceDropShelfV15.SelectWatchedFolder));
'@ @'
        menu.Items.Add("Watch folder for saved files...",null,(_,_)=>Dispatcher.Invoke(
            DeskSpaceDropShelfV15.SelectWatchedFolder));
        DeskSpaceWorkspaceProfilesV85.AddMenu(menu,this,prefs,()=>{
            if(activeModule==null && !ringExpanded)BuildCompact();
            else UpdateCenter();
        });
        menu.Items.Add("Universal command search · Ctrl+Alt+Space",
            null,(_,_)=>Dispatcher.Invoke(()=>OpenInWidget("Search")));
'@ 'profile menu'
WriteSource $p $source

$p="$r/DeskSpaceDropShelfV15.cs";$source=ReadSource $p
Swap @'
            var copy=new Button{Content="⧉",FontSize=13,MinWidth=25,Padding=new Thickness(1),
                ToolTip="Copy this file for pasting"};
'@ @'
            var previewFile=new Button{Content="◉",FontSize=12,MinWidth=25,
                Padding=new Thickness(1),ToolTip="Preview this file without opening it"};
            DeskSpaceFilePreviewV85.Attach(previewFile,item.StagedPath);
            DockPanel.SetDock(previewFile,Dock.Right);
            content.Children.Add(previewFile);
            var copy=new Button{Content="⧉",FontSize=13,MinWidth=25,Padding=new Thickness(1),
                ToolTip="Copy this file for pasting"};
'@ 'file preview in below-circle shelf'
WriteSource $p $source

$p="$r/DeskSpaceInlineV36.cs";$source=ReadSource $p
Swap @'
        actions.Children.Add(Button("Clear history",()=>{
            DeskSpaceClipboardV10.ClearHistory();Reload();
        }));
'@ @'
        actions.Children.Add(Button("Clear history",()=>{
            DeskSpaceClipboardV10.ClearHistory();Reload();
        }));
        actions.Children.Add(Button("Quick actions",()=>{
            string clip=history.SelectedItem is DeskSpaceClipboardV10.ClipboardEntry entry?
                entry.Text:(Clipboard.ContainsText()?Clipboard.GetText():"");
            if(!string.IsNullOrWhiteSpace(clip))
                DeskSpaceClipboardQuickActionsV85.Open(clip);
        }));
'@ 'explicit clipboard smart actions'
WriteSource $p $source
Write-Host 'R85: saved widget profiles, Ctrl+Alt+Space universal Search, preview on recent files, clipboard URL/file/date/note quick commands.'
