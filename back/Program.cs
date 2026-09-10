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

// Configurar Kestrel para escuchar en el puerto principal y en un puerto adicional para Swagger UI
builder.WebHost.ConfigureKestrel(options =>
{
    // Escuchar en el puerto por defecto (si se establece via URLs o entorno) y en 5001 para Swagger
    // Añade un listener explícito para 5001; el puerto 3000 normalmente lo gestiona la configuración existente
    options.ListenAnyIP(5001);
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

// 1. Colocar UseCors PRIMERO
app.UseCors(x => x
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .SetIsOriginAllowed(origin => true)); // Permite localhost:3000, localhost:4200, etc.

// 2. Registrar Swagger UI solo en el puerto dedicado (5001)
// Mapear únicamente las peticiones relacionadas con Swagger (UI y spec) al branch de Swagger.
// Permitir que las peticiones a /api/* sigan al pipeline principal para ejecutar los endpoints.
app.MapWhen(ctx => ctx.Request.Host.Port == 5001
                 && (ctx.Request.Path.StartsWithSegments("/swagger")
                     || ctx.Request.Path == "/"
                     || ctx.Request.Path.StartsWithSegments("/index.html")
                     || ctx.Request.Path.StartsWithSegments("/swagger/v1/swagger.json")), swaggerApp =>
{
    swaggerApp.UseSwagger();
    swaggerApp.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SIGAC API V1");
        c.RoutePrefix = string.Empty;
        c.IndexStream = () =>
        {
            var stream = c.GetType().Assembly.GetManifestResourceStream("Swashbuckle.AspNetCore.SwaggerUI.index.html");
            if (stream == null) return null!;
            using var reader = new StreamReader(stream);
            var html = reader.ReadToEnd();

            // 1. Elimina completamente el script obsoleto de 2017 para Edge que intentaba hacer 'window.fetch = undefined'
            var patchedHtml = System.Text.RegularExpressions.Regex.Replace(
                html,
                @"<script>\s*if\s*\(\s*window\.navigator\.userAgent\.indexOf\(""Edge""\)[^<]*</script>",
                "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            patchedHtml = patchedHtml.Replace("window.fetch = undefined;", "");

            // 2. Inyecta protector de window.fetch en el <head> para navegadores modernos o iframes donde fetch sea getter-only
            var fetchGuardScript = "<head><script>(function(){try{var _f=window.fetch;Object.defineProperty(window,'fetch',{get:function(){return _f;},set:function(fn){if(typeof fn==='function'){_f=fn;}},configurable:true,enumerable:true});}catch(e){}})();</script>";
            patchedHtml = patchedHtml.Replace("<head>", fetchGuardScript);

            return new MemoryStream(Encoding.UTF8.GetBytes(patchedHtml));
        };
    });
});

// 3. Demás middlewares
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/index.html"));

// Carga Inicial de Datos (Data Seeding)
await DbInitializer.InitializeAsync(app.Services);

app.Run();
