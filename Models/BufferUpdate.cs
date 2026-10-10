using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Uttu.Models;

public class BufferUpdate : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _profileId = string.Empty;
    private string _text = string.Empty;
    private string _status = "buffer"; // buffer, sent, error
    private DateTime? _dueAt;
    private DateTime _createdAt = DateTime.Now;
    private string? _mediaPhoto;
    private string _serviceName = "X";
    private string _profileName = "@usuario";

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string ProfileId
    {
        get => _profileId;
        set => SetField(ref _profileId, value);
    }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public DateTime? DueAt
    {
        get => _dueAt;
        set => SetField(ref _dueAt, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetField(ref _createdAt, value);
    }

    public string? MediaPhoto
    {
        get => _mediaPhoto;
        set => SetField(ref _mediaPhoto, value);
    }

    public string ServiceName
    {
        get => _serviceName;
        set => SetField(ref _serviceName, value);
    }

    public string ProfileName
    {
        get => _profileName;
        set => SetField(ref _profileName, value);
    }

    public string DueAtFormatted => _dueAt.HasValue ? _dueAt.Value.ToString("yyyy-MM-dd HH:mm") : "In Buffer queue";

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
