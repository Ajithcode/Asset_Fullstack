
using A360.Wip.Api.IoC;
using A360.Wip.Api.Middlewares;

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

builder.Services.AddWipApiServices(
    builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseWipApiMiddlewares();

await app.RunAsync();
