using Reko.Core;
using Reko.Loading;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Reko.Extras.Interactive.ViewModels;

public class CodeViewModel : ObservableObject
{
    private readonly IServiceProvider services;
    private DecompilerHost host;
    private IRewriterHost rwhost;
    private Decompiler? decompiler;
    private Task? decompilerTask;

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


    internal void StepAcross()
    {
        throw new NotImplementedException();
    }

    public async Task<bool> DoRun()
    {
        if (this.RunText == "Run")
        {
            if (decompiler is not null)

            decompiler = null;
            var loader = new Loader(services);
            var loadedFile = loader.Load(ImageLocation.FromUri(this.FileName));
            if (loadedFile is Program program)
            {
                this.decompiler = new Decompiler(services, this.host, this.rwhost, program);
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
        IsRunEnabled = !string.IsNullOrEmpty(this.FileName);
        if (e.PropertyName == nameof(FileName))
            this.decompiler = null;
    }

}
