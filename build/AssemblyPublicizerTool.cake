using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Common.Tools.DotNet;

#tool "dotnet:?package=bepinex.assemblypublicizer.cli&version=0.4.3"

public sealed class AssemblyPublicizerTool : DotNetTool<DotNetSettings>
{
    public AssemblyPublicizerTool(ICakeContext context)
        : base(context.FileSystem, context.Environment, context.ProcessRunner, context.Tools)
    {
    }

    public void Publicize(string inputPath, string outputPath)
    {
        // Build up the arguments you’d pass to: dotnet assembly-publicizer <input> <output>
        var builder = new ProcessArgumentBuilder();

        // First argument is the local tool command name:
        builder.Append("assembly-publicizer");

        // Then the two required file paths:
        builder.AppendQuoted(inputPath);
        builder.Append("-o");
        builder.AppendQuoted(outputPath);
        builder.Append("-f");

        // Run the actual command via 'dotnet'
        Run(
            new DotNetToolSettings(),
            builder
        );
    }
}
