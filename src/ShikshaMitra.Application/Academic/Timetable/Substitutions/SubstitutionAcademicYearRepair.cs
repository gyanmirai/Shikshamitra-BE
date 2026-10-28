using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

[RemoteService(false)]
public class SubstitutionAcademicYearRepair(
    IRepository<SubstitutionRequest, Guid> substitutions,
    IRepository<AcademicYear, Guid> years,
    IRepository<SubstitutionAuditLog, Guid> audits,
    ICurrentTenant tenant,
    ICurrentUser user,
    IUserScopeAccessService scopes,
    IAuthorizationService authorization,
    IAdmissionAssessmentResourceLock resourceLock,
    IUnitOfWorkManager units,
    IAsyncQueryableExecuter executer,
    IGuidGenerator guids) : ITransientDependency
{
    private static readonly Guid RepairResourceId = Guid.Parse("f48afc79-748a-47e4-9773-f2a57796fd48");

    [UnitOfWork(true)]
    public virtual async Task<AcademicYearRepairResult> RepairAsync(Guid academicYearId, int maxResultCount = 100)
    {
        if (maxResultCount is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(maxResultCount));
        await authorization.CheckAsync(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add);
        var scope = await scopes.GetCurrentUserScopeAsync();
        if (!tenant.Id.HasValue || scope.TenantId != tenant.Id || (!scope.IsAdmin && !scope.HasWildcardAcademicYear))
            throw new BusinessException("Substitution:AcademicYearRepairRequiresTenantWideScope");
        await resourceLock.AcquireAsync(tenant.Id, [RepairResourceId]);
        var year = await years.GetAsync(academicYearId);
        var first = year.StartDate.Date;
        var last = year.EndDate.Date;
        var pending = (await substitutions.GetQueryableAsync()).Where(row => !row.AcademicYearId.HasValue &&
            row.Date.Date >= first && row.Date.Date <= last);
        var rows = await executer.ToListAsync(pending.OrderBy(row => row.Date).ThenBy(row => row.Id).Take(maxResultCount));
        foreach (var row in rows) row.SetAcademicYear(year.Id);
        if (rows.Count > 0)
        {
            await substitutions.UpdateManyAsync(rows);
            await audits.InsertManyAsync(rows.Select(row => new SubstitutionAuditLog(guids.Create(), tenant.Id, row.Id,
                user.Name ?? user.UserName ?? "System", "Academic Year Repaired",
                $"Assigned legacy substitution to academic year {year.Id} ({year.StartDate:yyyy-MM-dd} to {year.EndDate:yyyy-MM-dd}).", user.Id)));
            await units.Current!.SaveChangesAsync();
        }
        return new AcademicYearRepairResult(rows.Count, await executer.AnyAsync(pending));
    }
}

public sealed record AcademicYearRepairResult(int UpdatedCount, bool HasMore);
