Unicode true
!ifdef TEST_ROOT
  !ifndef TEST_ID
    !error "TEST_ID is required for an isolated installer."
  !endif
  RequestExecutionLevel user
  !define INSTALL_ID "${TEST_ID}"
  !define APP_DIR "${TEST_ROOT}\installed"
  !define PROFILE_DIR "${TEST_ROOT}\profile"
  !define MENU_DIR "${TEST_ROOT}\StartMenu"
  !define DESKTOP_LINK "${TEST_ROOT}\QSL 邮务台.lnk"
!else
  RequestExecutionLevel admin
  !define INSTALL_ID "QslPost"
  !define APP_DIR "$LOCALAPPDATA\Programs\QslPost"
  !define PROFILE_DIR "$LOCALAPPDATA\QslPost"
  !define MENU_DIR "$SMPROGRAMS\QSL 邮务台"
  !define DESKTOP_LINK "$DESKTOP\QSL 邮务台.lnk"
!endif
!define INSTALL_KEY "Software\${INSTALL_ID}"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${INSTALL_ID}"
ManifestDPIAware true
CRCCheck force
SetCompressor /SOLID lzma
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "WinVer.nsh"
!include "x64.nsh"
!define RELEASE_VERSION "1.4.4"
Name "QSL 邮务台"
Caption "QSL 邮务台 ${RELEASE_VERSION} 安装向导"
OutFile "${OUTPUT}"
!ifdef SIGNED_BUILD
  !uninstfinalize 'powershell.exe -NoLogo -NoProfile -NonInteractive -File "$%QSL_SIGN_SCRIPT%" -Path "%1"' = 0
!endif
InstallDir "${APP_DIR}"
InstallDirRegKey HKCU "${INSTALL_KEY}" "InstallDir"
Icon "${ICON}"
UninstallIcon "${ICON}"
VIProductVersion "${RELEASE_VERSION}.0"
VIAddVersionKey /LANG=2052 "ProductName" "QSL 邮务台"
VIAddVersionKey /LANG=2052 "FileDescription" "QSL 卡片收发与封发系统安装程序"
VIAddVersionKey /LANG=2052 "FileVersion" "${RELEASE_VERSION}"
VIAddVersionKey /LANG=2052 "ProductVersion" "${RELEASE_VERSION}"
VIAddVersionKey /LANG=2052 "LegalCopyright" "QSL Post contributors"
!define MUI_ICON "${ICON}"
!define MUI_UNICON "${ICON}"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${__FILEDIR__}\wizard-panel.bmp"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "安装 QSL 邮务台 ${RELEASE_VERSION}"
!define MUI_WELCOMEPAGE_TEXT "管理业余无线电 QSL 卡片的登记、封发和追溯。$\r$\n$\r$\n升级前请关闭 QSL 邮务台，并使用原安装时的 Windows 账户运行安装包。$\r$\n$\r$\n升级将保留台账、打印设置及备份。"
!insertmacro MUI_PAGE_WELCOME
!define MUI_DIRECTORYPAGE_TEXT_TOP "请选择程序安装目录。升级时会带入原路径。$\r$\n台账和备份保存在独立的资料目录。"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE "QSL 邮务台已安装"
!define MUI_FINISHPAGE_TEXT "已添加桌面和开始菜单快捷方式。$\r$\n$\r$\n启动后，按 F1 可查阅使用手册。"
!define MUI_FINISHPAGE_RUN "$INSTDIR\QslPost.exe"
!define MUI_FINISHPAGE_RUN_TEXT "打开 QSL 邮务台"
!ifdef TEST_ROOT
  !define MUI_FINISHPAGE_RUN_PARAMETERS '--data $\"${PROFILE_DIR}\Data$\"'
!endif
!insertmacro MUI_PAGE_FINISH
!define MUI_UNCONFIRMPAGE_TEXT_TOP "卸载会清除本机台账、自动备份、台站和打印设置，$\r$\n以及查询缓存和演示资料。$\r$\n$\r$\n请关闭软件，再单击「卸载」。"
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

