using back.Entities;
using Microsoft.EntityFrameworkCore;

namespace back.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Persona> Personas { get; set; }
        public DbSet<Catedra> Catedras { get; set; }
        public DbSet<Inscripcion> Inscripciones { get; set; }
        public DbSet<Evaluacion> Evaluaciones { get; set; }
        public DbSet<IndicadorCualitativo> IndicadoresCualitativos { get; set; }
        public DbSet<Ayudantia> Ayudantias { get; set; }
        public DbSet<ActividadAyudantia> ActividadesAyudantia { get; set; }
        public DbSet<Bitacora> Bitacoras { get; set; }
        public DbSet<CronogramaActividad> Cronogramas { get; set; }
        public DbSet<Recurso> Recursos { get; set; }
        public DbSet<Actividad> Actividades { get; set; }
        public DbSet<ClaseSesion> ClasesSesiones { get; set; }
        public DbSet<Asistencia> Asistencias { get; set; }
        public DbSet<Clase> Clases { get; set; }
        public DbSet<RecursoVistoPorEstudiante> RecursosVistosPorEstudiante { get; set; } // Añadido

        // Presentaciones y evaluaciones realizadas por jurados en las postulaciones
        public DbSet<Presentacion> Presentaciones { get; set; }
        public DbSet<PresentacionEvaluacion> PresentacionEvaluaciones { get; set; }

        // Import jobs (bulk upload audits/results)
        public DbSet<ImportJob> ImportJobs { get; set; }
        public DbSet<ImportJobEntry> ImportJobEntries { get; set; }
        public DbSet<EstudianteActividadRealizada> EstudianteActividadesRealizadas { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Relación uno a uno User <-> Persona
            modelBuilder.Entity<User>()
                .HasOne(u => u.Persona)
                .WithOne(p => p.User)
                .HasForeignKey<Persona>(p => p.UserId);

            // Relaciones de Inscripcion
            modelBuilder.Entity<Inscripcion>()
                .HasKey(i => new { i.EstudianteId, i.CatedraId });

            modelBuilder.Entity<Inscripcion>()
                .HasOne(i => i.Estudiante)
                .WithMany(u => u.Inscripciones)
                .HasForeignKey(i => i.EstudianteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Inscripcion>()
                .HasOne(i => i.Catedra)
                .WithMany(c => c.Inscripciones)
                .HasForeignKey(i => i.CatedraId);

            // Relaciones de Catedra
            modelBuilder.Entity<Catedra>()
                .HasOne(c => c.Docente)
                .WithMany(u => u.CatedrasDocente)
                .HasForeignKey(c => c.DocenteId);

            // Relaciones de Ayudantia
            modelBuilder.Entity<Ayudantia>()
                .HasOne(a => a.Catedra)
                .WithMany(c => c.Ayudantias)
                .HasForeignKey(a => a.CatedraId);

            modelBuilder.Entity<Ayudantia>()
                .HasOne(a => a.Estudiante)
                .WithMany(u => u.AyudantiasEstudiante)
                .HasForeignKey(a => a.EstudianteId);

            // Relaciones para ClaseSesion
            modelBuilder.Entity<ClaseSesion>()
                .HasOne(cs => cs.Materia)
                .WithMany(m => m.ClasesSesiones)
                .HasForeignKey(cs => cs.MateriaId);

            modelBuilder.Entity<ClaseSesion>()
                .HasOne(cs => cs.Docente)
                .WithMany(u => u.ClasesSesionesDocente)
                .HasForeignKey(cs => cs.DocenteId);

            // Relaciones para Asistencia
            modelBuilder.Entity<Asistencia>()
                .HasOne(a => a.ClaseSesion)
                .WithMany(cs => cs.Asistencias)
                .HasForeignKey(a => a.ClaseSesionId);

            modelBuilder.Entity<Asistencia>()
                .HasOne(a => a.Estudiante)
                .WithMany(u => u.AsistenciasEstudiante)
                .HasForeignKey(a => a.EstudianteId);

            // Relaciones para Clase
            modelBuilder.Entity<Clase>()
                .HasOne(c => c.Materia)
                .WithMany(m => m.Clases)
                .HasForeignKey(c => c.MateriaId);

            modelBuilder.Entity<Clase>()
                .HasOne(c => c.Docente)
                .WithMany(u => u.ClasesDocente)
                .HasForeignKey(c => c.DocenteId);

            // Relación muchos a muchos entre Clase y User (Estudiantes)
            modelBuilder.Entity<Clase>()
                .HasMany(c => c.Estudiantes)
                .WithMany(u => u.ClasesEstudiante)
                .UsingEntity(j => j.ToTable("ClaseEstudiante")); // Tabla de unión

            // Relaciones para RecursoVistoPorEstudiante
            modelBuilder.Entity<RecursoVistoPorEstudiante>()
                .HasOne(rv => rv.Recurso)
                .WithMany() // No necesitamos una colección en Recurso para esto
                .HasForeignKey(rv => rv.RecursoId);

            modelBuilder.Entity<RecursoVistoPorEstudiante>()
                .HasOne(rv => rv.Estudiante)
                .WithMany(u => u.RecursosVistos) // Asumiendo que User tiene esta colección
                .HasForeignKey(rv => rv.EstudianteId);

            // Relaciones para Presentacion y PresentacionEvaluacion
            modelBuilder.Entity<Presentacion>()
                .HasOne(p => p.Ayudantia)
                .WithMany(a => a.Presentaciones)
                .HasForeignKey(p => p.AyudantiaId);

            modelBuilder.Entity<Presentacion>()
                .HasMany(p => p.Jurados)
                .WithMany(u => u.PresentacionesJurado)
                .UsingEntity(j => j.ToTable("PresentacionJurado"));

            modelBuilder.Entity<PresentacionEvaluacion>()
                .HasOne(pe => pe.Presentacion)
                .WithMany(p => p.Evaluaciones)
                .HasForeignKey(pe => pe.PresentacionId);

            modelBuilder.Entity<PresentacionEvaluacion>()
                .HasOne(pe => pe.Jurado)
                .WithMany() // No necesitamos colección inversa por ahora
                .HasForeignKey(pe => pe.JuradoId);

            modelBuilder.Entity<Presentacion>()
                .HasOne(p => p.Decano)
                .WithMany()
                .HasForeignKey(p => p.DecanoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Presentacion>()
                .HasOne(p => p.CoordinadorCarrera)
                .WithMany()
                .HasForeignKey(p => p.CoordinadorCarreraId)
                .OnDelete(DeleteBehavior.Restrict);

            // ImportJob relations
            modelBuilder.Entity<ImportJob>()
                .HasOne(j => j.CreatedBy)
                .WithMany() // no collection on User for jobs
                .HasForeignKey(j => j.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ImportJobEntry>()
                .HasOne(e => e.ImportJob)
                .WithMany(j => j.Entries)
                .HasForeignKey(e => e.ImportJobId);

            modelBuilder.Entity<EstudianteActividadRealizada>()
                .HasKey(e => e.Id);

            modelBuilder.Entity<EstudianteActividadRealizada>()
                .HasOne(e => e.Estudiante)
                .WithMany()
                .HasForeignKey(e => e.EstudianteId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EstudianteActividadRealizada>()
                .HasOne(e => e.Actividad)
                .WithMany()
                .HasForeignKey(e => e.ActividadId)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}
