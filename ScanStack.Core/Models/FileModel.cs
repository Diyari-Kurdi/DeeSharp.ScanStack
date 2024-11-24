using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScanStack.Core.Models;
public sealed class FileModel(string path, int number) : INotifyPropertyChanged
{
    private string _path = path;
    public string Path
    {
        get => _path;
        set
        {
            _path = value; 
            OnPropertyChanged();
        }
    }

    private int _number = number;
    public int Number
    {
        get => _number;
        set
        {
            _number = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public void OnPropertyChanged([CallerMemberName] string callerName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(callerName));
    }
}
