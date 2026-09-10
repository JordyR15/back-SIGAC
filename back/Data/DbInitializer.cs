using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using back.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace back.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("DbInitializer");

            try
            {
                if (context.Database.IsRelational())
                {
                    logger?.LogInformation("Aplicando migraciones en base de datos relacional...");
                    await context.Database.MigrateAsync();
                }
                else
                {
                    logger?.LogInformation("Inicializando esquema de base de datos InMemory...");
                    await context.Database.EnsureCreatedAsync();
                }

                // Verificar si ya existen usuarios
                if (await context.Users.AnyAsync())
                {
                    logger?.LogInformation("La base de datos ya contiene usuarios registrados. Omitiendo seeding.");
                    return;
                }

                logger?.LogInformation("Iniciando carga inicial de datos (Data Seeding)...");

                // 1. Usuarios base para cada rol institucional con contraseñas hasheadas
                var (adminUser, adminPersona) = CreateUser(
                    "admin",
                    "Admin123!",
                    "Administrador",
                    "Sistema",
                    "admin@uteq.edu.ec",
                    new[] { "Administrador" }
                );

                var (coordinadorUser, coordinadorPersona) = CreateUser(
                    "coordinador",
                    "Coord123!",
                    "Elena",
                    "Vargas",
                    "coordinador@uteq.edu.ec",
                    new[] { "Coordinador" }
                );

                var (docenteUser, docentePersona) = CreateUser(
                    "docente",
                    "Docente123!",
                    "Carlos",
                    "Mendoza",
                    "docente@uteq.edu.ec",
                    new[] { "Docente" }
                );

                var (estudianteUser, estudiantePersona) = CreateUser(
                    "estudiante",
                    "Estudiante123!",
                    "Jordy",
                    "Rivas",
                    "estudiante@uteq.edu.ec",
                    new[] { "Estudiante" }
                );

                var (ayudanteUser, ayudantePersona) = CreateUser(
                    "ayudante",
                    "Ayudante123!",
                    "Andres",
                    "Paredes",
                    "ayudante@uteq.edu.ec",
                    new[] { "Estudiante", "Ayudante" }
                );

                var (juradoUser, juradoPersona) = CreateUser(
                    "jurado",
                    "Jurado123!",
                    "Manuel",
                    "Castro",
                    "jurado@uteq.edu.ec",
                    new[] { "Jurado" }
                );

                var (decanoUser, decanoPersona) = CreateUser(
                    "decano",
                    "Decano123!",
                    "Roberto",
                    "Gomez",
                    "decano@uteq.edu.ec",
                    new[] { "Decano" }
                );

                context.Users.AddRange(adminUser, coordinadorUser, docenteUser, estudianteUser, ayudanteUser, juradoUser, decanoUser);
                context.Personas.AddRange(adminPersona, coordinadorPersona, docentePersona, estudiantePersona, ayudantePersona, juradoPersona, decanoPersona);
                await context.SaveChangesAsync();

                // 2. Cátedras de prueba vinculadas al Docente
                var catedra1 = new Catedra
                {
                    Nombre = "Arquitectura de Software",
                    Semestre = "2026-1",
                    DocenteId = docenteUser.Id,
                    MinimoNota = 75.0
                };

                var catedra2 = new Catedra
                {
                    Nombre = "Estructuras de Datos y Algoritmos",
                    Semestre = "2026-1",
                    DocenteId = docenteUser.Id,
                    MinimoNota = 70.0
                };

                var catedra3 = new Catedra
                {
                    Nombre = "Sistemas Operativos y Redes",
                    Semestre = "2026-1",
                    DocenteId = docenteUser.Id,
                    MinimoNota = 70.0
                };

                context.Catedras.AddRange(catedra1, catedra2, catedra3);
                await context.SaveChangesAsync();

                // 3. Materias correspondientes para soporte de clases y sesiones
                var materia1 = new Materia
                {
                    Nombre = "Arquitectura de Software",
                    Descripcion = "Patrones de diseño, microservicios y arquitectura orientada a eventos",
                    Codigo = "SOF401",
                    DocenteResponsableId = docenteUser.Id
                };

                var materia2 = new Materia
                {
                    Nombre = "Estructuras de Datos y Algoritmos",
                    Descripcion = "Complejidad algorítmica, grafos y árboles de búsqueda",
                    Codigo = "SOF201",
                    DocenteResponsableId = docenteUser.Id
                };

                var materia3 = new Materia
                {
                    Nombre = "Sistemas Operativos y Redes",
                    Descripcion = "Gestión de procesos, memoria y protocolos de red",
                    Codigo = "SOF301",
                    DocenteResponsableId = docenteUser.Id
                };

                context.Materias.AddRange(materia1, materia2, materia3);
                await context.SaveChangesAsync();

                // 4. Registros de inscripción, avance curricular y validación de malla para el Estudiante
                // Garantiza que GET /api/Estudiante/{id}/validacion-malla retorne 200 OK con avance aprobatorio
                var inscripcionEst1 = new Inscripcion
                {
                    EstudianteId = estudianteUser.Id,
                    CatedraId = catedra1.Id,
                    PromedioActual = 88.5,
                    AlertaRendimiento = false
                };

                var inscripcionEst2 = new Inscripcion
                {
                    EstudianteId = estudianteUser.Id,
                    CatedraId = catedra2.Id,
                    PromedioActual = 92.0,
                    AlertaRendimiento = false
                };

                var inscripcionEst3 = new Inscripcion
                {
                    EstudianteId = estudianteUser.Id,
                    CatedraId = catedra3.Id,
                    PromedioActual = 80.0,
                    AlertaRendimiento = false
                };

                var inscripcionAyu1 = new Inscripcion
                {
                    EstudianteId = ayudanteUser.Id,
                    CatedraId = catedra1.Id,
                    PromedioActual = 95.0,
                    AlertaRendimiento = false
                };

                var inscripcionAyu2 = new Inscripcion
                {
                    EstudianteId = ayudanteUser.Id,
                    CatedraId = catedra2.Id,
                    PromedioActual = 91.0,
                    AlertaRendimiento = false
                };

                context.Inscripciones.AddRange(inscripcionEst1, inscripcionEst2, inscripcionEst3, inscripcionAyu1, inscripcionAyu2);
                await context.SaveChangesAsync();

                // 5. Clases y Sesiones de Clase para que los endpoints no retornen 403 ni 404
                var clase1 = new Clase
                {
                    Nombre = "Arquitectura de Software - Paralelo A",
                    MateriaId = materia1.Id,
                    DocenteId = docenteUser.Id,
                    Estudiantes = new List<User> { estudianteUser, ayudanteUser }
                };

                context.Clases.Add(clase1);
                await context.SaveChangesAsync();

                inscripcionEst1.ClaseId = clase1.Id;
                inscripcionAyu1.ClaseId = clase1.Id;
                await context.SaveChangesAsync();

                var sesionPresencial = new ClaseSesion
                {
                    MateriaId = materia1.Id,
                    ClaseId = clase1.Id,
                    DocenteId = docenteUser.Id,
                    Fecha = DateTime.UtcNow.Date.AddDays(1),
                    HoraInicio = new TimeSpan(8, 0, 0),
                    HoraFin = new TimeSpan(10, 0, 0),
                    TipoClase = "Presencial",
                    EdificioPresencial = "Facultad de Ciencias de la Ingeniería",
                    AulaPresencial = "Aula 204",
                    PisoPresencial = "Piso 2",
                    LinkVirtual = string.Empty,
                    AplicacionVirtual = string.Empty
                };

                var sesionVirtual = new ClaseSesion
                {
                    MateriaId = materia1.Id,
                    ClaseId = clase1.Id,
                    DocenteId = docenteUser.Id,
                    Fecha = DateTime.UtcNow.Date.AddDays(3),
                    HoraInicio = new TimeSpan(10, 0, 0),
                    HoraFin = new TimeSpan(12, 0, 0),
                    TipoClase = "Virtual",
                    LinkVirtual = "https://meet.google.com/uteq-sigac-sesion",
                    AplicacionVirtual = "Google Meet",
                    EdificioPresencial = string.Empty,
                    AulaPresencial = string.Empty,
                    PisoPresencial = string.Empty
                };

                context.ClasesSesiones.AddRange(sesionPresencial, sesionVirtual);
                await context.SaveChangesAsync();

                // 6. Convocatoria de defensa para que GET /api/Jurado/presentaciones retorne datos
                var ayudantia = new Ayudantia
                {
                    CatedraId = catedra1.Id,
                    EstudianteId = ayudanteUser.Id,
                    Estado = "Activa",
                    HorasAsignadas = 16
                };

                context.Ayudantias.Add(ayudantia);
                await context.SaveChangesAsync();

                var presentacion = new Presentacion
                {
                    AyudantiaId = ayudantia.Id,
                    Fecha = DateTime.UtcNow.AddDays(7),
                    Jurados = new List<User> { juradoUser },
                    CoordinadorCarreraId = coordinadorUser.Id,
                    DecanoId = decanoUser.Id
                };

                context.Presentaciones.Add(presentacion);
                await context.SaveChangesAsync();

                logger?.LogInformation("Data Seeding completado exitosamente: 7 usuarios, 3 cátedras, 3 materias, inscripciones, clases, sesiones y presentación de jurados creados.");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error durante la inicialización de la base de datos (DbInitializer).");
                throw;
            }
        }

        private static (User User, Persona Persona) CreateUser(
            string username,
            string password,
            string nombre,
            string apellido,
            string correo,
            IEnumerable<string> roles)
        {
            using var hmac = new HMACSHA512();
            var user = new User
            {
                Username = username.ToLowerInvariant(),
                PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password)),
                PasswordSalt = hmac.Key
            };

            var persona = new Persona
            {
                Nombre = nombre,
                Apellido = apellido,
                Correo = correo,
                User = user
            };
            persona.SetRoles(roles);
            user.Persona = persona;

            return (user, persona);
        }
    }
}
