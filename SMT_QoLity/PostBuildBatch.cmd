:: ~ removes the double quotes
set MSBuildStartupDirectory=%~1
set SupermarketTogetherFolder=%~2
set PackageId=%~3
set TargetPath=%~4
set TargetDir=%~5
set TargetName=%~6
set TargetExt=%~7
set Configuration=%~8
set Version=%~9
:: cmd only allows 9 extra arguments, so we skip the first 9 with shift so that %1 now refers to the 10th argument
for /L %%i in (0,1,8) do @shift
set ConfigurationName=%~1

if "%SupermarketTogetherFolder%"=="" (
    echo ERROR: SupermarketTogetherFolder argument is empty. Are you passing the correct parameters to this batch?
    exit /B 33
)
if "%MSBuildStartupDirectory%"=="" (
    echo ERROR: MSBuildStartupDirectory argument is empty. Are you passing the correct parameters to this batch?
    exit /B 34
)

:: Must start with a drive letter and colon (e.g. C:\) so we dont get fucked with relative paths
set "first3=%SupermarketTogetherFolder:~0,3%"
echo %first3% | findstr /I ":\\" >nul
if errorlevel 1 (
    echo ERROR: SupermarketTogetherFolder has an invalid or relative path: "%SupermarketTogetherFolder%"
    exit /b 35
)
set "first3=%MSBuildStartupDirectory:~0,3%"
echo %first3% | findstr /I ":\\" >nul
if errorlevel 1 (
    echo ERROR: MSBuildStartupDirectory has an invalid or relative path: "%MSBuildStartupDirectory%"
    exit /b 36
)

if not exist "%SupermarketTogetherFolder%" (
    echo ERROR: Target folder "%SupermarketTogetherFolder%" doesnt exist. Make sure that the path in the PropertyGroup named "SupermarketTogetherFolder" is correct for the current active release mode "%ConfigurationName%"
    exit /B 52
)

set SmtTargetPath=%SupermarketTogetherFolder%\%PackageId%
set TempSmtTargetPath=%MSBuildStartupDirectory%\%PackageId%
set TempDedicatedSmtTargetPath=%TempSmtTargetPath%_DedicatedServer
set SoundEffectsPath=SoundEffects
set AssetsPath=Assets
set AssetsPathDebug=Assets\Debug
set DocsFolder=%MSBuildStartupDirectory%\Docs
set readmeSource=%DocsFolder%\README.md
set changelogSource=%DocsFolder%\CHANGELOG.md
set ReleaseFolder=%MSBuildStartupDirectory%\!Release

set DedicatedServerPluginsFolder="\\DAMNTRY-SERVER\plugins"\%PackageId%

::Cleanup
if exist "%TempSmtTargetPath%\" (
    ECHO *** The temp folder wasnt deleted correctly from previous launch. Removing it now.
    del "%TempSmtTargetPath%" /s /q
)
if exist "%TempDedicatedSmtTargetPath%\" (
    ECHO *** The temp folder for the dedicated server wasnt deleted correctly from previous launch. Removing it now.
    del "%TempDedicatedSmtTargetPath%" /s /q
)

::Cleanup files in the target directory manually instead of the entire
::  folder, to make sure we keep the method signature metadata
if exist "%SmtTargetPath%\" (
    ECHO *** Clearing mod files from target plugin folder
    del "%SmtTargetPath%\*.dll" /s /q
    del "%SmtTargetPath%\*.pdb" /s /q
    del "%SmtTargetPath%\*.md" /s /q
)
if exist "%SmtTargetPath%\%SoundEffectsPath%" (
    ECHO *** Removing old sound assets from target plugins folder
    rmdir /S /Q "%SmtTargetPath%\%SoundEffectsPath%"
)
if exist "%SmtTargetPath%\%AssetsPath%" (
    ECHO *** Removing old general assets from target plugins folder
    rmdir /S /Q "%SmtTargetPath%\%AssetsPath%"
)
if exist "%SmtTargetPath%\%AssetsPathDebug%" (
    ECHO *** Removing old debug files from target plugins folder
    rmdir /S /Q "%SmtTargetPath%\%AssetsPathDebug%"
)

:: Copy main dlls
ECHO *** Copying release files into temp folder
md "%TempSmtTargetPath%"
copy "%TargetPath%" "%TempSmtTargetPath%"\%TargetName%-%Configuration%%TargetExt%
copy "%TargetDir%\Damntry.Globals*.dll" "%TempSmtTargetPath%"

:: Copy external dependencies
copy "%TargetDir%\UniTask*.dll" "%TempSmtTargetPath%"
copy "%TargetDir%\LeanTween*.dll" "%TempSmtTargetPath%"
md "%TempSmtTargetPath%\%AssetsPath%"

xcopy "%TargetDir%\%AssetsPath%\*.*" "%TempSmtTargetPath%\%AssetsPath%" /Y /I /E
xcopy "%TargetDir%\%SoundEffectsPath%\*.*" "%TempSmtTargetPath%\%SoundEffectsPath%" /Y /I /E

