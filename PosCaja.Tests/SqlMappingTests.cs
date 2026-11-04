using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PosCaja.Domain;
using PosCaja.Infrastructure.SqlServer;

namespace PosCaja.Tests;

public sealed class SqlMappingTests
{
    [Fact]
    public void ExistingTableMappingPreservesSchemaTypesAndCompositeKey()
    {
        using var context = new PosCajaDbContext(new DbContextOptionsBuilder<PosCajaDbContext>()
            .UseSqlServer("Server=unused;Database=pos_caja;Integrated Security=true").Options);
        var entity = context.Model.FindEntityType(typeof(Venta))!;
        Assert.Equal("venta", entity.GetTableName());
        Assert.Equal("dbo", entity.GetSchema());
        Assert.Equal(new[] { "IdVenta", "IdPos" }, entity.FindPrimaryKey()!.Properties.Select(p => p.Name));
        foreach (var property in entity.FindPrimaryKey()!.Properties)
            Assert.Equal(ValueGenerated.Never, property.ValueGenerated);
        Assert.True(entity.FindProperty("Supervisor")!.IsNullable);
        Assert.False(entity.FindProperty("Vendedor")!.IsNullable);
        Assert.Equal("varchar(15)", entity.FindProperty("Vendedor")!.GetColumnType());
        Assert.Equal("varchar(15)", entity.FindProperty("Supervisor")!.GetColumnType());
        Assert.Equal("datetime", entity.FindProperty("FechaVenta")!.GetColumnType());
        foreach (var name in new[] { "TotalVendido", "PagoCheque", "PagoEfectivo", "PagoVales", "PagoTc" })
        {
            var property = entity.FindProperty(name)!;
            Assert.Equal(19, property.GetPrecision());
            Assert.Equal(name is "TotalVendido" or "PagoCheque" ? 3 : 2, property.GetScale());
        }
        var table = StoreObjectIdentifier.Table("venta", "dbo");
        Assert.Equal("id_venta", entity.FindProperty("IdVenta")!.GetColumnName(table));
        Assert.Equal("id_pos", entity.FindProperty("IdPos")!.GetColumnName(table));
    }
}
