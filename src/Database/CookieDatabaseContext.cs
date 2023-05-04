using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;
using OoLunar.CookieClicker.Entities;

namespace OoLunar.CookieClicker.Database
{
    public sealed class CookieDatabaseContext : DbContext, IDesignTimeDbContextFactory<CookieDatabaseContext>
    {
        public DbSet<Cookie> Cookies { get; init; } = null!;

        public CookieDatabaseContext() { }
        public CookieDatabaseContext(DbContextOptions<CookieDatabaseContext> options) : base(options) { }

        public CookieDatabaseContext CreateDbContext(string[] args)
        {
            ConfigurationBuilder configurationBuilder = new();
            configurationBuilder.Sources.Clear();
            configurationBuilder.AddJsonFile("config.json", true, true);
            configurationBuilder.AddEnvironmentVariables("CookieClicker_");
            configurationBuilder.AddCommandLine(args);
            IConfigurationRoot configuration = configurationBuilder.Build();
            DbContextOptionsBuilder<CookieDatabaseContext> optionsBuilder = new();
            ConfigureOptions(optionsBuilder, configuration);
            return new(optionsBuilder.Options);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Cookie>(entity =>
        {
            entity.HasKey(cookie => cookie.Id);
            entity.Property(cookie => cookie.Id)
                .HasConversion(id => id.ToGuid(), id => new Ulid(id))
                .ValueGeneratedOnAdd();
        });

        internal static NpgsqlConnectionStringBuilder GetConnectionString(IConfiguration configuration) => new()
        {
            ApplicationName = configuration.GetValue("Database:ApplicationName", "Cookie Clicker"),
            Database = configuration.GetValue("Database:DatabaseName", "cookie_clicker"),
            Host = configuration.GetValue("Database:Host", "localhost"),
            Username = configuration.GetValue("Database:Username", "cookie_clicker"),
            Port = configuration.GetValue("Database:Port", 5432),
            Password = configuration.GetValue<string>("Database:Password")
        };

        internal static void ConfigureOptions(DbContextOptionsBuilder optionsBuilder, IConfiguration configuration) => optionsBuilder.UseNpgsql(GetConnectionString(configuration).ToString(), options => options.EnableRetryOnFailure(5).CommandTimeout(5))
            .UseSnakeCaseNamingConvention()
            .EnableThreadSafetyChecks(false);
    }
}
