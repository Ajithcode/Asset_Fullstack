using A360.Report.Api.IoC;
using A360.Report.Api.Middlewares;

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

builder.Services.AddReportApiServices(builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseReportApiMiddlewares();

await app.RunAsync();
