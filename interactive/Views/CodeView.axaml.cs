using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Reko.Extras.Interactive.ViewModels;
using System.Diagnostics;

namespace Reko.Extras.Interactive.Views;

public partial class CodeView : UserControl
{
    public CodeView()
    {
        InitializeComponent();
    }

    private CodeViewModel ViewModel
    {
        get
        {
            Debug.Assert(DataContext is not null);
            return (CodeViewModel)DataContext;
        }
    }

    private async void openFile_Click(object? sender, RoutedEventArgs e)
    {
        var toplevel = TopLevel.GetTopLevel(this);
        if (toplevel is null)
            return;
        var files = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
             AllowMultiple = false,
        });
        if (files.Count != 1)
            return;

        this.ViewModel.FileName = files[0].Path.AbsolutePath;
    }
}