using System.Globalization;

namespace PosCaja.Infrastructure.Redis
{
    public static class VentaCacheKeys
    {
        public static string ForVenta(string prefix,Guid idVenta,int idPos)
        {
            var ventaId = idVenta.ToString("N");
            var posId = idPos.ToString(CultureInfo.InvariantCulture);
            return $"{prefix}item:{ventaId}:{posId}";
        }

        public static string ForPage(string prefix,int pagina,int tamanoPagina)
        {
            var page = pagina.ToString(CultureInfo.InvariantCulture);
            var size = tamanoPagina.ToString(CultureInfo.InvariantCulture);
            return $"{prefix}page:{page}:size:{size}";
        }
    }
}
