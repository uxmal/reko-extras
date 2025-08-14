using Reko.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.Interactive;

public class Workitem
{
    public Address Address { get; set; }
    public ProcessorState State { get; internal set; }
}
