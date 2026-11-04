using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace PosCaja.Infrastructure.SqlServer;

public sealed class SqlServerOptions
{
    public const string SectionName = "SqlServer";

    public string Server { get; set; } = "";
    public string Database { get; set; } = "";
    public bool IntegratedSecurity { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; }

    // No registrar el resultado: puede contener credenciales.
    public string BuildConnectionString()
    {
        var validation = new SqlServerOptionsValidator().Validate(Options.DefaultName, this);
        if (validation.Failed)
            throw new OptionsValidationException(Options.DefaultName, typeof(SqlServerOptions), validation.Failures);

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = Database,
            IntegratedSecurity = IntegratedSecurity,
            Encrypt = Encrypt,
            TrustServerCertificate = TrustServerCertificate,
            PersistSecurityInfo = false
        };
        if (!IntegratedSecurity)
        {
            builder.UserID = User!;
            builder.Password = Password!;
        }
        return builder.ConnectionString;
    }
}
