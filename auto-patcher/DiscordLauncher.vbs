' Lanceur 100% silencieux pour Discord + Vencord Auto-Patch
' Evite le clignotement de la fenetre de console PowerShell
Set WshShell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
psScript = fso.BuildPath(scriptDir, "launch-discord.ps1")

' Repli sur le dossier AppData\Vencord si non trouve a cote
If Not fso.FileExists(psScript) Then
    appData = WshShell.ExpandEnvironmentStrings("%APPDATA%")
    psScript = fso.BuildPath(appData, "Vencord\launch-discord.ps1")
End If

' Reconstitution fidele des arguments
Dim fullArgs, i
fullArgs = ""
For i = 0 To WScript.Arguments.Count - 1
    fullArgs = fullArgs & " """ & Replace(WScript.Arguments(i), """", "\""") & """"
Next

cmd = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & psScript & """" & fullArgs
WshShell.Run cmd, 0, False
