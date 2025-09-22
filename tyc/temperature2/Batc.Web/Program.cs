using Batc.Web.Components;
using Batc.Web.Services;   // �� �s�W�GAppState ���R�W�Ŷ�
using Batc.Web.Models.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// ���쪬�A�]�f�w�M��^
// �� Singleton ����ӯ��x�@�ΦP�@���O���餤�����
builder.Services.AddSingleton<AppState>();

// Razor Components
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// 註冊 PatientsService（DI）
builder.Services.AddScoped<IPatientsService, PatientsService>();

builder.Services.AddScoped<IUsersService, UsersService>();

builder.Services.AddScoped<PatientsService>();

builder.Services.AddScoped<CurrentUserState>();

// 註冊 DbContext（讀取 appsettings.json 的連線字串）
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    opt.EnableSensitiveDataLogging();                 // 印出參數值
    opt.LogTo(Console.WriteLine, LogLevel.Information); // 把 EF SQL 打到 Console
});

// Cookie 驗證（登入會發 Cookie）
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/login";  // 你的登入頁路由
        opt.Cookie.Name = "BATC.Auth";
    });

builder.Services.AddAuthorization();

builder.Services.AddCascadingAuthenticationState();

// 讓 Razor 頁可拿 HttpContext（簽發/清除 Cookie 會用到）
builder.Services.AddHttpContextAccessor();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.MapStaticAssets(); // wwwroot �U�� images�Bcss �|�q�o�̴���
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// === 登入：簽發 Cookie 後導到 /patients ===
app.MapGet("/auth/signin", async ( 
    int loginId,
    int roleId,
    IUsersService users, 
    HttpContext http) => 
{                                                                    
    var user = await users.GetByLoginAndRoleAsync(loginId, roleId);
    if (user is null) return Results.Redirect("/login?err=invalid");

    var claims = new List<Claim>
    {                                                                
        new(ClaimTypes.NameIdentifier, user.LoginID.ToString()),
        new(ClaimTypes.Name, user.Username ?? string.Empty),
        new(ClaimTypes.Role, user.RoleID.ToString())
    };                                                               
    var principal = new ClaimsPrincipal(                             
        new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    return Results.Redirect("/patients");           
});                                                                   

// === 登出：清 Cookie 後回登入頁 ===
app.MapPost("/auth/signout", async (HttpContext http) => 
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.Run();