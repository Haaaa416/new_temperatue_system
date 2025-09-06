using Batc.Web.Components;
using Batc.Web.Services;   // �� �s�W�GAppState ���R�W�Ŷ�

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ���쪬�A�]�f�w�M��^
// �� Singleton ����ӯ��x�@�ΦP�@���O���餤�����
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

app.MapStaticAssets(); // wwwroot �U�� images�Bcss �|�q�o�̴���
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
