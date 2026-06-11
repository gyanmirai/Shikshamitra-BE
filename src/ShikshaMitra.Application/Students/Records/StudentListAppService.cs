using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using DocumentTypeEntity = ShikshaMitra.Students.StudentEnrollments.Settings.DocumentType;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentListAppService(
	IStudentRepository studentRepository,
	IRepository<StudentDocument, Guid> studentDocumentRepository,
	IRepository<DocumentTypeEntity, Guid> documentTypeRepository,
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
			.Where(s => s.DivisionId == divisionId && s.Status == StudentLifeCycleStatusEnum.Enrolled);

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
					InquiryNo = string.Empty,
					FormNo = null,
					StudentName = (s.FirstName + " " + (s.MiddleName ?? string.Empty) + " " + s.LastName).Trim(),
					FirstName = s.FirstName,
					MiddleName = s.MiddleName,
					LastName = s.LastName,
					AcademicYearId = s.AcademicYearId,
					AcademicBoardId = s.AcademicBoardId,
					AcademicMediumId = s.AcademicMediumId,
					StandardId = s.StandardId ?? Guid.Empty,
					Standard = s.Standard != null ? s.Standard.Name : string.Empty,
					DivisionId = s.DivisionId,
					Division = s.Division != null ? s.Division.Name : string.Empty,
					RollNo = s.RollNo,
					GrNo = s.GrNo,
					IsFeeStructureAssigned = s.FeeStructureId.HasValue,
					HasPhoto = !string.IsNullOrWhiteSpace(s.PhotoBlobName),
					PhotoUrl = !string.IsNullOrWhiteSpace(s.PhotoBlobName) ? $"/api/students/records/{s.Id}/photo" : null
				})
				.PageBy(skipCount, maxResultCount));

		await AttachDocumentStatusAsync(students);
		return new PagedResultDto<StudentDto>(totalCount, students);
	}

	public async Task<StudentPhotoUploadResultDto> UploadPhotoAsync(Guid studentId, UploadStudentPhotoDto input)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		await userScopeAccessService.EnsureCurrentUserCanAccessAsync(student, nameof(UploadPhotoAsync));

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
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		await userScopeAccessService.EnsureCurrentUserCanAccessAsync(student, nameof(GetPhotoAsync));

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

		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(s => s.CreationTime)
				.Take(30)
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
		return new StudentDashboardSummaryDto
		{
			TotalStudents = total,
			MaleStudents = male,
			FemaleStudents = female,
			RecentActivities = rows.Take(5).Select(s => new StudentDashboardActivityDto
			{
				Title = BuildStudentName(s.FirstName, s.MiddleName, s.LastName),
				Subtitle = BuildClassName(s.Standard, s.Division),
				Tag = "Enrolled",
				Date = s.CreationTime.ToString("dd MMM yyyy"),
				TimeAgo = "Recent"
			}).ToList(),
			TodaysBirthdays = rows
				.Where(s => s.DateOfBirth.HasValue && s.DateOfBirth.Value.Month == now.Month && s.DateOfBirth.Value.Day == now.Day)
				.Select(s => MapBirthday(s.FirstName, s.MiddleName, s.LastName, s.DateOfBirth, s.Standard, s.Division, null))
				.ToList(),
			UpcomingBirthdays = rows
				.Where(s => s.DateOfBirth.HasValue && IsUpcomingBirthday(s.DateOfBirth.Value, now))
				.Select(s => MapBirthday(s.FirstName, s.MiddleName, s.LastName, s.DateOfBirth, s.Standard, s.Division, s.DateOfBirth!.Value.ToString("dd MMM")))
				.ToList()
		};
	}

	public async Task<StudentAttendanceDashboardSummaryDto> GetAttendanceDashboardSummaryAsync()
	{
		var query = await GetScopedEnrolledStudentsQueryAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Select(s => new
		{
			s.StandardId,
			Standard = s.Standard != null ? s.Standard.Name : "Unassigned",
			s.DivisionId,
			Division = s.Division != null ? s.Division.Name : "Unassigned"
		}));

		return new StudentAttendanceDashboardSummaryDto
		{
			SchoolAttendancePercentage = 0,
			Classes = rows
				.GroupBy(s => new { s.StandardId, s.Standard })
				.OrderBy(g => g.Key.Standard)
				.Select(g => new StudentAttendanceClassSummaryDto
				{
					StandardId = g.Key.StandardId,
					Name = g.Key.Standard,
					Percentage = 0,
					Divisions = g
						.GroupBy(x => new { x.DivisionId, x.Division })
						.OrderBy(d => d.Key.Division)
						.Select(d => new StudentAttendanceDivisionSummaryDto
						{
							DivisionId = d.Key.DivisionId,
							Name = d.Key.Division,
							Percentage = 0
						})
						.ToList()
				})
				.ToList()
		};
	}

	public async Task ChangeStudentStatusAsync(Guid studentId, ChangeStudentStatusDto input)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
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

	private static bool IsUpcomingBirthday(DateTimeOffset dateOfBirth, DateTime now)
	{
		var birthday = new DateTime(now.Year, dateOfBirth.Month, dateOfBirth.Day);
		if (birthday < now.Date)
		{
			birthday = birthday.AddYears(1);
		}

		return birthday > now.Date && birthday <= now.Date.AddDays(30);
	}

	private static StudentBirthdayDto MapBirthday(
		string? firstName,
		string? middleName,
		string? lastName,
		DateTimeOffset? dateOfBirth,
		string standard,
		string division,
		string? dateLabel)
	{
		var today = DateTimeOffset.UtcNow;
		var age = dateOfBirth.HasValue ? today.Year - dateOfBirth.Value.Year : 0;
		if (dateOfBirth.HasValue && dateOfBirth.Value.Date > today.AddYears(-age).Date)
		{
			age--;
		}

		return new StudentBirthdayDto
		{
			Name = BuildStudentName(firstName, middleName, lastName),
			Age = Math.Max(age, 0),
			ClassName = BuildClassName(standard, division),
			DateLabel = dateLabel
		};
	}
}
