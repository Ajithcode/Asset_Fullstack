using A360.Media.Api.IoC;
using A360.Media.Api.Middlewares;

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

builder.Services.AddMediaApiServices(
    builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseMediaApiMiddlewares();

await app.RunAsync();
