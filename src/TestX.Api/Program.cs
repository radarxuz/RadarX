using Microsoft.AspNetCore.Mvc.ApplicationModels;
using System.Text.Json.Serialization;
using TestX.Api.Extensions;
using TestX.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using TestX.Api.Middlewares;
using TestX.Api.Models;
using TestX.Service.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureSwagger();

builder.Services.AddHttpContextAccessor();

// Configure EF Core with PostgreSQL
builder.Services.AddDbContext<DataBaseContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlConnection")));

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Error);  // Suppress EF Core logs

builder.Services.AddCustomServices();

builder.Services.AddJwtService(builder.Configuration);

builder.WebHost.UseUrls("http://0.0.0.0:5000");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalHost", builder =>
    {
        builder.WithOrigins("http://127.0.0.1:5500") // Allow only specific origin
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); // If you are using cookies or authentication
    });
});

// Convert API URL name to dash-case
builder.Services.AddControllers(options =>
{
    options.Conventions.Add(new RouteTokenTransformerConvention(
        new ConfigureApiUrlName()));
});

builder.Services.AddControllersWithViews()
    .AddJsonOptions(x => x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

var app = builder.Build();

// Apply migrations automatically with logging
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DataBaseContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Applying migrations...");
        dbContext.Database.Migrate();
        logger.LogInformation("Migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying migrations.");
    }
}

// Set the web root path for static files
EnvironmentHelper.WebRootPath = Path.GetFullPath("wwwroot");

// Configure Swagger for Development and Production environments
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure Middlewares
app.UseMiddleware<ExceptionHandlerMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
