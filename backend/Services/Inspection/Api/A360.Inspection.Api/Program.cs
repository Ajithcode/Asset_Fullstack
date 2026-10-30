using A360.Inspection.Api.IoC;
using A360.Inspection.Api.Middlewares;

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

builder.Services.AddInspectionApiServices(builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseInspectionApiMiddlewares();

await app.RunAsync();
