using A360.UserAccount.Api.IoC;
using A360.UserAccount.Api.Middlewares;

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
builder.Services.AddUserAccountApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseCors("AllowAll");

app.UseUserAccountApiMiddlewares();
await app.RunAsync();
