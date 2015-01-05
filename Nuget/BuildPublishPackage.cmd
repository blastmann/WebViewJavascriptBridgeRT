@echo off

:: Set repository info
set key=77390135-ecaf-47e7-9f15-ae2b38102747
set url=https://www.nuget.org/packages/WebViewJavascriptBridgeRT/

:: Make sure the nuget executable is writable
attrib -R NuGet.exe

:: Make sure the nupkg files are writeable and create backup
IF EXIST *.nupkg (
	echo.
	echo Creating backup...
	forfiles /m *.nupkg /c "cmd /c attrib -R @File"
	forfiles /m *.nupkg /c "cmd /c move /Y @File @File.bak"
)

echo.
echo Updating NuGet...
rem cmd /c nuget.exe update -Self

echo.
echo Creating package...
 nuget.exe pack %1.nuspec -Verbose -Version %2
rem copy %1.%2.nupkg  ..\NugetPrivateSource\Packages  /Y
copy *.nupkg ..

:: Check if package should be published
IF /I "%3"=="Publish" goto :publish
goto :eof

:publish
IF EXIST *.nupkg (
	echo.
	echo Publishing package...
	echo API Key: %key%
	echo NuGet Url: %url%
	forfiles /m *.nupkg /c "cmd /c nuget.exe push @File %key% -Source %url%"
	goto :eof
)

:eof