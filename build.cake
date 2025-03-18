using Cake.Core.IO;
using Cake.Common.Diagnostics;
using Cake.Common.Tools.DotNet;

#addin "nuget:?package=Cake.Npm&version=5.1.0"
#load "./build/AssemblyPublicizerTool.cake"

var target = Argument("target", "Build");
var configuration = Argument("configuration", "Release");

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

RunTarget(target);
