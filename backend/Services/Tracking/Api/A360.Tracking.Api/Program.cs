using A360.Tracking.Api.IoC;
using A360.Tracking.Api.Middlewares;

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

builder.Services.AddTrackingApiServices(builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseTrackingApiMiddlewares();

await app.RunAsync();
