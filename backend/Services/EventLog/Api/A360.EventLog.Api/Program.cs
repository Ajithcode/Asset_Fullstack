using A360.EventLog.Api.IoC;
using A360.EventLog.Api.Middlewares;

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

builder.Services.AddEventLogApiServices(builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseEventLogApiMiddlewares();

await app.RunAsync();
