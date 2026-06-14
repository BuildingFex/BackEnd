using BuildingFex.Api.Finances.Infrastructure.Persistence.EntityFrameworkCore.Configuration.Extensions;
using BuildingFex.Api.Iam.Infrastructure.Persistence.EntityFrameworkCore.Configuration.Extensions;
using BuildingFex.Api.Incidents.Infrastructure.Persistence.EntityFrameworkCore.Configuration.Extensions;
using BuildingFex.Api.Information.Infrastructure.Persistence.EntityFrameworkCore.Configuration.Extensions;
using BuildingFex.Api.SocialSpaces.Infrastructure.Persistence.EntityFrameworkCore.Configuration.Extensions;
using BuildingFex.Api.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration.Extensions;
using Microsoft.EntityFrameworkCore;

namespace BuildingFex.Api.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;

/// <summary>
/// DbContext compartido por todos los bounded contexts de BuildingFex.
/// Cada módulo registra sus tablas en OnModelCreating via Apply*Configuration().
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Registrar configuraciones por bounded context
        builder.ApplyIamConfiguration();
        builder.ApplyIncidentsConfiguration();
        builder.ApplyFinancesConfiguration();
        builder.ApplySocialSpacesConfiguration();
        builder.ApplyInformationConfiguration();
        // ...

        builder.UseSnakeCaseNamingConvention();
    }
}
