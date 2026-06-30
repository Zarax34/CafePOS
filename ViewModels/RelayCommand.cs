using System.Windows.Input;

namespace CafePOS.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();

    /// <summary>
    /// Creates a RelayCommand from a parameterless action.
    /// </summary>
    public static RelayCommand Create(Action execute, Func<bool>? canExecute = null)
    {
        return new RelayCommand(
            _ => execute(),
            canExecute is not null ? _ => canExecute() : null
        );
    }

    /// <summary>
    /// Creates a RelayCommand for async operations.
    /// </summary>
    public static RelayCommand CreateAsync(Func<Task> execute, Func<bool>? canExecute = null)
    {
        return new RelayCommand(
            async _ => await execute(),
            canExecute is not null ? _ => canExecute() : null
        );
    }
}
