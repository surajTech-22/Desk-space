$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$base='DeskSpace_Windows_R1/DeskSpace'
$meta=[IO.File]::ReadAllText("$base/DeskSpaceDocumentMetadataV189.cs")
$preview=[IO.File]::ReadAllText("$base/DeskSpaceFilePreviewV85.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
  'MaxOfficeBytes=256L*1024*1024',
  'MaxPackageEntries=4096',
  'MaxFolderEntries=512',
  'ZipFile.OpenRead(info.FullName)',
  'zip.Entries.Count>MaxPackageEntries',
  'No document text, cells, slide contents or attachments were opened.',
  'dir.EnumerateFileSystemInfos()',
  'if(HasLink(item))continue;',
  'if(HasLink(dir))',
  'stream.Length,64*1024',
  'FileShare.ReadWrite',
  'No audio decoded or played.',
  'internal static FrameworkElement Create(string path)'
)){
 if(!$meta.Contains($token)){throw "R189 safe metadata guard missing: $token"}
}
foreach($bad in @(
   'ExtractToDirectory','ExtractToFile','entry.Open(',
   'Process.Start','Clipboard.Get',
   'File.WriteAllText','File.WriteAllBytes',
   'Directory.Delete','File.Delete','File.Move','HttpClient'
)){
 if($meta.Contains($bad)){throw "R189 read-only inspector contains unsafe behavior: $bad"}
}
foreach($token in @(
 'if(Directory.Exists(path))',
 'DeskSpaceDocumentMetadataV189.Create(path)',
 'DeskSpaceDocumentMetadataV189.Supports(extension)',
 'if(!File.Exists(path) && !Directory.Exists(path))return;',
 'Open file or folder'
)){
 if(!$preview.Contains($token)){throw "R189 Deck preview integration missing: $token"}
}
foreach($token in @(
 'TestDocumentMetadataV189();',
 'private static void TestDocumentMetadataV189()',
 'R189 corrupt Office documents must fail',
 'R189 one-level folder summary must render',
 'R189 WAV must display metadata',
 'DeskSpace_R189_Metadata_'
)){
 if(!$smoke.Contains($token)){throw "R189 native test coverage missing: $token"}
}
Write-Host 'R189 STATIC PASS: safe Office/WAV/folder previews, bounded metadata, no extraction/mutation, native WPF tests and Downloads-only fixtures.'
