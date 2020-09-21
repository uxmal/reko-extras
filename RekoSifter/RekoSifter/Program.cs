using System;

namespace RekoSifter
{
    class Program
    {
        static void Main(string[] args)
        {
            var sifter = new Sifter(args);
            sifter.Run();
        }
    }
}
