using System;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// R270: user-invoked capture of manually typed text, optionally supplied
// by a deliberate clipboard read. No passive secrets capture and no timers.
// Uses the existing MainWindow workspace list and original atomic Save().
public partial class MainWindow
{
    private Window? quickNoteWindowV270;
    internal void ShowQuickNoteV270()
    {
        if(quickNoteWindowV270?.IsVisible==true)
        {
            quickNoteWindowV270.Activate();
            return;
        }
        var title=new TextBox{Margin=new Thickness(0,6,0,10),
            MaxLength=DeskSpaceQuickNotePolicyV270.MaxTitleChars,
            MinHeight=32,FontSize=14,Text="Quick note"};
        AutomationProperties.SetName(title,"Quick note title");
        var body=new TextBox{Margin=new Thickness(0,6,0,10),
            AcceptsReturn=true,AcceptsTab=true,TextWrapping=TextWrapping.Wrap,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            Height=205,MaxLength=DeskSpaceQuickNotePolicyV270.MaxBodyChars,
            FontSize=13};
        AutomationProperties.SetName(body,"Quick note content, editable before saving");
        var message=new TextBlock{Text="Nothing has been saved. "+
            "Clipboard is never read unless you choose Read clipboard.",
            TextWrapping=TextWrapping.Wrap,
            Foreground=Brushes.DimGray,Margin=new Thickness(0,6,0,7)};
        var actions=new WrapPanel{Margin=new Thickness(0,8,0,0)};
        var read=new Button{Content="Read clipboard (on demand)",
            Padding=new Thickness(10,6,10,6),
            Margin=new Thickness(0,0,8,8)};
        var save=new Button{Content="Save new note",Padding=new Thickness(10,6,10,6),
            Margin=new Thickness(0,0,8,8),IsDefault=true};
        var cancel=new Button{Content="Cancel",Padding=new Thickness(10,6,10,6),
            Margin=new Thickness(0,0,8,8),IsCancel=true};
        actions.Children.Add(read);actions.Children.Add(save);actions.Children.Add(cancel);
        var content=new StackPanel{Margin=new Thickness(18)};
        content.Children.Add(new TextBlock{Text="Quick note",
            FontSize=21,FontWeight=FontWeights.SemiBold});
        content.Children.Add(new TextBlock{
            Text="Creates a new note in your existing DeskSpace Notes. "+
                "Review the text before saving. Nothing is captured automatically.",
            TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,5,0,10)});
        content.Children.Add(new TextBlock{Text="Title"});
        content.Children.Add(title);
        content.Children.Add(new TextBlock{Text="Content"});
        content.Children.Add(body);
        content.Children.Add(message);
        content.Children.Add(actions);
        var popup=new Window{Title="DeskSpace — Quick note",
            Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,
            Width=620,Height=515,MinWidth=430,MinHeight=400,
            ShowInTaskbar=false,
            Content=new ScrollViewer{Content=content,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};
        quickNoteWindowV270=popup;
        popup.Closed+=(_,_)=>quickNoteWindowV270=null;
        cancel.Click+=(_,_)=>popup.Close();
        read.Click+=(_,_)=>{
            try
            {
                if(!Clipboard.ContainsText(TextDataFormat.UnicodeText))
                {
                    message.Text="Clipboard has no text. No note was saved.";
                    return;
                }
                string candidate=Clipboard.GetText(TextDataFormat.UnicodeText);
                if(candidate.Length>DeskSpaceQuickNotePolicyV270.MaxBodyChars)
                {
                    message.Text="Clipboard text exceeds 16,384 characters. "+
                        "Nothing was imported.";
                    return;
                }
                body.Text=candidate;
                message.Text="Clipboard text loaded into the editor. "+
                    "Review it and choose Save new note explicitly.";
            }
            catch(System.Runtime.InteropServices.COMException)
            {
                message.Text="Clipboard is busy; retry manually. Nothing was saved.";
            }
        };
        save.Click+=(_,_)=>{
            if(readOnlyRecoveryMode)
            {
                message.Text="Workspace is in read-only recovery mode. "+
                    "Restore the original data before saving any note.";
                return;
            }
            if(!DeskSpaceQuickNotePolicyV270.TryPrepare(
                title.Text,body.Text,out var name,out var text,out var reason))
            {
                message.Text=reason;
                return;
            }
            var note=new NoteItem{Title=name,Content=text,Updated=DateTime.Now};
            data.Notes.Add(note);
            try
            {
                // Standard WorkspaceStore uses a cross-process Windows mutex,
                // atomic move and .bak copy. Never create a second data store.
                store.Save(data);
            }
            catch(Exception error)when(error is IOException or
                UnauthorizedAccessException or SecurityException)
            {
                data.Notes.Remove(note);
                message.Text="Could not save note ("+error.GetType().Name+
                    "). Your editor text is unchanged; retry when storage is available.";
                return;
            }
            NotesList.Items.Refresh();
            NotesList.SelectedItem=note;
            MainTabs.SelectedIndex=1;
            RefreshDashboard();
            popup.Close();
        };
        popup.Show();
        title.Focus();
    }
}
