using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using wledBridge.Application;
using wledBridge.Infrastructure;
using wledBridge.Infrastructure.Persistence;
using wledBridge.VirtualMixer;
using wledBridge.VirtualMixer.Http;
using wledBridge.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Virtual Mixer module (standalone RCL) + SignalR for its opt-in remote-control hub.
builder.Services.AddSignalR();
builder.Services.AddVirtualMixer(options =>
{
    options.StoragePath = Path.Combine(builder.Environment.ContentRootPath, "virtualmixer-data");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WledBridgeDbContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Opt-in remote control for the Virtual Mixer (REST + SignalR hub at /api/virtualmixer/hub).
// Unauthenticated by design for the local network - securing this is a host decision.
app.MapVirtualMixerApi();

app.Run();
