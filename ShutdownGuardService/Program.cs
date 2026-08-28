using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShutdownGuardService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ShutdownGuardService";
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
