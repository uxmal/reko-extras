using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.Analysis;

internal static class TraceSwitchExtensions
{
    public static void Inform(this TraceSwitch trace, string message)
    {
        if (trace.TraceInfo)
        {
            Debug.WriteLine(message);
        }
    }

    public static void Inform(this TraceSwitch trace, string format, params object?[] args)
    {
        if (trace.TraceInfo)
        {
            Debug.WriteLine(string.Format(format, args));
        }
    }



    public static void Verbose(this TraceSwitch trace, string message)
    {
        if (trace.TraceVerbose)
        {
            Debug.WriteLine(message);
        }
    }

    public static void Verbose(this TraceSwitch trace, string format, params object?[] args)
    {
        if (trace.TraceVerbose)
        {
            Debug.WriteLine(string.Format(format, args));
        }
    }

    public static void Warn(this TraceSwitch trace, string message)
    {
        if (trace.TraceWarning)
        {
            Debug.WriteLine(message);
        }
    }

    public static void Warn(this TraceSwitch trace, string format, params object?[] args)
    {
        if (trace.TraceWarning)
        {
            Debug.WriteLine(string.Format(format, args));
        }
    }

}
