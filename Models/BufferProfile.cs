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
    private int _pendingUpdatesCount = 0;
    private int _sentUpdatesCount = 0;
    private string _timezone = "America/Sao_Paulo";

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
        "facebook" => "Facebook",
        "pinterest" => "Pinterest",
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

    public int PendingUpdatesCount
    {
        get => _pendingUpdatesCount;
        set
        {
            if (SetField(ref _pendingUpdatesCount, value))
            {
                OnPropertyChanged(nameof(QueueStatusText));
            }
        }
    }

    public int SentUpdatesCount
    {
        get => _sentUpdatesCount;
        set => SetField(ref _sentUpdatesCount, value);
    }

    public string Timezone
    {
        get => _timezone;
        set => SetField(ref _timezone, value);
    }

    public string QueueStatusText => _pendingUpdatesCount switch
    {
        0 => "Empty queue",
        1 => "1 post in queue",
        _ => $"{_pendingUpdatesCount} posts in queue"
    };

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
