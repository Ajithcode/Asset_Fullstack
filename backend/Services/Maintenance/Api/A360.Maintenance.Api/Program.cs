
using A360.Maintenance.Api.IoC;
using A360.Maintenance.Api.Middlewares;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddMaintenanceApiServices(
    builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseMaintenanceApiMiddlewares();

await app.RunAsync();
