using Reko.Core.Output;
using System;

namespace Reko.Extras.Interactive.ViewModels;

public class ProgressIndicator : ObservableObject, IProgressIndicator
{
    public ProgressIndicator()
    {
        this.statusText = "";
    }

    public bool IsProgressVisible
    {
        get => isProgressVisible;
        set => this.RaiseAndSetIfChanged(ref isProgressVisible, value);
    }
    private bool isProgressVisible;

    public string StatusText
    {
        get => statusText;
        set => this.RaiseAndSetIfChanged(ref statusText, value);
    }
    private string statusText;

    public int Value 
    {
        get => value;
        set => this.RaiseAndSetIfChanged(ref this.value, value);
    }
    private int value;

    public void Advance(int count)
    {
        throw new System.NotImplementedException();
    }

    public void Finish()
    {
        IsProgressVisible = false;
        StatusText = "";
        Value = 0;
    }

    public void SetCaption(string newCaption)
    {
        StatusText = newCaption;
    }

    public void ShowProgress(string caption, int numerator, int denominator)
    {
        this.StatusText = caption;
        this.IsProgressVisible = true;
        this.Value = CalculateValue(numerator, denominator);
    }

    public void ShowProgress(int numerator, int denominator)
    {
        this.IsProgressVisible = true;
        this.Value = CalculateValue(numerator, denominator);
    }

    private int CalculateValue(int numerator, int denominator)
    {
        if (denominator == 0)
        {
            return 0;
        }
        return (int)(100.0 * numerator / denominator);
    }

    public void ShowStatus(string caption)
    {
        StatusText = caption;
    }
}