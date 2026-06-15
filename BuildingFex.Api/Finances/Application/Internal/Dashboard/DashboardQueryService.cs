using BuildingFex.Api.Finances.Domain.Model.Aggregates;
using BuildingFex.Api.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using Microsoft.EntityFrameworkCore;

namespace BuildingFex.Api.Finances.Application.Internal.Dashboard;

public class DashboardQueryService(AppDbContext dbContext) : IDashboardQueryService
{
    public async Task<DashboardKpiResponse> GetKpisAsync(int ownerAdminId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthStartIso = monthStart.ToString("yyyy-MM-dd");

        var totalCollected = await dbContext.Set<Payment>()
            .Where(p => p.OwnerAdminId == ownerAdminId)
            .Where(p => p.PaidAt != null && p.PaidAt.CompareTo(monthStartIso) >= 0)
            .SumAsync(p => p.Amount, ct);

        var totalPendingDebt = await dbContext.Set<Receipt>()
            .Where(r => r.OwnerAdminId == ownerAdminId && r.Status != "Paid")
            .SumAsync(r => r.Amount + r.LateFee + r.ExtraCharges, ct);

        var kpiRecord = await dbContext.Set<KpiRecord>()
            .Where(k => k.OwnerAdminId == ownerAdminId)
            .OrderByDescending(k => k.Id)
            .FirstOrDefaultAsync(ct);

        var totalResidents = kpiRecord?.TotalResidents ?? 0;
        var occupiedUnits = kpiRecord?.OccupiedUnits ?? 0;
        var emptyUnits = kpiRecord?.EmptyUnits ?? 0;
        var totalDebt = kpiRecord?.TotalDebt ?? totalPendingDebt;

        var recentPayments = await dbContext.Set<Payment>()
            .Where(p => p.OwnerAdminId == ownerAdminId)
            .OrderByDescending(p => p.Id)
            .Take(10)
            .Select(p => new RecentPaymentDto(
                p.ExternalId,
                p.ResidentExternalId,
                p.Amount,
                p.PaidAt,
                p.Method,
                p.Reference))
            .ToListAsync(ct);

        var adminExpenses = await dbContext.Set<AdminManagementExpense>()
            .Where(e => e.OwnerAdminId == ownerAdminId)
            .OrderByDescending(e => e.PurchaseDate)
            .Take(20)
            .Select(e => new AdminExpenseSummaryDto(e.Name, e.Amount, e.PurchaseDate))
            .ToListAsync(ct);

        var totalAdminExpenses = adminExpenses.Sum(e => e.Amount);

        return new DashboardKpiResponse(
            TotalCollectedThisMonth: totalCollected,
            TotalPendingDebt: totalPendingDebt,
            TotalResidents: totalResidents,
            OccupiedUnits: occupiedUnits,
            EmptyUnits: emptyUnits,
            TotalDebt: totalDebt,
            RecentPayments: recentPayments,
            TotalAdminExpenses: totalAdminExpenses,
            AdminExpenses: adminExpenses);
    }
}