Function .onInit
  SetShellVarContext current
  SetRegView 32
  ${IfNot} ${AtLeastWinXP}
    MessageBox MB_OK|MB_ICONSTOP "本安装程序需要 Windows XP SP3 或更新版本。" /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
  ${If} ${IsWinXP}
  ${AndIfNot} ${AtLeastServicePack} 3
    MessageBox MB_OK|MB_ICONSTOP "请先安装 Windows XP Service Pack 3，再运行安装程序。" /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
  ReadRegDWORD $0 HKLM "Software\Microsoft\NET Framework Setup\NDP\v4\Full" "Install"
  ${If} $0 != 1
  ${AndIf} ${RunningX64}
    SetRegView 64
    ReadRegDWORD $0 HKLM "Software\Microsoft\NET Framework Setup\NDP\v4\Full" "Install"
    SetRegView 32
  ${EndIf}
  ${If} $0 != 1
    MessageBox MB_OK|MB_ICONINFORMATION "未检测到 .NET Framework 4.0 Full 或较新的 4.x 运行库。请先安装 Microsoft .NET Framework，再运行本安装程序。" /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
FunctionEnd

Section "QSL 邮务台"
  SetShellVarContext current
  SetRegView 32
  SetOutPath "$INSTDIR"
  File "${PAYLOAD}\QslPost.exe"
  File "${PAYLOAD}\QslPost.exe.config"
  File "${PAYLOAD}\使用说明.txt"
  Delete "$INSTDIR\DemoStation.xml"
  SetOutPath "$INSTDIR\tracking"
  File "${PAYLOAD}\tracking\QslPost.TrackingView.dll"
  File "${PAYLOAD}\tracking\Microsoft.Web.WebView2.Core.dll"
  File "${PAYLOAD}\tracking\Microsoft.Web.WebView2.WinForms.dll"
  File "${PAYLOAD}\tracking\WebView2Loader.dll"
  File "${PAYLOAD}\tracking\LICENSE.txt"
  File "${PAYLOAD}\tracking\NOTICE.txt"
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "${MENU_DIR}"
  CreateShortcut "${MENU_DIR}\QSL 邮务台.lnk" "$INSTDIR\QslPost.exe"
  Delete "${MENU_DIR}\独立演示库.lnk"
  Delete "${MENU_DIR}\使用说明.lnk"
  CreateShortcut "${MENU_DIR}\使用手册.lnk" "$INSTDIR\QslPost.exe" "--manual"
  CreateShortcut "${MENU_DIR}\卸载 QSL 邮务台.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut "${DESKTOP_LINK}" "$INSTDIR\QslPost.exe"
  WriteRegStr HKCU "${INSTALL_KEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "QSL 邮务台"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${RELEASE_VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\QslPost.exe"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  SetShellVarContext current
  SetRegView 32
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File "${PAYLOAD}\QslPost.DataCleanup.exe"
  DetailPrint "正在清除本机台账、设置和查询缓存。"
  ClearErrors
  ExecWait '"$PLUGINSDIR\QslPost.DataCleanup.exe" "${PROFILE_DIR}"' $0
  ${If} ${Errors}
  ${OrIf} $0 != 0
    MessageBox MB_OK|MB_ICONSTOP "清除资料未完成。请关闭 QSL 邮务台及查询窗口，确认资料盘已连接、资料目录可以访问后，再次卸载。" /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
  Delete "$INSTDIR\QslPost.exe"
  Delete "$INSTDIR\QslPost.exe.config"
  Delete "$INSTDIR\使用说明.txt"
  Delete "$INSTDIR\DemoStation.xml"
  Delete "$INSTDIR\tracking\QslPost.TrackingView.dll"
  Delete "$INSTDIR\tracking\Microsoft.Web.WebView2.Core.dll"
  Delete "$INSTDIR\tracking\Microsoft.Web.WebView2.WinForms.dll"
  Delete "$INSTDIR\tracking\WebView2Loader.dll"
  Delete "$INSTDIR\tracking\LICENSE.txt"
  Delete "$INSTDIR\tracking\NOTICE.txt"
  RMDir "$INSTDIR\tracking"
  Delete "$INSTDIR\Uninstall.exe"
  Delete "${MENU_DIR}\QSL 邮务台.lnk"
  Delete "${MENU_DIR}\独立演示库.lnk"
  Delete "${MENU_DIR}\使用说明.lnk"
  Delete "${MENU_DIR}\使用手册.lnk"
  Delete "${MENU_DIR}\卸载 QSL 邮务台.lnk"
  RMDir "${MENU_DIR}"
  Delete "${DESKTOP_LINK}"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  DeleteRegKey HKCU "${INSTALL_KEY}"
  RMDir "$INSTDIR"
SectionEnd
