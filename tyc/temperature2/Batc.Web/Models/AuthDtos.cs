namespace Batc.Web.Models;

public record RegisterRequest(string Username, int RoleID);
public record LoginRequest(string Username);
