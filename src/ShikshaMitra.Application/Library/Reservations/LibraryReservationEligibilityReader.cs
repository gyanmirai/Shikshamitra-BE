using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Reservations;

public sealed record LibraryReservationBorrowerFacts(Guid PersonId, int Role, Guid? AcademicYearId, Guid? StageId,
	Guid? StandardId, LibraryReservationEligibility Eligibility, string? Reason, IReadOnlyList<Guid> MemberIds,
	Guid? BoardId = null, Guid? MediumId = null, Guid? DivisionId = null);

[RemoteService(false)]
public class LibraryReservationEligibilityReader(IRepository<Student, Guid> students, IRepository<Employee, Guid> employees,
	IRepository<LibraryMember, Guid> memberships, IRepository<AcademicYear, Guid> years,
	IRepository<Standard, Guid> standards) : ApplicationService, ITransientDependency
{
	public async Task<Dictionary<Guid, LibraryReservationBorrowerFacts>> ReadAsync(IEnumerable<Guid> memberIds)
	{
		var submitted = memberIds.Distinct().ToArray();
		if (submitted.Length == 0) return new();
		var aliases = await AsyncExecuter.ToListAsync((await memberships.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(submitted, row.Id))
			.Select(row => new { row.Id, row.StudentId }));
		var canonical = submitted.Concat(aliases.Select(row => row.StudentId)).Distinct().ToArray();
		var studentRows = await AsyncExecuter.ToListAsync((await students.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(canonical, row.Id))
			.Select(row => new { row.Id, row.AcademicYearId, row.AcademicBoardId, row.AcademicMediumId,
				row.AcademicMediumStageId, row.StandardId, row.DivisionId, row.Status,
				IsMember = row.AdditionalDetail != null && row.AdditionalDetail.IsLibraryMember == true }));
		var employeeRows = await AsyncExecuter.ToListAsync((await employees.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(canonical, row.Id))
			.Select(row => new { row.Id, Active = row.EmployeeStatusType == EmployeeStatusType.Active,
				IsMember = row.AdditionalDetails != null && row.AdditionalDetails.LibraryMember == true }));
		var studentIds = studentRows.Select(row => row.Id).ToArray();
		var memberRows = await AsyncExecuter.ToListAsync((await memberships.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(studentIds, row.StudentId))
			.Select(row => new { row.Id, row.StudentId, row.IsActive, row.CreationTime }));
		var validYears = (await AsyncExecuter.ToListAsync((await years.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id).Select(row => row.Id))).ToHashSet();
		var standardIds = studentRows.Where(row => row.StandardId.HasValue).Select(row => row.StandardId!.Value).ToArray();
		var stages = (await AsyncExecuter.ToListAsync((await standards.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(standardIds, row.Id))
			.Select(row => new { row.Id, row.EducationMediumStageId }))).ToDictionary(row => row.Id);
		var result = new Dictionary<Guid, LibraryReservationBorrowerFacts>();
		foreach (var student in studentRows)
		{
			var linked = memberRows.Where(row => row.StudentId == student.Id).OrderByDescending(row => row.CreationTime).ThenBy(row => row.Id).ToList();
			var latest = linked.FirstOrDefault();
			var conflicting = latest != null && linked.Where(row => row.CreationTime == latest.CreationTime).Select(row => row.IsActive).Distinct().Count() > 1;
			var invalidContext = !student.AcademicYearId.HasValue || !validYears.Contains(student.AcademicYearId.Value) ||
				student.StandardId.HasValue && (!stages.TryGetValue(student.StandardId.Value, out var standard) ||
				student.AcademicMediumStageId.HasValue && standard.EducationMediumStageId != student.AcademicMediumStageId);
			var inactive = !student.IsMember || latest?.IsActive == false || student.Status is StudentLifeCycleStatusEnum.Left or StudentLifeCycleStatusEnum.InActive;
			var eligibility = conflicting || invalidContext ? LibraryReservationEligibility.NeedsReview : inactive
				? LibraryReservationEligibility.PermanentlyIneligible : LibraryReservationEligibility.Eligible;
			var reason = conflicting ? "Membership records with the same timestamp disagree. Review the current membership." : invalidContext
				? "Review the borrower's academic context." : inactive ? "Library membership or student status is no longer active." : null;
			var ids = linked.Select(row => row.Id).Append(student.Id).Distinct().ToArray();
			var stageId = student.AcademicMediumStageId;
			if (!stageId.HasValue && student.StandardId.HasValue && stages.TryGetValue(student.StandardId.Value, out var currentClass))
				stageId = currentClass.EducationMediumStageId;
			var facts = new LibraryReservationBorrowerFacts(student.Id, 0, student.AcademicYearId, stageId,
				student.StandardId, eligibility, reason, ids, student.AcademicBoardId, student.AcademicMediumId, student.DivisionId);
			foreach (var id in ids) result[id] = facts;
		}
		foreach (var employee in employeeRows)
			result[employee.Id] = new(employee.Id, 1, null, null, null,
				employee.Active && employee.IsMember ? LibraryReservationEligibility.Eligible : LibraryReservationEligibility.PermanentlyIneligible,
				employee.Active && employee.IsMember ? null : "Library membership or staff employment is no longer active.", [employee.Id]);
		return result;
	}

	public async Task<LibraryReservationBorrowerFacts> RequireAsync(Guid memberId, Guid academicYearId)
	{
		var facts = (await ReadAsync([memberId])).GetValueOrDefault(memberId)
			?? throw new BusinessException(LibraryErrorCodes.MemberNotFound);
		if (facts.Eligibility != LibraryReservationEligibility.Eligible)
			throw new BusinessException(facts.Eligibility == LibraryReservationEligibility.NeedsReview
				? LibraryReservationErrorCodes.ReviewRequired : LibraryErrorCodes.MemberInactive, facts.Reason);
		if (facts.Role == 0 && facts.AcademicYearId != academicYearId ||
			!await years.AnyAsync(row => row.Id == academicYearId && row.TenantId == CurrentTenant.Id))
			throw new BusinessException(LibraryReservationErrorCodes.ReviewRequired, "Use the borrower's actual academic context.");
		return facts;
	}

	public async Task<System.Collections.Generic.HashSet<Guid>> GetAcademicYearIdsAsync() =>
		(await AsyncExecuter.ToListAsync((await years.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id).Select(row => row.Id))).ToHashSet();
}
