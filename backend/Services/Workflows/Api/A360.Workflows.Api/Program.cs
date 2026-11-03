
using A360.Workflows.Api.IoC;
using A360.Workflows.Api.Middlewares;

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

builder.Services.AddWorkflowsApiServices(
    builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseWorkflowsApiMiddlewares();

await app.RunAsync();
