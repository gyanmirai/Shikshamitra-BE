using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Records;

public sealed record StudentRecordsAccess(Student Student, string Access, string EntrySource, bool PlacementEditable, StudentRecordsPolicy Policy);

/// <summary>Records eligibility is separate from operational enrollment and never gates daily module lookup.</summary>
[RemoteService(false)]
public class StudentRecordsAccessService(
	StudentScopeAccessService scope,
	IStudentRepository students,
	IRepository<StudentOnboardingCase, Guid> cases,
	IRepository<StudentRecordRenewalCase, Guid> renewals,
	IRepository<StudentEnrollment, Guid> enrollments,
	IRepository<StudentRecordLegacyOverride, Guid> overrides,
	StudentRecordsPolicyResolver policies,
	IdentityUserManager users) : ApplicationService, ITransientDependency
{
    public async Task<IQueryable<Guid>> GetOnboardingEligibleStudentIdsAsync()
    {
        var caseQuery = await cases.GetQueryableAsync();
        var enrollmentQuery = await enrollments.GetQueryableAsync();
        return caseQuery.Where(c => c.TenantId == CurrentTenant.Id &&
            (c.Status == StudentOnboardingStatus.Completed || c.Status != StudentOnboardingStatus.Cancelled && c.StudentEnrollmentId.HasValue &&
                enrollmentQuery.Any(e => e.TenantId == CurrentTenant.Id && e.Id == c.StudentEnrollmentId && e.StudentId == c.StudentId && e.AcademicYearId == c.AcademicYearId)))
            .Select(c => c.StudentId).Distinct();
    }

	public async Task<StudentRecordsAccess> EnsureReadableAsync(Guid studentId)
	{
		return await EvaluateEligibilityAsync(await GetScopedStudentAsync(studentId));
	}

	// Internal: caller must first establish its own tenant and association boundary.
	internal async Task<StudentRecordsAccess> EvaluateEligibilityAsync(Student student)
	{
		var studentId = student.Id;
		var policy = (await policies.ResolveAsync(student.AcademicYearId)).Policy;
		var caseQuery = await cases.GetQueryableAsync();
		var currentProvisioning = await AsyncExecuter.AnyAsync(caseQuery.Where(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId &&
			x.AcademicYearId == student.AcademicYearId && x.Status == StudentOnboardingStatus.Provisioning && !x.StudentEnrollmentId.HasValue));
		var renewalQuery = await renewals.GetQueryableAsync();
		currentProvisioning = currentProvisioning || await AsyncExecuter.AnyAsync(renewalQuery.Where(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId &&
			x.AcademicYearId == student.AcademicYearId && x.Status == "Provisioning"));
		var placementEditable = !currentProvisioning || policy.Provisioning.AllowPlacementEdits;
        if (await AsyncExecuter.AnyAsync((await GetOnboardingEligibleStudentIdsAsync()).Where(id => id == studentId)))
        {
            var completed = await AsyncExecuter.AnyAsync(caseQuery.Where(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.Status == StudentOnboardingStatus.Completed));
            return new(student, "Full", completed ? "CompletedOnboarding" : "ActivatedOnboarding", placementEditable, policy);
        }
		var overrideQuery = await overrides.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(overrideQuery.Where(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.Status == "Approved")))
			return new(student, "Full", "LegacyOverride", placementEditable, policy);
		var enrollmentQuery = await enrollments.GetQueryableAsync();
		var provisioning = await AsyncExecuter.AnyAsync(caseQuery.Where(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId &&
			x.Status == StudentOnboardingStatus.Provisioning && x.StudentEnrollmentId.HasValue && enrollmentQuery.Any(e =>
				e.TenantId == CurrentTenant.Id && e.Id == x.StudentEnrollmentId && e.StudentId == studentId && e.IsActive)));
		if (!provisioning || policy.Provisioning.Mode == "OnboardingOnly") throw new BusinessException("Students:Records:OnboardingIncomplete");
		return new(student, policy.Provisioning.Mode, "Provisioning", policy.Provisioning.Mode == "Full" && policy.Provisioning.AllowPlacementEdits, policy);
	}

	public async Task<Student> GetScopedStudentAsync(Guid id)
	{
		await EnsureActiveActorAsync();
		if (!CurrentTenant.Id.HasValue) throw new BusinessException("Students:Records:TenantRequired");
		var query = await students.GetQueryableWithoutAutoIncludesAsync();
		var student = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id && x.TenantId == CurrentTenant.Id))
			?? throw new EntityNotFoundException(typeof(Student), id);
		await scope.EnsureCanAccessStudentAsync(student, "student records");
		return student;
	}

	public async Task<StudentRecordsAccess> EnsureWritableAsync(Guid studentId, bool placement = false)
	{
		var access = await EnsureReadableAsync(studentId);
		if (access.Access != "Full" || (placement && !access.PlacementEditable)) throw new BusinessException("Students:Records:ReadOnly");
		return access;
	}

	public async Task<bool> IsEligibleHistoricalStudentAsync(Guid studentId)
	{
		var student = await GetScopedStudentAsync(studentId);
		if (student.Status is not (StudentLifeCycleStatusEnum.Enrolled or StudentLifeCycleStatusEnum.InActive or StudentLifeCycleStatusEnum.Left)) return false;
		var enrollmentQuery = await enrollments.GetQueryableAsync();
		if (!await AsyncExecuter.AnyAsync(enrollmentQuery.Where(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId)) &&
			(student.Status != StudentLifeCycleStatusEnum.Left || !student.AcademicYearId.HasValue)) return false;
		var caseQuery = await cases.GetQueryableAsync();
		return !await AsyncExecuter.AnyAsync(caseQuery.Where(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId &&
			(x.Status == StudentOnboardingStatus.Completed || !x.IsHistoricalBackfill)));
	}

	public async Task<List<string>> GetDatabaseRoleNamesAsync()
	{
		var user = await LazyServiceProvider.LazyGetRequiredService<StudentRecordsActorService>().EnsureActiveAsync();
		return (await users.GetRolesAsync(user)).ToList();
	}

	public Task EnsureActiveActorAsync() => LazyServiceProvider.LazyGetRequiredService<StudentRecordsActorService>().EnsureActiveAsync();
}
