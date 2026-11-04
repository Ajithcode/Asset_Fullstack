using MongoDB.Driver;
using WorkerService.Services;
using WorkerService.Settings;

var builder = Host.CreateApplicationBuilder(args);

// Load settings
builder.Services.Configure<MongoSettings>(
    builder.Configuration.GetSection("MongoSettings"));

builder.Services.Configure<RedisSettings>(
    builder.Configuration.GetSection("RedisSettings"));

// MongoDB
var mongoSettings = builder.Configuration
    .GetSection("MongoSettings")
    .Get<MongoSettings>();

var mongoClient = new MongoClient(mongoSettings!.ConnectionString);

builder.Services.AddSingleton<IMongoDatabase>(
    mongoClient.GetDatabase(mongoSettings.Database));

// Repository
builder.Services.AddSingleton<MongoRepository>();

// Worker Service
builder.Services.AddHostedService<RedisStreamConsumerService>();

var host = builder.Build();

await host.RunAsync();