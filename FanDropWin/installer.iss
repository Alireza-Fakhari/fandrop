[Setup]
AppName=FanDrop
AppVersion=1.0.0
DefaultDirName={localappdata}\Programs\FanDrop
DefaultGroupName=FanDrop
PrivilegesRequired=lowest
OutputDir=dist
OutputBaseFilename=FanDrop-Setup
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\FanDrop.exe
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "autostart"; Description: "Start FanDrop when Windows starts"

[Files]
Source: "publish\FanDrop.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#FFMPEG}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\FanDrop"; Filename: "{app}\FanDrop.exe"
Name: "{group}\Uninstall FanDrop"; Filename: "{uninstallexe}"
Name: "{userstartup}\FanDrop"; Filename: "{app}\FanDrop.exe"; Tasks: autostart

[Run]
Filename: "{app}\FanDrop.exe"; Description: "Launch FanDrop"; Flags: nowait postinstall

[UninstallRun]
Filename: "taskkill"; Parameters: "/im FanDrop.exe /f"; Flags: runhidden; RunOnceId: "KillFanDrop"

