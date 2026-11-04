using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;
using WorkerNetworkService.Services;
using WorkerNetworkService.Settings;

var builder = WebApplication.CreateBuilder(args);

var httpSettings = builder.Configuration.GetSection("Http").Get<HttpSettings>() ?? new HttpSettings();
builder.WebHost.UseUrls(httpSettings.ListenUrl);

builder.Services.Configure<RedisSettings>(builder.Configuration.GetSection("Redis"));
builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("Mongo"));
builder.Services.Configure<MqttSettings>(builder.Configuration.GetSection("Mqtt"));
builder.Services.Configure<List<StreamMapping>>(builder.Configuration.GetSection("StreamMappings"));

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var options = ConfigurationOptions.Parse(sp.GetRequiredService<IOptions<RedisSettings>>().Value.ConnectionString);
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoSettings>>().Value;
    return new MongoClient(settings.ConnectionString).GetDatabase(settings.Database);
});
builder.Services.AddSingleton<TimeSeriesCollectionInitializer>();
builder.Services.AddHostedService<RedisStreamIngestWorker>();
builder.Services.AddHostedService<MqttSubscriberWorker>();

var app = builder.Build();
app.MapHttpIngest(httpSettings);
app.Run();
