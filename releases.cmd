@ECHO OFF
dotnet restore

@IF """%~1"""=="""""" (set pver=1.0.0) ELSE (set pver=%~1)
@ECHO version set to %pver%

@FOR %%r in (linux-x64 linux-arm win-x64) DO CALL :loopbody %%r
@ECHO Complete
GOTO :EOF

:loopbody
@ECHO Publishing %1
@DEL bin\release\publish\%1\*.* /F /Q /S >NUL
@DEL bin\release\gcodeclean-%1-standalone.zip >NUL

@set outDir=bin/release/publish/%1
@set destZip=bin/release/gcodeclean-%1-standalone.zip

@dotnet publish --configuration Release /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary -p:Version=%pver% --runtime %1 --output %outDir% --self-contained
@SET psCmdA="Add-Type -A 'System.IO.Compression.FileSystem'; [IO.Compression.ZipFile]::CreateFromDirectory("
@SET psCmd=%psCmdA%'%outDir%', '%destZip%');
powershell.exe -nologo -noprofile -command %psCmd%
GOTO :EOF
