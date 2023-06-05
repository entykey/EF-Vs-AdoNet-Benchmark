namespace EF_Vs_AdoNet_Benchmark.DAL
{
    using Models;
    using Microsoft.EntityFrameworkCore;


    public class EFDbContext : DbContext
    {
        public EFDbContext(DbContextOptions<EFDbContext> options)
        : base(options)
        {
        }

        public DbSet<MyEntity> MyEntities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure the entity mappings and relationships
            // ...

            base.OnModelCreating(modelBuilder);
        }
    }
}
