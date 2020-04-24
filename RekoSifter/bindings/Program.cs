using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;
using System;

namespace bindings
{
	class Libopcodes : ILibrary
	{
		public void Postprocess(Driver driver, ASTContext ctx) {
			//throw new NotImplementedException();
		}

		public void Preprocess(Driver driver, ASTContext ctx) {
			//throw new NotImplementedException();
		}

		public void Setup(Driver driver) {
			var options = driver.Options;
			options.GeneratorKind = GeneratorKind.CSharp;
			var module = options.AddModule("libopcodes");
			module.Defines.AddRange(new[] { "PACKAGE", "PACKAGE_VERSION" });

			module.IncludeDirs.Add(@"C:\msys64\mingw64\include\binutils");
			module.Headers.AddRange(new[] { "dis-asm.h" });
			module.LibraryDirs.Add(@"C:\msys64\mingw64\lib\binutils");
			module.Libraries.Add("libopcodes.a");
		}

		public void SetupPasses(Driver driver) {
			//throw new NotImplementedException();
		}
	}

	class Program
	{

		static void Main(string[] args) {
			Console.WriteLine("Hello World!");
			ConsoleDriver.Run(new Libopcodes());
		}
	}
}
