@ECHO off

SET NAME=%1

ECHO Building %NAME%...

dotnet publish "%~dp0/../Copilot4.csproj" --nologo ^
	-v quiet ^
	-r %NAME% ^
	-c Release ^
	--self-contained true ^
	-p:PublishReadyToRun=true ^
	-p:PublishSingleFile=true ^
	-o "%~dp0bin/Build/copilot4-v0.1-%NAME%/"