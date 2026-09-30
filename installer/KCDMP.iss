; KCD2 Multiplayer, Host World fork -- one installer for every kind of machine.
;
; Compile with tools\Build-Installer.ps1, not by hand: this script installs
; the *output* of tools\Publish-Release.ps1 (release\KCDMP\) and will refuse
; to compile if that folder is not there.
;
; How this differs from the stock 0.18.2 installer it started as:
;   * It does not look for the game and does not gate on it. The stock one
;     refused to continue without the Steam Modding Tools, because it put the
;     mod into the game at install time. This fork also runs on the Xbox Game
;     Pass build, so finding the game (either build) is the launcher's job,
;     and the mod is put into the game when the game is launched -- an update
;     of the launcher can then never leave the game on an older mod.
;   * Everything ships in it: launcher, agent, relay, master server, native
;     plugin and injector, the game mod, the skip save and the two scripts the
;     launcher runs (Start-GamePass.ps1, Start-WorldHost.ps1). Nothing else
;     has to be installed first.
;   * Its own AppId and folder, so it installs beside a stock KCDMP rather
;     than over it.
; What it kept: no elevation, the WebView2 runtime check, the refusal to
; install over running programs, and the post-install size check.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "Kingdom Come Co-op"
#define AppExeName "KCDMP_launcher.exe"
#define AppPublisher "KCD2-MP contributors (Host World fork)"
#define AppUrl "https://github.com/ILliTAH/KingdomCome-Together"

; Evergreen WebView2 Runtime bootstrapper. Microsoft's documented permanent
; redirect; ~2 MB, and it installs per-user when Setup is not elevated, which
; is exactly our case (PrivilegesRequired=lowest).
#define WebView2BootstrapUrl "https://go.microsoft.com/fwlink/p/?LinkId=2124703"

[Setup]
AppId={{C00D510F-9A40-4B7E-88DA-1E681BA576C4}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
VersionInfoVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}

; %LocalAppData%, deliberately, and PrivilegesRequired=lowest with it: the
; launcher writes settings.json beside itself, and a non-technical friend
; never sees a UAC prompt from Setup. (The Game Pass script asks for one
; elevation, once, to add a firewall rule.)
DefaultDirName={localappdata}\KCDMP-HostWorld
PrivilegesRequired=lowest
UsePreviousAppDir=yes
; No folder page. The uninstaller removes the whole install folder (settings
; and logs are created in it at run time), so a folder picked by hand -- an
; existing D:\Games, say -- would be deleted with everything else in it.
DisableDirPage=yes

DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; The product was first called "KCD2 Multiplayer - Host World": an upgrade must
; not keep that Start Menu folder.
UsePreviousGroup=no
LicenseFile=..\LICENSE
OutputDir=..\release
OutputBaseFilename=KingdomCome-Coop-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
SetupLogging=yes
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; The whole Publish-Release.ps1 output: launcher, agent, relay, native DLL,
; injector, the self-contained .NET runtime beside them, and the fork's own
; payload (mod\, save\, Start-*.ps1). The launcher and the scripts resolve
; everything against this one flat folder.
Source: "..\release\KCDMP\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Shortcuts under the product's first name (see UsePreviousGroup above).
Type: files; Name: "{autodesktop}\KCD2 Multiplayer - Host World.lnk"
Type: filesandordirs; Name: "{autoprograms}\KCD2 Multiplayer - Host World"
; The shortcut the launcher's window library (Photino) makes for itself in the
; Start Menu, named after the window title of that first version.
Type: files; Name: "{userprograms}\KCD2 MP Launcher - Host World.lnk"

[Icons]
; WorkingDir is kept for the shortcut's sake; the launcher no longer depends
; on it (it sets its working directory to its own folder at startup).
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\KCDMP-HostWorld"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\KCDMP-HostWorld"; ValueType: string; ValueName: "InstallDir"; ValueData: "{app}"

[Run]
Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Description: "Launch {#AppName} now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; settings.json, favourites and the native plugin's log are created at
; runtime, not by [Files], so nothing else would clean them up.
Type: filesandordirs; Name: "{app}"
; Photino's own Start Menu shortcut (see [InstallDelete]); Setup did not
; create it, so nothing else would remove it.
Type: files; Name: "{userprograms}\Kingdom Come Co-op.lnk"

[Code]

{ -------------------------------------------------------------- WebView2 }

