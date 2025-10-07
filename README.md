Build:
dotnet build
dotnet publish -c Release -r win-x64

com.yourname.counter.sdPlugin/
├── manifest.json
├── CounterPlugin.exe
└── images/
    ├── pluginIcon.png
    └── actionIcon.png

Placement:
%appdata%\Hotspot\StreamDock\plugins\
