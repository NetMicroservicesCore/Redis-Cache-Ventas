using Microsoft.EntityFrameworkCore;
using PosCaja.Domain;

namespace PosCaja.Infrastructure.SqlServer;

public sealed class PosCajaDbContext(DbContextOptions<PosCajaDbContext> options) : DbContext(options)
{
    public DbSet<Venta> Ventas => Set<Venta>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfiguration(new VentaConfiguration());
}
