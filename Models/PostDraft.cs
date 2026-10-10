using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Uttu.Models;

public class PostDraft : INotifyPropertyChanged
{
    private int _id;
    private string _theme = string.Empty;
    private string _platform = "X";
    private string _content = string.Empty;
    private string _status = "Draft"; // Draft, Approved, SentToBuffer, Published, Discarded
    private string? _cardImagePath;
    private DateTime _createdAt = DateTime.Now;
    private DateTime? _scheduledAt;
    private string? _bufferUpdateId;
    private string _refinePrompt = string.Empty;
    private bool _isEditing;

    public int Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Theme
    {
        get => _theme;
        set => SetField(ref _theme, value);
    }

    public string Platform
    {
        get => _platform;
        set
        {
            if (SetField(ref _platform, value))
            {
                OnPropertyChanged(nameof(CharLimit));
                OnPropertyChanged(nameof(IsOverLimit));
                OnPropertyChanged(nameof(PlatformBadge));
            }
        }
    }

    public string Content
    {
        get => _content;
        set
        {
            if (SetField(ref _content, value))
            {
                OnPropertyChanged(nameof(CharCount));
                OnPropertyChanged(nameof(IsOverLimit));
                OnPropertyChanged(nameof(CharCountDisplay));
            }
        }
    }

    public int CharLimit => Platform switch
    {
        "X" or "Twitter" => 280,
        "LinkedIn" => 3000,
        "Threads" => 500,
        "Bluesky" => 300,
        "Instagram" => 2200,
        _ => 280
    };

    public int CharCount => _content?.Length ?? 0;

    public bool IsOverLimit => CharCount > CharLimit;

    public string CharCountDisplay => $"{CharCount} / {CharLimit} Caracteres";

    public string PlatformBadge => Platform switch
    {
        "X" or "Twitter" => "X (Twitter)",
        "LinkedIn" => "LinkedIn",
        "Threads" => "Threads",
        "Bluesky" => "Bluesky",
        "Instagram" => "Instagram",
        _ => Platform
    };

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public string? CardImagePath
    {
        get => _cardImagePath;
        set
        {
            if (SetField(ref _cardImagePath, value))
            {
                OnPropertyChanged(nameof(HasCardImage));
            }
        }
    }

    public bool HasCardImage => !string.IsNullOrWhiteSpace(_cardImagePath) && System.IO.File.Exists(_cardImagePath);

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetField(ref _createdAt, value);
    }

    public DateTime? ScheduledAt
    {
        get => _scheduledAt;
        set => SetField(ref _scheduledAt, value);
    }

    public string? BufferUpdateId
    {
        get => _bufferUpdateId;
        set => SetField(ref _bufferUpdateId, value);
    }

    public string RefinePrompt
    {
        get => _refinePrompt;
        set => SetField(ref _refinePrompt, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetField(ref _isEditing, value);
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
