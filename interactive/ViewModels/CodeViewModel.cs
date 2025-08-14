using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.Interactive.ViewModels;

public class CodeViewModel : ObservableObject
{
    public CodeViewModel()
    {
        this.Lines = [];
    }

    public ObservableCollection<CodeLineModel> Lines { get; }

    internal void StepAcross()
    {
        throw new NotImplementedException();
    }
}
