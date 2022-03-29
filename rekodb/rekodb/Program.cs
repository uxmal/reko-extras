// See https://aka.ms/new-console-template for more information

using Reko;
using Reko.Core;
using Reko.Core.Configuration;
using Reko.Core.Services;
using Reko.Database;
using Reko.Loading;
using System.ComponentModel.Design;

public class Driver
{
    static void Main(string[]args)
    {
        var program = LoadProgram(args[0]);
        SaveProgramToDatabase(program);
    }

    private static void SaveProgramToDatabase(Program program)
    {
        using var file = File.CreateText(Path.ChangeExtension(program.Location.GetFilename(), ".rekodb"));
        var json = new JsonWriter(file);
        var programSer = new ProgramSerializer(json);
        programSer.Serialize(program);
    }

    static Program LoadProgram(string filename)
    {
        var sc = new ServiceContainer();
        sc.AddService<IPluginLoaderService>(new PluginLoaderService());
        var fsSvc = new FileSystemServiceImpl();
        sc.AddService<IFileSystemService>(fsSvc);
        var cfgSvc = RekoConfigurationService.Load(sc, "reko/reko.config");
        sc.AddService<IConfigurationService>(cfgSvc);
        var listener = new NullDecompilerEventListener();
        sc.AddService<DecompilerEventListener>(listener);
        sc.AddService<ITypeLibraryLoaderService>(new TypeLibraryLoaderServiceImpl(sc));
        sc.AddService<IDecompiledFileService>(new DecompiledFileService(sc, fsSvc, listener));
        var ldr = new Loader(sc);
        var image = (Program) ldr.Load(ImageLocation.FromUri(filename));
        var project = Project.FromSingleProgram(image);
        var dec = new Reko.Decompiler(project, sc);
        dec.ScanPrograms();
        return image;
    }
}
