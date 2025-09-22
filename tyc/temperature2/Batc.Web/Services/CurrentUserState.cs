using Batc.Web.Models.Data;

namespace Batc.Web.Services;

public class CurrentUserState
{
    public bool IsAuthenticated { get; private set; }
    public int? LoginID { get; private set; }
    public string? Username { get; private set; } = "";
    public int RoleID { get; private set; }
    
    public event Action? Changed;

    public void SignIn(UserEntity u)
    {
        IsAuthenticated = true;
        LoginID = u.LoginID;
        Username = u.Username;
        RoleID = u.RoleID;
        Changed?.Invoke();
    }

    public void SignOut()
    {
        IsAuthenticated = false;
        LoginID = 0;
        Username = "";
        RoleID = 0;
        Changed?.Invoke();
    }
}
