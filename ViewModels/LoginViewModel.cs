using CafePOS.Models;
using CafePOS.Services;

namespace CafePOS.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isLoading;

    public event Action<User>? LoginSucceeded;

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public RelayCommand LoginCommand { get; }

    public LoginViewModel()
    {
        LoginCommand = RelayCommand.Create(ExecuteLogin, () => !IsLoading);
    }

    private void ExecuteLogin()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "الرجاء إدخال رقم الكاشير";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "الرجاء إدخال كلمة المرور";
            return;
        }

        IsLoading = true;

        try
        {
            var user = AuthService.Login(Username, Password);

            if (user != null)
            {
                LoginSucceeded?.Invoke(user);
            }
            else
            {
                ErrorMessage = "رقم الكاشير أو كلمة المرور غير صحيحة";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"حدث خطأ: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
