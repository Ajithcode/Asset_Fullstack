using A360.Dashboard.Api.IoC;
using A360.Dashboard.Api.Middlewares;

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

builder.Services.AddDashboardApiServices(builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseDashboardApiMiddlewares();

await app.RunAsync();
