using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance;

public partial class FinanceScopeAccessService
{
	// Refund history follows the placement recorded for its year. Current permission and
	// scope grants still apply; a past class assignment is not itself an access grant.
	internal async Task<IQueryable<Guid>> GetRefundScopedStudentIdsQueryAsync(
		Guid userId, Guid academicYearId, string actionPermission, bool capTeacherAssignments = true,
		CancellationToken cancellationToken = default)
	{
		if (!CurrentTenant.Id.HasValue || userId == Guid.Empty || academicYearId == Guid.Empty ||
			string.IsNullOrWhiteSpace(actionPermission) || !await permissionChecker.IsGrantedAsync(actionPermission))
			throw new AbpAuthorizationException("Finance refund scope is unavailable.");
		var current = (await GetRequestScopedStudentsQueryForUserAsync(
			userId, academicYearId, cancellationToken, capTeacherAssignments)).Select(s => s.Id);
		var identity = await GetActiveIdentityAsync(userId);
		var scope = await userScopeService.GetUserScopeAsync(userId, cancellationToken);
		var enrollmentRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<StudentEnrollment, Guid>>();
		var standardRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<Standard, Guid>>();
		var tenantStudentIds = (await studentRepository.GetQueryableAsync())
			.Where(s => s.TenantId == CurrentTenant.Id).Select(s => s.Id);
		var enrollments = (await enrollmentRepository.GetQueryableAsync()).Where(e =>
			e.TenantId == CurrentTenant.Id && e.AcademicYearId == academicYearId &&
			tenantStudentIds.Contains(e.StudentId));
		var standards = await standardRepository.GetQueryableAsync();
		var placed = from enrollment in enrollments
			join standard in standards on enrollment.StandardId equals (Guid?)standard.Id
			where standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId == academicYearId
			select new
			{
				enrollment.StudentId, enrollment.DivisionId, enrollment.StandardId,
				StageId = standard.EducationMediumStageId,
				MediumId = standard.EducationMediumStage.EducationMediumId,
				BoardId = standard.EducationMediumStage.EducationMedium.EducationBoardId
			};
		if (!scope.IsAdmin && !scope.HasWildcardAcademicYear)
		{
			if (!scope.HasAnyAccess || !scope.AcademicYearIds.Contains(academicYearId))
				return current;
			placed = placed.Where(p =>
				scope.WildcardBoardAcademicYearIds.Contains(academicYearId) ||
				scope.WildcardMediumBoardIds.Contains(p.BoardId) ||
				scope.WildcardMediumStageMediumIds.Contains(p.MediumId) ||
				scope.WildcardStandardMediumStageIds.Contains(p.StageId) ||
				p.StandardId.HasValue && scope.WildcardDivisionStandardIds.Contains(p.StandardId.Value) ||
				p.DivisionId.HasValue && scope.DivisionIds.Contains(p.DivisionId.Value));
		}
		if (!scope.IsAdmin && capTeacherAssignments)
		{
			var employee = await GetCurrentTenantEmployeeAsync(userId, cancellationToken);
			var hasHistory = await HasClassTeacherHistoryAsync(identity.Id, employee, cancellationToken);
			if (employee == null)
			{
				if (hasHistory) return current;
			}
			else if (employee.EmployeeStatusType != EmployeeStatusType.Active) return current;
			else if (hasHistory)
			{
				var assignments = (await classTeacherRepository.GetQueryableAsync()).Where(a =>
					a.TenantId == CurrentTenant.Id && a.TeacherId == employee.Id &&
					a.Status == ClassTeacherAssignmentStatus.Active && a.AcademicYearId == academicYearId);
				placed = placed.Where(p => assignments.Any(a =>
					a.EducationBoardId == p.BoardId && a.EducationMediumId == p.MediumId &&
					a.EducationMediumStageId == p.StageId && a.StandardId == p.StandardId &&
					a.DivisionId == p.DivisionId));
			}
		}
		return current.Union(placed.Select(p => p.StudentId));
	}
}
