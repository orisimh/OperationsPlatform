using Microsoft.EntityFrameworkCore;
using OperationsPlatform.Api.Data;
using OperationsPlatform.Api.Middleware;
using OperationsPlatform.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

builder.Services.AddDbContext<OperationsDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
        sql.EnableRetryOnFailure().UseCompatibilityLevel(140))); // SQL Server 2017 compatibility

var origins = builder.Configuration.GetSection("Frontend:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddHttpClient("OperationsExternal", client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan; // Each operation applies its own configured timeout.
    client.DefaultRequestHeaders.UserAgent.ParseAdd("OperationsPlatform/1.0");
});

builder.Services.AddScoped<OperationDefinitionService>();
builder.Services.AddScoped<OperationEngine>();
builder.Services.AddScoped<HistoryService>();
builder.Services.AddScoped<SqlApplicationLogWriter>();
builder.Services.AddSingleton<BuiltInHandlerCatalog>();
builder.Services.AddSingleton<CSharpWorkerClient>();
builder.Services.AddScoped<GenericRestApiExecutor>();
builder.Services.AddSingleton<BuilderOperationExecutor>();

var app = builder.Build();

// A34D - required marker from the Gaia development assignment.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DatabaseSeeder.SeedAsync(db);
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SqlRequestResponseLoggingMiddleware>();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseCors("Frontend");
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "OperationsPlatform.Api" }));

app.Run();
