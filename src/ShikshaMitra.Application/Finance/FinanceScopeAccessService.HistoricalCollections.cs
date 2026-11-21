using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance;

public partial class FinanceScopeAccessService
{
	public async Task<IQueryable<Guid>> GetScopedFeeCollectionAccountIdsQueryAsync(Guid academicYearId,
		CancellationToken cancellationToken = default)
	{
		if (!CurrentUser.Id.HasValue || !CurrentTenant.Id.HasValue)
			throw new AbpAuthorizationException("Fee collection context is unavailable.");
		var identity = await GetActiveIdentityAsync(CurrentUser.Id.Value);
		var scope = await LazyServiceProvider.LazyGetRequiredService<IUserScopeAccessService>()
			.GetCurrentUserScopeAsync();
		var query = (await LazyServiceProvider
			.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>().GetQueryableAsync())
			.Where(account => account.TenantId == CurrentTenant.Id &&
				account.AcademicYearId == academicYearId);
		query = FinanceFeeAccountScopeQueryBuilder.ApplyScope(query, scope);
		if (!scope.IsAdmin)
		{
			var employee = await GetCurrentTenantEmployeeAsync(CurrentUser.Id.Value, cancellationToken);
			if (employee != null && employee.EmployeeStatusType != EmployeeStatusType.Active)
				return query.Where(_ => false).Select(account => account.Id);
			if (await HasClassTeacherHistoryAsync(identity.Id, employee, cancellationToken))
			{
				var currentIds = await GetScopedStudentIdsQueryAsync();
				var historicalIds = (await GetScopedFeeCollectionEnrollmentsQueryAsync(
					academicYearId, cancellationToken)).Select(enrollment => enrollment.StudentId);
				query = FinanceFeeAccountScopeQueryBuilder.ApplyLinkedStudentCap(
					query, currentIds.Concat(historicalIds));
			}
		}
		return query.Select(account => account.Id);
	}

	public async Task EnsureFeeCollectionAccountAccessibleAsync(FinanceFeeAccount account,
		CancellationToken cancellationToken = default)
	{
		var ids = await GetScopedFeeCollectionAccountIdsQueryAsync(account.AcademicYearId, cancellationToken);
		if (account.TenantId != CurrentTenant.Id ||
			!await AsyncExecuter.AnyAsync(ids.Where(id => id == account.Id), cancellationToken))
			throw new AbpAuthorizationException();
		if (account.StudentId.HasValue)
			await EnsureFeeCollectionStudentAccessibleAsync(account.StudentId.Value,
				account.AcademicYearId, cancellationToken);
	}

	public async Task<IQueryable<StudentEnrollment>> GetScopedFeeCollectionEnrollmentsQueryAsync(
		Guid academicYearId, CancellationToken cancellationToken = default)
	{
		if (!CurrentUser.Id.HasValue || !CurrentTenant.Id.HasValue || academicYearId == Guid.Empty)
			throw new AbpAuthorizationException("Fee collection context is unavailable.");
		var identity = await GetActiveIdentityAsync(CurrentUser.Id.Value);
		var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value, cancellationToken);
		var enrollments = (await LazyServiceProvider
			.LazyGetRequiredService<IRepository<StudentEnrollment, Guid>>().GetQueryableAsync())
			.Where(enrollment => enrollment.TenantId == CurrentTenant.Id &&
				enrollment.AcademicYearId == academicYearId);
		if (!scope.HasAcademicYearWideScope(academicYearId))
		{
			if (!scope.HasAnyAccess || !Enumerable.Contains(scope.AcademicYearIds, academicYearId))
				return enrollments.Where(_ => false);
			var standards = await LazyServiceProvider
				.LazyGetRequiredService<IRepository<Standard, Guid>>().GetQueryableAsync();
			enrollments = enrollments.Where(enrollment => enrollment.StandardId.HasValue &&
				(Enumerable.Contains(scope.WildcardDivisionStandardIds, enrollment.StandardId.Value) ||
				 enrollment.DivisionId.HasValue && Enumerable.Contains(scope.DivisionIds, enrollment.DivisionId.Value) ||
				 standards.Any(standard => standard.Id == enrollment.StandardId.Value &&
					(Enumerable.Contains(scope.WildcardMediumStageMediumIds,
						standard.EducationMediumStage.EducationMediumId) ||
					 Enumerable.Contains(scope.WildcardStandardMediumStageIds,
						standard.EducationMediumStageId) ||
					 Enumerable.Contains(scope.WildcardMediumBoardIds,
						standard.EducationMediumStage.EducationMedium.EducationBoardId)))));
		}
		if (scope.IsAdmin) return enrollments;
		var employee = await GetCurrentTenantEmployeeAsync(CurrentUser.Id.Value, cancellationToken);
		if (employee != null && employee.EmployeeStatusType != EmployeeStatusType.Active)
			return enrollments.Where(_ => false);
		if (!await HasClassTeacherHistoryAsync(identity.Id, employee, cancellationToken))
			return enrollments;
		if (employee == null) return enrollments.Where(_ => false);
		var assignments = (await classTeacherRepository.GetQueryableAsync()).Where(assignment =>
			assignment.TenantId == CurrentTenant.Id && assignment.TeacherId == employee.Id &&
			assignment.Status == ClassTeacherAssignmentStatus.Active &&
			assignment.AcademicYearId == academicYearId);
		return enrollments.Where(enrollment => assignments.Any(assignment =>
			assignment.StandardId == enrollment.StandardId &&
			assignment.DivisionId == enrollment.DivisionId));
	}

	public async Task<IQueryable<Guid>> GetScopedFeeCollectionEnrollmentIdsQueryAsync(
		Guid academicYearId, Guid boardId, Guid educationMediumId, Guid? standardId,
		Guid? divisionId, CancellationToken cancellationToken = default)
	{
		var enrollments = await GetScopedFeeCollectionEnrollmentsQueryAsync(academicYearId, cancellationToken);
		var standards = await LazyServiceProvider
			.LazyGetRequiredService<IRepository<Standard, Guid>>().GetQueryableAsync();
		enrollments = enrollments.Where(enrollment => enrollment.StandardId.HasValue &&
			standards.Any(standard => standard.Id == enrollment.StandardId.Value &&
				standard.EducationMediumStage.EducationMediumId == educationMediumId &&
				standard.EducationMediumStage.EducationMedium.EducationBoardId == boardId));
		if (standardId.HasValue) enrollments = enrollments.Where(e => e.StandardId == standardId.Value);
		if (divisionId.HasValue) enrollments = enrollments.Where(e => e.DivisionId == divisionId.Value);
		return enrollments.Select(enrollment => enrollment.StudentId).Distinct();
	}

	public async Task EnsureFeeCollectionStudentAccessibleAsync(Guid studentId, Guid academicYearId,
		CancellationToken cancellationToken = default)
	{
		var current = (await studentRepository.GetQueryableAsync()).Where(student =>
			student.Id == studentId && student.AcademicYearId == academicYearId &&
			student.TenantId == CurrentTenant.Id);
		var scopedCurrent = await GetScopedStudentIdsQueryAsync();
		if (await AsyncExecuter.AnyAsync(current.Where(student => scopedCurrent.Contains(student.Id)),
			cancellationToken)) return;
		var historical = await GetScopedFeeCollectionEnrollmentsQueryAsync(academicYearId, cancellationToken);
		if (!await AsyncExecuter.AnyAsync(historical.Where(enrollment =>
			enrollment.StudentId == studentId), cancellationToken))
			throw new AbpAuthorizationException("Student is outside the selected fee collection year and scope.");
	}
}