copy "%readmeSource%" "%TempSmtTargetPath%\!README.md"
copy "%changelogSource%" "%TempSmtTargetPath%\!CHANGELOG.md"

if "%ConfigurationName%" NEQ "Release" (
    ECHO *** GENERATING IN DEBUG MODE ***
    ECHO *** Copying DEBUG files into temp folder
    copy "%TargetDir%"\"%TargetName%".pdb "%TempSmtTargetPath%"\"%TargetName%"-%Configuration%.pdb
    copy "%TargetDir%\Damntry.Globals*.pdb" "%TempSmtTargetPath%"
    copy "%TargetDir%\UnityHotReload.dll" "%TempSmtTargetPath%"
    md "%TempSmtTargetPath%\%AssetsPathDebug%"
    xcopy  "%TargetDir%\%AssetsPathDebug%\*.*" "%TempSmtTargetPath%\%AssetsPathDebug%\" /Y /I /E
)else (
    ECHO *** GENERATING IN RELEASE MODE ***
    ECHO *** Removing debug assets from temp folder
    :: We need this so earlier we can just copy the entirety of %TargetDir%\%AssetsPath%, which includes debug.
    rmdir /S /Q "%TempSmtTargetPath%\%AssetsPathDebug%"
)

ECHO *** Creating target plugin folder
md "%SmtTargetPath%"

if not exist "%SmtTargetPath%" (
    echo ERROR: Target folder "%SmtTargetPath%" doesnt exist and could not be created. Is it a valid path?
    exit /B 52
)

if "%ConfigurationName%" EQU "Debug" (
    ECHO *** Creating dedicated temp folder
    md "%TempDedicatedSmtTargetPath%"
    xcopy "%TempSmtTargetPath%\*.*" "%TempDedicatedSmtTargetPath%" /Y /I /E

    :: TODO - This doesnt work to create a separate process for the copy. The way msbuild works, this
    :: will only execute after all other operations below are finished, so it makes no damn difference.
    :: I would need to execute a powershell .ps1 file from msbuild, move all logic from PostBuildBatch.cmd
    :: to the .ps1 file, and for the dedicated file copy logic I would use Start-Process
    :: which theoretically should actually be launched as a new separate process.
    ECHO *** Launching new process to copy the files to dedicated server
    start "" cmd /c "echo *** Attempting to copy files to dedicated server from path "%TempDedicatedSmtTargetPath%\*.*" && xcopy "%TempDedicatedSmtTargetPath%\*.*" "%DedicatedServerPluginsFolder%" /Y /I /E && echo *** Deleting temp folder to clean up && rmdir /S /Q "%TempDedicatedSmtTargetPath%""
)

ECHO *** Copying from temp folder to plugins folder
xcopy "%TempSmtTargetPath%\*.*" "%SmtTargetPath%" /Y /I /E
if errorlevel 1 (
    echo ERROR: At least one target file seems to be in use. Is the game running?
    exit /B 57
)

set nexusPath=%ReleaseFolder%\Nexus
set thunderstorePath=%ReleaseFolder%\Thunderstore

set ZipFileName=%SupermarketTogetherFolder%\%TargetName%_%Version%.zip
set ZipFileNameThunderStore=%thunderstorePath%\%TargetName%_%Version%.zip

set ZipTempTSPath=%thunderstorePath%\Temp
set ZipTempTSModPath=%ZipTempTSPath%\BepInEx\plugins\%PackageId%

if "%ConfigurationName%" EQU "Release" (
    ECHO *** Creating base zip file at "%ZipFileName%
    tar -a -cf "%ZipFileName%" -C "%MSBuildStartupDirectory%" "%PackageId%\*.*"
 
    md "%ReleaseFolder%"

    ECHO *** Creating Nexus release
    rmdir /S /Q "%nexusPath%"
    md "%nexusPath%"
    "C:\Users\Damntry\Visual Studio Projects\Visual Studio 2019 Projects\repos\Markdown2NexusBB\bin\Release\net8.0\Markdown2NexusBB.exe" "%readmeSource%" "%nexusPath%\README.md"
    copy "%changelogSource%" "%nexusPath%"
    copy "%ZipFileName%" "%nexusPath%"
 
    ECHO *** Creating Thunderstore release
    rmdir /S /Q "%thunderstorePath%"
    md "%thunderstorePath%" "%ZipTempTSPath%" "%ZipTempTSModPath%"
    copy "%readmeSource%" "%ZipTempTSPath%"
    copy "%changelogSource%" "%ZipTempTSPath%"
    copy "%DocsFolder%\icon_256.png" "%ZipTempTSPath%\icon.png"
    copy "%DocsFolder%\manifest.json" "%ZipTempTSPath%"
    copy "%TempSmtTargetPath%" "%ZipTempTSModPath%"
    tar -a -cf "%ZipFileNameThunderStore%" -C "%ZipTempTSPath%" "*.*"
    rmdir /S /Q "%ZipTempTSPath%"
)

ECHO *** Deleting temp folders to clean up
rmdir /S /Q "%TempSmtTargetPath%"
rmdir /S /Q "%TempDedicatedSmtTargetPath%"

ECHO *** FINISHED