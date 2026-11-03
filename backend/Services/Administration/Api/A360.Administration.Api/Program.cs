using A360.Administration.Api.IoC;
using A360.Administration.Api.Middlewares;

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

builder.Services.AddAdministrationApiServices(builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseAdministrationApiMiddlewares();

await app.RunAsync();
