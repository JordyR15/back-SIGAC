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
                .WithMany(c => c.Ayudantias) // Corregido
                .HasForeignKey(a => a.CatedraId);

            modelBuilder.Entity<Ayudantia>()
                .HasOne(a => a.Estudiante)
                .WithMany(u => u.AyudantiasEstudiante)
                .HasForeignKey(a => a.EstudianteId);
        }
    }
}
