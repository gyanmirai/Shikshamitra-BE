using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
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
	LeaveValidationService leaveValidationService)
	: ApplicationService, ILeaveManagementAppService
{
	public async Task<PagedLeaveManagementResultDto> GetListAsync(Guid mediumStageId, int? month, int? year, int page, int pageSize)
	{
		if (month.HasValue && (month < 1 || month > 12))
			throw new BusinessException(HRErrorCodes.LeaveInvalidMonth);

		if (year.HasValue && (year < 1900 || year > 2100))
			throw new BusinessException(HRErrorCodes.LeaveInvalidYear);

		if (page < 1)
			throw new BusinessException(HRErrorCodes.LeaveInvalidPage);

		if (pageSize < 10 || pageSize > 100)
			throw new BusinessException(HRErrorCodes.LeaveInvalidPageSize);

		var skipCount = (page - 1) * pageSize;

		var query = await employeeLeaveRepository.WithDetailsAsync(
			x => x.Employee,
			x => x.LeavePolicy);

		if (month.HasValue && year.HasValue)
		{
			var startOfMonth = new DateTimeOffset(new DateTime(year.Value, month.Value, 1));
			var endOfMonth = new DateTimeOffset(startOfMonth.DateTime.AddMonths(1).AddDays(-1));
			query = query.Where(x => x.StartDate >= startOfMonth && x.StartDate <= endOfMonth);
		}
		else if (year.HasValue)
		{
			var startOfYear = new DateTimeOffset(new DateTime(year.Value, 1, 1));
			var endOfYear = new DateTimeOffset(new DateTime(year.Value, 12, 31));
			query = query.Where(x => x.StartDate >= startOfYear && x.StartDate <= endOfYear);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);

		var leaveRequests = await AsyncExecuter.ToListAsync(
			query.OrderByDescending(x => x.CreationTime).Skip(skipCount).Take(pageSize));

		var items = leaveRequests.Select(leave => new LeaveManagementDto
		{
			Id = leave.PublicId,
			Name = leave.Employee?.EmployeeNameWithFather ?? "Unknown",
			EmployeeCode = leave.Employee?.EmployeeCode ?? "Unknown",
			LeaveType = leave.LeavePolicy?.LeaveType?.Type ?? "Unknown",
			StartDate = leave.StartDate.DateTime,
			EndDate = leave.EndDate.DateTime,
			TotalDays = CalculateDays(leave.StartDate.DateTime, leave.EndDate.DateTime, leave.HalfDay),
			Organization = "N/A",
			Description = leave.Description ?? "",
			Remarks = leave.RejectionReason,
			Status = leave.Status.ToString().ToUpper()
		}).ToList();

		return new PagedLeaveManagementResultDto { TotalCount = totalCount, Items = items };
	}

	public async Task<LeaveRequestDetailsDto> GetAsync(int leaveRequestId)
	{
		var query = await employeeLeaveRepository.WithDetailsAsync(
			x => x.Employee,
			x => x.LeavePolicy);

		var leave = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.PublicId == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		return new LeaveRequestDetailsDto
		{
			Id = leave.PublicId,
			MediumStageId = Guid.Empty,
			EmployeeId = leave.EmployeeId,
			EmployeeName = leave.Employee?.EmployeeNameWithFather ?? "Unknown",
			EmployeeCode = leave.Employee?.EmployeeCode ?? "Unknown",
			LeaveTypeId = leave.LeaveTypeId,
			LeaveType = leave.LeavePolicy?.LeaveType?.Type ?? "Unknown",
			StartDate = leave.StartDate.DateTime,
			EndDate = leave.EndDate.DateTime,
			TotalDays = CalculateDays(leave.StartDate.DateTime, leave.EndDate.DateTime, leave.HalfDay),
			Description = leave.Description ?? "",
			AttachmentUrl = leave.Attachment != null ? $"/api/leave-management/{leave.Id}/attachment" : null,
			Remarks = leave.RejectionReason,
			Status = leave.Status.ToString().ToUpper(),
			CreatedAt = leave.CreationTime,
			UpdatedAt = leave.LastModificationTime ?? leave.CreationTime
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_LeaveManagement.Add)]
	public async Task<LeaveRequestCreatedDto> CreateAsync(LeaveRequestCreateDto input, object attachment)
	{
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
			Status = "PENDING",
			AppliedAt = DateTime.UtcNow
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_LeaveManagement.Add)]
	public async Task<LeaveRequestDetailsDto> UpdateAsync(int leaveRequestId, LeaveRequestUpdateRequest input)
	{
		var query = await employeeLeaveRepository.WithDetailsAsync(x => x.Employee);

		var leave = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.PublicId == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		if (!Enum.TryParse<EmployeeLeaveStatusType>(input.Status, true, out var status))
			throw new BusinessException(HRErrorCodes.LeaveInvalidStatus);

		if (!IsValidStatusTransition(leave.Status, status))
			throw new BusinessException(HRErrorCodes.LeaveInvalidStatusTransition);

		var currentUserId = CurrentUser.Id
			?? throw new BusinessException(HRErrorCodes.LeaveUserNotAuthenticated);

		var actorEmployee = await employeeRepository.GetEmployeeByUserIdAsync(currentUserId)
			?? throw new BusinessException(HRErrorCodes.LeaveEmployeeNotFound);

		await leaveValidationService.EnsureActorMayChangeLeaveStatusAsync(
			leave.EmployeeId,
			actorEmployee.Id);

		await employeeLeaveManager.UpdateLeaveRequestStatusAsync(leave, status, input.Remarks);

		return await GetAsync(leaveRequestId);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_LeaveManagement.Delete)]
	public async Task<LeaveRequestDeleteDto> DeleteAsync(int leaveRequestId)
	{
		var query = await employeeLeaveRepository.GetQueryableAsync();

		var leave = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.PublicId == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		if (leave.Status == EmployeeLeaveStatusType.Cancelled)
			throw new BusinessException(HRErrorCodes.LeaveAlreadyCancelled);

		var currentUserId = CurrentUser.Id
			?? throw new BusinessException(HRErrorCodes.LeaveUserNotAuthenticated);

		var actingEmployee = await employeeRepository.GetEmployeeByUserIdAsync(currentUserId)
			?? throw new BusinessException(HRErrorCodes.LeaveEmployeeNotFound);

		if (leave.EmployeeId != actingEmployee.Id)
			throw new BusinessException(HRErrorCodes.LeaveDeleteOwnOnly);

		// Soft-cancel: transitions to Cancelled status (domain invariant enforces valid source states)
		await employeeLeaveManager.CancelAsync(leave);

		return new LeaveRequestDeleteDto { Id = leave.PublicId };
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

	/// <summary>
	/// Extracts byte[] from an attachment object. Accepts byte[], Stream, or IFormFile-like objects.
	/// File validation (size, type, magic bytes) is applied when content is present.
	/// </summary>
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

	private static int CalculateDays(DateTime startDate, DateTime endDate, bool isHalfDay)
	{
		var days = (endDate.Date - startDate.Date).Days + 1;
		return isHalfDay ? (int)Math.Ceiling(days / 2.0) : days;
	}
}
