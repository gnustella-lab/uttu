using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Uttu.Models;

public class VoiceProfile : INotifyPropertyChanged
{
    private int _id;
    private string _sampleText = string.Empty;
    private double _avgSentenceLength = 12.5; // Words per sentence
    private double _punctuationDensity = 7.2; // Punctuation per 100 words
    private string _prohibitedWords = "synergy, disruptive, revolutionary, game changer, leverage, mindset";
    private string _hookStyle = "Contrarian / Provocative (Short direct hook)";
    private string _toneKeywords = "Pragmatic, Direct, Engineering-focused, Zero fluff, Product-driven";
    private DateTime _analyzedAt = DateTime.Now;

    public int Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string SampleText
    {
        get => _sampleText;
        set => SetField(ref _sampleText, value);
    }

    public double AvgSentenceLength
    {
        get => _avgSentenceLength;
        set => SetField(ref _avgSentenceLength, value);
    }

    public double PunctuationDensity
    {
        get => _punctuationDensity;
        set => SetField(ref _punctuationDensity, value);
    }

    public string ProhibitedWords
    {
        get => _prohibitedWords;
        set => SetField(ref _prohibitedWords, value);
    }

    public string HookStyle
    {
        get => _hookStyle;
        set => SetField(ref _hookStyle, value);
    }

    public string ToneKeywords
    {
        get => _toneKeywords;
        set => SetField(ref _toneKeywords, value);
    }

    public DateTime AnalyzedAt
    {
        get => _analyzedAt;
        set => SetField(ref _analyzedAt, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
