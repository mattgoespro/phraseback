#ifndef Payload
  #error Payload must identify the tested self-contained folder
#endif
#ifndef Output
  #error Output must identify an isolated build folder
#endif
[Setup]
AppId={{AA7BD096-84EA-43DC-92CB-4E8E85F82401}
AppName=Phraseback
AppVersion=0.2.0
AppPublisher=Phraseback
DefaultDirName={localappdata}\Programs\Phraseback
DefaultGroupName=Phraseback
UsePreviousGroup=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir={#Output}
OutputBaseFilename=Phraseback-Setup-0.2.0-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Phraseback.exe
CloseApplications=yes
RestartApplications=no
DisableProgramGroupPage=yes
; Application data is deliberately absent from Files/InstallDelete/UninstallDelete.
; Recordings, models and metadata backups survive updates and uninstall.
[Files]
Source: "{#Payload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Phraseback"; Filename: "{app}\Phraseback.exe"
Name: "{group}\Uninstall Phraseback"; Filename: "{uninstallexe}"
[InstallDelete]
; Remove only obsolete application shortcuts during a rebrand upgrade.
Type: files; Name: "{userprograms}\Flow Recorder\Flow Recorder.lnk"
Type: files; Name: "{userprograms}\Flow Recorder\Uninstall Flow Recorder.lnk"
Type: dirifempty; Name: "{userprograms}\Flow Recorder"
