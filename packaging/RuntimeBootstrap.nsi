Unicode true
!include "LogicLib.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"

Name "Capico 靓号"
Caption "Capico 靓号"
OutFile "${OUTPUT_EXE}"
Icon "${APP_ICON}"
RequestExecutionLevel user
SilentInstall silent
AutoCloseWindow true
SetCompressor /SOLID lzma
VIProductVersion "1.0.0.0"
VIAddVersionKey /LANG=2052 "ProductName" "Capico 靓号"
VIAddVersionKey /LANG=2052 "FileDescription" "Capico 靓号启动程序"
VIAddVersionKey /LANG=2052 "CompanyName" "Capico"
VIAddVersionKey /LANG=2052 "FileVersion" "1.0.0.0"

Var Release
Var Parameters
Var CheckOnly

Section
  ${GetParameters} $Parameters
  ClearErrors
  ${GetOptions} $Parameters "--check-runtime" $0
  StrCpy $CheckOnly 0
  ${IfNot} ${Errors}
    StrCpy $CheckOnly 1
  ${EndIf}

  ${IfNot} ${RunningX64}
    ${If} $CheckOnly == 0
      MessageBox MB_OK|MB_ICONINFORMATION "Capico 靓号需要 Windows 64 位系统。"
    ${EndIf}
    SetErrorLevel 3
    Quit
  ${EndIf}

  StrCpy $Release 0
  SetRegView 32
  ReadRegDWORD $Release HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $Release < 461808
    SetRegView 64
    ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
    ${If} $0 > $Release
      StrCpy $Release $0
    ${EndIf}
  ${EndIf}
  ; Compile-time override only for regression executables, never a launch argument.
  !ifdef RUNTIME_TEST_RELEASE
    StrCpy $Release ${RUNTIME_TEST_RELEASE}
  !endif
  ${If} $Release < 461808
    ${If} $CheckOnly == 0
      MessageBox MB_YESNO|MB_ICONINFORMATION|MB_DEFBUTTON2 "此电脑尚未检测到 .NET Framework 4.7.2 或更高版本，暂时无法启动 Capico 靓号。$\r$\n$\r$\n请安装微软 .NET Framework 运行时（例如 4.8），安装完成后重新打开本程序；如果提示重启，请先重启电脑。$\r$\n$\r$\n是否打开微软官方下载页面？" IDNO MissingDone
      ExecShell "open" "https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/net48"
      IfErrors 0 MissingDone
      MessageBox MB_OK|MB_ICONINFORMATION "无法打开浏览器，请手动访问：$\r$\nhttps://dotnet.microsoft.com/zh-cn/download/dotnet-framework/net48"
    ${EndIf}
    MissingDone:
    SetErrorLevel 2
    Quit
  ${EndIf}

  ${If} $CheckOnly == 1
    SetErrorLevel 0
    Quit
  ${EndIf}

  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File /oname=ShareLauncher.exe "${MANAGED_EXE}"
  StrCpy $0 ""
  ClearErrors
  ${GetOptions} $Parameters "--extract-only" $1
  ${IfNot} ${Errors}
    StrCpy $0 "--extract-only"
  ${EndIf}
  ClearErrors
  ${GetOptions} $Parameters "--verify-only" $1
  ${IfNot} ${Errors}
    StrCpy $0 "--verify-only"
  ${EndIf}
  ClearErrors
  ExecWait '"$PLUGINSDIR\ShareLauncher.exe" $0' $1
  ${If} ${Errors}
    MessageBox MB_OK|MB_ICONSTOP "启动失败，请重新下载完整程序后再试。"
    SetErrorLevel 1
  ${Else}
    SetErrorLevel $1
  ${EndIf}
SectionEnd
