set solutionDir=%~f1
set targetDir=%~f2

xcopy "%targetDir%bin\Release\JukeboxSpotify.dll" "%solutionDir%"  /I /Q /Y
xcopy "%targetDir%SubnauticaJukeboxMod\mod.json" "%solutionDir%"  /I /Q /Y