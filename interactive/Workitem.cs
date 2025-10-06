using Reko.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.Interactive;

public class Workitem
{
    public Workitem(Address address, ProcessorState state)
    {
        this.Address = address;
        this.State = state;
    }

    public Address Address { get; set; }
    public ProcessorState State { get; set; }
}
