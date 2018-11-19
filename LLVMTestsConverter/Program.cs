/*
 * Copyright 2018 Stefano Moioli<smxdev4@gmail.com>
 **/
using System;

namespace LLVMTestsConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage: <path to llvm/test/MC>");
                Environment.Exit(1);
            }

            new TestScanner(args[0]).Work();
            Console.WriteLine("Press enter to continue...");
            Console.ReadLine();
        }
    }
}
