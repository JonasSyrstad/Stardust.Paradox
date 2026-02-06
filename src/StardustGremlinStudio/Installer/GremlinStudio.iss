; Inno Setup Script for Stardust Gremlin Studio
; https://jrsoftware.org/isinfo.php

#define MyAppName "Stardust Gremlin Studio"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Jonas Syrstad"
#define MyAppURL "https://github.com/JonasSyrstad/Stardust.Paradox"
#define MyAppExeName "GremlinStudio.exe"
#define MyAppIcon "..\Stardust.Paradox.GremlinStudio\Resources\icon.ico"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
AppId={{8F4E9B2A-5C7D-4A3E-B8F1-2D6E9A7C4B5F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Output settings
OutputDir=..\..\..\dist
OutputBaseFilename=GremlinStudio-{#MyAppVersion}-Setup
; Compression
Compression=lzma2/ultra64
SolidCompression=yes
; Modern installer appearance
WizardStyle=modern
; Installer icon and images
SetupIconFile={#MyAppIcon}
WizardImageFile=..\Stardust.Paradox.GremlinStudio\Resources\installer.png
WizardSmallImageFile=..\Stardust.Paradox.GremlinStudio\Resources\icon.png
; Privileges
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; Uninstaller
UninstallDisplayIcon={app}\{#MyAppExeName}
; Minimum Windows version (Windows 10)
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main executable (published single-file)
Source: "..\Stardust.Paradox.GremlinStudio\bin\publish\win-x64\GremlinStudio.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Custom initialization code if needed
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
