Unicode true
!include "MUI2.nsh"
!ifndef APP_BINARY
!define APP_BINARY "..\dist-native\Moo-Thumbnail.exe"
!endif
!ifndef SETUP_OUTPUT
!define SETUP_OUTPUT "..\dist-native\Moo-Thumbnail-1.1.0-Setup.exe"
!endif
Name "Moo-Thumbnail"
OutFile "${SETUP_OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\Moo-Thumbnail"
InstallDirRegKey HKCU "Software\Moo-Thumbnail" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
Icon "..\assets\icon.ico"
UninstallIcon "..\assets\icon.ico"
VIProductVersion "1.1.0.0"
VIAddVersionKey "ProductName" "Moo-Thumbnail"
VIAddVersionKey "FileDescription" "Moo-Thumbnail Setup"
VIAddVersionKey "FileVersion" "1.1.0"
VIAddVersionKey "LegalCopyright" "Moo-Thumbnail contributors"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\Moo-Thumbnail.exe"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Arabic"
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  IntCmp $0 528040 ready missing ready
  missing:
    MessageBox MB_OK|MB_ICONINFORMATION "Moo-Thumbnail requires .NET Framework 4.8 or newer. Install it from Microsoft first."
    Abort
  ready:
FunctionEnd

Section "Moo-Thumbnail"
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File /oname=Moo-Thumbnail.exe "${APP_BINARY}"
  File "..\LICENSE"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\Moo-Thumbnail"
  CreateShortCut "$SMPROGRAMS\Moo-Thumbnail\Moo-Thumbnail.lnk" "$INSTDIR\Moo-Thumbnail.exe"
  CreateShortCut "$DESKTOP\Moo-Thumbnail.lnk" "$INSTDIR\Moo-Thumbnail.exe"
  WriteRegStr HKCU "Software\Moo-Thumbnail" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Moo-Thumbnail" "DisplayName" "Moo-Thumbnail"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Moo-Thumbnail" "DisplayVersion" "1.1.0"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Moo-Thumbnail" "DisplayIcon" "$INSTDIR\Moo-Thumbnail.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Moo-Thumbnail" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Moo-Thumbnail" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Moo-Thumbnail" "NoRepair" 1
SectionEnd

Section "Uninstall"
  SetShellVarContext current
  Delete "$INSTDIR\Moo-Thumbnail.exe"
  Delete "$INSTDIR\LICENSE"
  Delete "$INSTDIR\Uninstall.exe"
  Delete "$DESKTOP\Moo-Thumbnail.lnk"
  Delete "$SMPROGRAMS\Moo-Thumbnail\Moo-Thumbnail.lnk"
  RMDir "$SMPROGRAMS\Moo-Thumbnail"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "Software\Moo-Thumbnail"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Moo-Thumbnail"
SectionEnd
