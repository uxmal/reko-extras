using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace angr.analyses;

#pragma warning disable CS8981
#pragma warning disable IDE1006

public static class logging
{
    public static Logger getLogger(string name)
    {
        throw new NotImplementedException();
    }

    public class Logger
    {
        public void debug(string message)
        {
            Console.WriteLine($"DEBUG: {message}");
        }
        public static void info(string message)
        {
            Console.WriteLine($"INFO: {message}");
        }
        public void warning(string message)
        {
            Console.WriteLine($"WARNING: {message}");
        }
        public static void error(string message)
        {
            Console.WriteLine($"ERROR: {message}");
        }
        public static void critical(string message)
        {
            Console.WriteLine($"CRITICAL: {message}");
        }

        internal void info(string format, params object?[] args)
        {
            throw new NotImplementedException();
        }

        internal void debug(Exception ex)
        {
            throw new NotImplementedException();
        }
    }
}
