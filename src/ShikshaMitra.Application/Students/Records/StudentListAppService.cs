using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
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
	IRepository<StudentEnrollment, Guid> enrollmentRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	AttendanceReportService attendanceReports,
	IRepository<StudentDocument, Guid> studentDocumentRepository,
	IRepository<DocumentTypeEntity, Guid> documentTypeRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<Charge, Guid> chargeRepository,
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
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
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
					GrNumber = s.GrNumber,
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
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add);
		var student = await studentScopeAccessService.GetStudentAsync(studentId, nameof(UploadPhotoAsync));
		if (student.Status is StudentLifeCycleStatusEnum.Enrolled or StudentLifeCycleStatusEnum.InActive or StudentLifeCycleStatusEnum.Left)
			await LazyServiceProvider.LazyGetRequiredService<StudentRecordWriteGuard>().EnsureAsync(studentId, nameof(UploadPhotoAsync));

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
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		var student = await studentScopeAccessService.GetStudentAsync(studentId, nameof(GetPhotoAsync));
		if (student.Status is StudentLifeCycleStatusEnum.Enrolled or StudentLifeCycleStatusEnum.InActive or StudentLifeCycleStatusEnum.Left)
			await LazyServiceProvider.LazyGetRequiredService<StudentRecordReadGuard>().EnsureAsync(studentId, "photo");

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
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
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
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		if (!CurrentTenant.Id.HasValue) return new();
		var (startDate, endDate) = ResolveAttendanceDateRange(input.StartDate, input.EndDate);
		if ((endDate - startDate).TotalDays > 1096) throw new BusinessException(AttendanceOperationErrorCodes.InvalidDateRange);
		if (input.AcademicYearId.HasValue)
			await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(input.AcademicYearId.Value, "attendance dashboard");
		var years = await AsyncExecuter.ToListAsync((await academicYearRepository.GetQueryableAsync()).Where(y =>
			y.TenantId == CurrentTenant.Id && (!input.AcademicYearId.HasValue || y.Id == input.AcademicYearId) &&
			y.StartDate <= endDate && y.EndDate >= startDate));
		var slices = new List<AttendanceDashboardSlice>();
		var partialEnrollments = new HashSet<Guid>();
		var scopedDays = await studentScopeAccessService.ApplyCurrentUserAttendanceDayScopeAsync(await attendanceDayRepository.GetQueryableAsync());
		foreach (var year in years)
		{
			var start = startDate > year.StartDate.Date ? startDate : year.StartDate.Date;
			var end = endDate < year.EndDate.Date ? endDate : year.EndDate.Date;
			var members = await AsyncExecuter.ToListAsync((await enrollmentRepository.GetQueryableAsync()).Where(e =>
				e.TenantId == CurrentTenant.Id && e.AcademicYearId == year.Id));
			var paths = new Dictionary<(Guid Standard, Guid Division), HashSet<Guid>>();
			void Add(Guid? standard, Guid? division, Guid enrollmentId)
			{
				if (!standard.HasValue || !division.HasValue) return;
				var path = (standard.Value, division.Value);
				if (!paths.TryGetValue(path, out var cohort)) paths[path] = cohort = [];
				cohort.Add(enrollmentId);
			}
			foreach (var member in members)
			{
				Add(member.StandardId, member.DivisionId, member.Id);
				for (var date = start; date <= end; date = date.AddDays(1))
				{
					var placement = member.AttendancePlacementOn(date);
					Add(placement?.StandardId, placement?.DivisionId, member.Id);
				}
			}
			// Submitted day scopes retain the original class even after a later move or exit.
			var historic = await AsyncExecuter.ToListAsync(from entry in await attendanceEntryRepository.GetQueryableAsync()
				join day in scopedDays on entry.AttendanceDayId equals day.Id
				where day.TenantId == CurrentTenant.Id && day.AcademicYearId == year.Id && day.Date >= start && day.Date <= end
				select new { entry.EnrollmentId, entry.StudentId, day.StandardId, day.DivisionId });
			foreach (var item in historic)
			{
				var member = members.SingleOrDefault(e => item.EnrollmentId.HasValue ? e.Id == item.EnrollmentId : e.StudentId == item.StudentId);
				if (member != null) Add(item.StandardId, item.DivisionId, member.Id);
			}
			foreach (var path in paths)
			{
				try
				{
					var rows = await attendanceReports.BuildAsync(year.Id, path.Key.Standard, path.Key.Division, start, end,
						members.Where(e => path.Value.Contains(e.Id)).ToList(), includeDaily: true);
					slices.Add(new(path.Key.Standard, path.Key.Division, rows));
				}
				catch (BusinessException exception) when (exception.Code == UserScopeErrorCodes.AccessDenied)
				{
					// An enrollment's current path does not authorize its other historical paths.
					partialEnrollments.UnionWith(path.Value);
				}
			}
		}
		var standardIds = slices.Select(s => s.StandardId).Distinct().ToArray();
		var divisionIds = slices.Select(s => s.DivisionId).Distinct().ToArray();
		var standardNames = (await AsyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync())
			.Where(s => s.TenantId == CurrentTenant.Id && Enumerable.Contains(standardIds, s.Id)).Select(s => new { s.Id, s.Name })))
			.ToDictionary(s => s.Id, s => s.Name);
		var divisionNames = (await AsyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync())
			.Where(d => d.TenantId == CurrentTenant.Id && Enumerable.Contains(divisionIds, d.Id)).Select(d => new { d.Id, d.Name })))
			.ToDictionary(d => d.Id, d => d.Name);
		foreach (var row in slices.SelectMany(s => s.Rows)) row.IsProvisional |= partialEnrollments.Contains(row.EnrollmentId);
		var result = new StudentAttendanceDashboardSummaryDto();
		result.SchoolAttendancePercentage = ApplyDashboardMetrics(result, slices.SelectMany(s => s.Rows));
		result.Classes = slices.GroupBy(s => s.StandardId).OrderBy(g => GetStandardName(g.Key, standardNames)).Select(group =>
		{
			var item = new StudentAttendanceClassSummaryDto { StandardId = group.Key, Name = GetStandardName(group.Key, standardNames) };
			item.Percentage = ApplyDashboardMetrics(item, group.SelectMany(s => s.Rows));
			item.Divisions = group.GroupBy(s => s.DivisionId).OrderBy(g => GetDivisionName(g.Key, divisionNames)).Select(division =>
			{
				var row = new StudentAttendanceDivisionSummaryDto { DivisionId = division.Key, Name = GetDivisionName(division.Key, divisionNames) };
				row.Percentage = ApplyDashboardMetrics(row, division.SelectMany(s => s.Rows));
				return row;
			}).ToList();
			return item;
		}).ToList();
		return result;
	}

	private static decimal? ApplyDashboardMetrics(StudentAttendanceDashboardMetricsDto target, IEnumerable<AttendanceSummaryDto> rows)
	{
		// Different students on the same date are different student-days; merge moved paths within each enrollment.
		foreach (var group in rows.GroupBy(r => r.EnrollmentId))
		{
			var result = AttendanceCalculation.Calculate(group.SelectMany(r => r.Daily).Select(d => new AttendanceCalculationDay(
				d.Date, d.Expected, d.Recorded, d.PresentCredit, d.DenominatorCredit, d.IsProvisional, d.Status)));
			target.ExpectedDays += result.ExpectedDays;
			target.RecordedDays += result.RecordedDays;
			target.MissingDays += result.MissingDays;
			target.PresentCredit += result.PresentCredit;
			target.DenominatorDays += result.DenominatorDays;
			target.IsProvisional |= result.IsProvisional || group.Any(row => row.IsProvisional);
		}
		target.CompletionPercentage = target.ExpectedDays == 0 ? null : Math.Round(target.RecordedDays * 100m / target.ExpectedDays, 2);
		return target.DenominatorDays == 0 ? null : (decimal)Math.Round(target.PresentCredit * 100 / target.DenominatorDays, 2);
	}

	private sealed record AttendanceDashboardSlice(Guid StandardId, Guid DivisionId, List<AttendanceSummaryDto> Rows);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add)]
	public async Task ChangeStudentStatusAsync(Guid studentId, ChangeStudentStatusDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add);
		var studentQuery = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var student = await AsyncExecuter.FirstOrDefaultAsync(studentQuery.Where(s => s.Id == studentId));
		if (student == null)
		{
			throw new EntityNotFoundException(typeof(Student), studentId);
		}

		await studentScopeAccessService.EnsureCanAccessStudentAsync(student, nameof(ChangeStudentStatusAsync));
		await LazyServiceProvider.LazyGetRequiredService<StudentRecordWriteGuard>()
			.EnsureAsync(studentId, nameof(ChangeStudentStatusAsync));

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

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
	public async Task<List<StudentStatusHistoryDto>> GetStatusHistoryAsync(Guid studentId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		await studentScopeAccessService.EnsureCanAccessStudentAsync(student, nameof(GetStatusHistoryAsync));

		return student.StatusHistories
			.OrderByDescending(h => h.CreationTime)
			.Select(MapToDto)
			.ToList();
	}

	public async Task<List<StudentLookupDto>> GetStudentsByScopeAsync(GetStudentsByScopeRequest input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		if ((input.ChargeCategoryId.HasValue || input.AlreadyCharged.HasValue)
			&& !await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View)
			&& !await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)
			&& !await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update))
		{
			throw new AbpAuthorizationException("Charge permission is required to view students' charge status.");
		}

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

		if (input.RollNoFrom.HasValue && input.RollNoTo.HasValue && input.RollNoFrom > input.RollNoTo)
			throw new UserFriendlyException("Roll No. from must not exceed Roll No. to.");
		if (input.AlreadyCharged.HasValue && !input.ChargeCategoryId.HasValue)
			throw new UserFriendlyException("Select a charge category before filtering charged students.");
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(s => (s.FirstName + " " + (s.MiddleName ?? "") + " " + s.LastName).ToLower().Contains(search)
				|| (s.GrNo.HasValue && s.GrNo.Value.ToString().Contains(search))
				|| (s.RollNo.HasValue && s.RollNo.Value.ToString().Contains(search)));
		}
		if (input.Gender.HasValue) query = query.Where(s => s.AdditionalDetail != null && s.AdditionalDetail.Gender == input.Gender);
		if (input.RollNoFrom.HasValue) query = query.Where(s => s.RollNo >= input.RollNoFrom);
		if (input.RollNoTo.HasValue) query = query.Where(s => s.RollNo <= input.RollNoTo);

		var assignments = await studentFeeStructureRepository.GetQueryableAsync();
		var charges = await chargeRepository.GetQueryableAsync();
		if (input.OnlyWithFeeStructureAssignment || input.HasFeeStructureAssignment == true)
		{
			query = query.Where(student => assignments.Any(assignment =>
				assignment.StudentId == student.Id
				&& assignment.AcademicYearId == input.AcademicYearId));
		}
		if (input.HasFeeStructureAssignment == false)
			query = query.Where(student => !assignments.Any(assignment => assignment.StudentId == student.Id && assignment.AcademicYearId == input.AcademicYearId));
		if (input.AlreadyCharged.HasValue)
			query = query.Where(student => charges.Any(charge => charge.StudentId == student.Id
				&& charge.AcademicYearId == input.AcademicYearId && charge.ChargeCategoryId == input.ChargeCategoryId
				&& (charge.Status == ChargeStatus.Pending || charge.Status == ChargeStatus.Approved)) == input.AlreadyCharged.Value);

		// 4. Enforce row-level query filtering based on user scope
		query = await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);

		var lookup = query
				.OrderBy(s => s.RollNo)
				.ThenBy(s => s.FirstName)
				.ThenBy(s => s.Id)
				.Select(s => new StudentLookupDto
				{
					StudentId = s.Id,
					Gender = s.AdditionalDetail != null ? s.AdditionalDetail.Gender : null,
					HasFeeStructureAssignment = assignments.Any(assignment => assignment.StudentId == s.Id && assignment.AcademicYearId == input.AcademicYearId),
					HasChargeInCategory = input.ChargeCategoryId.HasValue && charges.Any(charge => charge.StudentId == s.Id
						&& charge.AcademicYearId == input.AcademicYearId && charge.ChargeCategoryId == input.ChargeCategoryId
						&& (charge.Status == ChargeStatus.Pending || charge.Status == ChargeStatus.Approved)),
					StudentName = (s.FirstName + " " + (s.MiddleName ?? string.Empty) + " " + s.LastName).Trim(),
					GrNo = s.GrNo,
					RollNo = s.RollNo,
					StandardId = s.StandardId,
					StandardName = s.Standard != null ? s.Standard.Name : string.Empty,
					DivisionId = s.DivisionId,
					DivisionName = s.Division != null ? s.Division.Name : string.Empty
				});
		if (input.SkipCount > 0) lookup = lookup.Skip(input.SkipCount);
		if (input.MaxResultCount.HasValue) lookup = lookup.Take(input.MaxResultCount.Value);
		return await AsyncExecuter.ToListAsync(lookup);
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

	private (DateTime StartDate, DateTime EndDate) ResolveAttendanceDateRange(DateTime? startDate, DateTime? endDate)
	{
		var now = Clock.Now.Date;
		var defaultStart = new DateTime(now.Year, now.Month, 1);
		var defaultEnd = defaultStart.AddMonths(1).AddDays(-1);
		var start = (startDate ?? defaultStart).Date;
		var end = (endDate ?? defaultEnd).Date;

		if (start > end)
		{
			throw new BusinessException(AttendanceOperationErrorCodes.InvalidDateRange)
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

}
