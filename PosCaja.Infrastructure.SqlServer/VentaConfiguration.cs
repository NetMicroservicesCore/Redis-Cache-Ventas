using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PosCaja.Domain;

namespace PosCaja.Infrastructure.SqlServer;

public sealed class VentaConfiguration : IEntityTypeConfiguration<Venta>
{
    public void Configure(EntityTypeBuilder<Venta> builder)
    {
        builder.ToTable("venta", "dbo");
        builder.HasKey(v => new { v.IdVenta, v.IdPos }).HasName("pk_venta");
        builder.Property(v => v.IdVenta).HasColumnName("id_venta").ValueGeneratedNever();
        builder.Property(v => v.IdPos).HasColumnName("id_pos").ValueGeneratedNever();
        builder.Property(v => v.Vendedor).HasColumnName("vendedor").HasColumnType("varchar(15)").HasMaxLength(15).IsUnicode(false).IsRequired();
        builder.Property(v => v.Supervisor).HasColumnName("supervisor").HasColumnType("varchar(15)").HasMaxLength(15).IsUnicode(false).IsRequired(false);
        builder.Property(v => v.Folio).HasColumnName("folio");
        builder.Property(v => v.FechaVenta).HasColumnName("fecha_venta").HasColumnType("datetime");
        builder.Property(v => v.TotalVendido).HasColumnName("total_vendido").HasPrecision(19, 3);
        builder.Property(v => v.PagoEfectivo).HasColumnName("pago_efectivo").HasPrecision(19, 2);
        builder.Property(v => v.PagoCheque).HasColumnName("pago_cheque").HasPrecision(19, 3);
        builder.Property(v => v.PagoVales).HasColumnName("pago_vales").HasPrecision(19, 2);
        builder.Property(v => v.PagoTc).HasColumnName("pago_tc").HasPrecision(19, 2);
        builder.Property(v => v.Upload).HasColumnName("upload");
        builder.Property(v => v.NumRegistros).HasColumnName("num_registros");
    }
}
