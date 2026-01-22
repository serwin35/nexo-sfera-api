using NexoSferaApi.Configuration;
using NexoSferaApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Nexo Sfera REST API",
        Version = "v1",
        Description = "REST API for InsERT Nexo Sfera integration"
    });
});

// Configure Sfera connection settings
builder.Services.Configure<SferaSettings>(builder.Configuration.GetSection("Sfera"));

// Register Sfera service as singleton (connection is expensive)
builder.Services.AddSingleton<ISferaService, SferaService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Nexo Sfera API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Initialize Sfera connection on startup
var sferaService = app.Services.GetRequiredService<ISferaService>();
await sferaService.InitializeAsync();

app.Run();