function WebView2Installed(): Boolean;
var
  V: String;
begin
  Result := False;
  { Per-machine install writes the 32-bit view; per-user writes HKCU. }
  if RegQueryStringValue(HKLM32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', V) then
    if (V <> '') and (V <> '0.0.0.0') then Result := True;
  if not Result then
    if RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', V) then
      if (V <> '') and (V <> '0.0.0.0') then Result := True;
  if not Result then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', V) then
      if (V <> '') and (V <> '0.0.0.0') then Result := True;
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if not WizardSilent then
  begin
    if ProgressMax > 0 then
      WizardForm.PreparingLabel.Caption :=
        'Downloading the Microsoft WebView2 runtime (' +
        IntToStr(Progress div 1024) + ' of ' + IntToStr(ProgressMax div 1024) + ' KB)...'
    else
      WizardForm.PreparingLabel.Caption :=
        'Downloading the Microsoft WebView2 runtime (' + IntToStr(Progress div 1024) + ' KB)...';
  end;
  Result := True;
end;

{ Returns '' on success, or a message explaining why setup cannot continue. }
function EnsureWebView2(): String;
var
  Bootstrapper: String;
  ResultCode: Integer;
begin
  Result := '';
  if WebView2Installed() then Exit;

  if not WizardSilent then
    WizardForm.PreparingLabel.Caption := 'Downloading the Microsoft WebView2 runtime...';

  Bootstrapper := ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe');
  try
    DownloadTemporaryFile('{#WebView2BootstrapUrl}', 'MicrosoftEdgeWebview2Setup.exe', '', @OnDownloadProgress);
  except
    Result :=
      'The launcher needs the Microsoft WebView2 runtime, and it could not be downloaded:' + #13#10 +
      GetExceptionMessage + #13#10#13#10 +
      'Check your internet connection and try again, or install "Microsoft Edge WebView2 Runtime"' + #13#10 +
      'from microsoft.com and re-run this installer.';
    Exit;
  end;

  if not WizardSilent then
    WizardForm.PreparingLabel.Caption := 'Installing the Microsoft WebView2 runtime...';

  if not Exec(Bootstrapper, '/silent /install', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'The WebView2 runtime installer could not be started (error ' + IntToStr(ResultCode) + ').';
    Exit;
  end;

  if not WebView2Installed() then
    Result :=
      'The WebView2 runtime installer finished (exit code ' + IntToStr(ResultCode) + ') but the' + #13#10 +
      'runtime is still not registered. The launcher cannot draw its window without it.' + #13#10#13#10 +
      'Install "Microsoft Edge WebView2 Runtime" from microsoft.com, then run this installer again.';
end;

{ ------------------------------------------------------------ wizard flow }

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
    WizardForm.FinishedLabel.Caption :=
      'Kingdom Come Co-op is installed. Nothing else is needed: the launcher' + #13#10 +
      'finds your game by itself -- Kingdom Come: Deliverance II from Xbox Game Pass, or' + #13#10 +
      'the KCD2 Modding Tools from Steam -- and puts the mod into it when you launch.' + #13#10#13#10 +
      'To play together, one machine hosts (HOST GAME) and shares the address the' + #13#10 +
      'launcher shows; everyone else adds that address with ADD SERVER and clicks' + #13#10 +
      'JOIN SERVER.';
end;

{ Kills this project's own processes (never the game). Shared by the silent
  install gate below and by silent uninstall -- unattended means unattended,
  and the launcher only writes settings.json when the user presses Save, so
  there is no in-flight state to lose. }
procedure KillOursQuietly();
var
  I, ResultCode: Integer;
  Names: array[0..3] of String;
begin
  Names[0] := 'KCDMP_launcher.exe';
  Names[1] := 'KcdMpClient.exe';
  Names[2] := 'KcdMpServer.exe';
  Names[3] := 'KcdMpMasterServer.exe';
  for I := 0 to 3 do
    Exec(ExpandConstant('{cmd}'), '/c taskkill /f /im "' + Names[I] + '"',
         '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

{ WO-32 follow-up -- the install-time process gate (kept from the stock
  installer). Refuse to install while ANY of this project's processes, or the
  game, is running, no matter where they run from: installing over running
  programs is how an update half-applies. Interactive gets Retry/Cancel;
  silent kills our own processes but never the game. }
function FirstInstallBlocker(): String;
var
  I, ResultCode: Integer;
  Names: array[0..4] of String;
begin
  Result := '';
  Names[0] := 'KCDMP_launcher.exe';
  Names[1] := 'KcdMpClient.exe';
  Names[2] := 'KcdMpServer.exe';
  Names[3] := 'KcdMpMasterServer.exe';
  Names[4] := 'KingdomCome.exe';

  for I := 0 to 4 do
    if Exec(ExpandConstant('{cmd}'),
            '/c tasklist /fi "imagename eq ' + Names[I] + '" /nh | find /i "' + Names[I] + '"',
            '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      if ResultCode = 0 then
      begin
        Result := Names[I];
        Exit;
      end;
end;

{ Returns '' when clear to install, or an abort message. }
function EnsureNothingRunning(): String;
var
  Blocker: String;
begin
  Result := '';

  Blocker := FirstInstallBlocker();
  while Blocker <> '' do
  begin
    if WizardSilent then
    begin
      if Blocker = 'KingdomCome.exe' then
      begin
        Result := 'The game (KingdomCome.exe) is running. Setup cannot safely replace files while it is. Close the game and run Setup again.';
        Exit;
      end;
      KillOursQuietly();
      Sleep(1500);
      Blocker := FirstInstallBlocker();
      if Blocker <> '' then
      begin
        Result := Blocker + ' is still running and could not be closed. Close it and run Setup again.';
        Exit;
      end;
      Break;
    end;

    if MsgBox(Blocker + ' is still running.' + #13#10#13#10 +
              'Installing over running programs is how an update half-applies: some files update, the ones in use silently do not, and the result looks installed but is a mix of two versions.' + #13#10#13#10 +
              'Close it (launcher, agent, relay, and the game), then click Retry.',
              mbConfirmation, MB_RETRYCANCEL) <> IDRETRY then
    begin
      Result := 'Setup was cancelled because ' + Blocker + ' was still running.';
      Exit;
    end;
    Blocker := FirstInstallBlocker();
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  { The process gate, before anything is written -- see its comment above. }
  Result := EnsureNothingRunning();
  if Result <> '' then Exit;

  Result := EnsureWebView2();
end;

{ WO-32 follow-up -- the install proves itself before declaring success.

  Build-Installer.ps1 writes install-manifest.txt (<relative path>|<size>)
  into the payload after publish, so it ships inside the install directory
  and describes exactly what this Setup carried. Comparing sizes on disk
  against it catches a file that could not be overwritten and kept its old
  size, instead of Setup finishing green over a mix of two versions.

  The verdict is always written to install-verify.txt beside the launcher;
  interactive failures also get a message box. }
procedure VerifyInstalledFiles();
var
  Manifest, Failures: TArrayOfString;
  Verdict: TArrayOfString;
  I, FailCount: Integer;
  Line, RelPath, FullPath: String;
  Bar: Integer;
  WantSize, GotSize: Int64;
begin
  if not LoadStringsFromFile(ExpandConstant('{app}\install-manifest.txt'), Manifest) then
  begin
    Log('verify: install-manifest.txt missing -- nothing to check against');
    Exit;
  end;

  FailCount := 0;
  SetArrayLength(Failures, 0);
  for I := 0 to GetArrayLength(Manifest) - 1 do
  begin
    Line := Trim(Manifest[I]);
    if Line = '' then Continue;
    Bar := Pos('|', Line);
    if Bar = 0 then Continue;
    RelPath := Copy(Line, 1, Bar - 1);
    WantSize := StrToInt64Def(Copy(Line, Bar + 1, MaxInt), -1);
    if WantSize < 0 then Continue;

    FullPath := ExpandConstant('{app}\') + RelPath;
    GotSize := -1;
    if not FileSize64(FullPath, GotSize) then GotSize := -1;
    if GotSize <> WantSize then
    begin
      FailCount := FailCount + 1;
      SetArrayLength(Failures, FailCount);
      if GotSize < 0 then
        Failures[FailCount - 1] := RelPath + '  (missing)'
      else
        Failures[FailCount - 1] := RelPath + '  (' + IntToStr(GotSize) + ' bytes, expected ' + IntToStr(WantSize) + ')';
      Log('verify FAIL: ' + Failures[FailCount - 1]);
    end;
  end;

  if FailCount = 0 then
  begin
    SetArrayLength(Verdict, 1);
    Verdict[0] := 'PASS  all files match the install manifest';
    SaveStringsToFile(ExpandConstant('{app}\install-verify.txt'), Verdict, False);
    Log('verify: PASS (' + IntToStr(GetArrayLength(Manifest)) + ' files)');
    Exit;
  end;

  SetArrayLength(Verdict, FailCount + 1);
  Verdict[0] := 'FAIL  ' + IntToStr(FailCount) + ' file(s) did not install correctly:';
  for I := 0 to FailCount - 1 do
    Verdict[I + 1] := '  ' + Failures[I];
  SaveStringsToFile(ExpandConstant('{app}\install-verify.txt'), Verdict, False);

  if not WizardSilent then
    MsgBox('Setup finished, but ' + IntToStr(FailCount) + ' file(s) did not install correctly -- ' +
           'most likely something was still using them.' + #13#10#13#10 +
           'First affected file: ' + Failures[0] + #13#10#13#10 +
           'Close the launcher, the agent, the relay and the game, then run this installer again.' + #13#10 +
           'The full list is in install-verify.txt in the install folder.',
           mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    VerifyInstalledFiles();
end;

{ ----------------------------------------------------------- uninstalling }

function AnyOfOursRunning(): Boolean;
var
  I, ResultCode: Integer;
  Names: array[0..3] of String;
begin
  Result := False;
  Names[0] := 'KCDMP_launcher.exe';
  Names[1] := 'KcdMpClient.exe';
  Names[2] := 'KcdMpServer.exe';
  Names[3] := 'KcdMpMasterServer.exe';

  for I := 0 to 3 do
    if Exec(ExpandConstant('{cmd}'),
            '/c tasklist /fi "imagename eq ' + Names[I] + '" /nh | find /i "' + Names[I] + '"',
            '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      if ResultCode = 0 then
      begin
        Result := True;
        Exit;
      end;
end;

{ Runs before anything is removed, and returning False aborts cleanly with
  nothing touched: a half-finished uninstall is worse than one that did not
  start. }
function InitializeUninstall(): Boolean;
begin
  Result := True;

  while AnyOfOursRunning() do
  begin
    if UninstallSilent then
    begin
      KillOursQuietly();
      Sleep(1500);
      if AnyOfOursRunning() then
      begin
        Result := False;
        Exit;
      end;
      Break;
    end;

    if MsgBox('KCD2 Multiplayer is still running.' + #13#10#13#10 +
              'Close the launcher (and the game, if it is open) first, then click Retry.' + #13#10 +
              'Uninstalling now would leave files behind that Windows will not let it delete.',
              mbConfirmation, MB_RETRYCANCEL) <> IDRETRY then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

{ What this project put into the game itself is undone by a script that ships
  in the install folder (Uninstall-GameChanges.ps1), because it needs the game
  paths from settings.json and the same user.cfg handling the launcher uses:
    * the auto-load line in the Game Pass game's user.cfg -- always, since it
      changes how the game starts and nothing would be left to switch it off;
    * the mod folders -- only when asked, and never in an unattended run: they
      are in the player's game, which is not ours to empty unasked. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Script, Params: String;
  ResultCode: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if FileExists(ExpandConstant('{app}\KCDMP_launcher.exe')) and (not UninstallSilent) then
      MsgBox('Some files were in use and could not be removed. They are still in:' + #13#10#13#10 +
             ExpandConstant('{app}') + #13#10#13#10 +
             'Nothing there is needed any more -- deleting that folder by hand finishes the job.',
             mbInformation, MB_OK);
    Exit;
  end;

  if CurUninstallStep <> usUninstall then Exit;

  Script := ExpandConstant('{app}\Uninstall-GameChanges.ps1');
  if not FileExists(Script) then Exit;

  Params := '-NoProfile -ExecutionPolicy Bypass -File "' + Script + '"';
  if not UninstallSilent then
    if MsgBox('Also remove the multiplayer mod from your game?' + #13#10#13#10 +
              'Choosing No leaves the mod files where they are. Either way, the line that makes the game load your last save by itself is taken out of user.cfg in the game folder.',
              mbConfirmation, MB_YESNO) = IDYES then
      Params := Params + ' -RemoveMods';

  Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Params, '', SW_HIDE,
       ewWaitUntilTerminated, ResultCode);
end;
