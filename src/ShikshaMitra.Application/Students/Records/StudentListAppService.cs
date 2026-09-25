using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using DocumentTypeEntity = ShikshaMitra.Students.StudentEnrollments.Settings.DocumentType;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentListAppService(
	IStudentRepository studentRepository,
	IRepository<AttendanceEntry, Guid> attendanceEntryRepository,
	IAttendanceDayRepository attendanceDayRepository,
	IRepository<StudentDocument, Guid> studentDocumentRepository,
	IRepository<DocumentTypeEntity, Guid> documentTypeRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<global::ShikshaMitra.TenantSettings.Standards.Standard, Guid> standardRepository,
	IRepository<global::ShikshaMitra.TenantSettings.Divisions.Division, Guid> divisionRepository,
	IBlobContainer blobContainer,
	IUserScopeAccessService userScopeAccessService,
	StudentScopeAccessService studentScopeAccessService
) : ApplicationService, IStudentListAppService
{
	public async Task<PagedResultDto<StudentDto>> GetStudentsByDivisionAsync(
		Guid divisionId,
		int skipCount = 0,
		int maxResultCount = 10,
		string? searchTerm = null)
	{
		await studentScopeAccessService.EnsureCanAccessDivisionAsync(divisionId, nameof(GetStudentsByDivisionAsync));

		var studentsQuery = (await studentRepository.GetQueryableWithoutAutoIncludesAsync())
			.Where(s => s.DivisionId == divisionId
				&& (s.Status == StudentLifeCycleStatusEnum.Enrolled
					|| s.Status == StudentLifeCycleStatusEnum.InActive
					|| s.Status == StudentLifeCycleStatusEnum.Left));

		studentsQuery = await studentScopeAccessService.ApplyCurrentUserScopeAsync(studentsQuery);

		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			studentsQuery = studentsQuery.Where(s =>
				(s.FirstName != null && s.FirstName.Contains(searchTerm)) ||
				(s.MiddleName != null && s.MiddleName.Contains(searchTerm)) ||
				(s.LastName != null && s.LastName.Contains(searchTerm)) ||
				(s.GrNo.HasValue && s.GrNo.Value.ToString().Contains(searchTerm)));
		}

		var totalCount = await AsyncExecuter.CountAsync(studentsQuery);
		var students = await AsyncExecuter.ToListAsync(
			studentsQuery
				.OrderByDescending(s => s.CreationTime)
				.Select(s => new StudentDto
				{
					StudentId = s.Id,
					InquiryNo = s.AdditionalDetail != null ? s.AdditionalDetail.InquiryNo ?? string.Empty : string.Empty,
					FormNo = s.AdditionalDetail != null ? s.AdditionalDetail.RegistrationFormNo : null,
					StudentName = (s.FirstName + " " + (s.MiddleName ?? string.Empty) + " " + s.LastName).Trim(),
					FirstName = s.FirstName,
					MiddleName = s.MiddleName,
					LastName = s.LastName,
					AcademicYearId = s.AcademicYearId,
					AcademicYearName = s.AcademicYear != null ? s.AcademicYear.Name : null,
					AcademicBoardId = s.AcademicBoardId,
					AcademicBoardName = s.AcademicBoard != null ? s.AcademicBoard.Type.ToString() : null,
					AcademicMediumId = s.AcademicMediumId,
					AcademicMediumName = s.AcademicMedium != null ? s.AcademicMedium.Name : null,
					StandardId = s.StandardId ?? Guid.Empty,
					Standard = s.Standard != null ? s.Standard.Name : string.Empty,
					DivisionId = s.DivisionId,
					Division = s.Division != null ? s.Division.Name : string.Empty,
					DateOfBirth = s.AdditionalDetail != null ? s.AdditionalDetail.DateOfBirth : null,
					DateOfInquiry = s.AdditionalDetail != null ? s.AdditionalDetail.DateOfInquiry : null,
					Gender = s.AdditionalDetail != null ? s.AdditionalDetail.Gender : null,
					RollNo = s.RollNo,
					GrNo = s.GrNo,
					Category = s.AdditionalDetail != null ? s.AdditionalDetail.Category : Category.General,
					IsLibraryMember = s.AdditionalDetail != null && s.AdditionalDetail.IsLibraryMember == true,
					IsTransportMember = s.AdditionalDetail != null && s.AdditionalDetail.IsTransportMember == true,
					Address1 = s.ContactDetail != null ? s.ContactDetail.Address1 : null,
					Address2 = s.ContactDetail != null ? s.ContactDetail.Address2 : null,
					Address3 = s.ContactDetail != null ? s.ContactDetail.Address3 : null,
					City = s.ContactDetail != null ? s.ContactDetail.City : null,
					State = s.ContactDetail != null ? s.ContactDetail.State : null,
					Country = s.ContactDetail != null ? s.ContactDetail.Country : null,
					PinCode = s.ContactDetail != null ? s.ContactDetail.Pincode : null,
					PrimaryContactNo = s.ContactDetail != null ? s.ContactDetail.PrimaryContactNo : null,
					SecondaryContactNo = s.ContactDetail != null ? s.ContactDetail.SecondaryContactNo : null,
					Email = s.ContactDetail != null ? s.ContactDetail.Email : null,
					PreviousSchool = s.AdditionalDetail != null ? s.AdditionalDetail.PreviousSchool : null,
					InquiryPreferenceId = s.AdditionalDetail != null ? s.AdditionalDetail.InquiryPreferenceId : null,
					InquiryPreference = s.AdditionalDetail != null && s.AdditionalDetail.InquiryPreference != null
						? s.AdditionalDetail.InquiryPreference.PreferenceName
						: null,
					IsFeeStructureAssigned = s.FeeStructureId.HasValue,
					StudentEnrollmentState = s.Status,
					Status = s.Status,
					OperationalStatus = s.Status == StudentLifeCycleStatusEnum.Left
						? StudentStatus.Left
						: s.Status == StudentLifeCycleStatusEnum.InActive
							? StudentStatus.Inactive
							: StudentStatus.Active,
					HasPhoto = !string.IsNullOrWhiteSpace(s.PhotoBlobName),
					PhotoUrl = !string.IsNullOrWhiteSpace(s.PhotoBlobName) ? $"/api/students/records/{s.Id}/photo" : null
				})
				.PageBy(skipCount, maxResultCount));

		foreach (var student in students)
		{
			student.Age = CalculateAge(student.DateOfBirth);
			student.Address = string.Join(", ", new[]
			{
				student.Address1,
				student.Address2,
				student.Address3,
				student.City,
				student.State?.Name,
				student.Country?.Name
			}.Where(x => !string.IsNullOrWhiteSpace(x)));
		}

		await AttachDocumentStatusAsync(students);
		return new PagedResultDto<StudentDto>(totalCount, students);
	}

	public async Task<StudentPhotoUploadResultDto> UploadPhotoAsync(Guid studentId, UploadStudentPhotoDto input)
	{
		var student = await studentScopeAccessService.GetStudentAsync(studentId, nameof(UploadPhotoAsync));

		FileValidationService.ValidateProfilePictureUpload(input.FileName, input.Content);

		if (!string.IsNullOrWhiteSpace(student.PhotoBlobName))
		{
			await blobContainer.DeleteAsync(student.PhotoBlobName);
		}

		var blobName = BuildPhotoBlobName(studentId, input.FileName);
		await blobContainer.SaveAsync(blobName, input.Content, overrideExisting: true);
		student.SetPhotoBlobName(blobName);
		await studentRepository.UpdateAsync(student);

		return new StudentPhotoUploadResultDto
		{
			StudentId = student.Id,
			HasPhoto = true,
			PhotoUrl = $"/api/students/records/{student.Id}/photo"
		};
	}

	public async Task<StudentPhotoDownloadDto?> GetPhotoAsync(Guid studentId)
	{
		var student = await studentScopeAccessService.GetStudentAsync(studentId, nameof(GetPhotoAsync));

		if (string.IsNullOrWhiteSpace(student.PhotoBlobName))
		{
			return null;
		}

		await using var blobStream = await blobContainer.GetAsync(student.PhotoBlobName);
		using var memoryStream = new MemoryStream();
		await blobStream.CopyToAsync(memoryStream);

		return new StudentPhotoDownloadDto
		{
			FileName = Path.GetFileName(student.PhotoBlobName),
			ContentType = GetContentType(student.PhotoBlobName),
			Content = memoryStream.ToArray()
		};
	}

	public async Task<StudentDashboardSummaryDto> GetDashboardSummaryAsync()
	{
		var query = await GetScopedEnrolledStudentsQueryAsync();
		var total = await AsyncExecuter.CountAsync(query);
		var male = await AsyncExecuter.CountAsync(query.Where(s =>
			s.AdditionalDetail != null && s.AdditionalDetail.Gender == GenderEnum.Male));
		var female = await AsyncExecuter.CountAsync(query.Where(s =>
			s.AdditionalDetail != null && s.AdditionalDetail.Gender == GenderEnum.Female));

		var recentRows = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(s => s.CreationTime)
				.Take(5)
				.Select(s => new
				{
					s.FirstName,
					s.MiddleName,
					s.LastName,
					s.CreationTime,
					Standard = s.Standard != null ? s.Standard.Name : string.Empty,
					Division = s.Division != null ? s.Division.Name : string.Empty,
					DateOfBirth = s.AdditionalDetail != null ? s.AdditionalDetail.DateOfBirth : null
				}));

		var now = Clock.Now;
		var birthdayRows = await AsyncExecuter.ToListAsync(
			query
				.Where(s => s.AdditionalDetail != null && s.AdditionalDetail.DateOfBirth.HasValue)
				.Select(s => new
				{
					s.FirstName,
					s.MiddleName,
					s.LastName,
					Standard = s.Standard != null ? s.Standard.Name : string.Empty,
					Division = s.Division != null ? s.Division.Name : string.Empty,
					DateOfBirth = s.AdditionalDetail!.DateOfBirth
				}));

		return new StudentDashboardSummaryDto
		{
			TotalStudents = total,
			MaleStudents = male,
			FemaleStudents = female,
			RecentActivities = recentRows.Select(s => new StudentDashboardActivityDto
			{
				Title = BuildStudentName(s.FirstName, s.MiddleName, s.LastName),
				Subtitle = BuildClassName(s.Standard, s.Division),
				Tag = "Enrolled",
				Date = s.CreationTime.ToString("dd MMM yyyy"),
				TimeAgo = "Recent"
			}).ToList(),
			TodaysBirthdays = birthdayRows
				.Where(s => s.DateOfBirth.HasValue && s.DateOfBirth.Value.Month == now.Month && s.DateOfBirth.Value.Day == now.Day)
				.Select(s => MapBirthday(s.FirstName, s.MiddleName, s.LastName, s.DateOfBirth, s.Standard, s.Division, null, now))
				.Take(30)
				.ToList(),
			UpcomingBirthdays = birthdayRows
				.Where(s => s.DateOfBirth.HasValue && IsUpcomingBirthday(s.DateOfBirth.Value, now))
				.OrderBy(s => GetNextBirthday(s.DateOfBirth!.Value, now))
				.Select(s => MapBirthday(s.FirstName, s.MiddleName, s.LastName, s.DateOfBirth, s.Standard, s.Division, s.DateOfBirth!.Value.ToString("dd MMM"), now))
				.Take(30)
				.ToList()
		};
	}

	public async Task<StudentAttendanceDashboardSummaryDto> GetAttendanceDashboardSummaryAsync(GetStudentAttendanceDashboardSummaryInput input)
	{
		var (startDate, endDate) = ResolveAttendanceDateRange(input.StartDate, input.EndDate);
		var query = await GetScopedEnrolledStudentsQueryAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Select(s => new
		{
			s.StandardId,
			Standard = s.Standard != null ? s.Standard.Name : "Unassigned",
			s.DivisionId,
			Division = s.Division != null ? s.Division.Name : "Unassigned"
		}));
		var attendanceRows = new List<StudentAttendanceSummaryRow>();
		if (rows.Count > 0)
		{
			var scopedAttendanceDays = await studentScopeAccessService.ApplyCurrentUserAttendanceDayScopeAsync(
				await attendanceDayRepository.GetQueryableAsync());
			var attendanceEntries = await attendanceEntryRepository.GetQueryableAsync();
			var projectedAttendanceRows = await AsyncExecuter.ToListAsync(
				from entry in attendanceEntries
				join day in scopedAttendanceDays on entry.AttendanceDayId equals day.Id
				join student in query on entry.StudentId equals student.Id
				where day.Date >= startDate
				      && day.Date <= endDate
				      && (entry.Status == AttendanceStatus.Present || entry.Status == AttendanceStatus.Absent)
				select new
				{
					entry.Status,
					StandardId = (Guid?)day.StandardId,
					DivisionId = (Guid?)day.DivisionId
				});
			attendanceRows = projectedAttendanceRows
				.Select(x => new StudentAttendanceSummaryRow(x.Status, x.StandardId, x.DivisionId))
				.ToList();
		}

		var groupRows = rows
			.Select(s => new StudentAttendanceGroupRow(s.StandardId, s.DivisionId))
			.Concat(attendanceRows.Select(s => new StudentAttendanceGroupRow(s.StandardId, s.DivisionId)))
			.Distinct()
			.ToList();
		var standardIds = groupRows
			.Where(x => x.StandardId.HasValue)
			.Select(x => x.StandardId!.Value)
			.Distinct()
			.ToList();
		var divisionIds = groupRows
			.Where(x => x.DivisionId.HasValue)
			.Select(x => x.DivisionId!.Value)
			.Distinct()
			.ToList();
		var standardNames = rows
			.Where(x => x.StandardId.HasValue)
			.GroupBy(x => x.StandardId!.Value)
			.ToDictionary(x => x.Key, x => x.First().Standard);
		var divisionNames = rows
			.Where(x => x.DivisionId.HasValue)
			.GroupBy(x => x.DivisionId!.Value)
			.ToDictionary(x => x.Key, x => x.First().Division);

		if (standardIds.Count > 0)
		{
			var standards = await AsyncExecuter.ToListAsync(
				(await standardRepository.GetQueryableAsync())
					.Where(x => standardIds.Contains(x.Id))
					.Select(x => new { x.Id, x.Name }));
			foreach (var standard in standards)
			{
				standardNames[standard.Id] = standard.Name;
			}
		}

		if (divisionIds.Count > 0)
		{
			var divisions = await AsyncExecuter.ToListAsync(
				(await divisionRepository.GetQueryableAsync())
					.Where(x => divisionIds.Contains(x.Id))
					.Select(x => new { x.Id, x.Name }));
			foreach (var division in divisions)
			{
				divisionNames[division.Id] = division.Name;
			}
		}

		return new StudentAttendanceDashboardSummaryDto
		{
			SchoolAttendancePercentage = CalculateAttendancePercentage(attendanceRows.Select(x => x.Status)),
			Classes = groupRows
				.GroupBy(s => s.StandardId)
				.OrderBy(g => GetStandardName(g.Key, standardNames))
				.Select(g => new StudentAttendanceClassSummaryDto
				{
					StandardId = g.Key,
					Name = GetStandardName(g.Key, standardNames),
					Percentage = CalculateAttendancePercentage(attendanceRows
						.Where(x => x.StandardId == g.Key)
						.Select(x => x.Status)),
					Divisions = g
						.GroupBy(x => x.DivisionId)
						.OrderBy(d => GetDivisionName(d.Key, divisionNames))
						.Select(d => new StudentAttendanceDivisionSummaryDto
						{
							DivisionId = d.Key,
							Name = GetDivisionName(d.Key, divisionNames),
							Percentage = CalculateAttendancePercentage(attendanceRows
								.Where(x => x.StandardId == g.Key && x.DivisionId == d.Key)
								.Select(x => x.Status))
						})
						.ToList()
				})
				.ToList()
		};
	}

	public async Task ChangeStudentStatusAsync(Guid studentId, ChangeStudentStatusDto input)
	{
		var studentQuery = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var student = await AsyncExecuter.FirstOrDefaultAsync(studentQuery.Where(s => s.Id == studentId));
		if (student == null)
		{
			throw new EntityNotFoundException(typeof(Student), studentId);
		}

		await userScopeAccessService.EnsureCurrentUserCanAccessAsync(student, nameof(ChangeStudentStatusAsync));

		try
		{
			student.ChangeOperationalStatus(input.Status, input.Remarks);
		}
		catch (InvalidOperationException ex)
		{
			throw new UserFriendlyException(ex.Message);
		}

		await studentRepository.UpdateAsync(student);
	}

	public async Task<List<StudentStatusHistoryDto>> GetStatusHistoryAsync(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		await userScopeAccessService.EnsureCurrentUserCanAccessAsync(student, nameof(GetStatusHistoryAsync));

		return student.StatusHistories
			.OrderByDescending(h => h.CreationTime)
			.Select(MapToDto)
			.ToList();
	}

	public async Task<List<StudentLookupDto>> GetStudentsByScopeAsync(GetStudentsByScopeRequest input)
	{
		var classIdSet = input.ClassIds.Distinct().ToList();

		// Division selection is only allowed/applied when exactly one standard is selected.
		var divisionIdSet = classIdSet.Count == 1 && input.DivisionIds != null
			? input.DivisionIds.Distinct().ToList()
			: null;

		// 1. Verify that the logged-in user has access to the requested Academic Year and Medium
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(input.AcademicYearId, nameof(GetStudentsByScopeAsync));
		await studentScopeAccessService.EnsureCanAccessMediumAsync(input.EducationMediumId, nameof(GetStudentsByScopeAsync));

		// 2. Verify that the logged-in user has access to all requested Classes/Standards
		foreach (var classId in classIdSet)
		{
			await studentScopeAccessService.EnsureCanAccessStandardAsync(classId, nameof(GetStudentsByScopeAsync));
		}

		// 3. Verify that the logged-in user has access to all requested Divisions (if applicable)
		if (divisionIdSet is { Count: > 0 })
		{
			foreach (var divisionId in divisionIdSet)
			{
				await studentScopeAccessService.EnsureCanAccessDivisionAsync(divisionId, nameof(GetStudentsByScopeAsync));
			}
		}

		var query = (await studentRepository.GetQueryableWithoutAutoIncludesAsync())
			.Where(s =>
				s.AcademicYearId == input.AcademicYearId
				&& s.AcademicBoardId == input.BoardId
				&& s.AcademicMediumId == input.EducationMediumId
				&& s.StandardId != null
				&& classIdSet.Contains(s.StandardId.Value)
				&& s.Status == StudentLifeCycleStatusEnum.Enrolled);

		if (divisionIdSet is { Count: > 0 })
		{
			query = query.Where(s => s.DivisionId != null && divisionIdSet.Contains(s.DivisionId.Value));
		}

		if (input.OnlyWithFeeStructureAssignment)
		{
			var assignments = await studentFeeStructureRepository.GetQueryableAsync();
			query = query.Where(student => assignments.Any(assignment =>
				assignment.StudentId == student.Id
				&& assignment.AcademicYearId == input.AcademicYearId));
		}

		// 4. Enforce row-level query filtering based on user scope
		query = await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);

		return await AsyncExecuter.ToListAsync(
			query
				.OrderBy(s => s.RollNo)
				.ThenBy(s => s.FirstName)
				.Select(s => new StudentLookupDto
				{
					StudentId = s.Id,
					StudentName = (s.FirstName + " " + (s.MiddleName ?? string.Empty) + " " + s.LastName).Trim(),
					GrNo = s.GrNo,
					RollNo = s.RollNo,
					StandardId = s.StandardId,
					StandardName = s.Standard != null ? s.Standard.Name : string.Empty,
					DivisionId = s.DivisionId,
					DivisionName = s.Division != null ? s.Division.Name : string.Empty
				}));
	}

	private async Task<IQueryable<Student>> GetScopedEnrolledStudentsQueryAsync()
	{
		var query = (await studentRepository.GetQueryableWithoutAutoIncludesAsync())
			.Where(s => s.Status == StudentLifeCycleStatusEnum.Enrolled);

		return await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);
	}

	private async Task AttachDocumentStatusAsync(IReadOnlyCollection<StudentDto> students)
	{
		if (students.Count == 0)
		{
			return;
		}

		var studentIds = students.Select(s => s.StudentId).ToList();
		var requiredCount = await AsyncExecuter.CountAsync(
			(await documentTypeRepository.GetQueryableAsync()).Where(d => d.IsRequired));

		var uploadedCounts = await AsyncExecuter.ToListAsync(
			(await studentDocumentRepository.GetQueryableAsync())
				.Where(d => studentIds.Contains(d.StudentId))
				.GroupBy(d => d.StudentId)
				.Select(g => new { StudentId = g.Key, Count = g.Count() }));

		var uploadedByStudent = uploadedCounts.ToDictionary(x => x.StudentId, x => x.Count);

		foreach (var student in students)
		{
			student.RequiredDocumentsCount = requiredCount;
			student.UploadedDocumentsCount = uploadedByStudent.GetValueOrDefault(student.StudentId);
		}
	}

	private string BuildPhotoBlobName(Guid studentId, string fileName)
	{
		var tenantId = CurrentTenant.Id?.ToString("N") ?? "host";
		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		return $"student-photos/{tenantId}/{studentId:N}/{GuidGenerator.Create():N}{extension}";
	}

	private static string GetContentType(string blobName)
	{
		var extension = Path.GetExtension(blobName).ToLowerInvariant();
		return extension switch
		{
			".jpg" or ".jpeg" => "image/jpeg",
			".png" => "image/png",
			".gif" => "image/gif",
			".webp" => "image/webp",
			_ => "application/octet-stream"
		};
	}

	private static StudentStatusHistoryDto MapToDto(StudentStatusHistory h) => new()
	{
		Id = h.Id,
		StudentId = h.StudentId,
		FromStatus = h.FromStatus,
		ToStatus = h.ToStatus,
		OperationalStatus = h.ToStatus switch
		{
			StudentLifeCycleStatusEnum.Enrolled => StudentStatus.Active,
			StudentLifeCycleStatusEnum.InActive => StudentStatus.Inactive,
			_ => StudentStatus.Left,
		},
		Remarks = h.Remarks,
		CreationTime = h.CreationTime,
		CreatorId = h.CreatorId,
	};

	private static string BuildStudentName(string? firstName, string? middleName, string? lastName) =>
		string.Join(" ", new[] { firstName, middleName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

	private static string BuildClassName(string standard, string division) =>
		string.IsNullOrWhiteSpace(division) ? standard : $"{standard} - {division}";

	private static string? CalculateAge(DateTimeOffset? dateOfBirth)
	{
		if (!dateOfBirth.HasValue)
		{
			return null;
		}

		var today = DateTime.UtcNow.Date;
		var age = today.Year - dateOfBirth.Value.Year;
		var birthdayThisYear = GetBirthdayInYear(dateOfBirth.Value, today.Year);
		if (birthdayThisYear > today)
		{
			age--;
		}

		return Math.Max(age, 0).ToString();
	}

	private static string GetStandardName(Guid? standardId, IReadOnlyDictionary<Guid, string> standardNames)
	{
		return standardId.HasValue && standardNames.TryGetValue(standardId.Value, out var name)
			? name
			: "Unassigned";
	}

	private static string GetDivisionName(Guid? divisionId, IReadOnlyDictionary<Guid, string> divisionNames)
	{
		return divisionId.HasValue && divisionNames.TryGetValue(divisionId.Value, out var name)
			? name
			: "Unassigned";
	}

	private static bool IsUpcomingBirthday(DateTimeOffset dateOfBirth, DateTime now)
	{
		var birthday = GetNextBirthday(dateOfBirth, now);

		return birthday > now.Date && birthday <= now.Date.AddDays(30);
	}

	private static DateTime GetNextBirthday(DateTimeOffset dateOfBirth, DateTime now)
	{
		var birthday = GetBirthdayInYear(dateOfBirth, now.Year);
		return birthday < now.Date ? GetBirthdayInYear(dateOfBirth, now.Year + 1) : birthday;
	}

	private static DateTime GetBirthdayInYear(DateTimeOffset dateOfBirth, int year)
	{
		var day = Math.Min(dateOfBirth.Day, DateTime.DaysInMonth(year, dateOfBirth.Month));
		return new DateTime(year, dateOfBirth.Month, day);
	}

	private static decimal CalculateAttendancePercentage(IEnumerable<AttendanceStatus> statuses)
	{
		var marked = statuses.Count(status => status is AttendanceStatus.Present or AttendanceStatus.Absent);
		if (marked == 0)
		{
			return 0;
		}

		var present = statuses.Count(status => status == AttendanceStatus.Present);
		return Math.Round(present * 100m / marked, 2);
	}

	private (DateTime StartDate, DateTime EndDate) ResolveAttendanceDateRange(DateTime? startDate, DateTime? endDate)
	{
		var now = Clock.Now.Date;
		var defaultStart = new DateTime(now.Year, now.Month, 1);
		var defaultEnd = defaultStart.AddMonths(1).AddDays(-1);
		var start = (startDate ?? defaultStart).Date;
		var end = (endDate ?? defaultEnd).Date;

		if (start > end)
		{
			throw new BusinessException("StudentAttendance:InvalidDateRange")
				.WithData("StartDate", start)
				.WithData("EndDate", end);
		}

		return (start, end);
	}

	private static StudentBirthdayDto MapBirthday(
		string? firstName,
		string? middleName,
		string? lastName,
		DateTimeOffset? dateOfBirth,
		string standard,
		string division,
		string? dateLabel,
		DateTime now)
	{
		var age = dateOfBirth.HasValue ? now.Year - dateOfBirth.Value.Year : 0;
		if (dateOfBirth.HasValue)
		{
			var birthdayThisYear = GetBirthdayInYear(dateOfBirth.Value, now.Year);
			if (birthdayThisYear > now.Date)
			{
				age--;
			}
		}

		return new StudentBirthdayDto
		{
			Name = BuildStudentName(firstName, middleName, lastName),
			Age = Math.Max(age, 0),
			ClassName = BuildClassName(standard, division),
			DateLabel = dateLabel
		};
	}

	private sealed record StudentAttendanceSummaryRow(
		AttendanceStatus Status,
		Guid? StandardId,
		Guid? DivisionId);

	private sealed record StudentAttendanceGroupRow(
		Guid? StandardId,
		Guid? DivisionId);
}
