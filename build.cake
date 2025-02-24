using Cake.Core.IO;
using Cake.Common.Diagnostics;
using Cake.Common.Tools.DotNet;

#load "./build/AssemblyPublicizerTool.cake"

var target = Argument("target", "Build");
var configuration = Argument("configuration", "Release");

Task("Clean")
    .WithCriteria(c => HasArgument("rebuild"))
    .Does(() =>
{
    CleanDirectory($"./WebMap/bin/{configuration}");
});

Task("Publicize")
    .Does((context) =>
{
    var publicizer = new AssemblyPublicizerTool(context);

    var assembliesToPublicize = new[]
    {
        new { Input = "./libs/valheim/assembly_utils.dll",   Output = "./libs/valheim/assembly_utils.public.dll" },
        new { Input = "./libs/valheim/assembly_valheim.dll", Output = "./libs/valheim/assembly_valheim.public.dll" }
    };


    foreach (var asm in assembliesToPublicize)
    {
        var inputFile = context.FileSystem.GetFile(asm.Input);
        var outputFile = context.FileSystem.GetFile(asm.Output);

        bool needsPublicizing = !outputFile.Exists ||
            System.IO.File.GetLastWriteTimeUtc(outputFile.Path.FullPath) < System.IO.File.GetLastWriteTimeUtc(inputFile.Path.FullPath);

        if (needsPublicizing)
        {
            publicizer.Publicize(asm.Input, asm.Output);
            context.Information($"Publicized: {asm.Input} -> {asm.Output}");
        }
        else
        {
            context.Information($"Skipping publicize for {asm.Input} (already up-to-date).");
        }
    }
});

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Publicize")
    .Does(() =>
{
    DotNetBuild("./WebMap/WebMap.csproj", new DotNetBuildSettings
    {
        Configuration = configuration,
    });
});

RunTarget(target);
