using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DeskSpace;
using Recognizer=DeskSpace.DeskSpaceFolderAppRecognitionV363;

internal static class Program
{
    private static int passed;
    private static void Check(bool condition,string description)
    {
        if(!condition)throw new Exception("R363 TEST FAILURE: "+description);
        Console.WriteLine("R363 PASS: "+description);
        passed++;
    }
    private static string NewFolder(string root,string name,params string[] files)
    {
        var folder=Path.Combine(root,name);
        Directory.CreateDirectory(folder);
        foreach(var file in files)File.WriteAllText(Path.Combine(folder,file),"");
        return folder;
    }
    private static string[] Ids(string folder)=>
        Recognizer.Recognize(folder).Select(x=>x.AppId).ToArray();
    private static Recognizer.Suggestion Pick(string folder,string app)=>
        Recognizer.Recognize(folder).Single(x=>x.AppId==app);
    public static int Main()
    {
        var root=Path.Combine(Path.GetTempPath(),"DeskSpaceR363-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var solution=NewFolder(root,"solution","My App.sln","src.txt");
            var matches=Recognizer.Recognize(solution);
            Check(matches[0].AppId=="VisualStudio"&&matches[1].AppId=="Code"&&
                  matches[2].AppId=="Explorer","Visual Studio solutions get Visual Studio, VS Code and Explorer options");
            Check(matches[0].Target==Path.Combine(solution,"My App.sln"),
                  "Visual Studio opens a solution file, not an arbitrary file");

            var android=NewFolder(root,"android","settings.gradle.kts","gradlew.bat");
            Check(Ids(android).SequenceEqual(new[]{"AndroidStudio","Code","Explorer"}),
                  "Android Gradle root recognized before generic code");
            var python=NewFolder(root,"python","pyproject.toml");
            Directory.CreateDirectory(Path.Combine(python,".idea"));
            Check(Ids(python).SequenceEqual(new[]{"PyCharm","Code","Explorer"}),
                  "Python and JetBrains metadata provides two explicit options");
            var js=NewFolder(root,"javascript","package.json");
            Check(Ids(js).SequenceEqual(new[]{"Code","Explorer"}),
                  "Node manifest selects installed VS Code or Explorer");
            var workspace=NewFolder(root,"workspace","Team.code-workspace");
            Check(Pick(workspace,"Code").Target==Path.Combine(workspace,"Team.code-workspace"),
                  "VS Code workspace launches the exact workspace file");
            var r=NewFolder(root,"r-project","Analysis.Rproj");
            Check(Ids(r).SequenceEqual(new[]{"RStudio","Code","Explorer"}),
                  "RStudio project is identified");
            var cs=NewFolder(root,"csharp","Widgets.csproj");
            Check(Ids(cs).SequenceEqual(new[]{"VisualStudio","Code","Explorer"}),
                  ".NET project is recognized without scanning source code");

            var excel=NewFolder(root,"excel","Budget.xlsx");
            Check(Ids(excel).SequenceEqual(new[]{"Default","Explorer"}),
                  "a single Excel workbook is opened by its Windows file association");
            var pdf=NewFolder(root,"pdf","Report.pdf");
            Check(Ids(pdf).SequenceEqual(new[]{"Default","Explorer"}),
                  "a single PDF is opened only via the chosen Windows default action");
            var many=NewFolder(root,"ambiguous","Budget.xlsx","Other.xlsx");
            Check(Ids(many).SequenceEqual(new[]{"Explorer"}),
                  "ambiguous multi-document folder never chooses one file arbitrarily");
            var unsafe=NewFolder(root,"unknown-exe","setup.exe","install.cmd");
            Check(Ids(unsafe).SequenceEqual(new[]{"Explorer"}),
                  "unknown EXE/CMD in a folder is never proposed for execution");
            var empty=NewFolder(root,"empty");
            Check(Ids(empty).SequenceEqual(new[]{"Explorer"}),
                  "unrecognized ordinary folders safely fall back to File Explorer");
            Check(Recognizer.Recognize(Path.Combine(root,"missing")).Count==0,
                  "missing folder does not generate an app launch suggestion");

            // A fake installed executable is used only with an injected
            // launcher; no app is actually executed by these tests.
            var dummy=Path.Combine(root,"Code.exe");
            File.WriteAllBytes(dummy,new byte[]{1,2,3});
            var code=Pick(js,"Code");
            Check(!Recognizer.IsAvailable(code,_=>null),
                  "application is unavailable when it is not installed");
            Check(Recognizer.IsAvailable(code,_=>dummy),
                  "known installed application can be offered as an action");
            ProcessStartInfo? started=null;
            bool ok=Recognizer.TryOpen(code,out var error,
                _=>dummy,info=>{started=info;return null;});
            Check(ok&&error==null&&started!=null&&
                  started.FileName==dummy &&
                  started.WorkingDirectory==js &&
                  started.ArgumentList.Count==1&&
                  started.ArgumentList[0]==js &&
                  !started.UseShellExecute,
                  "explicit Open uses one structured argument without shell injection");
            Check(!Recognizer.TryOpen(code,out error,_=>null,_=>null) &&
                  error!=null,"missing app cannot launch");

            ProcessStartInfo? docCommand=null;
            bool documentOpened=Recognizer.TryOpen(Pick(excel,"Default"),
                out error,null,info=>{docCommand=info;return null;});
            Check(documentOpened&&docCommand!=null&&docCommand.UseShellExecute &&
                  docCommand.FileName==Path.Combine(excel,"Budget.xlsx"),
                  "explicit document action uses Windows default association");
            ProcessStartInfo? explorerCommand=null;
            bool folderOpened=Recognizer.TryOpen(Pick(empty,"Explorer"),
                out error,null,info=>{explorerCommand=info;return null;});
            Check(folderOpened&&explorerCommand!=null&&
                  explorerCommand.ArgumentList.Count==1&&
                  explorerCommand.ArgumentList[0]==empty,
                  "Explorer action opens the original folder only");

            var outside=Path.Combine(root,"outside.exe");
            File.WriteAllBytes(outside,new byte[]{4});
            var forged=new Recognizer.Suggestion(js,outside,"Code",
                "Visual Studio Code","Code project","package.json");
            Check(!Recognizer.TryOpen(forged,out error,_=>dummy,_=>null),
                  "forged target outside the dropped folder is blocked");
            File.Delete(Path.Combine(excel,"Budget.xlsx"));
            Check(!Recognizer.TryOpen(Pick(pdf,"Default") with
                  {Target=Path.Combine(excel,"Budget.xlsx")},
                  out error,null,_=>null),
                  "deleted or changed document target is blocked");
            // Limit the root enumeration; no recursive scan or file reads.
            var huge=NewFolder(root,"huge");
            for(int i=0;i<360;i++)File.WriteAllText(Path.Combine(huge,
                $"unrelated-{i:D4}.txt"),"not a project");
            Check(Ids(huge).SequenceEqual(new[]{"Explorer"}),
                  "large folder scanning is bounded and never infers a random application");
            Console.WriteLine($"R363 ALL {passed} WINDOWS FOLDER RECOGNITION TESTS PASSED.");
            return 0;
        }
        finally
        {
            try{Directory.Delete(root,true);}catch(IOException){}
        }
    }
}
