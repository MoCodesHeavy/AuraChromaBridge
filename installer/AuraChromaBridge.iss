; AuraChromaBridge x64 installer (Inno Setup 6).
; Built by .github/workflows/build.yml:  ISCC.exe /DAppVersion=<Program.Version> installer\AuraChromaBridge.iss
; Expects AuraChromaBridge.exe and AuraChromaBridge-bg.exe already built in the repo root (build.cmd main / bg).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#define Root ".."
#define TaskName "AuraChromaBridge"

[Setup]
; Never change AppId: later versions find and upgrade the installed one through it.
AppId={{B0BD0AC8-A696-4F66-B755-5BC4047EAC45}
AppName=AuraChromaBridge
AppVersion={#AppVersion}
AppVerName=AuraChromaBridge {#AppVersion}
AppPublisher=MoCodesHeavy
AppPublisherURL=https://github.com/MoCodesHeavy/AuraChromaBridge
AppSupportURL=https://github.com/MoCodesHeavy/AuraChromaBridge/issues
DefaultDirName={autopf}\AuraChromaBridge
DefaultGroupName=AuraChromaBridge
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
OutputDir={#Root}\dist
OutputBaseFilename=AuraChromaBridge-Setup-x64
LicenseFile={#Root}\LICENSE
UninstallDisplayName=AuraChromaBridge
UninstallDisplayIcon={app}\AuraChromaBridge.exe
; The sync is stopped by this script (PrepareToInstall), not by Restart Manager.
CloseApplications=no
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "{#Root}\AuraChromaBridge.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Root}\AuraChromaBridge-bg.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Root}\install-task.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Root}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Root}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
; Someone's existing settings are never replaced; only razer_app_id is set, after install (CurStepChanged).
Source: "{#Root}\settings.ini"; DestDir: "{code:DataDir}"; Flags: onlyifdoesntexist uninsneveruninstall

[Dirs]
Name: "{code:DataDir}\logs"; Flags: uninsneveruninstall

[Registry]
; Read by the uninstaller to find the settings and logs.
Root: HKLM64; Subkey: "Software\AuraChromaBridge"; ValueType: string; ValueName: "DataDir"; ValueData: "{code:DataDir}"; Flags: uninsdeletekey

[Icons]
Name: "{group}\Scan Razer app IDs"; Filename: "{app}\AuraChromaBridge.exe"; Parameters: "scan pause data=""{code:DataDir}"""; WorkingDir: "{code:DataDir}"
Name: "{group}\Edit settings"; Filename: "{win}\notepad.exe"; Parameters: """{code:DataDir}\settings.ini"""
Name: "{group}\Open logs folder"; Filename: "{code:DataDir}\logs"
Name: "{group}\Start sync"; Filename: "{sys}\schtasks.exe"; Parameters: "/run /tn {#TaskName}"; Flags: runminimized
Name: "{group}\Stop sync"; Filename: "{cmd}"; Parameters: "/c echo stop> ""{code:DataDir}\stop.request"""; Flags: runminimized
Name: "{group}\Uninstall AuraChromaBridge"; Filename: "{uninstallexe}"

[Run]
; Finish page, in this order: the scan first (while the sync isn't holding the app ID), then start the sync.
Filename: "{app}\AuraChromaBridge.exe"; Parameters: "scan pause data=""{code:DataDir}"""; WorkingDir: "{code:DataDir}"; Description: "Run the Razer app ID scan now"; Flags: postinstall waituntilterminated; Check: FeedChosen
Filename: "{sys}\schtasks.exe"; Parameters: "/run /tn {#TaskName}"; Description: "Start the sync now"; Flags: postinstall runhidden

[Messages]
FinishedLabel=AuraChromaBridge is installed and starts by itself every time you sign in.%n%nIf you follow Synapse's colours: in the Razer Chroma app > Connect, Chroma Connect and the bridge's entry must be on, and the Connect effect must light Connect devices (for example Quick Effects > Spectrum Cycling).

[Code]
var
  DataDirValue: String;
  UninstallDataDir: String;
  ExistingFeed: String;
  HadSettings: Boolean;
  RazerFound: Boolean;
  AsusFound: Boolean;
  FeedPage: TInputOptionWizardPage;

{ The folder with settings.ini and logs: the existing task's folder, else %LOCALAPPDATA%\AuraChromaBridge. }
function DataDir(Param: String): String;
begin
  Result := DataDirValue;
end;

function FeedChosen(): Boolean;
begin
  Result := FeedPage.Values[0];
end;

{ Runs a setup command of the console exe extracted to {tmp} and returns what it wrote to out=. }
function RunCapture(const Params: String): String;
var
  OutFile: String;
  Text: AnsiString;
  ResultCode: Integer;
begin
  Result := '';
  OutFile := ExpandConstant('{tmp}\acb-out.txt');
  DeleteFile(OutFile);
  if Exec(ExpandConstant('{tmp}\AuraChromaBridge.exe'), Params + ' out="' + OutFile + '"', '',
          SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
    if LoadStringFromFile(OutFile, Text) then
      Result := Trim(String(Text));
end;

{ Asks a running sync to stop (it hands the lights back to Synapse and Armoury Crate), then makes sure it has. }
procedure StopSync(const Dir: String);
var
  ResultCode: Integer;
begin
  if (Dir <> '') and DirExists(Dir) then
  begin
    SaveStringToFile(AddBackslash(Dir) + 'stop.request', 'stop', False);
    Sleep(3000);
  end;
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/im AuraChromaBridge-bg.exe /f', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/im AuraChromaBridge.exe /f', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function IsGuid(const Value: String): Boolean;
begin
  Result := (Length(Value) >= 36) and (Pos('-', Value) > 0);
end;

procedure InitializeWizard;
var
  Existing: String;
  Note: String;
begin
  ExtractTemporaryFile('AuraChromaBridge.exe');
  Existing := RunCapture('task-data');
  if (Existing <> '') and DirExists(Existing) then
    DataDirValue := Existing
  else
    DataDirValue := ExpandConstant('{localappdata}\AuraChromaBridge');

  RazerFound := FileExists(ExpandConstant('{sys}\RzChromaBroadcastAPI64.dll'));
  AsusFound := RegKeyExists(HKCR64, 'aura.sdk.1') or RegKeyExists(HKCR32, 'aura.sdk.1');
  HadSettings := FileExists(AddBackslash(DataDirValue) + 'settings.ini');
  if HadSettings then
    ExistingFeed := LowerCase(RunCapture('get-setting razer_app_id data="' + DataDirValue + '"'))
  else
    ExistingFeed := '';

  Note := 'The bridge can pass the colour Razer Synapse is showing to your ASUS lighting and PS5 light bar, ' +
          'so they match your Razer gear exactly. Razer only sends it to approved apps; until Razer approves this ' +
          'project, the bridge uses another brand''s published app ID. Untick to use the bridge''s own clock effect instead.';
  if not RazerFound then
    Note := Note + #13#10#13#10 + 'Razer Synapse''s Chroma Connect wasn''t found, so this option is off.';
  if not AsusFound then
    Note := Note + #13#10#13#10 + 'ASUS Armoury Crate''s Aura SDK wasn''t found. The bridge drives ASUS lighting, so it ' +
            'will have nothing to drive until Armoury Crate is installed. You can still continue.';
  if HadSettings then
    Note := Note + #13#10#13#10 + 'Your settings in ' + DataDirValue + ' are kept.';

  FeedPage := CreateInputOptionPage(wpSelectDir, 'Synapse colour feed', 'Follow the colours Razer Synapse shows?',
                                    Note, False, False);
  FeedPage.Add('Follow Synapse''s colours (unofficial: uses published app IDs from other brands until Razer issues one for this project)');
  if not RazerFound then
  begin
    FeedPage.Values[0] := False;
    FeedPage.CheckListBox.ItemEnabled[0] := False;
  end
  else if HadSettings then
    FeedPage.Values[0] := (ExistingFeed <> '') and (ExistingFeed <> 'off')
  else
    FeedPage.Values[0] := AsusFound;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopSync(DataDirValue);
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Value: String;
  Params: String;
  ResultCode: Integer;
begin
  if CurStep <> ssPostInstall then Exit;

  if not FeedPage.Values[0] then
    Value := 'off'
  else if IsGuid(ExistingFeed) then
    Value := ExistingFeed   { an ID set by hand, e.g. Razer's official one: keep it }
  else
    Value := 'auto';
  if not Exec(ExpandConstant('{app}\AuraChromaBridge.exe'),
              'set-setting razer_app_id ' + Value + ' data="' + DataDirValue + '"', '',
              SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
    SuppressibleMsgBox('Could not set razer_app_id in ' + DataDirValue + '\settings.ini (code ' + IntToStr(ResultCode) + ').',
                       mbError, MB_OK, IDOK);

  Params := '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\install-task.ps1') + '"' +
            ' -Exe "' + ExpandConstant('{app}\AuraChromaBridge-bg.exe') + '" -Data "' + DataDirValue + '"';
  if not Exec('powershell.exe', Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
    SuppressibleMsgBox('Setting up the start-with-Windows task failed (code ' + IntToStr(ResultCode) + '). ' +
                       'Run setup again, or start the sync from the Start menu.', mbError, MB_OK, IDOK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    if not RegQueryStringValue(HKLM64, 'Software\AuraChromaBridge', 'DataDir', UninstallDataDir) then
      UninstallDataDir := '';
    StopSync(UninstallDataDir);
    Exec(ExpandConstant('{sys}\schtasks.exe'), '/delete /tn {#TaskName} /f', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    { Only the folder setup itself creates is offered for deletion. A folder reused from an earlier install
      may be someone's project folder (source code and all), so it is always kept. }
    if (UninstallDataDir <> '') and DirExists(UninstallDataDir) and
       (CompareText(RemoveBackslash(UninstallDataDir), ExpandConstant('{localappdata}\AuraChromaBridge')) = 0) then
    begin
      if SuppressibleMsgBox('Keep your AuraChromaBridge settings and logs?' + #13#10#13#10 + UninstallDataDir,
                            mbConfirmation, MB_YESNO, IDYES) = IDNO then
        DelTree(UninstallDataDir, True, True, True);
    end;
  end;
end;
