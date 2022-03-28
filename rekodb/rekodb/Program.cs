// See https://aka.ms/new-console-template for more information

using Reko.Core;

public class Driver
{
    static void Main()
    {
        var program = LoadProgram();
        SaveProgramToDatabase(program);
    }

    private static void SaveProgramToDatabase(Program program)
    {
        throw new NotImplementedException();
    }

    static Program LoadProgram()
    {
        var program = new Program()
        {
        };
        return program;
    }
}
