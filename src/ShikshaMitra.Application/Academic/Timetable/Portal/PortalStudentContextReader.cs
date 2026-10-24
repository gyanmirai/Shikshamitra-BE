using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.Portal;

/// <summary>Internal reader: callers must resolve portal ownership before supplying IDs.</summary>
[RemoteService(false)]
public class PortalStudentContextReader(
	IRepository<Student, Guid> students, IDataFilter dataFilter,
	IRepository<StudentEnrollment, Guid> enrollments, IRepository<AcademicYear, Guid> years,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions,
	ITimetableRepository timetables, ILectureSlotConfigRepository configs) : ApplicationService
{
	protected internal virtual async Task<Dictionary<Guid, PortalStudentContextResult>> ReadAsync(Guid[] authorizedIds, Guid? academicYearId = null)
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		if (authorizedIds.Length == 0) return [];
		var people = await AsyncExecuter.ToListAsync((await students.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(authorizedIds, x.Id) && x.Status != StudentLifeCycleStatusEnum.Left)
			.Select(x => new { x.Id, x.FirstName, x.MiddleName, x.LastName, Gender = x.AdditionalDetail == null ? (GenderEnum?)null : x.AdditionalDetail.Gender,
				x.AcademicYearId, x.StandardId, x.DivisionId, x.RollNo, x.Status, x.AcademicMediumId }));
		if (people.Count != authorizedIds.Distinct().Count()) throw new AbpAuthorizationException();
		var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(Clock.Now, DateTimeKind.Utc),
			TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")).Date;
		var currentYears = await years.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StartDate <= today && x.EndDate >= today &&
			(!academicYearId.HasValue || x.Id == academicYearId));
		var yearIds = currentYears.Select(x => x.Id).ToArray();
		if (yearIds.Length == 0)
			return people.ToDictionary(x => x.Id, x => academicYearId.HasValue
				? new PortalStudentContextResult(null, Invalid("Historical timetables are not versioned; only the current academic year is supported."))
				: new PortalStudentContextResult(new PortalTimetableContextDto { Availability = "noAcademicYear", Student = new() {
					Id = x.Id, Gender = x.Gender, Name = string.Join(" ", new[] { x.FirstName, x.MiddleName, x.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))) } }, null));
		List<EnrollmentSnapshot> history;
		using (dataFilter.Disable<ISoftDelete>())
			history = await AsyncExecuter.ToListAsync((await enrollments.GetQueryableAsync())
				.Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(authorizedIds, x.StudentId) && Enumerable.Contains(yearIds, x.AcademicYearId))
				.Select(x => new EnrollmentSnapshot(x.StudentId, x.AcademicYearId, x.StandardId, x.DivisionId, x.RollNumber, x.IsActive, x.IsDeleted)));
		var active = history.Where(x => x.IsActive && !x.IsDeleted).ToList();
		var legacyPeople = people.Where(x => !history.Any(enrollment => enrollment.StudentId == x.Id) &&
			x.Status is StudentLifeCycleStatusEnum.Admission or StudentLifeCycleStatusEnum.Enrolled &&
			x.AcademicYearId.HasValue && yearIds.Contains(x.AcademicYearId.Value)).ToList();
		var standardIds = active.Where(x => x.StandardId.HasValue).Select(x => x.StandardId!.Value)
			.Concat(legacyPeople.Where(x => x.StandardId.HasValue).Select(x => x.StandardId!.Value)).Distinct().ToArray();
		var divisionIds = active.Where(x => x.DivisionId.HasValue).Select(x => x.DivisionId!.Value)
			.Concat(legacyPeople.Where(x => x.DivisionId.HasValue).Select(x => x.DivisionId!.Value)).Distinct().ToArray();
		var classes = standardIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync((await standards.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(standardIds, x.Id))
			.Select(x => new { x.Id, x.Name, MediumId = x.EducationMediumStage.EducationMediumId,
				MediumName = x.EducationMediumStage.EducationMedium.Name, StageId = x.EducationMediumStageId,
				StageName = x.EducationMediumStage.Name, YearId = x.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId }));
		var sections = divisionIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync((await divisions.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(divisionIds, x.Id)).Select(x => new { x.Id, x.StandardId, x.Name }));
		// Read working-day groups only; do not include periods or student graphs.
		var settings = standardIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync((await configs.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(standardIds, x.StandardId))
			.Select(x => new { x.StandardId, Groups = x.SlotGroups.ToList() }));
		var schedules = standardIds.Length == 0 || divisionIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync((await timetables.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(standardIds, x.StandardId) && Enumerable.Contains(divisionIds, x.DivisionId))
			.Select(x => new { x.StandardId, x.DivisionId, x.Status, Days = x.Slots.Select(slot => (int)slot.Day).ToList() }));
		var result = new Dictionary<Guid, PortalStudentContextResult>();
		foreach (var student in people)
		{
			var context = new PortalTimetableContextDto { Student = new() { Id = student.Id, Gender = student.Gender,
				Name = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))) } };
			try
			{
				if (currentYears.Count == 0)
				{
					if (academicYearId.HasValue) throw Invalid("Historical timetables are not versioned; only the current academic year is supported.");
					context.Availability = "noAcademicYear";
				}
				else
				{
					var studentHistory = history.Where(x => x.StudentId == student.Id).ToList();
					var matches = studentHistory.Where(x => x.IsActive && !x.IsDeleted).ToList();
					var legacy = matches.Count == 0 && studentHistory.Count == 0 &&
						student.Status is StudentLifeCycleStatusEnum.Admission or StudentLifeCycleStatusEnum.Enrolled &&
						student.AcademicYearId.HasValue && yearIds.Contains(student.AcademicYearId.Value);
					if (matches.Count > 1) throw Invalid("The student's current enrollment is ambiguous.");
					if (matches.Count == 1 || legacy)
					{
						var enrollment = matches.SingleOrDefault();
						var yearId = enrollment?.AcademicYearId ?? student.AcademicYearId!.Value;
						var standardId = enrollment?.StandardId ?? (legacy ? student.StandardId : null);
						var divisionId = enrollment?.DivisionId ?? (legacy ? student.DivisionId : null);
						var year = currentYears.Single(x => x.Id == yearId);
						context.CurrentAcademicYear = new() { Id = year.Id, Name = year.Name, StartDate = DateOnly.FromDateTime(year.StartDate), EndDate = DateOnly.FromDateTime(year.EndDate) };
						context.AvailableAcademicYears.Add(context.CurrentAcademicYear);
						context.Student.RollNumber = enrollment?.RollNumber ?? (legacy ? student.RollNo : null);
						if (!standardId.HasValue || !divisionId.HasValue) context.Availability = "noClassAssignment";
						else
						{
							var standard = classes.SingleOrDefault(x => x.Id == standardId);
							var division = sections.SingleOrDefault(x => x.Id == divisionId && x.StandardId == standardId);
							if (standard == null || division == null || standard.YearId != year.Id || standard.MediumId != student.AcademicMediumId)
								throw Invalid("The student's class assignment does not match the enrolled academic year.");
							context.Student.StandardId = standard.Id; context.Student.StandardName = standard.Name;
							context.Student.MediumId = standard.MediumId; context.Student.MediumName = standard.MediumName;
							context.Student.StageId = standard.StageId; context.Student.StageName = standard.StageName;
							context.Student.DivisionId = division.Id; context.Student.DivisionName = division.Name;
							var config = settings.SingleOrDefault(x => x.StandardId == standard.Id);
							var timetable = schedules.SingleOrDefault(x => x.StandardId == standard.Id && x.DivisionId == division.Id);
							var dayIndexes = config?.Groups.SelectMany(x => x.ActiveDays).Distinct().Order().ToList() ?? [];
							if (dayIndexes.Count == 0 && timetable?.Status == TimetableStatus.Published)
								dayIndexes = timetable.Days.Distinct().Order().ToList();
							context.WorkingDays = dayIndexes.Where(x => Enum.IsDefined((TimetableDayOfWeek)x))
								.Select(x => ((TimetableDayOfWeek)x).ToString().ToLowerInvariant()).ToList();
							context.Availability = timetable?.Status == TimetableStatus.Published ? "available" : "noPublishedTimetable";
						}
					}
				}
				result[student.Id] = new(context, null);
			}
			catch (BusinessException error) when (error.Code == "Academic:PortalTimetable:InvalidContext")
			{
				result[student.Id] = new(null, error);
			}
		}
		return result;
	}

	private static BusinessException Invalid(string reason) => new BusinessException("Academic:PortalTimetable:InvalidContext").WithData("Reason", reason);

	private sealed record EnrollmentSnapshot(Guid StudentId, Guid AcademicYearId, Guid? StandardId, Guid? DivisionId, int? RollNumber, bool IsActive, bool IsDeleted);
}

public record PortalStudentContextResult(PortalTimetableContextDto? Context, BusinessException? Error);
