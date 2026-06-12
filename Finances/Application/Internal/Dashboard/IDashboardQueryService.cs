namespace BuildingFex.Api.Finances.Application.Internal.Dashboard;

public interface IDashboardQueryService
{
    /// <summary>
    /// Computes dashboard KPIs in real-time from the database for the given admin owner.
    /// </summary>
    Task<DashboardKpiResponse> GetKpisAsync(int ownerAdminId, CancellationToken ct = default);
}
