using Batc.Web.Components;
using Batc.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<AppState>();
builder.Services.AddScoped<VisitLogService>();
builder.Services.AddScoped<PositionSelectionState>();

// ChartState（供圖表綁定用）
builder.Services.AddSingleton<ChartState>();

// 濾波器工廠 & 串流服務
builder.Services.AddSingleton<Func<IChannelFilter>>(_ => () => new SportIirFilter());
builder.Services.AddSingleton<SignalService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();   // ← 這行取代 MapStaticAssets
app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();
