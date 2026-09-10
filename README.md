# SIGAC API (Sistema Integrado de Gestión Académica y Cátedras)

Backend REST API desarrollado en **C# con ASP.NET Core (.NET Web API)** y **Entity Framework Core**, con autenticación JWT, control de acceso basado en roles (RBAC) y documentación interactiva OpenAPI / Swagger UI.

## Stack Tecnológico Oficial

- **Lenguaje y Framework:** C# (.NET 10 / ASP.NET Core Web API)
- **Persistencia y ORM:** Entity Framework Core (PostgreSQL con soporte InMemory para desarrollo)
- **Seguridad:** JWT Bearer Authentication con HMAC-SHA512
- **Documentación Interactiva:** Swashbuckle Swagger UI OpenAPI

## Estructura del Proyecto

```
├── back/
│   ├── Controllers/     # Controladores de la API (Clase, Docente, Estudiante, Login, etc.)
│   ├── DTOs/            # Data Transfer Objects
│   ├── Entities/        # Modelos de entidad de dominio de EF Core
│   ├── Migrations/      # Migraciones de EF Core
│   ├── Middleware/      # Manejo global de excepciones
│   ├── Services/        # Servicios (TokenService JWT, etc.)
│   ├── AppDbContext.cs  # DbContext de Entity Framework Core
│   ├── Program.cs       # Punto de entrada y configuración de servicios / pipeline
│   └── back.csproj      # Proyecto .NET
├── back.slnx            # Solución .NET
└── backend_context.txt  # Contexto de archivos del backend
```

## Ejecución y Compilación

```bash
# Compilar proyecto
dotnet build back/back.csproj

# Ejecutar API
dotnet run --project back/back.csproj --urls "http://0.0.0.0:3000"
```
