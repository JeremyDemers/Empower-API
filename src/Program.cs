using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for logging to a file
builder.Host.UseSerilog((hostingContext, services, loggerConfiguration) => loggerConfiguration
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(builder.Environment.ContentRootPath, "empower_access.log"),
        rollingInterval: RollingInterval.Day)
    .ReadFrom.Configuration(builder.Configuration));

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Register the database context
builder.Services.AddDbContext<ResultsCheckContext>();

// Register the services
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<OracleDataService>();

// Register the daily task
builder.Services.AddHostedService<DailyTask>();

// Swagger configuration
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1",
        new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "SmartLab Empower API",
            Version = "v1"
        });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});

// Add CORS services with a specific policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
    builder =>
    {
        builder.AllowAnyOrigin() // Changed to allow any origin
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});

var app = builder.Build();

// Use CORS with the specified policy
app.UseCors("AllowAllOrigins");

// Middleware to log each request, response, and user information
app.Use(async (context, next) =>
{
    // This assumes you have some form of authentication in place
    var userName = context.User.Identity.IsAuthenticated ? context.User.Identity.Name : "anonymous";

    // Log the incoming request with user information
    Log.Information($"Incoming request: {context.Request.Method} {context.Request.Path} by user {userName}");

    await next.Invoke();

    // Log the outgoing response with user information
    Log.Information($"Outgoing response: {context.Response.StatusCode} for user {userName}");
});

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Middleware to log each request and response
app.UseSerilogRequestLogging();

app.Run();