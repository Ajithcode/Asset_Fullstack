using A360.Asset.Api.IoC;
using A360.Asset.Api.Middlewares;

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

builder.Services.AddAssetApiServices(builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseAssetApiMiddlewares();

await app.RunAsync();
