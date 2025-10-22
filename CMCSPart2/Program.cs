using CMCSPart2.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSession(o => { o.Cookie.HttpOnly = true; o.IdleTimeout = TimeSpan.FromHours(4); });

builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));

builder.Services.Configure<PersistenceOptions>(builder.Configuration.GetSection("Persistence"));

builder.Services.AddSingleton<InMemoryStore>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

var store = app.Services.GetRequiredService<InMemoryStore>();
await store.LoadAsync();

app.Lifetime.ApplicationStopping.Register(() =>
{
    try
    {
        store.GetType().GetMethod("SaveAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(store, new object[] { });
    }
    catch { }
});

app.Run();