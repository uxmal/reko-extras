
using System.ComponentModel.Design;
using Reko.Core;
using Reko.Core.Configuration;
using Reko.Core.Services;
using Reko.Extras.blocksoup;
using Reko.Services;

var sc = new ServiceContainer();
sc.AddService<IFileSystemService>(new FileSystemService());
sc.AddService<IPluginLoaderService>(new PluginLoaderService());
var e = new EventListener();
sc.AddService<IEventListener>(e);
sc.AddService<IDecompilerEventListener>(e);
var configSvc = RekoConfigurationService.Load(sc,"reko/reko.config");
sc.AddService<IConfigurationService>(configSvc);

var loader = new Reko.Loading.Loader(sc);
var image = loader.Load(ImageLocation.FromUri(args[0]));
if (image is not Reko.Core.Program program)
{
    Console.WriteLine("Not a program");
    return;
}
var blockSoup = new BlockSoup(program);
blockSoup.Extract();
