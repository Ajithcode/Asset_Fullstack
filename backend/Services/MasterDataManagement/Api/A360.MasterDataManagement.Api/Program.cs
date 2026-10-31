using A360.MasterDataManagement.Api.IoC;
using A360.MasterDataManagement.Api.Middlewares;

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

builder.Services.AddMasterDataManagementApiServices(builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseMasterDataManagementApiMiddlewares();

await app.RunAsync();
