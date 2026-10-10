using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Uttu.Models;

public class MemoryItem : INotifyPropertyChanged
{
    private int _id;
    private string _text = string.Empty;
    private bool _isActive = true;
    private string _category = "Rules";
    private DateTime _createdAt = DateTime.Now;

    public int Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    public string Category
    {
        get => _category;
        set => SetField(ref _category, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetField(ref _createdAt, value);
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
