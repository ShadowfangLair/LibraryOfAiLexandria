; installer.iss – Inno Setup script for Library of Ai‑Lexandria
[Setup]
AppName=Library of Ai‑Lexandria
AppPublisher=The Shadowfang Lair
AppVersion=1.0
DefaultDirName={pf}\Shadowfang Lair\LibraryOfAiLexandria
DefaultGroupName=Shadowfang Lair\Library of Ai‑Lexandria
OutputBaseFilename=LibraryOfAiLexandria-Setup
Compression=lzma
SolidCompression=yes

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Library of Ai‑Lexandria"; Filename: "{app}\LibraryOfAiLexandria.exe"; WorkingDir: "{app}"
Name: "{group}\Uninstall Library of Ai‑Lexandria"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\LibraryOfAiLexandria.exe"; Description: "Launch Library of Ai‑Lexandria now"; Flags: nowait postinstall skipifsilent
