# Build requirements

1. Copy the `BepInEx` directory from current release of [BepInExPack Valheim](https://old.thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/versions/)
   to this directory.
2. Create a directory named `valhiem` and copy these files from your Valhiem
   installation into it:

   ```sh
   assembly_utils.dll
   assembly_valheim.dll
   Assembly-CSharp.dll
   Mono.Security.dll
   UnityEngine.CoreModule.dll
   UnityEngine.dll
   UnityEngine.ImageConversionModule.dll
   UnityEngine.JSONSerializeModule.dll
   Splatform.dll
   com.rlabrecque.steamworks.net.dll
   netstandard.dll
   ```

3. _Publicize_ the utils and valheim assemblies.
   The most straight-forward was to do this is with the provided cake build
   file. From the project directory root, run in a terminal:

   ```sh
   dotnet tool restore
   dotnet cake --target=Publicize
   ```
