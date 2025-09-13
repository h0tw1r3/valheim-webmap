using Cake.Core.IO;
using Cake.Common.Diagnostics;
using Cake.Common.Tools.DotNet;

#r "System.IO.Compression"
#r "System.IO.Compression.FileSystem"

#addin "nuget:?package=Cake.Npm&version=5.1.0"
#addin "nuget:?package=Cake.Compression&version=0.4.0"
#addin "nuget:?package=SharpZipLib&version=1.4.2"
#load "./build/AssemblyPublicizerTool.cake"
#tool "dotnet:?package=GitVersion.Tool&version=6.4.0"

var target = Argument("target", "Build");
var configuration = Argument("configuration", "Release");
var outputDir = "./output";

var tempDir = System.IO.Path.GetTempPath();

var publicizerInputPath = "./libs/valheim/";
var publicizerOutputPath = publicizerInputPath;

if (DirectoryExists("/opt/steam/libs"))
{
    publicizerInputPath = "/opt/steam/libs/";
    publicizerOutputPath = tempDir;
}

var assembliesToPublicize = new[]
{
    new { Input = $"{publicizerInputPath}assembly_utils.dll",   Output = $"{publicizerOutputPath}assembly_utils.public.dll" },
    new { Input = $"{publicizerInputPath}assembly_valheim.dll", Output = $"{publicizerOutputPath}assembly_valheim.public.dll" }
};

Task("Clean")
    .Does(() =>
{
    DotNetClean("./WebMap/WebMap.csproj", new DotNetCleanSettings
    {
        Configuration = configuration,
        OutputDirectory = outputDir
    });

    CleanDirectory($"./WebMap/obj");
    DeleteFiles("./WebMap/web/main.js");
    foreach (var asm in assembliesToPublicize)
    {
      DeleteFiles(asm.Output);
    }
});

Task("Publicize")
    .Does((context) =>
{
    var publicizer = new AssemblyPublicizerTool(context);

    foreach (var asm in assembliesToPublicize)
    {
        var inputFile = context.FileSystem.GetFile(asm.Input);
        var outputFile = context.FileSystem.GetFile(asm.Output);

        bool needsPublicizing = !outputFile.Exists ||
            System.IO.File.GetLastWriteTimeUtc(outputFile.Path.FullPath) < System.IO.File.GetLastWriteTimeUtc(inputFile.Path.FullPath);

        if (needsPublicizing)
        {
            publicizer.Publicize(asm.Input, asm.Output);
        }
        else
        {
            context.Information($"Skipping publicize for {asm.Input} (already up-to-date).");
        }
    }
});

var BuildTask = Task("Build")
    .Does(() =>
{
    DotNetBuild("./WebMap/WebMap.csproj", new DotNetBuildSettings
    {
        Configuration = configuration,
        OutputDirectory = outputDir
    });
});

if (HasArgument("rebuild")) {
    BuildTask.IsDependentOn("Clean");
}
BuildTask.IsDependentOn("Publicize");
BuildTask.IsDependentOn("BuildNpm");

Task("BuildNpm").Does(() => {
    var settings = new NpmInstallSettings();

    settings.LogLevel = NpmLogLevel.Info;
    settings.WorkingDirectory = "./";
    settings.Production = true;

    NpmInstall(settings);

    if (configuration.Equals("Debug"))
    {
        NpmRunScript("build-dev");
    }
    else
    {
        NpmRunScript("build");
    }
});

Task("Zip-Release").IsDependentOn("Build").Does(() => {
    var tempPackageDir = "./temp";
    var zipFile = $"{outputDir}/WebMap-Release.zip";

    CleanDirectory(tempPackageDir);
    CleanDirectory($"{tempPackageDir}/web");

    CopyFileToDirectory(outputDir + "/WebMap.dll", tempPackageDir);
    CopyFileToDirectory(outputDir + "/websocket-sharp.dll", tempPackageDir);
    CopyFileToDirectory("./README.md", tempPackageDir);
    CopyFileToDirectory("./manifest.json", tempPackageDir);
    CopyFiles("./WebMap/web/*", $"{tempPackageDir}/web/");

    ZipCompress(tempPackageDir, zipFile);

    Information("Release zip created at: {0}", zipFile);
});

RunTarget(target);
