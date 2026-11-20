using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Dashboard;

[Volo.Abp.RemoteService(false)]
public class FinanceDashboardPayerScope(FinanceScopeAccessService scope, IReadOnlyRepository<Student, Guid> students,
    IReadOnlyRepository<FinanceFeeAccount, Guid> accounts) : ApplicationService
{
    internal sealed class Placement
    {
        public Guid StudentId { get; init; }
        public Guid? ClassId { get; init; }
    }
    internal record Payers(IQueryable<Placement> Placements, IQueryable<FinanceFeeAccount> Accounts);

    internal async Task<Payers> ReadAsync(Guid yearId, CancellationToken cancellationToken)
    {
        var tenantStudents = (await students.GetQueryableAsync()).Where(student => student.TenantId == CurrentTenant.Id);
        var visibleStudents = await scope.GetScopedStudentIdsQueryAsync();
        var enrollments = await scope.GetScopedFeeCollectionEnrollmentsQueryAsync(yearId, cancellationToken);
        var current = tenantStudents.Where(student => student.AcademicYearId == yearId);
        var currentPlacements = current.Where(student => visibleStudents.Contains(student.Id))
            .Select(student => new { StudentId = student.Id, ClassId = student.StandardId });
        var historicalPlacements = enrollments.Where(enrollment => tenantStudents.Any(student =>
            student.Id == enrollment.StudentId && student.AcademicYearId != yearId))
            .Select(enrollment => new { enrollment.StudentId, ClassId = enrollment.StandardId });

        // A year has one enrollment per student. Current placement wins for students still in that year.
        var placements = currentPlacements.Concat(historicalPlacements)
            .Select(row => new Placement { StudentId = row.StudentId, ClassId = row.ClassId });
        var visibleAccounts = await scope.GetScopedFeeCollectionAccountIdsQueryAsync(yearId, cancellationToken);
        var visibleStudentIds = placements.Select(row => row.StudentId);
        var scopedAccounts = (await accounts.GetQueryableAsync()).Where(account =>
            account.TenantId == CurrentTenant.Id && account.AcademicYearId == yearId &&
            visibleAccounts.Contains(account.Id) &&
            (!account.StudentId.HasValue || visibleStudentIds.Contains(account.StudentId.Value)));
        return new(placements, scopedAccounts);
    }
}
