using A360.Locating.Api.IoC;
using A360.Locating.Api.Middlewares;

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

builder.Services.AddLocatingApiServices(builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseLocatingApiMiddlewares();

await app.RunAsync();
