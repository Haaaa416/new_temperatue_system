using Batc.Web.Components;
using Batc.Web.Services;

Console.WriteLine("===== 應用程式啟動 =====");

var builder = WebApplication.CreateBuilder(args);

// ===== 設定 Logging（要在最前面）=====
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

Console.WriteLine("✓ Logging 已設定");

// ===== Razor Components =====
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

Console.WriteLine("✓ Razor Components 已註冊");

// ===== 應用程式狀態服務 =====
builder.Services.AddSingleton<AppState>();
builder.Services.AddScoped<VisitLogService>();
builder.Services.AddScoped<PositionSelectionState>();

Console.WriteLine("✓ 應用狀態服務已註冊");

// ===== 訊號處理服務（關鍵）=====
// ChartState（供圖表綁定用）- 必須是 Singleton
builder.Services.AddSingleton<ChartState>();

// SignalService - 必須是 Singleton（避免多實例衝突）
builder.Services.AddSingleton<SignalService>();

// 濾波器工廠（如果需要的話）
builder.Services.AddSingleton<Func<IChannelFilter>>(_ => () => new SportIirFilter());

Console.WriteLine("✓ SignalService 和 ChartState 已註冊");

var app = builder.Build();

Console.WriteLine("===== 開始驗證服務注入 =====");

// ===== 驗證服務是否正確注入 =====
try
{
    var signalService = app.Services.GetRequiredService<SignalService>();
    Console.WriteLine("✓ SignalService 已成功注入");

    var chartState = app.Services.GetRequiredService<ChartState>();
    Console.WriteLine("✓ ChartState 已成功注入");

    var appState = app.Services.GetRequiredService<AppState>();
    Console.WriteLine("✓ AppState 已成功注入");
}
catch (Exception ex)
{
    Console.WriteLine($"✗✗✗ 服務注入失敗 ✗✗✗");
    Console.WriteLine($"錯誤: {ex.Message}");
    Console.WriteLine($"詳細: {ex}");
}

// ===== HTTP Pipeline 設定 =====
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

Console.WriteLine("===== 應用程式設定完成 =====");
Console.WriteLine($"環境: {app.Environment.EnvironmentName}");
Console.WriteLine($"內容根路徑: {app.Environment.ContentRootPath}");

// ===== 監聽 URL 資訊 =====
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine("========================================");
    Console.WriteLine("應用程式已啟動！");
    Console.WriteLine($"請在瀏覽器開啟: http://localhost:5000");
    Console.WriteLine("========================================");
});

Console.WriteLine("===== 開始執行應用程式 =====");

app.Run();