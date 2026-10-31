using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DeskSpace;

// R199: permanent visible entry to working DeskSpace features from the
// main Workspace, not just the background widget tray. No duplicate data
// store, no new process, no background scan and no silent modifications.
public partial class MainWindow
{
    private bool featureHubV199Initialized;
    private TabItem? featureHubV199Tab;

    private void InitializeFeatureHubV199()
    {
        if(featureHubV199Initialized)return;
        featureHubV199Initialized=true;
        var tab=new TabItem
        {
            Header="✦  Features",
            Tag="DeskSpaceFeatures",
            ToolTip="Open DeskSpace tools in one place"
        };
        AutomationProperties.SetName(tab,"DeskSpace Features");
        var outer=new ScrollViewer
        {
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled
        };
        var body=new StackPanel
        {
            Margin=new Thickness(18),
            MaxWidth=900
        };
        outer.Content=body;
        body.Children.Add(new TextBlock
        {
            Text="DeskSpace features",
            FontSize=23,
            FontWeight=FontWeights.SemiBold,
            Foreground=new SolidColorBrush(Color.FromRgb(42,32,76)),
            Margin=new Thickness(0,0,0,6)
        });
        string version=Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? "not reported";
        body.Children.Add(new TextBlock
        {
            Text="Installed version: "+version+"   ·   Works in Workspace and widget",
            FontSize=12,
            Foreground=Brushes.DimGray,
            Margin=new Thickness(0,0,0,14)
        });
        body.Children.Add(new TextBlock
        {
            Text="Every button below opens an existing DeskSpace tool. "+
                 "No command runs until you select it. Press Ctrl+K in "+
                 "Workspace to open the full searchable Action Palette.",
            TextWrapping=TextWrapping.Wrap,
            FontSize=13,
            Margin=new Thickness(0,0,0,16)
        });

        void Section(string title)
        {
            body.Children.Add(new TextBlock
            {
                Text=title,
                FontSize=17,
                FontWeight=FontWeights.SemiBold,
                Margin=new Thickness(0,14,0,8)
            });
        }
        void Add(string caption,string detail,Action action)
        {
            var button=new Button
            {
                Content=new StackPanel
                {
                    Children=
                    {
                        new TextBlock
                        {
                            Text=caption,FontSize=14,
                            FontWeight=FontWeights.SemiBold
                        },
                        new TextBlock
                        {
                            Text=detail,FontSize=12,
                            Foreground=Brushes.DimGray,
                            TextWrapping=TextWrapping.Wrap
                        }
                    }
                },
                HorizontalContentAlignment=HorizontalAlignment.Stretch,
                HorizontalAlignment=HorizontalAlignment.Stretch,
                Padding=new Thickness(13,10,13,10),
                MinHeight=58,
                Margin=new Thickness(0,0,0,7),
                Background=Brushes.White
            };
            AutomationProperties.SetName(button,caption);
            AutomationProperties.SetHelpText(button,detail);
            // One failing tool/settings action cannot close the entire
            // Workspace. Fatal runtime failures still propagate normally.
            button.Click+=(_,_)=>{
                DeskSpaceFeatureActionGuardV255.Execute(caption,action,
                    warning=>MessageBox.Show(this,warning,
                        "DeskSpace features",MessageBoxButton.OK,
                        MessageBoxImage.Warning));
            };
            body.Children.Add(button);
        }
        Section("Find and use tools");
        Add("Action Palette","Find DeskSpace commands with type, arrow keys and Enter.",
            ()=>DeskSpaceActionPaletteV197.Show(this,false));
        Add("Universal search",
            "Search files, installed apps, settings, notes, reminders and calendar in one place.",
            ()=>{
                var window=new UniversalSearchWindow(()=>data,WidgetSettingsV3.Load()){
                    Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner
                };
                window.Show();
            });
        Add("Keyboard shortcuts guide",
            "Press F1 in Workspace to view verified DeskSpace shortcuts and their scope.",
            ()=>DeskSpaceKeyboardHelpV246.Show(this));
        Add("Excel mappings","Open the existing Excel mappings tab in Workspace.",
            ShowExcelMappingFromWheelV49);
        Add("Context suggestions",
            "Opt in for this window only: suggest existing actions from the app category.",
            ()=>DeskSpaceContextSuggestionsV237.Show(this));
        Add("Clipboard conversions",
            "Preview JSON formatting, CSV tables, Markdown and text transforms.",
            ()=>DeskSpaceClipboardTransformsV177.Show(this));

        Section("Files");
        Add("Preview file or folder",
            "Choose a PDF, Word, ZIP, image, text file or folder for a safe read-only preview.",
            ()=>DeskSpacePreviewChooserV273.Show(this));
        Add("Duplicate-file inspector",
            "Select one folder to find matching SHA-256 files, without deleting or moving anything.",
            ()=>DeskSpaceFileDuplicatesV244.Show(this));

        Add("Batch rename preview",
            "Preview prefix/suffix filename changes and collision errors. Never renames files.",
            ()=>DeskSpaceBatchRenamePreviewV254.Show(this));

        Section("Windows and workspace");
        Add("Settings",
            "Open the existing Settings tab without closing Workspace.",
            ()=>SelectExistingFeatureTabV199("Settings"));
        Add("Window layout profiles",
            "Capture and restore layouts of already-running application windows.",
            ()=>DeskSpaceWindowLayoutsV179.Show(this));
        Add("Export widget profiles",
            "Create a profile-only JSON file; no notes, credentials or Excel data.",
            ()=>DeskSpaceProfilePortabilityV201.ExportWithDialog(this));
        Add("Import widget profiles",
            "Preview a validated JSON import; existing names are not overwritten.",
            ()=>DeskSpaceProfilePortabilityV201.ImportWithDialog(this));
        Add("Reminders","Open the existing reminders tab.",
            ()=>SelectExistingFeatureTabV199("Reminders"));
        Add("Preview saved workflows",
            "Review steps and conditions without launching any app, URL or file.",
            ()=>DeskSpaceWorkflowDryRunV203.Show(this));
        Add("Notes","Open the existing notes tab, when present.",
            ()=>SelectExistingFeatureTabV199("Notes"));
        Add("Quick note",
            "Write a new note or explicitly paste clipboard text, preview it, then save locally.",
            ShowQuickNoteV270);

        Section("Information and privacy");
        Add("Find vault site labels",
            "After manually unlocking, match an HTTPS site to encrypted vault labels without revealing passwords.",
            ()=>DeskSpaceVaultContextWindowV202.Show(this));

        Section("Health and recovery");
        Add("Monitor diagnostics",
            "See connected monitors, coordinates, Windows-reported DPI and per-screen Deck positions.",
            ()=>DeskSpaceMonitorDiagnosticsV239.Show(this));
        Add("Search index health",
            "Review aggregate multi-drive indexing progress without showing paths or file contents.",
            ()=>DeskSpaceSearchIndexHealthV260.Show(this));
        Add("Component status",
            "Check Search, background listeners, startup recovery and memory.",
            ()=>DeskSpaceComponentHealthV175.Show(this,false));
        Add("Diagnostics and safe export",
            "Open Action Palette, select Export diagnostics, then confirm.",
            ()=>DeskSpaceActionPaletteV197.Show(this,false));
        var explanation=new TextBlock
        {
            Text="The widget donut, Deck and main Workspace still share their "+
                 "existing data. This page provides access; it does not "+
                 "automatically edit workbooks, store credentials or move windows.",
            TextWrapping=TextWrapping.Wrap,
            FontSize=12,
            Foreground=Brushes.DimGray,
            Margin=new Thickness(0,14,0,4)
        };
        body.Children.Add(explanation);
        tab.Content=outer;
        featureHubV199Tab=tab;
        // The tab is appended rather than selected. Preserve the user's
        // current Workspace view when upgrading.
        MainTabs.Items.Add(tab);
        PreviewKeyDown+=(_,e)=>{
            if(e.Key==Key.F1 && Keyboard.Modifiers==ModifierKeys.None)
            {
                DeskSpaceKeyboardHelpV246.Show(this);
                e.Handled=true;
                return;
            }
            if(e.Key==Key.K &&
               Keyboard.Modifiers==ModifierKeys.Control)
            {
                DeskSpaceActionPaletteV197.Show(this,false);
                e.Handled=true;
            }
        };
    }
    private void SelectExistingFeatureTabV199(string target)
    {
        var item=MainTabs.Items.OfType<TabItem>().FirstOrDefault(tab=>
            string.Equals(tab.Tag?.ToString(),target,
                StringComparison.OrdinalIgnoreCase) ||
            (tab.Header?.ToString()?.Contains(target,
                StringComparison.OrdinalIgnoreCase)??false));
        if(item==null)
        {
            MessageBox.Show(this,target+" is not available in this Workspace view.",
                "DeskSpace features",MessageBoxButton.OK,MessageBoxImage.Information);
            return;
        }
        MainTabs.SelectedItem=item;
    }
}
