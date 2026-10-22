$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
if(!(Test-Path "$root/DeskSpaceWeatherSceneV11.cs")){
    throw 'Existing native weather renderer missing'
}
# Layer over the verified R37 source, preserve all other modules and
# previous application settings, vault and weather cache.
Copy-Item 'r38/DeskSpaceWeatherSceneV38.cs' "$root/DeskSpaceWeatherSceneV11.cs" -Force
# Real WPF screenshot and geometry test is added to the existing STA test.
$smokePath='r36/UiSmoke/Program.cs'
$smoke=[IO.File]::ReadAllText($smokePath)
$oldSamples=@'
            ("rain",new WeatherReading("Bengaluru",22,63,true,DateTime.UtcNow)),
            ("night",new WeatherReading("Bengaluru",21,2,false,DateTime.UtcNow))
'@
$newSamples=@'
            ("rain",new WeatherReading("Bengaluru",22,63,true,DateTime.UtcNow)),
            ("rain-night",new WeatherReading("Bengaluru",19,63,false,DateTime.UtcNow)),
            ("thunder",new WeatherReading("Bengaluru",23,95,true,DateTime.UtcNow)),
            ("drizzle",new WeatherReading("Bengaluru",23,51,true,DateTime.UtcNow)),
            ("snow",new WeatherReading("Bengaluru",-1,73,true,DateTime.UtcNow)),
            ("cloudy",new WeatherReading("Bengaluru",21,3,true,DateTime.UtcNow)),
            ("night",new WeatherReading("Bengaluru",21,2,false,DateTime.UtcNow))
'@
if(!$smoke.Contains($oldSamples)){throw 'R38 WPF weather samples changed unexpectedly'}
$smoke=$smoke.Replace($oldSamples,$newSamples)
$anchor='            Render(scene,272,272,"weather-"+name);'
$extended=@'
            var emitterNodes=Walk(scene).OfType<Canvas>().Where(c=>
                (c.Tag?.ToString()??"")=="CloudAttachedRain").ToArray();
            var snowNodes=Walk(scene).OfType<Canvas>().Where(c=>
                (c.Tag?.ToString()??"")=="CloudAttachedSnow").ToArray();
            bool expectRain=name=="rain"||name=="rain-night"||
                name=="thunder"||name=="drizzle";
            bool expectSnow=name=="snow";
            Assert(emitterNodes.Length==(int)scene.GetType().GetProperty(
                "RainEmitterCount")!.GetValue(scene)!,
                "Rain emitters are missing or are not attached to cloud visuals");
            Assert(expectRain==(emitterNodes.Length>0),
                "Wrong weather condition classification for rain: "+name);
            Assert(expectSnow==(snowNodes.Length>0),
                "Wrong weather condition classification for snow: "+name);
            foreach(var emitter in emitterNodes.Concat(snowNodes))
            {
                var parent=VisualTreeHelper.GetParent(emitter) as Canvas;
                Assert(parent!=null && parent.Tag?.ToString()=="CloudPuff",
                    "Precipitation is not owned by a visible cloud");
                var clip=emitter.Clip as RectangleGeometry;
                Assert(clip!=null && clip.Rect.Top>0 && clip.Rect.Width>0,
                    "Cloud-origin precipitation has no bounded underside");
                Assert(VisualTreeHelper.GetParent(parent) is Canvas ribbon &&
                    ribbon.Tag?.ToString()=="CloudRibbon",
                    "Cloud and precipitation must share a horizontal glide");
            }
            Render(scene,272,272,"weather-"+name);
'@
if(!$smoke.Contains($anchor)){throw 'R38 WPF render anchor missing'}
$smoke=$smoke.Replace($anchor,$extended.TrimEnd())
[IO.File]::WriteAllText($smokePath,$smoke,[Text.UTF8Encoding]::new($false))
Write-Host 'R38: condition-aware cloud-linked rain/snow, night precipitation, full circular sky; WPF visual-tree smoke installed.'
