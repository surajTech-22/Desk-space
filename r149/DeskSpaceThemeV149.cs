using System;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using Microsoft.Win32;

namespace DeskSpace;

internal sealed class DeskSpaceThemeV149
{
    public string Style { get; set; } = "Soft";
    public string Appearance { get; set; } = "System";
    public string AccentHex { get; set; } = "#7364D3";
    public int Softness { get; set; } = 16;
    public int DonutOpacity { get; set; } = 96;

    private static string Filename => Path.Combine(WorkspaceStore.Root,"donut-theme-v149.json");
    public static DeskSpaceThemeV149 Load()
    {
        try
        {
            if (!File.Exists(Filename)) return new();
            return Sanitize(JsonSerializer.Deserialize<DeskSpaceThemeV149>(File.ReadAllText(Filename)) ?? new());
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        { return new(); }
    }
    public void Save()
    {
        var valid=Sanitize(this);
        Directory.CreateDirectory(WorkspaceStore.Root);
        var tmp=Filename+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(tmp,JsonSerializer.Serialize(valid));
            File.Move(tmp,Filename,true);
        }
        finally { try { if(File.Exists(tmp))File.Delete(tmp); } catch(IOException) {} }
    }
    private static DeskSpaceThemeV149 Sanitize(DeskSpaceThemeV149 value)
        => new()
        {
            Style=value.Style=="Adaptive"?"Adaptive":"Soft",
            Appearance=value.Appearance is "Light" or "Dark"?value.Appearance:"System",
            AccentHex=TryColor(value.AccentHex,out var color)?
                $"#{color.R:X2}{color.G:X2}{color.B:X2}":"#7364D3",
            Softness=Math.Clamp(value.Softness,0,24),
            DonutOpacity=Math.Clamp(value.DonutOpacity,55,100)
        };
    public static bool TryColor(string? input,out Color color)
    {
        color=Colors.MediumPurple;
        if(string.IsNullOrWhiteSpace(input))return false;
        string value=input.Trim();
        if(value.Length!=7 || value[0]!='#')return false;
        for(int i=1;i<value.Length;i++)
            if(!Uri.IsHexDigit(value[i]))return false;
        try
        {
            color=(Color)ColorConverter.ConvertFromString(value)!;
            return true;
        }
        catch{return false;}
    }
    public Color Accent => TryColor(AccentHex,out var color)?color:Color.FromRgb(115,100,211);
    public bool IsDark
    {
        get
        {
            if(Appearance=="Dark")return true;
            if(Appearance=="Light")return false;
            try
            {
                using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return Convert.ToInt32(key?.GetValue("AppsUseLightTheme",1))==0;
            }
            catch{return false;}
        }
    }
    public Color LightColor => IsDark?
        (Style=="Adaptive"?Color.FromRgb(47,47,73):Color.FromRgb(45,47,71)):
        (Style=="Adaptive"?Color.FromRgb(241,238,255):Color.FromRgb(238,235,251));
    public Color AltColor => IsDark?
        (Style=="Adaptive"?Color.FromRgb(62,59,96):Color.FromRgb(55,57,84)):
        (Style=="Adaptive"?Color.FromRgb(226,224,253):Color.FromRgb(227,221,246));
    public Color TextColor => IsDark?Color.FromRgb(240,240,249):Color.FromRgb(63,52,99);
    public byte ShadowAlpha => (byte)(16 + Softness*2);
    public double Opacity => DonutOpacity/100.0;
    public Brush LightBrush => new SolidColorBrush(LightColor);
    public Brush AltBrush => new SolidColorBrush(AltColor);
    public Brush TextBrush => new SolidColorBrush(TextColor);
    public Brush AccentBrush => new SolidColorBrush(Accent);
}
