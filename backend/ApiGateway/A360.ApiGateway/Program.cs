using A360.ApiGateway.IoC;
using A360.ApiGateway.Middlewares;

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

builder.Services.AddApiGatewayServices(builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseApiGatewayMiddlewares();

await app.RunAsync();