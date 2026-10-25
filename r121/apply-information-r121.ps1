$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceInformationV36.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old=@'
        shelfBody.Children.Add(name);
        var rowsHost=new StackPanel();
'@
$new=@'
        shelfBody.Children.Add(name);
        // PLACEHOLDER ONLY. No site recognition, browser observation,
        // autofill, credential lookup, password copying or secret storage.
        // Future implementation can associate this information collection
        // with a website/application after explicit permission and vault
        // unlock. These controls deliberately remain disabled.
        var planned=new StackPanel{Margin=new Thickness(2,2,2,6)};
        planned.Children.Add(new TextBlock{
            Text="Website / app login suggestions — planned",
            FontSize=11,FontWeight=FontWeights.SemiBold,
            Foreground=Ink,TextWrapping=TextWrapping.Wrap});
        planned.Children.Add(new TextBlock{
            Text="Future: choose a website or application for a UI/PW entry. "+
                 "Suggestions will require explicit approval; passwords "+
                 "will remain locked in the existing Security Vault.",
            FontSize=10,Foreground=Ink,TextWrapping=TextWrapping.Wrap});
        planned.Children.Add(new TextBox{
            Text="Target website or application (coming later)",
            IsEnabled=false,Margin=new Thickness(0,4,0,2)});
        planned.Children.Add(new Button{
            Content="Link UI / PW to this site (not enabled yet)",
            IsEnabled=false,FontSize=10});
        shelfBody.Children.Add(new Expander{
            Header="Login suggestions · future placeholder",
            IsExpanded=false,Content=planned});
        var rowsHost=new StackPanel();
'@
if(([regex]::Matches($s,[regex]::Escape($old))).Count -ne 1){
 throw 'R121 information placeholder anchor not unique'}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),
    [Text.UTF8Encoding]::new($false))
Write-Host 'R121 Information placeholder visible; password/web detection deliberately disabled.'
