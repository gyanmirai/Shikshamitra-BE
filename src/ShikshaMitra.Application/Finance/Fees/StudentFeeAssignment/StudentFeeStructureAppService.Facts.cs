using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	private sealed class EnrollmentFact
	{
		public Guid Id { get; set; }
		public Guid StudentId { get; set; }
		public Guid? StandardId { get; set; }
		public Guid? DivisionId { get; set; }
		public bool IsActive { get; set; }
		public string ConcurrencyStamp { get; set; } = "";
	}
	private async Task<IQueryable<EnrollmentFact>> ScopedEnrollmentsAsync(Guid year)
	{
		if (year == Guid.Empty) throw new BusinessException(AssignmentErrorCodes.InvalidInput);
		if (!CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
		var students = await studentRepository.GetQueryableAsync();
		var assignments = await studentFeeStructureRepository.GetQueryableAsync();
		var structures = await feeStructureRepository.GetQueryableAsync();
		// StudentEnrollment is an unmapped draft aggregate. The live student academic context is authoritative.
		var current = students.Where(s => s.AcademicYearId == year).Select(s => new EnrollmentFact {
			Id = s.Id, StudentId = s.Id, StandardId = s.StandardId, DivisionId = s.DivisionId,
			IsActive = s.Status == ShikshaMitra.Students.StudentLifeCycleStatusEnum.Enrolled, ConcurrencyStamp = s.ConcurrencyStamp });
		var historical = from a in assignments join s in students on a.StudentId equals s.Id
			join f in structures on a.FeeStructureStandardId equals f.Id
			where a.AcademicYearId == year && s.AcademicYearId != year
			select new EnrollmentFact { Id = a.Id, StudentId = s.Id, StandardId = f.StandardId, DivisionId = null,
				IsActive = false, ConcurrencyStamp = a.ConcurrencyStamp };
		var enrollments = current.Concat(historical);
		if (scope.IsAdmin || scope.HasWildcardAcademicYear) return enrollments;
		if (!scope.HasAnyAccess || !scope.AcademicYearIds.Contains(year)) return enrollments.Where(x => false);
		if (scope.WildcardBoardAcademicYearIds.Contains(year)) return enrollments;
		var standards = await standardRepository.GetQueryableAsync();
		return from e in enrollments
			join s in standards on e.StandardId equals s.Id
			where (e.DivisionId.HasValue && scope.DivisionIds.Contains(e.DivisionId.Value)) ||
				scope.WildcardDivisionStandardIds.Contains(s.Id) ||
				scope.WildcardStandardMediumStageIds.Contains(s.EducationMediumStageId) ||
				scope.WildcardMediumStageMediumIds.Contains(s.EducationMediumStage.EducationMediumId) ||
				scope.WildcardMediumBoardIds.Contains(s.EducationMediumStage.EducationMedium.EducationBoardId)
			select e;
	}

	private async Task<EnrollmentFact> RequireEnrollmentAsync(Guid studentId, Guid year, bool assigning)
	{
		var query = (await ScopedEnrollmentsAsync(year)).Where(x => x.StudentId == studentId);
		var records = await AsyncExecuter.ToListAsync(query);
		if (records.Count == 0) throw new BusinessException(AssignmentErrorCodes.EnrollmentRequired);
		if (!assigning) return records.OrderByDescending(x => x.IsActive).First();
		var active = records.Where(x => x.IsActive).ToList();
		if (active.Count != 1 || !active[0].StandardId.HasValue)
			throw new BusinessException(active.Count == 0 ? AssignmentErrorCodes.InactiveEnrollment : AssignmentErrorCodes.EnrollmentRequired);
		return active[0];
	}

	private async Task<(AssignmentServicePeriodDto Period, string Verification, string? Revision)> ResolveServiceAsync(
		Guid studentId, AssignmentServicePeriodDto input, AcademicYear year)
	{
		var start = year.StartDate.Date;
		var end = year.EndDate.Date;
		var verification = "Configured academic year (not a verified joining date)";
		string? revision = year.ConcurrencyStamp;
		switch (input.Source)
		{
			case "AcademicYear":
				if ((input.StartDate.HasValue && input.StartDate.Value.Date != start) ||
					(input.EndDate.HasValue && input.EndDate.Value.Date != end) || input.SourceReferenceId.HasValue)
					throw new BusinessException(AssignmentErrorCodes.InvalidInput);
				break;
			case "TransportService":
				if (!input.SourceReferenceId.HasValue) throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
				var route = await transportRouteRepository.FindAsync(input.SourceReferenceId.Value);
				var member = route == null ? null : await transportMemberRepository.FindAsync(route.MemberId);
				if (route == null || member?.StudentId != studentId) throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
				start = route.EffectiveFrom.Date > start ? route.EffectiveFrom.Date : start;
				end = route.EffectiveTo?.Date < end ? route.EffectiveTo.Value.Date : end;
				if ((input.StartDate.HasValue && input.StartDate.Value.Date != start) || (input.EndDate.HasValue && input.EndDate.Value.Date != end))
					throw new BusinessException(AssignmentErrorCodes.InvalidInput);
				verification = "Verified transport route assignment";
				revision = route.ConcurrencyStamp;
				break;
			case "ManualOverride":
				await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.OverrideChargePeriod);
				if (input.SourceReferenceId.HasValue || !input.StartDate.HasValue || !input.EndDate.HasValue || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 500)
					throw new BusinessException(AssignmentErrorCodes.InvalidInput);
				start = input.StartDate.Value.Date;
				end = input.EndDate.Value.Date;
				verification = "Authorized manual override";
				revision = null;
				break;
			default:
				// Admission/Hostel/Exit labels cannot turn caller-entered dates into verified facts.
				throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
		}
		if (start > end || start < year.StartDate.Date || end > year.EndDate.Date)
			throw new BusinessException(AssignmentErrorCodes.InvalidInput);
		return (new AssignmentServicePeriodDto { FeeHeadId = input.FeeHeadId, Source = input.Source,
			StartDate = DateTime.SpecifyKind(start, DateTimeKind.Utc), EndDate = DateTime.SpecifyKind(end, DateTimeKind.Utc), SourceReferenceId = input.SourceReferenceId, Reason = input.Reason?.Trim() }, verification, revision);
	}
}
