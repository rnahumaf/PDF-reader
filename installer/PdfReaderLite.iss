#define MyAppName "PDF Reader Lite"
#define MyAppVersion "0.1.2"
#define MyAppPublisher "PDF Reader Lite"
#define MyAppExeName "PdfReaderLite.exe"
#define MyAppAssocName "PDF Reader Lite Document"
#define MyAppAssocExt ".pdf"
#define MyAppAssocKey "PDFReaderLite.pdf"

[Setup]
AppId={{9D61953D-7DA9-4BDA-B98A-A8AAB6E66593}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\PDF Reader Lite
DefaultGroupName=PDF Reader Lite
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=yes
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\assets\PdfReaderLite.ico
OutputDir=..\dist
OutputBaseFilename=PDFReaderLite-Setup-{#MyAppVersion}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "associatepdf"; Description: "Registrar PDF Reader Lite para arquivos .pdf"; GroupDescription: "Associacao de arquivos:"; Flags: checkedonce
Name: "desktopicon"; Description: "Criar atalho na area de trabalho"; GroupDescription: "Atalhos adicionais:"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\PDF Reader Lite"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\PDF Reader Lite"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\{#MyAppAssocExt}\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppAssocKey}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associatepdf
Root: HKCU; Subkey: "Software\Classes\{#MyAppAssocKey}"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocName}"; Flags: uninsdeletekey; Tasks: associatepdf
Root: HKCU; Subkey: "Software\Classes\{#MyAppAssocKey}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"",0"; Tasks: associatepdf
Root: HKCU; Subkey: "Software\Classes\{#MyAppAssocKey}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associatepdf
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".pdf"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associatepdf
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associatepdf

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir PDF Reader Lite"; Flags: nowait postinstall skipifsilent

[Code]
procedure SHChangeNotify(wEventId: Cardinal; uFlags: Cardinal; dwItem1: Integer; dwItem2: Integer);
external 'SHChangeNotify@shell32.dll stdcall';

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    SHChangeNotify($08000000, 0, 0, 0);
end;
