using Batc.Web.Components;
using Batc.Web.Services;   // ← 新增：AppState 的命名空間

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 全域狀態（病患清單）
// 用 Singleton 讓整個站台共用同一份記憶體中的資料
builder.Services.AddSingleton<AppState>();

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
