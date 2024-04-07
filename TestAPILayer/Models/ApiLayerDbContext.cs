using TestAPILayer.Models.Configurations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TestAPILayer.Models
{
    public class ApiLayerDbContext : IdentityDbContext<ApiUser>
    {
        // options coming from Program.cs
        public ApiLayerDbContext(DbContextOptions<ApiLayerDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfiguration(new RoleConfiguration());
        }
    }

    public class ApiLayerDbContextFactory : IDesignTimeDbContextFactory<ApiLayerDbContext>
    {
        public ApiLayerDbContext CreateDbContext(string[] args)
        {
            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<ApiLayerDbContext>();
            var conn = config.GetConnectionString("ApiLayerDbConnectionString");
            optionsBuilder.UseSqlServer(conn);

            return new ApiLayerDbContext(optionsBuilder.Options);

        }
    }
}
