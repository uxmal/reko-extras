using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.Interactive.ViewModels;

public class MainWindowViewModel
{
    public CodeViewModel? CodeView { get; set; }

    public void StepAcross()
    {
        CodeView?.StepAcross();
    }
}
