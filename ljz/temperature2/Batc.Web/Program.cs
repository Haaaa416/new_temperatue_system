using Batc.Web.Components;
using Batc.Web.Services;   // AppState、VisitLogService 的命名空間

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 全域狀態（病患清單）
builder.Services.AddSingleton<AppState>();

// ✅ 在 Build 之前註冊你的 VisitLogService（Blazor Server 用 Scoped）
builder.Services.AddScoped<VisitLogService>();

builder.Services.AddScoped<PositionSelectionState>();
// 這行要在所有 AddXXX 後面
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets(); // wwwroot 下的 images、css 會從這裡提供
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
