using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_LeaveManagement.View)]
public class LeaveManagementAppService(
	IEmployeeLeaveRepository employeeLeaveRepository,
	IEmployeeRepository employeeRepository,
	EmployeeLeaveManager employeeLeaveManager,
	LeaveValidationService leaveValidationService,
	IAcademicYearRepository academicYearRepository)
	: ApplicationService, ILeaveManagementAppService
{
	public async Task<PagedLeaveManagementResultDto> GetListAsync(
		Guid academicYearId,
		DateTime monthYearAnchor,
		string? search,
		int page,
		int pageSize)
	{
		if (page < 1)
			throw new BusinessException(HRErrorCodes.LeaveInvalidPage);

		if (pageSize < 10 || pageSize > 100)
			throw new BusinessException(HRErrorCodes.LeaveInvalidPageSize);

		var year = monthYearAnchor.Year;
		var month = monthYearAnchor.Month;
		if (month is < 1 or > 12)
			throw new BusinessException(HRErrorCodes.LeaveInvalidMonth);

		if (year is < 1900 or > 2100)
			throw new BusinessException(HRErrorCodes.LeaveInvalidYear);

		var academicYear = await academicYearRepository.FindAsync(academicYearId)
			?? throw new EntityNotFoundException(typeof(AcademicYear), academicYearId);

		var monthStart = new DateTime(year, month, 1).Date;
		var monthEnd = monthStart.AddMonths(1).AddDays(-1);
		var ayStart = academicYear.StartDate.Date;
		var ayEnd = academicYear.EndDate.Date;
		var rangeStart = monthStart > ayStart ? monthStart : ayStart;
		var rangeEnd = monthEnd < ayEnd ? monthEnd : ayEnd;
		if (rangeStart > rangeEnd)
			throw new BusinessException(HRErrorCodes.LeaveInvalidMonthForAcademicYear);

		var skipCount = (page - 1) * pageSize;

		var (totalCount, pageIds) = await employeeLeaveRepository.GetPagedIdsForManagementListAsync(
			rangeStart,
			rangeEnd,
			search,
			skipCount,
			pageSize);

		var detailQuery = await employeeLeaveRepository.WithDetailsAsync(
			x => x.Employee,
			x => x.LeavePolicy);

		var leaveRequests = await AsyncExecuter.ToListAsync(
			detailQuery.Where(x => pageIds.Contains(x.Id)));

		var idOrder = pageIds.Select((id, index) => (id, index)).ToDictionary(t => t.id, t => t.index);
		leaveRequests = leaveRequests.OrderBy(l => idOrder[l.Id]).ToList();

		var items = leaveRequests.Select(leave => new LeaveManagementDto
		{
			Id = leave.Id,
			Name = leave.Employee?.EmployeeNameWithFather ?? "Unknown",
			EmployeeIdentifier = leave.Employee?.EmployeeCode ?? "Unknown",
			LeaveType = leave.LeavePolicy?.LeaveType?.Type ?? "Unknown",
			StartDate = leave.StartDate.DateTime,
			EndDate = leave.EndDate.DateTime,
			TotalDays = CalculateDays(leave.StartDate.DateTime, leave.EndDate.DateTime, leave.HalfDay),
			MediumStageName = leave.LeavePolicy?.EducationMediumStage?.Name ?? "",
			Description = leave.Description ?? "",
			Remarks = leave.RejectionReason,
			Status = leave.Status.ToString().ToUpperInvariant()
		}).ToList();

		return new PagedLeaveManagementResultDto { TotalCount = totalCount, Items = items };
	}

	public async Task<LeaveRequestDetailsDto> GetAsync(Guid leaveRequestId)
	{
		var query = await employeeLeaveRepository.WithDetailsAsync(
			x => x.Employee,
			x => x.LeavePolicy);

		var leave = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		return MapToDetailsDto(leave);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_LeaveManagement.Add)]
	public async Task<LeaveRequestCreatedDto> CreateAsync(
		Guid academicYearId,
		LeaveRequestCreateDto input,
		object? attachment)
	{
		var academicYear = await academicYearRepository.FindAsync(academicYearId)
			?? throw new EntityNotFoundException(typeof(AcademicYear), academicYearId);

		var start = input.StartDate.Date;
		var end = input.EndDate.Date;
		if (start < academicYear.StartDate.Date || end > academicYear.EndDate.Date)
			throw new BusinessException(HRErrorCodes.LeaveDatesOutsideAcademicYear);

		var currentUserId = CurrentUser.Id
			?? throw new BusinessException(HRErrorCodes.LeaveUserNotAuthenticated);

		var employee = await employeeRepository.GetEmployeeByUserIdAsync(currentUserId)
			?? throw new BusinessException(HRErrorCodes.LeaveEmployeeNotFound);

		var resolvedPolicyId = await leaveValidationService.ValidateLeaveRequestAsync(
			employee.Id, input.LeaveTypeId, input.StartDate, input.EndDate);

		byte[]? attachmentBytes = await ExtractAttachmentBytesAsync(attachment);

		var leave = await employeeLeaveManager.CreateAsync(
			employee.Id, resolvedPolicyId,
			new DateTimeOffset(input.StartDate), new DateTimeOffset(input.EndDate),
			input.Reason, false);

		if (attachmentBytes is { Length: > 0 })
		{
			leave.Attachment = attachmentBytes;
		}

		await employeeLeaveRepository.InsertAsync(leave);

		return new LeaveRequestCreatedDto
		{
			Id = leave.Id,
			Status = "PENDING"
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_LeaveManagement.Add)]
	public async Task<LeaveActionResultDto> ApproveAsync(Guid leaveRequestId, LeaveApprovalRequestDto? input)
	{
		var query = await employeeLeaveRepository.WithDetailsAsync(x => x.Employee);

		var leave = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		EnsureNotTerminalForApproverAction(leave.Status);

		if (!IsValidStatusTransition(leave.Status, EmployeeLeaveStatusType.Approved))
			throw new BusinessException(HRErrorCodes.LeaveCannotActOnLeaveState);

		var currentUserId = CurrentUser.Id
			?? throw new BusinessException(HRErrorCodes.LeaveUserNotAuthenticated);

		var actorEmployee = await employeeRepository.GetEmployeeByUserIdAsync(currentUserId)
			?? throw new BusinessException(HRErrorCodes.LeaveEmployeeNotFound);

		await leaveValidationService.EnsureActorMayChangeLeaveStatusAsync(
			leave.EmployeeId,
			actorEmployee.Id);

		await employeeLeaveManager.UpdateLeaveRequestStatusAsync(
			leave,
			EmployeeLeaveStatusType.Approved,
			rejectionReason: null);

		return new LeaveActionResultDto
		{
			Id = leave.Id,
			Status = EmployeeLeaveStatusType.Approved.ToString().ToUpperInvariant(),
			Message = "Leave request approved successfully"
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_LeaveManagement.Add)]
	public async Task<LeaveActionResultDto> RejectAsync(Guid leaveRequestId, LeaveRejectionRequestDto input)
	{
		var query = await employeeLeaveRepository.WithDetailsAsync(x => x.Employee);

		var leave = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		EnsureNotTerminalForApproverAction(leave.Status);

		if (!IsValidStatusTransition(leave.Status, EmployeeLeaveStatusType.Rejected))
			throw new BusinessException(HRErrorCodes.LeaveCannotActOnLeaveState);

		var currentUserId = CurrentUser.Id
			?? throw new BusinessException(HRErrorCodes.LeaveUserNotAuthenticated);

		var actorEmployee = await employeeRepository.GetEmployeeByUserIdAsync(currentUserId)
			?? throw new BusinessException(HRErrorCodes.LeaveEmployeeNotFound);

		await leaveValidationService.EnsureActorMayChangeLeaveStatusAsync(
			leave.EmployeeId,
			actorEmployee.Id);

		await employeeLeaveManager.UpdateLeaveRequestStatusAsync(
			leave,
			EmployeeLeaveStatusType.Rejected,
			rejectionReason: input.Remarks);

		return new LeaveActionResultDto
		{
			Id = leave.Id,
			Status = EmployeeLeaveStatusType.Rejected.ToString().ToUpperInvariant(),
			Message = "Leave request rejected successfully"
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_LeaveManagement.Delete)]
	public async Task<LeaveRequestDeleteDto> DeleteAsync(Guid leaveRequestId)
	{
		var query = await employeeLeaveRepository.GetQueryableAsync();

		var leave = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		if (leave.Status == EmployeeLeaveStatusType.Cancelled)
			throw new BusinessException(HRErrorCodes.LeaveAlreadyCancelled);

		var currentUserId = CurrentUser.Id
			?? throw new BusinessException(HRErrorCodes.LeaveUserNotAuthenticated);

		var actingEmployee = await employeeRepository.GetEmployeeByUserIdAsync(currentUserId)
			?? throw new BusinessException(HRErrorCodes.LeaveEmployeeNotFound);

		if (leave.EmployeeId != actingEmployee.Id)
			throw new BusinessException(HRErrorCodes.LeaveDeleteOwnOnly);

		await employeeLeaveManager.CancelAsync(leave);

		return new LeaveRequestDeleteDto { Id = leave.Id };
	}

	public async Task<LeaveAttachmentDto> GetAttachmentAsync(Guid leaveRequestId)
	{
		var query = await employeeLeaveRepository.GetQueryableAsync();

		var leave = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		if (leave.Attachment is not { Length: > 0 })
			throw new BusinessException(HRErrorCodes.LeaveAttachmentNotFound);

		var (contentType, extension) = DetectContentType(leave.Attachment);
		return new LeaveAttachmentDto
		{
			FileContent = leave.Attachment,
			ContentType = contentType,
			FileName = $"leave-attachment-{leaveRequestId:N}{extension}"
		};
	}

	private static LeaveRequestDetailsDto MapToDetailsDto(EmployeeLeave leave)
	{
		return new LeaveRequestDetailsDto
		{
			Id = leave.Id,
			MediumStageId = leave.LeavePolicy?.EducationMediumStageId ?? Guid.Empty,
			MediumStageName = leave.LeavePolicy?.EducationMediumStage?.Name ?? "",
			EmployeeId = leave.EmployeeId,
			EmployeeName = leave.Employee?.EmployeeNameWithFather ?? "Unknown",
			EmployeeCode = leave.Employee?.EmployeeCode ?? "Unknown",
			LeaveTypeId = leave.LeavePolicy?.LeaveTypeId ?? Guid.Empty,
			LeaveType = leave.LeavePolicy?.LeaveType?.Type ?? "Unknown",
			StartDate = leave.StartDate.DateTime,
			EndDate = leave.EndDate.DateTime,
			TotalDays = CalculateDays(leave.StartDate.DateTime, leave.EndDate.DateTime, leave.HalfDay),
			Description = leave.Description ?? "",
			AttachmentUrl = leave.Attachment != null ? $"/api/leave-management/{leave.Id}/attachment" : null,
			Remarks = leave.RejectionReason,
			Status = leave.Status.ToString().ToUpperInvariant()
		};
	}

	private static void EnsureNotTerminalForApproverAction(EmployeeLeaveStatusType status)
	{
		if (status is EmployeeLeaveStatusType.Approved
		    or EmployeeLeaveStatusType.Rejected
		    or EmployeeLeaveStatusType.Cancelled)
		{
			throw new BusinessException(HRErrorCodes.LeaveConflictTerminalState);
		}
	}

	private static bool IsValidStatusTransition(EmployeeLeaveStatusType from, EmployeeLeaveStatusType to)
	{
		return (from, to) switch
		{
			(EmployeeLeaveStatusType.Pending, EmployeeLeaveStatusType.Reviewed) => true,
			(EmployeeLeaveStatusType.Pending, EmployeeLeaveStatusType.Approved) => true,
			(EmployeeLeaveStatusType.Pending, EmployeeLeaveStatusType.Rejected) => true,
			(EmployeeLeaveStatusType.Pending, EmployeeLeaveStatusType.Cancelled) => true,
			(EmployeeLeaveStatusType.Reviewed, EmployeeLeaveStatusType.Approved) => true,
			(EmployeeLeaveStatusType.Reviewed, EmployeeLeaveStatusType.Rejected) => true,
			(EmployeeLeaveStatusType.Reviewed, EmployeeLeaveStatusType.Cancelled) => true,
			_ => false
		};
	}

	private static async Task<byte[]?> ExtractAttachmentBytesAsync(object? attachment)
	{
		if (attachment == null) return null;

		if (attachment is byte[] bytes)
			return bytes;

		if (attachment is Stream stream)
		{
			using var ms = new MemoryStream();
			await stream.CopyToAsync(ms);
			return ms.ToArray();
		}

		var formFileType = attachment.GetType();
		var lengthProp = formFileType.GetProperty("Length");
		var fileNameProp = formFileType.GetProperty("FileName");
		var copyToAsyncMethod = formFileType.GetMethod("CopyToAsync");

		if (lengthProp == null || fileNameProp == null || copyToAsyncMethod == null)
			return null;

		var length = (long)(lengthProp.GetValue(attachment) ?? 0L);
		var fileName = (string?)fileNameProp.GetValue(attachment);

		if (length == 0 || string.IsNullOrEmpty(fileName))
			return null;

		ValidateFileUpload(length, fileName);

		using var memoryStream = new MemoryStream();
		await (Task)copyToAsyncMethod.Invoke(attachment,
			new object[] { memoryStream, default(System.Threading.CancellationToken) })!;
		var fileBytes = memoryStream.ToArray();

		ValidateFileContent(fileBytes, fileName);

		return fileBytes;
	}

	private static void ValidateFileUpload(long fileSize, string fileName)
	{
		const long maxSize = 5 * 1024 * 1024;
		if (fileSize > maxSize)
			throw new BusinessException(HRErrorCodes.LeaveFileTooLarge);
		if (fileSize == 0)
			throw new BusinessException(HRErrorCodes.LeaveFileEmpty);

		var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
			throw new BusinessException(HRErrorCodes.LeaveFileInvalidExtension);
		if (fileName.Length > 255)
			throw new BusinessException(HRErrorCodes.LeaveFileNameTooLong);
		if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
			throw new BusinessException(HRErrorCodes.LeaveFileInvalidCharacters);
	}

	private static void ValidateFileContent(byte[] fileBytes, string fileName)
	{
		if (fileBytes.Length < 4)
			throw new BusinessException(HRErrorCodes.LeaveFileTooSmall);

		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		var isPdf = fileBytes[0] == 0x25 && fileBytes[1] == 0x50 && fileBytes[2] == 0x44 && fileBytes[3] == 0x46;
		var isJpeg = fileBytes[0] == 0xFF && fileBytes[1] == 0xD8 && fileBytes[2] == 0xFF;
		var isPng = fileBytes[0] == 0x89 && fileBytes[1] == 0x50 && fileBytes[2] == 0x4E && fileBytes[3] == 0x47;

		var isValid = extension switch
		{
			".pdf" => isPdf,
			".jpg" or ".jpeg" => isJpeg,
			".png" => isPng,
			_ => false
		};

		if (!isValid)
			throw new BusinessException(HRErrorCodes.LeaveFileContentMismatch);
	}

	private static (string ContentType, string Extension) DetectContentType(byte[] fileBytes)
	{
		if (fileBytes.Length >= 4 &&
		    fileBytes[0] == 0x25 && fileBytes[1] == 0x50 && fileBytes[2] == 0x44 && fileBytes[3] == 0x46)
			return ("application/pdf", ".pdf");
		if (fileBytes.Length >= 3 && fileBytes[0] == 0xFF && fileBytes[1] == 0xD8 && fileBytes[2] == 0xFF)
			return ("image/jpeg", ".jpg");
		if (fileBytes.Length >= 4 &&
		    fileBytes[0] == 0x89 && fileBytes[1] == 0x50 && fileBytes[2] == 0x4E && fileBytes[3] == 0x47)
			return ("image/png", ".png");
		return ("application/octet-stream", ".bin");
	}

	private static int CalculateDays(DateTime startDate, DateTime endDate, bool isHalfDay)
	{
		var days = (endDate.Date - startDate.Date).Days + 1;
		return isHalfDay ? (int)Math.Ceiling(days / 2.0) : days;
	}
}
