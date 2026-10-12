using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Mobile;

public record MobilePlacement(Student Student, AcademicYear Year, Guid StandardId, Guid DivisionId, string StandardName, string DivisionName, int? RollNumber, Guid MediumId, bool IsGuardian);

[Authorize]
[RemoteService(false)]
public class MobileAcademicContextAppService(
	StudentPortalAccessService access,
	IRepository<StudentEnrollment, Guid> enrollments,
	IRepository<AcademicYear, Guid> years,
	IRepository<Standard, Guid> standards,
	IRepository<Division, Guid> divisions,
	IRepository<AssignedSubject, Guid> assignedSubjects,
	IRepository<StudentElectiveSelection, Guid> electives,
	IDataFilter filter) : ApplicationService, IMobileAcademicContextAppService, ITransientDependency
{
	public static DateTime SchoolToday => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
		TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")).Date;

	public async Task<MobilePlacement> ResolveAsync(MobileAcademicInput input)
	{
		var student = await access.ResolveStudentAsync(input.StudentId);
		var available = await GetYearsForStudentAsync(student);
		var matchingYears = available.Where(x => input.AcademicYearId.HasValue ? x.Id == input.AcademicYearId : x.StartDate.Date <= SchoolToday && x.EndDate.Date >= SchoolToday).ToList();
		if (matchingYears.Count > 1) throw Invalid("The current academic year is ambiguous.");
		var year = matchingYears.SingleOrDefault();
		if (year == null) throw Invalid("Select an enrolled academic year.");
		List<StudentEnrollment> history;
		using (filter.Disable<ISoftDelete>())
			history = await enrollments.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id && x.AcademicYearId == year.Id);
		var matches = history.Where(x => !x.IsDeleted && (x.IsActive || year.EndDate.Date < SchoolToday)).ToList();
		if (matches.Count > 1) throw Invalid("The student enrollment is ambiguous.");
		var enrollment = matches.SingleOrDefault();
		var legacy = history.Count == 0 && student.AcademicYearId == year.Id &&
			student.Status is StudentLifeCycleStatusEnum.Admission or StudentLifeCycleStatusEnum.Enrolled;
		if (enrollment == null && !legacy) throw Invalid("No active enrollment is available.");
		var standardId = enrollment?.StandardId ?? (legacy ? student.StandardId : null);
		var divisionId = enrollment?.DivisionId ?? (legacy ? student.DivisionId : null);
		if (!standardId.HasValue || !divisionId.HasValue) throw Invalid("No class assignment is available.");
		var standard = await AsyncExecuter.FirstOrDefaultAsync((await standards.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && x.Id == standardId && x.EducationMediumStage.TenantId == CurrentTenant.Id &&
				x.EducationMediumStage.EducationMedium.TenantId == CurrentTenant.Id && x.EducationMediumStage.EducationMedium.EducationBoard.TenantId == CurrentTenant.Id)
			.Select(x => new { x.Name, MediumId = x.EducationMediumStage.EducationMediumId,
				YearId = x.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId }));
		var division = await divisions.FindAsync(x => x.TenantId == CurrentTenant.Id && x.Id == divisionId && x.StandardId == standardId);
		if (standard == null || division == null || standard.YearId != year.Id || legacy && standard.MediumId != student.AcademicMediumId)
			throw Invalid("The class assignment does not match the academic year and medium.");
		var guardian = (await access.GetMyStudentsAsync()).Any(x => x.StudentId == student.Id && x.AccountType == "guardian");
		return new(student, year, standardId.Value, divisionId.Value, standard.Name, division.Name, enrollment?.RollNumber ?? (legacy ? student.RollNo : null), standard.MediumId, guardian);
	}

	public async Task<MobileContextDto> GetAsync(MobileAcademicInput input)
	{
		var p = await ResolveAsync(input);
		return new(new(p.Student.Id, Name(p.Student), p.StandardId, p.StandardName, p.DivisionId, p.DivisionName, p.RollNumber),
			MapYear(p.Year), (await GetYearsForStudentAsync(p.Student)).Select(MapYear).ToList());
	}

	public async Task<List<AcademicYear>> GetYearsForStudentAsync(Student student)
	{
		var history = await enrollments.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id);
		var ids = history.Select(x => x.AcademicYearId).Distinct().ToList();
		if (student.AcademicYearId.HasValue && !history.Any(x => x.AcademicYearId == student.AcademicYearId))
		{
			bool revoked;
			using (filter.Disable<ISoftDelete>())
				revoked = await enrollments.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id && x.AcademicYearId == student.AcademicYearId);
			if (!revoked) ids.Add(student.AcademicYearId.Value);
		}
		var array = ids.ToArray();
		return (await years.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(array, x.Id)))
			.OrderByDescending(x => x.StartDate).ToList();
	}

	public async Task<List<AssignedSubject>> GetSubjectsAsync(MobilePlacement p)
	{
		var selections = (await electives.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id && x.IsLocked))
			.Select(x => x.SelectedSubjectId).ToArray();
		return await assignedSubjects.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StandardId == p.StandardId &&
			x.Status == SubjectStatus.Published && (!x.ElectiveGroupId.HasValue || Enumerable.Contains(selections, x.SubjectId)));
	}

	public static MobileYearDto MapYear(AcademicYear year) => new(year.Id, year.Name, DateOnly.FromDateTime(year.StartDate), DateOnly.FromDateTime(year.EndDate));
	public static string Name(Student student) => string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
	public static BusinessException Invalid(string reason) => new BusinessException("MobileAcademics:InvalidRequest").WithData("Reason", reason);
	public static MobilePage<T> Page<T>(IEnumerable<T> rows, MobileListInput input, MobilePlacement p)
	{
		if (input.PageNumber < 1 || input.PageSize is < 1 or > 100) throw Invalid("Supply a valid page and a page size between 1 and 100.");
		var list = rows.ToList();
		return new() { StudentId = p.Student.Id, AcademicYearId = p.Year.Id, Items = list.Skip((int)Math.Min((long)(input.PageNumber - 1) * input.PageSize, int.MaxValue)).Take(input.PageSize).ToList(),
			TotalCount = list.Count, PageNumber = input.PageNumber, PageSize = input.PageSize };
	}
}
