using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Uttu.Models;

public class BufferProfile : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _service = "twitter";
    private string _formattedUsername = "@usuario";
    private string? _avatar;
    private bool _isSelected = true;

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Service
    {
        get => _service;
        set
        {
            if (SetField(ref _service, value))
            {
                OnPropertyChanged(nameof(FormattedService));
            }
        }
    }

    public string FormattedService => _service?.ToLowerInvariant() switch
    {
        "twitter" or "x" => "X (Twitter)",
        "linkedin" => "LinkedIn",
        "threads" => "Threads",
        "bluesky" => "Bluesky",
        "instagram" => "Instagram",
        _ => _service ?? "Social"
    };

    public string FormattedUsername
    {
        get => _formattedUsername;
        set => SetField(ref _formattedUsername, value);
    }

    public string? Avatar
    {
        get => _avatar;
        set => SetField(ref _avatar, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
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
