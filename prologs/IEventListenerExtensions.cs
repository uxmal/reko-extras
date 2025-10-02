using Reko.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace prologs;
internal static class IEventListenerExtensions
{
    public static void debug(this IEventListener listener, string message)
    {
        listener.Info(new NullCodeLocation(""), message);
    }

    public static void debug(this IEventListener listener, string format, params object?[] args)
    {
        listener.Info(new NullCodeLocation(""), string.Format(format, args));
    }
}
