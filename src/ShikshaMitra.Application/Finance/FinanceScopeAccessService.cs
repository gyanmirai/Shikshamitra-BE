using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance;

[RemoteService(false)]
public class FinanceScopeAccessService(
	IUserScopeService userScopeService,
	IAcademicYearRepository academicYearRepository,
	IReadOnlyRepository<Student, Guid> studentRepository)
	: ApplicationService, ITransientDependency
{
	public async Task EnsureAcademicContextAccessibleAsync(
		Guid? academicYearId,
		Guid? boardId,
		Guid? educationMediumId,
		Guid? standardId,
		Guid? divisionId,
		CancellationToken cancellationToken = default)
	{
		if (await HasTenantWideYearAccessAsync(academicYearId, boardId, educationMediumId, standardId, divisionId, cancellationToken))
		{
			return;
		}

		var scopedStudents = await GetScopedStudentsQueryAsync();

		if (academicYearId.HasValue && academicYearId.Value != Guid.Empty)
		{
			scopedStudents = scopedStudents.Where(s => s.AcademicYearId == academicYearId.Value);
		}

		if (boardId.HasValue && boardId.Value != Guid.Empty)
		{
			scopedStudents = scopedStudents.Where(s => s.AcademicBoardId == boardId.Value);
		}

		if (educationMediumId.HasValue && educationMediumId.Value != Guid.Empty)
		{
			scopedStudents = scopedStudents.Where(s => s.AcademicMediumId == educationMediumId.Value);
		}

		if (standardId.HasValue && standardId.Value != Guid.Empty)
		{
			scopedStudents = scopedStudents.Where(s => s.StandardId == standardId.Value);
		}

		if (divisionId.HasValue && divisionId.Value != Guid.Empty)
		{
			scopedStudents = scopedStudents.Where(s => s.DivisionId == divisionId.Value);
		}

		if (!await AsyncExecuter.AnyAsync(scopedStudents, cancellationToken))
		{
			throw new AbpAuthorizationException("Access denied: requested academic context is outside your user scope.");
		}
	}

	/// <summary>
	/// Non-throwing variant of <see cref="EnsureAcademicContextAccessibleAsync"/>.
	/// Use for list endpoints that should return an empty result (not 403) when the
	/// requested context is outside the current user's scope.
	/// </summary>
	public async Task<bool> IsAcademicContextAccessibleAsync(
		Guid? academicYearId,
		Guid? boardId,
		Guid? educationMediumId,
		Guid? standardId,
		Guid? divisionId,
		CancellationToken cancellationToken = default)
	{
		if (await HasTenantWideYearAccessAsync(academicYearId, boardId, educationMediumId, standardId, divisionId, cancellationToken))
		{
			return true;
		}

		var scopedStudents = await GetScopedStudentsQueryAsync();

		if (academicYearId.HasValue && academicYearId.Value != Guid.Empty)
			scopedStudents = scopedStudents.Where(s => s.AcademicYearId == academicYearId.Value);

		if (boardId.HasValue && boardId.Value != Guid.Empty)
			scopedStudents = scopedStudents.Where(s => s.AcademicBoardId == boardId.Value);

		if (educationMediumId.HasValue && educationMediumId.Value != Guid.Empty)
			scopedStudents = scopedStudents.Where(s => s.AcademicMediumId == educationMediumId.Value);

		if (standardId.HasValue && standardId.Value != Guid.Empty)
			scopedStudents = scopedStudents.Where(s => s.StandardId == standardId.Value);

		if (divisionId.HasValue && divisionId.Value != Guid.Empty)
			scopedStudents = scopedStudents.Where(s => s.DivisionId == divisionId.Value);

		return await AsyncExecuter.AnyAsync(scopedStudents, cancellationToken);
	}

	private async Task<bool> HasTenantWideYearAccessAsync(
		Guid? academicYearId, Guid? boardId, Guid? educationMediumId, Guid? standardId, Guid? divisionId,
		CancellationToken cancellationToken)
	{
		// Year-level Finance setup must be possible before the first student is admitted.
		// More specific contexts and individual students still use the scoped student query.
		if (!academicYearId.HasValue || academicYearId.Value == Guid.Empty ||
			boardId.HasValue || educationMediumId.HasValue || standardId.HasValue || divisionId.HasValue)
		{
			return false;
		}

		if (!CurrentUser.Id.HasValue)
		{
			throw new AbpAuthorizationException("Current user context is required.");
		}

		var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
		if (!scope.HasTenantWideScope())
		{
			return false;
		}

		return await academicYearRepository.AnyAsync(
			year => year.Id == academicYearId.Value && year.TenantId == CurrentTenant.Id,
			cancellationToken);
	}

	public async Task<IQueryable<Guid>> GetScopedStudentIdsQueryAsync()
	{
		var query = await GetScopedStudentsQueryAsync();
		return query.Select(s => s.Id);
	}

	public async Task EnsureStudentAccessibleAsync(Guid studentId, CancellationToken cancellationToken = default)
	{
		var scopedStudents = await GetScopedStudentsQueryAsync();
		if (!await AsyncExecuter.AnyAsync(scopedStudents.Where(s => s.Id == studentId), cancellationToken))
		{
			throw new AbpAuthorizationException("Access denied: student is outside your user scope.");
		}
	}

	private async Task<IQueryable<Student>> GetScopedStudentsQueryAsync()
	{
		var currentUserId = CurrentUser.Id;
		if (!currentUserId.HasValue)
		{
			throw new AbpAuthorizationException("Current user context is required.");
		}

		var scope = await userScopeService.GetUserScopeAsync(currentUserId.Value);
		var query = await studentRepository.GetQueryableAsync();

		if (scope.IsAdmin)
		{
			return query;
		}

		if (!scope.HasAnyAccess)
		{
			return query.Where(_ => false);
		}

		var scopedQuery = query.Where(s =>
			scope.HasWildcardAcademicYear ||
			(s.AcademicYearId.HasValue && scope.WildcardBoardAcademicYearIds.Contains(s.AcademicYearId.Value)) ||
			(s.DivisionId.HasValue && (
				scope.DivisionIds.Contains(s.DivisionId.Value) ||
				(scope.HasWildcardDivision && s.StandardId.HasValue && scope.WildcardDivisionStandardIds.Contains(s.StandardId.Value)))) ||
			(s.StandardId.HasValue && (
				scope.StandardIds.Contains(s.StandardId.Value) ||
				(scope.HasWildcardStandard && s.AcademicMediumStageId.HasValue && scope.WildcardStandardMediumStageIds.Contains(s.AcademicMediumStageId.Value)))) ||
			(s.AcademicMediumStageId.HasValue && (
				scope.MediumStageIds.Contains(s.AcademicMediumStageId.Value) ||
				(scope.HasWildcardMediumStage && scope.WildcardMediumStageMediumIds.Contains(s.AcademicMediumId)))) ||
			scope.MediumIds.Contains(s.AcademicMediumId) ||
			(scope.HasWildcardMedium && s.AcademicBoardId.HasValue && scope.WildcardMediumBoardIds.Contains(s.AcademicBoardId.Value)) ||
			(s.AcademicBoardId.HasValue && scope.BoardIds.Contains(s.AcademicBoardId.Value)));

		if (scope.HasWildcardAcademicYear)
		{
			return scopedQuery;
		}

		if (scope.AcademicYearIds.Any())
		{
			return scopedQuery.Where(s =>
				!s.AcademicYearId.HasValue ||
				scope.AcademicYearIds.Contains(s.AcademicYearId.Value));
		}

		return scopedQuery.Where(s => !s.AcademicYearId.HasValue);
	}
}
