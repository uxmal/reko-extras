
using System.ComponentModel.Design;
using Reko.Core;
using Reko.Core.Configuration;
using Reko.Core.Services;
using Reko.Extras.blocksoup;
using Reko.Services;

var sc = new ServiceContainer();
var fsSvc = new FileSystemService();
sc.AddService<IFileSystemService>(fsSvc);
sc.AddService<IPluginLoaderService>(new PluginLoaderService());
var e = new EventListener();
sc.AddService<IEventListener>(e);
sc.AddService<IDecompilerEventListener>(e);
var configSvc = RekoConfigurationService.Load(sc,"reko/reko.config");
sc.AddService<IConfigurationService>(configSvc);
var typelibSvc = new TypeLibraryLoaderServiceImpl(sc);
sc.AddService<ITypeLibraryLoaderService>(typelibSvc);
var decFileSvc = new DecompiledFileService(sc, fsSvc, e);
sc.AddService<IDecompiledFileService>(decFileSvc);

var loader = new Reko.Loading.Loader(sc);
var image = loader.Load(ImageLocation.FromUri(args[0]));
if (image is Project project)
{
    var blockSoup = new BlockSoupAnalysis(project.Programs[0]);
    //blockSoup.Scan(project, sc);
    blockSoup.Extract();
    return;
}
if (image is not Reko.Core.Program program)
{
    Console.WriteLine("Not a program");
    return;
}
else
{
    var blockSoup = new BlockSoupAnalysis(program);
    blockSoup.Extract();
}
