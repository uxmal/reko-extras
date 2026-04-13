using Reko.Analysis;
using Reko.Core.Configuration;
using Reko.Core;
using Reko.Core.Services;
using Reko.Core.Loading;
using Reko.Core.Output;
using Reko.Extras.SeaOfNodes.Nodes;
using Reko.Loading;
using Reko.Services;
using System.ComponentModel.Design;

// args = new[] { @"\dev\uxmal\reko\master\subjects\regressions\angr-148\test" };
if (args.Length != 1)
{
	Console.Error.WriteLine("Usage: SeaOfNodes <path-to-executable>");
	return 1;
}

var imagePath = args[0];
if (!File.Exists(imagePath))
{
	Console.Error.WriteLine($"File not found: {imagePath}");
	return 1;
}

var decompiler = LoadAndCreateDecompiler(imagePath);
decompiler.ScanPrograms();

foreach (Reko.Core.Program program in decompiler.Project.Programs)
{
	var programFlow = new ProgramDataFlow(program);

	foreach (var proc in program.Procedures.Values)
	{
		Console.WriteLine($"== {proc.Name} ======");

		var builder = new NodeRepresentationBuilder(programFlow);
		var hadError = false;
		try
		{
			var graph = builder.Select(proc);
			hadError = builder.ProcedureHadTranslationError;
			var renderer = new NodeGraphRenderer();
			renderer.Render(graph, Console.Out);
		}
		catch
		{
			hadError = true;
		}
		Console.WriteLine();

		if (hadError)
		{
			Console.WriteLine($"** {proc.Name} ******");
			MockGenerator.DumpMethod(proc);
			Console.WriteLine();
		}
	}
}

return 0;

static global::Reko.Decompiler LoadAndCreateDecompiler(string imagePath)
{
	var services = new ServiceContainer();
	var rekoConfigPath = Path.Combine(AppContext.BaseDirectory, "reko", "reko.config");
	var configService = RekoConfigurationService.Load(services, rekoConfigPath);
	var eventListener = new NullDecompilerEventListener();
	services.AddService(typeof(IDecompilerEventListener), eventListener);
	services.AddService(typeof(IEventListener), eventListener);
	services.AddService(typeof(IFileSystemService), new FileSystemService());
	services.AddService(typeof(IPluginLoaderService), new PluginLoaderService());
	services.AddService(typeof(ITypeLibraryLoaderService), new TypeLibraryLoaderServiceImpl(services));
	services.AddService(typeof(IDecompiledFileService), NullDecompiledFileService.Instance);
	services.AddService(typeof(IConfigurationService), configService);

	var loader = new Loader(services);
	var location = ImageLocation.FromUri(imagePath);
	var loaded = loader.Load(location, null!, null!, null);

	var project = loaded.Accept(new LoadedImageToProject(), 0);
	return new global::Reko.Decompiler(project, services);
}

sealed class LoadedImageToProject : ILoadedImageVisitor<Project, int>
{
	public Project VisitProgram(Reko.Core.Program program, int context)
	{
		return Project.FromSingleProgram(program);
	}

	public Project VisitProject(Project project, int context)
	{
		return project;
	}

	public Project VisitArchive(IArchive archive, int context)
	{
		throw new NotSupportedException("Archive inputs are not yet supported.");
	}

	public Project VisitBlob(Blob blob, int context)
	{
		throw new NotSupportedException("Unable to load file format.");
	}

	public Project VisitBinaryImage(IBinaryImage image, int context)
	{
		var loaded = image.Load(null);
		return loaded.Accept(this, context);
	}
}
