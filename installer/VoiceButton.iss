#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "Voice Button"
#define AppPublisher "Mykola Nalivaiko"
#define AppUrl "https://github.com/nick-nalivaiko/voice-button"
#define ReleaseRoot "..\artifacts\release\v" + AppVersion

[Setup]
AppId={{66249719-40C0-4387-B549-30B42E35D00B}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl + "/issues"}
AppUpdatesURL={#AppUrl + "/releases"}
DefaultDirName={localappdata}\Programs\Voice Button
DefaultGroupName=Voice Button
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UseSetupLdr=x64
OutputDir={#ReleaseRoot}
OutputBaseFilename=VoiceButton-Setup-v{#AppVersion}-win-x64
SetupIconFile=..\Assets\AppIcon.ico
UninstallDisplayIcon={app}\VoiceButton.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Voice Button installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#ReleaseRoot}\publish\VoiceButton.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Voice Button"; Filename: "{app}\VoiceButton.exe"
Name: "{autodesktop}\Voice Button"; Filename: "{app}\VoiceButton.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VoiceButton.exe"; Description: "{cm:LaunchProgram,Voice Button}"; Flags: nowait postinstall skipifsilent
