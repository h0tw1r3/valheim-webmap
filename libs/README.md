External requirements:

1. Extract the current release of [BepInEx](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.2/BepInEx_win_x64_5.4.23.2.zip)
   to this directory and rename to `BepInEx`
2. Create a directory named `valhiem` and copy these files from your Valhiem
   installation into it:
   ```
   assembly_utils.dll
   assembly_valheim.dll
   Mono.Security.dll
   UnityEngine.CoreModule.dll
   UnityEngine.dll
   UnityEngine.ImageConversionModule.dll
   UnityEngine.JSONSerializeModule.dll
   ```
3. _Publicize_ the utils and valheim assemblies.
   The most straight-forward was to do this is with the provided cake build
   file. From the project directory root, run in a terminal:
   ```
   dotnet tool restore
   dotnet cake --target=Publicize
   ```
