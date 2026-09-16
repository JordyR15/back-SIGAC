using System.Text;
using back.Data;
using back.Middleware;
using back.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

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

// Cadena oficial del Pooler IPv4 de Supabase
const string poolerConnection = "Host=aws-0-us-west-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.twqiaebuxluhvleijapf;Password=9p-a6@rr7Z/vbTg;SSL Mode=Require;Trust Server Certificate=true;";

var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");

// Si viene vacía o si alguna variable externa sigue inyectando el host viejo con solo IPv6, forzar el pooler IPv4
if (string.IsNullOrWhiteSpace(defaultConn) || defaultConn.Contains("db.twqiaebuxluhvleijapf.supabase.co"))
{
    defaultConn = poolerConnection;
}

Console.WriteLine($"\n[INFO BD] Conectando a: {defaultConn}\n");

var useInMemory = (builder.Configuration["USE_INMEMORY"] ?? Environment.GetEnvironmentVariable("USE_INMEMORY")) == "true";

if (useInMemory)
{
    // Use in-memory DB for local testing if USE_INMEMORY=true
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("SIGAC_InMemory"));
}
else
{
    // Persistencia formal relacional en PostgreSQL (Supabase Pooler IPv4)
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

// Puerto por defecto de la API
var port = builder.Configuration["PORT"] ?? Environment.GetEnvironmentVariable("PORT") ?? "5001";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

// 1. Colocar UseCors PRIMERO
app.UseCors(policy => policy
    .WithOrigins("http://localhost:3000", "http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials());

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

// Carga Inicial de Datos (Data Seeding y Migraciones)
await DbInitializer.InitializeAsync(app.Services);

app.Run();