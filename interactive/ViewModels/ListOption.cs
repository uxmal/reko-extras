using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.Interactive.ViewModels;

public record ListOption(string Text, object Value)
{
    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Text))
            return Value?.ToString() ?? "(null)";
        return Text;
    }
}
