using System;

namespace RekoSifter
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var sifter = new Sifter(args);
                sifter.Sift();
            } catch
            {
                Console.WriteLine("Beume");
            }
        }
    }
}
