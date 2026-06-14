using BuildingFex.Api.Iam.Domain.Model.Aggregates;
using BuildingFex.Api.Shared.Domain.Model.Entities;

namespace BuildingFex.Api.Incidents.Domain.Model.Aggregates;

public class Incident : IAuditableEntity
{
    public int Id { get; private set; }
    public string ExternalId { get; private set; } = string.Empty;
    public int OwnerAdminId { get; private set; }
    public User? OwnerAdmin { get; private set; }
    public string? ResidentExternalId { get; private set; }
    public string ResidentName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Status { get; private set; } = "open";
    public string? Provider { get; private set; }
    public DateTimeOffset ReportedAt { get; private set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Incident()
    {
    }

    public static Incident Create(
        

        string externalId,
        int ownerAdminId,
        string description,
        string status = "open",
        string? residentExternalId = null,
        string? residentName = null,
        string? provider = null,
        DateTimeOffset? reportedAt = null
        )
    
    {
        var validStatuses = new[] { "open", "in_progress", "closed" };
        if (!validStatuses.Contains(status))
            throw new ArgumentException("Estado inválido", nameof(status));
        return new Incident
        {
            ExternalId = externalId.Trim(),
            OwnerAdminId = ownerAdminId,
            Description = description.Trim(),
            Status = string.IsNullOrWhiteSpace(status) ? "open" : status.Trim(),
            ResidentExternalId = string.IsNullOrWhiteSpace(residentExternalId) ? null : residentExternalId.Trim(),
            ResidentName = residentName?.Trim() ?? string.Empty,
            Provider = provider,
            ReportedAt = reportedAt ?? DateTimeOffset.UtcNow,
        };
    }

    public void Update(
        string description,
        string status,
        string? provider,
        string? residentExternalId = null,
        string? residentName = null)
    {
        if (description.Length < 10)
            throw new ArgumentException("La descripción debe tener al menos 10 caracteres.", nameof(description));

        var validStatuses = new[] { "open", "in_progress", "closed" };
        if (!string.IsNullOrWhiteSpace(status) && !validStatuses.Contains(status))
            throw new ArgumentException("Estado inválido", nameof(status));
        Description = description.Trim();
        Status = string.IsNullOrWhiteSpace(status) ? Status : status.Trim();
        Provider = provider;

        if (residentExternalId is not null)
            ResidentExternalId = string.IsNullOrWhiteSpace(residentExternalId) ? null : residentExternalId.Trim();

        if (residentName is not null)
            
            ResidentName = residentName.Trim();

        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
