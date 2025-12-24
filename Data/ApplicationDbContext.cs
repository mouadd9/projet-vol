using Microsoft.EntityFrameworkCore;
using MoteurDeRechercheDeVol.Models;

namespace MoteurDeRechercheDeVol.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<SearchHistory> SearchHistories { get; set; }
        public DbSet<City> Cities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SearchHistory>()
                .HasIndex(s => s.SearchDate);

            modelBuilder.Entity<City>()
                .HasIndex(c => c.CityName);
        }
    }
}
