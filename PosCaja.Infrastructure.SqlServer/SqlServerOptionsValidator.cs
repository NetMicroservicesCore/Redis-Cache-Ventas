using Microsoft.Extensions.Options;

namespace PosCaja.Infrastructure.SqlServer;

public sealed class SqlServerOptionsValidator : IValidateOptions<SqlServerOptions>
{
    public ValidateOptionsResult Validate(string? name, SqlServerOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Server))
            errors.Add("SqlServer:Server es obligatorio.");
        if (string.IsNullOrWhiteSpace(options.Database))
            errors.Add("SqlServer:Database es obligatorio.");
        if (options.IntegratedSecurity)
        {
            if (!string.IsNullOrEmpty(options.User) || !string.IsNullOrEmpty(options.Password))
                errors.Add("SqlServer:User y SqlServer:Password deben omitirse cuando IntegratedSecurity=true.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(options.User))
                errors.Add("SqlServer:User es obligatorio cuando IntegratedSecurity=false.");
            if (string.IsNullOrEmpty(options.Password))
                errors.Add("SqlServer:Password es obligatorio cuando IntegratedSecurity=false.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
