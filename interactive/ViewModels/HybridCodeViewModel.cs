using Avalonia.Skia.Helpers;
using Reko.Core.Loading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.Interactive.ViewModels;

public class HybridCodeViewModel
{
    public HybridCodeViewModel(ImageSegment? segment = null)
    {
        this.Items = new HybridCodeCollection(segment);
    }

    public HybridCodeCollection Items { get; }
}

public class HybridItem
{
    public HybridElement[]? Elements { get; set; }
}

public class HybridElement
{
    public string? Text { get; set; }
    public string? Classes { get; set; }

}
