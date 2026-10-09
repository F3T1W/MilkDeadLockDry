using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MilkDeadLockDry.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusMessage))]
    [NotifyCanExecuteChangedFor(nameof(ClearSelectionCommand))]
    public partial string? GameDirectory { get; set; }

    public string StatusMessage => GameDirectory is null ? "No folder selected." : "Folder selected.";

    [RelayCommand(CanExecute = nameof(CanClearSelection))]
    private void ClearSelection()
    {
        GameDirectory = null;
    }

    private bool CanClearSelection()
    {
        return GameDirectory is not null;
    }
}
