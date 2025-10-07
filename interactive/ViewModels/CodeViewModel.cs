using Reko.Core;
using Reko.Core.Configuration;
using Reko.Core.Loading;
using Reko.Core.Services;
using Reko.Loading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Threading.Tasks;

namespace Reko.Extras.Interactive.ViewModels;

public class CodeViewModel : ObservableObject
{
    private readonly IServiceProvider services;
    private DecompilerHost host;
    private IRewriterHost rwhost;
    private Decompiler? decompiler;
    private Task? decompilerTask;
    private Program? program;

    public CodeViewModel(
        IServiceProvider services, 
        DecompilerHost host,
        IRewriterHost rwhost)
    {
        this.services = services;
        this.Lines = [];
        this.runText = "Run";
        this.filename = "";
        this.host = host;
        this.rwhost = rwhost;
        this.codeItems = new();
        this.PropertyChanged += OnChanged;
    }


    public ObservableCollection<CodeLineModel> Lines { get; }

    public bool IsRunEnabled
    {
        get => this.isRunEnabled;
        set => this.RaiseAndSetIfChanged(ref this.isRunEnabled, value);
    }
    private bool isRunEnabled;

    public string RunText
    {
        get => this.runText;
        set => this.RaiseAndSetIfChanged(ref this.runText, value);
    }
    private string runText;

    public string FileName
    {
        get => this.filename;
        set => this.RaiseAndSetIfChanged(ref this.filename, value);
    }
    private string filename;


    public ObservableCollection<ListOption> Segments
    {
        get => this.segments;
        set => this.RaiseAndSetIfChanged(ref this.segments, value);
    }
    private ObservableCollection<ListOption> segments = [];

    public int SelectedSegmentIndex
    {
        get => this.selectedSegmentIndex;
        set => this.RaiseAndSetIfChanged(ref this.selectedSegmentIndex, value);
    }
    private int selectedSegmentIndex;

    public HybridCodeViewModel CodeItems
    {
        get => codeItems;
        set => this.RaiseAndSetIfChanged(ref this.codeItems, value);
    }
    private HybridCodeViewModel codeItems;

    internal void StepAcross()
    {
        throw new NotImplementedException();
    }

    public async Task<bool> DoRun()
    {
        if (this.RunText == "Run")
        {
            if (decompiler is null)
            {
                if (program is not null)
                {
                    var listener = services.RequireService<IEventListener>();
                    this.decompiler = new Decompiler(services, listener, this.host, this.rwhost, program);
                }
            }
            else
            {
                this.host.Run();
                this.RunText = "Pause";
                decompilerTask = Task.Run(() =>
                {
                    decompiler.ScanImage();
                });
                await decompilerTask;
                this.RunText = "Run";
            }
        }
        else
        {
            host.Pause();
        }
        return true;
    }

    public bool DoStep()
    {
        return false;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileName))
        {
            var loader = new Loader(services);
            var loadedFile = loader.Load(ImageLocation.FromUri(this.FileName));
            if (loadedFile is Program program)
            {
                this.program = program;
                this.Segments = new ObservableCollection<ListOption>(program.SegmentMap.Segments.Values
                    .Select(s => new ListOption(s.Name, s)));
                this.SelectedSegmentIndex = -1;
                this.SelectedSegmentIndex = 0;
            }
            else
            {
                this.program = null;
            }
        }
        if (e.PropertyName == nameof(SelectedSegmentIndex))
        {
            var segment = this.SelectedSegmentIndex >= 0 && this.SelectedSegmentIndex < this.Segments.Count
                ? (ImageSegment)this.Segments[this.SelectedSegmentIndex].Value
                : null;
            this.CodeItems = new HybridCodeViewModel(segment);
        }
         
        IsRunEnabled = program is not null;
        if (e.PropertyName == nameof(FileName))
            this.decompiler = null;
    }
}
