$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r258/DeskSpaceContextAuditV258.cs' -Destination "$root/DeskSpaceContextAuditV258.cs" -Force
$p="$root/DeskSpaceContextSuggestionsV237.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R258 "+$why+": expected one anchor, found "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap @'
        private readonly StackPanel suggestions;
'@ @'
        private readonly StackPanel suggestions;
        private readonly TextBlock auditV258;
'@ 'session audit text binding'
Swap @'
        private string category="None";
'@ @'
        private string category="None";
        private int recognizedV258;
        internal string AuditV258()=>DeskSpaceContextAuditV258.Summary(
            hook!=IntPtr.Zero && !disposed,recognizedV258,category);
'@ 'no raw process or document identifiers in audit'
Swap @'
        internal Session(Window dialog,Window owner,TextBlock status,
            StackPanel suggestions)
        {
            this.dialog=dialog;this.owner=owner;
            this.status=status;this.suggestions=suggestions;
        }
'@ @'
        internal Session(Window dialog,Window owner,TextBlock status,
            StackPanel suggestions,TextBlock audit)
        {
            this.dialog=dialog;this.owner=owner;
            this.status=status;this.suggestions=suggestions;
            auditV258=audit;
        }
'@ 'bind read-only audit view'
Swap @'
            if(hook==IntPtr.Zero)
            {
                callback=null;
                status.Text="Windows foreground notifications unavailable. No monitoring started.";
                return;
            }
'@ @'
            if(hook==IntPtr.Zero)
            {
                callback=null;
                status.Text="Windows foreground notifications unavailable. No monitoring started.";
                auditV258.Text=AuditV258();
                return;
            }
            recognizedV258=0;
            category="None";
            suggestions.Children.Clear();
            auditV258.Text=AuditV258();
'@ 'reset opt-in session at Start'
Swap @'
                    if(disposed || !dialog.IsLoaded)return;
                    category=proposed;UpdateSuggestions();
'@ @'
                    if(disposed || hook==IntPtr.Zero || !dialog.IsLoaded)return;
                    category=proposed;
                    recognizedV258=Math.Min(10000,recognizedV258+1);
                    UpdateSuggestions();
                    auditV258.Text=AuditV258();
'@ 'ignore queued foreground callbacks after Stop'
Swap @'
            callback=null;
            if(!disposed)
                status.Text="Stopped. No foreground application monitoring is active.";
'@ @'
            callback=null;
            // Stop clears all session-only category state and recommendations.
            // Do not preserve foreground-app details after consent ends.
            recognizedV258=0;
            category="None";
            suggestions.Children.Clear();
            auditV258.Text=AuditV258();
            if(!disposed)
                status.Text="Stopped. No foreground application monitoring is active.";
'@ 'forget session when listener is stopped'
Swap @'
        var suggestions=new StackPanel();
        content.Children.Add(suggestions);
'@ @'
        var suggestions=new StackPanel();
        content.Children.Add(suggestions);
        var auditV258=new TextBlock{
            Text=DeskSpaceContextAuditV258.Summary(false,0,"None"),
            TextWrapping=TextWrapping.Wrap,
            Foreground=Brushes.DimGray,
            Margin=new Thickness(0,14,0,0)};
        content.Children.Add(auditV258);
        var auditButtonV258=new Button{
            Content="Show privacy audit",
            Padding=new Thickness(10,5,10,5),
            Margin=new Thickness(8,0,0,0)};
        controls.Children.Add(auditButtonV258);
'@ 'visible opt-in audit action'
Swap @'
        var session=new Session(panel,owner,status,suggestions);
'@ @'
        var session=new Session(panel,owner,status,suggestions,auditV258);
        auditButtonV258.Click+=(_,_)=>auditV258.Text=session.AuditV258();
'@ 'audit uses only local category counters'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R258: local context session privacy audit and hard stop clears all recommendations/counters.'
