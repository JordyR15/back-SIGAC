using System.Text;
using back.Data;
using back.Middleware;
using back.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer(); // Needed for API Explorer
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SIGAC API", Version = "v1" });

    // Resuelve posibles acciones duplicadas o con la misma ruta
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    // Evita colisiones de nombres de clases/DTOs
    c.CustomSchemaIds(type => type.FullName);

    // Define the BearerAuth scheme
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

builder.Services.AddCors(); // Add CORS services

var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
var useInMemory = (builder.Configuration["USE_INMEMORY"] ?? Environment.GetEnvironmentVariable("USE_INMEMORY")) == "true";
if (string.IsNullOrWhiteSpace(defaultConn) || useInMemory)
{
    // Use in-memory DB for local testing if no connection string is configured or USE_INMEMORY=true
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("SIGAC_InMemory"));
}
else
{
    // Persistencia formal relacional en PostgreSQL
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(defaultConn));
}

builder.Services.AddScoped<ITokenService, TokenService>();

// Configuración del servicio de correo SMTP
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

// Configure JWT authentication. Read TokenKey from configuration or environment and fail fast with a clear error
var tokenKey = builder.Configuration["TokenKey"] ?? Environment.GetEnvironmentVariable("TOKEN_KEY");
if (string.IsNullOrWhiteSpace(tokenKey))
{
    throw new InvalidOperationException("TokenKey no está configurado. Establezca 'TokenKey' en appsettings.json o la variable de entorno 'TOKEN_KEY'.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

// Puerto por defecto de la API. Si el entorno o el despliegue define PORT/ASPNETCORE_URLS,
// se respeta esa configuración; de lo contrario, queda fijo en 5001 para evitar conflictos.
var port = builder.Configuration["PORT"] ?? Environment.GetEnvironmentVariable("PORT") ?? "5001";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

// 1. Colocar UseCors PRIMERO
app.UseCors(x => x
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .SetIsOriginAllowed(origin => true)); // Permite localhost:3000, localhost:4200, etc.

// 2. Registrar Swagger UI en el mismo puerto de la API
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SIGAC API V1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "SIGAC API Docs";
});

// 3. Demás middlewares
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

// Carga Inicial de Datos (Data Seeding)
await DbInitializer.InitializeAsync(app.Services);

app.Run();
