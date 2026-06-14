using System.Text;
using BuildingFex.Api.Iam.Application.CommandServices;
using BuildingFex.Api.Iam.Application.Internal.CommandServices;
using BuildingFex.Api.Iam.Application.Internal.OutboundServices;
using BuildingFex.Api.Iam.Application.Internal.QueryServices;
using BuildingFex.Api.Iam.Application.QueryServices;
using BuildingFex.Api.Iam.Domain.Repositories;
using BuildingFex.Api.Iam.Infrastructure.Hashing.BCrypt.Services;
using BuildingFex.Api.Iam.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using BuildingFex.Api.Iam.Infrastructure.Persistence.Seeding;
using BuildingFex.Api.Incidents.Application.CommandServices;
using BuildingFex.Api.Incidents.Application.Internal.CommandServices;
using BuildingFex.Api.Incidents.Application.Internal.QueryServices;
using BuildingFex.Api.Incidents.Application.QueryServices;
using BuildingFex.Api.Incidents.Domain.Repositories;
using BuildingFex.Api.Incidents.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using BuildingFex.Api.Finances.Application.Internal;
using BuildingFex.Api.Finances.Application.Internal.Dashboard;
using BuildingFex.Api.Finances.Application.Internal.MercadoPago;
using BuildingFex.Api.Finances.Domain.Repositories;
using BuildingFex.Api.Finances.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using BuildingFex.Api.Finances.Infrastructure.Persistence.Seeding;
using BuildingFex.Api.Incidents.Infrastructure.Persistence.Seeding;
using BuildingFex.Api.Information.Application.CommandServices;
using BuildingFex.Api.Information.Application.Internal.CommandServices;
using BuildingFex.Api.Information.Application.Internal.QueryServices;
using BuildingFex.Api.Information.Application.QueryServices;
using BuildingFex.Api.Information.Domain.Repositories;
using BuildingFex.Api.Information.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using BuildingFex.Api.Information.Infrastructure.Persistence.Seeding;
using BuildingFex.Api.SocialSpaces.Application.Internal;
using BuildingFex.Api.SocialSpaces.Domain.Repositories;
using BuildingFex.Api.SocialSpaces.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using BuildingFex.Api.SocialSpaces.Infrastructure.Persistence.Seeding;
using BuildingFex.Api.Iam.Infrastructure.Tokens.Jwt.Configuration;
using BuildingFex.Api.Iam.Infrastructure.Tokens.Jwt.Services;
using BuildingFex.Api.Shared.Domain.Repositories;
using BuildingFex.Api.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using BuildingFex.Api.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var tokenSettings = builder.Configuration.GetSection("TokenSettings").Get<TokenSettings>()
    ?? throw new InvalidOperationException("TokenSettings section is missing.");

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "BuildingFex API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = [],
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.Configure<TokenSettings>(builder.Configuration.GetSection("TokenSettings"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenSettings.Secret)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    options.UseMySQL(connectionString);
});

// Shared
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// IAM
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserCommandService, UserCommandService>();
builder.Services.AddScoped<IUserQueryService, UserQueryService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IHashingService, HashingService>();
builder.Services.AddScoped<DbJsonUserSeeder>();

// Incidents
builder.Services.AddScoped<IIncidentRepository, IncidentRepository>();
builder.Services.AddScoped<IIncidentCommandService, IncidentCommandService>();
builder.Services.AddScoped<IIncidentQueryService, IncidentQueryService>();
builder.Services.AddScoped<DbJsonIncidentSeeder>();

// Finances
builder.Services.AddScoped<IFeeRepository, FeeRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IReceiptRepository, ReceiptRepository>();
builder.Services.AddScoped<IFinanceSettingRepository, FinanceSettingRepository>();
builder.Services.AddScoped<IKpiRepository, KpiRepository>();
builder.Services.AddScoped<IAdminManagementExpenseRepository, AdminManagementExpenseRepository>();
builder.Services.AddScoped<ISharedUtilityServiceRepository, SharedUtilityServiceRepository>();
builder.Services.AddScoped<IFixedPayoutRecipientRepository, FixedPayoutRecipientRepository>();
builder.Services.AddScoped<FinanceOwnerResolver>();
builder.Services.AddScoped<DbJsonFinanceSeeder>();

// Mercado Pago
builder.Services.Configure<MercadoPagoSettings>(builder.Configuration.GetSection("MercadoPago"));
builder.Services.AddScoped<IMercadoPagoService, MercadoPagoService>();

// Dashboard
builder.Services.AddScoped<IDashboardQueryService, DashboardQueryService>();

// SocialSpaces
builder.Services.AddScoped<ISocialSpaceRepository, SocialSpaceRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<SocialSpacesOwnerResolver>();
builder.Services.AddScoped<DbJsonSocialSpacesSeeder>();

// Information
builder.Services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
builder.Services.AddScoped<IAnnouncementCommandService, AnnouncementCommandService>();
builder.Services.AddScoped<IAnnouncementQueryService, AnnouncementQueryService>();
builder.Services.AddScoped<DbJsonAnnouncementSeeder>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        // Usamos EnsureCreated en lugar de Migrate para evitar un bug conocido
        // de MySql.EntityFrameworkCore con MariaDB (XAMPP) al intentar hacer GET_LOCK.
        context.Database.EnsureCreated();

        var seedPath = builder.Configuration["Seed:DbJsonPath"];
        if (!string.IsNullOrWhiteSpace(seedPath))
        {
            if (!Path.IsPathRooted(seedPath))
                seedPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, seedPath));

            var userSeeder = scope.ServiceProvider.GetRequiredService<DbJsonUserSeeder>();
            await userSeeder.SeedAsync(seedPath);

            var financeSeeder = scope.ServiceProvider.GetRequiredService<DbJsonFinanceSeeder>();
            await financeSeeder.SeedAsync(seedPath);

            var incidentSeeder = scope.ServiceProvider.GetRequiredService<DbJsonIncidentSeeder>();
            await incidentSeeder.SeedAsync(seedPath);

            var socialSpacesSeeder = scope.ServiceProvider.GetRequiredService<DbJsonSocialSpacesSeeder>();
            await socialSpacesSeeder.SeedAsync(seedPath);

            var announcementSeeder = scope.ServiceProvider.GetRequiredService<DbJsonAnnouncementSeeder>();
            await announcementSeeder.SeedAsync(seedPath);
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "⚠️ Hubo un error al inicializar la base de datos (EnsureCreated / Seeding). La API iniciará pero algunas funciones podrían fallar.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "buildingfex-api" }))
    .WithTags("Health");

app.Run();
