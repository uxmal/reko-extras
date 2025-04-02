// See https://aka.ms/new-console-template for more information

using Reko;
using Reko.Core;
using Reko.Core.Configuration;
using Reko.Core.Services;
using Reko.Database;
using Reko.Loading;
using Reko.Services;
using System.ComponentModel.Design;
using System.Diagnostics;

public class Driver
{
    static void Main(string[]args)
    {
        var program = LoadProgram(args[0]);
        SaveProgramToDatabase(program);
    }

    private static void SaveProgramToDatabase(Program program)
    {
        var path = Path.ChangeExtension(program.Location.GetFilename(), ".rekodb");
        using var file = File.CreateText(path);
        var json = new JsonWriter(file);
        var programSer = new ProgramSerializer(json);
        var stopw = new Stopwatch();
        stopw.Start();
        programSer.Serialize(program);
        stopw.Start();
        Console.WriteLine("Serialized to {0} in {1} msec", path, stopw.ElapsedMilliseconds);
    }

    static Program LoadProgram(string filename)
    {
        var sc = new ServiceContainer();
        sc.AddService<IPluginLoaderService>(new PluginLoaderService());
        var fsSvc = new FileSystemService();
        sc.AddService<IFileSystemService>(fsSvc);
        var cfgSvc = RekoConfigurationService.Load(sc, "reko/reko.config");
        sc.AddService<IConfigurationService>(cfgSvc);
        var listener = new NullDecompilerEventListener();
        sc.AddService<IDecompilerEventListener>(listener);
        sc.AddService<IEventListener>(listener);
        sc.AddService<ITypeLibraryLoaderService>(new TypeLibraryLoaderServiceImpl(sc));
        sc.AddService<IDecompiledFileService>(new DecompiledFileService(sc, fsSvc, listener));
        var ldr = new Loader(sc);
        Project project;
        switch (ldr.Load(ImageLocation.FromUri(filename)))
        {
        case Program image:
            project = Project.FromSingleProgram(image);
            break;
        case Project proj:
            project = proj;
            break;
        default: throw new NotSupportedException();
        }
        var dec = new Reko.Decompiler(project, sc);
        var stopw = new Stopwatch();
        stopw.Start();
        dec.ScanPrograms();
        stopw.Stop();
        Console.WriteLine("Scanned {0} in {1} msec", filename, stopw.ElapsedMilliseconds);
        return project.Programs[0];
    }
}
