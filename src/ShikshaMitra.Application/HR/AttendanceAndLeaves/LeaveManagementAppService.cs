using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Users;

namespace ShikshaMitra.HR.AttendanceAndLeaves
{
	/// <summary>
	/// Application service for managing leave requests and approvals
	/// </summary>
	public class LeaveManagementAppService : ApplicationService, ILeaveManagementAppService
	{
		private readonly IEmployeeLeaveRepository _employeeLeaveRepository;
		private readonly IEmployeeRepository _employeeRepository;
		private readonly ILeavePolicyRepository _leavePolicyRepository;
		private readonly ILeaveTypeRepository _leaveTypeRepository;
		private readonly EmployeeLeaveManager _employeeLeaveManager;

		public LeaveManagementAppService(
			IEmployeeLeaveRepository employeeLeaveRepository,
			IEmployeeRepository employeeRepository,
			ILeavePolicyRepository leavePolicyRepository,
			ILeaveTypeRepository leaveTypeRepository,
			EmployeeLeaveManager employeeLeaveManager)
		{
			_employeeLeaveRepository = employeeLeaveRepository;
			_employeeRepository = employeeRepository;
			_leavePolicyRepository = leavePolicyRepository;
			_leaveTypeRepository = leaveTypeRepository;
			_employeeLeaveManager = employeeLeaveManager;
		}

		public async Task<PagedLeaveManagementResultDto> GetListAsync(Guid mediumStageId, int? month, int? year, int page, int pageSize)
		{
			var skipCount = (page - 1) * pageSize;

			// Get all leave requests (in real implementation, you would filter by organization/tenant)
			var allLeaves = await _employeeLeaveRepository.GetListAsync();

			// Apply date filters if provided
			var filteredLeaves = allLeaves.AsQueryable();

			if (month.HasValue && year.HasValue)
			{
				var startOfMonth = new DateTime(year.Value, month.Value, 1);
				var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

				filteredLeaves = filteredLeaves.Where(x => 
					x.StartDate.Date >= startOfMonth.Date && 
					x.StartDate.Date <= endOfMonth.Date);
			}
			else if (year.HasValue)
			{
				var startOfYear = new DateTime(year.Value, 1, 1);
				var endOfYear = new DateTime(year.Value, 12, 31);

				filteredLeaves = filteredLeaves.Where(x => 
					x.StartDate.Date >= startOfYear.Date && 
					x.StartDate.Date <= endOfYear.Date);
			}

			var totalCount = filteredLeaves.Count();

			// Get paginated results
			var leaveRequests = filteredLeaves
				.OrderByDescending(x => x.CreationTime)
				.Skip(skipCount)
				.Take(pageSize)
				.ToList();

			var items = new List<LeaveManagementDto>();
			foreach (var leave in leaveRequests)
			{
				// Get employee details
				var employee = await _employeeRepository.FindAsync(leave.EmployeeId);

				// Get leave type details  
				var leavePolicy = await _leavePolicyRepository.FindAsync(x => x.Id == leave.LeaveTypeId);
				var leaveTypeName = leavePolicy?.LeaveType ?? "Unknown";

				items.Add(new LeaveManagementDto
				{
					Id = (int)leave.Id.GetHashCode(), // Convert Guid to int for API compatibility
					Name = employee?.EmployeeNameWithFather ?? "Unknown",
					EmployeeCode = employee?.EmployeeCode ?? "Unknown",
					LeaveType = leaveTypeName,
					StartDate = leave.StartDate.DateTime,
					EndDate = leave.EndDate.DateTime,
					TotalDays = CalculateDays(leave.StartDate.DateTime, leave.EndDate.DateTime, leave.HalfDay),
					Organization = "Organization", // TODO: Get from employee's organization
					Description = leave.Description ?? "",
					Remarks = leave.RejectionReason,
					Status = leave.Status.ToString().ToUpper()
				});
			}

			return new PagedLeaveManagementResultDto
			{
				TotalCount = totalCount,
				Items = items
			};
		}

		public async Task<LeaveRequestDetailsDto> GetAsync(int leaveRequestId)
		{
			// Find leave by matching hash code (since we convert Guid to int)
			var allLeaves = await _employeeLeaveRepository.GetListAsync();
			var leave = allLeaves.FirstOrDefault(x => x.Id.GetHashCode() == leaveRequestId);

			if (leave == null)
			{
				throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);
			}

			// Get related data
			var employee = await _employeeRepository.FindAsync(leave.EmployeeId);
			var leavePolicy = await _leavePolicyRepository.FindAsync(x => x.Id == leave.LeaveTypeId);
			var leaveTypeName = leavePolicy?.LeaveType ?? "Unknown";

			return new LeaveRequestDetailsDto
			{
				Id = leaveRequestId,
				MediumStageId = employee?.EducationMediumStageId ?? Guid.Empty,
				EmployeeId = leave.EmployeeId,
				EmployeeName = employee?.EmployeeNameWithFather ?? "Unknown",
				EmployeeCode = employee?.EmployeeCode ?? "Unknown",
				LeaveTypeId = leave.LeaveTypeId,
				LeaveType = leaveTypeName,
				StartDate = leave.StartDate.DateTime,
				EndDate = leave.EndDate.DateTime,
				TotalDays = CalculateDays(leave.StartDate.DateTime, leave.EndDate.DateTime, leave.HalfDay),
				Description = leave.Description ?? "",
				AttachmentUrl = leave.Attachment != null ? GenerateAttachmentUrl(leave.Id) : null,
				Remarks = leave.RejectionReason,
				Status = leave.Status.ToString().ToUpper(),
				CreatedAt = leave.CreationTime,
				UpdatedAt = leave.LastModificationTime ?? leave.CreationTime
			};
		}

		public async Task<LeaveRequestCreatedDto> CreateAsync(LeaveRequestCreateDto input, object attachment)
		{
			// Validate employee exists
			var currentUserId = CurrentUser.Id ?? throw new UserFriendlyException("User not authenticated");
			var employee = await _employeeRepository.FindAsync(currentUserId);
			if (employee == null)
			{
				throw new UserFriendlyException("Employee not found for current user");
			}

			// Validate leave type exists
			var leaveType = await _leaveTypeRepository.GetAsync(input.LeaveTypeId);

			// Validate dates
			if (input.StartDate > input.EndDate)
			{
				throw new UserFriendlyException("Start date cannot be after end date");
			}

			if (input.StartDate.Date < DateTime.Today)
			{
				throw new UserFriendlyException("Cannot apply leave for past dates");
			}

			// Create leave request using manager
			var leave = await _employeeLeaveManager.CreateAsync(
				employee.Id,
				input.LeaveTypeId,
				new DateTimeOffset(input.StartDate),
				new DateTimeOffset(input.EndDate),
				input.Reason,
				false); // HalfDay - TODO: Add to input if needed

			// Handle attachment if provided
			if (attachment != null)
			{
				// TODO: Handle file attachment storage
				// For now, we'll skip attachment handling due to interface complexity
			}

			await _employeeLeaveRepository.InsertAsync(leave);

			return new LeaveRequestCreatedDto
			{
				Id = leave.Id,
				Status = "PENDING",
				AppliedAt = DateTime.UtcNow
			};
		}

		public async Task<LeaveRequestDetailsDto> UpdateAsync(int leaveRequestId, LeaveRequestUpdateRequest input)
		{
			// Find leave by hash code match
			var allLeaves = await _employeeLeaveRepository.GetListAsync();
			var leave = allLeaves.FirstOrDefault(x => x.Id.GetHashCode() == leaveRequestId);

			if (leave == null)
			{
				throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);
			}

			// Parse status
			if (!Enum.TryParse<EmployeeLeaveStatusType>(input.Status, true, out var status))
			{
				throw new UserFriendlyException($"Invalid status: {input.Status}");
			}

			// Update leave status
			await _employeeLeaveManager.UpdateLeaveRequestStatusAsync(leave, status, input.Remarks);

			return await GetAsync(leaveRequestId);
		}

		public async Task<LeaveRequestDeleteDto> DeleteAsync(int leaveRequestId)
		{
			// Find leave by hash code match
			var allLeaves = await _employeeLeaveRepository.GetListAsync();
			var leave = allLeaves.FirstOrDefault(x => x.Id.GetHashCode() == leaveRequestId);

			if (leave == null)
			{
				throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);
			}

			// Check if leave can be cancelled
			if (leave.Status == EmployeeLeaveStatusType.Approved)
			{
				throw new UserFriendlyException("Cannot cancel approved leave request");
			}

			if (leave.Status == EmployeeLeaveStatusType.Rejected)
			{
				throw new UserFriendlyException("Cannot cancel rejected leave request");
			}

			// Soft delete
			await _employeeLeaveRepository.DeleteAsync(leave);

			return new LeaveRequestDeleteDto
			{
				Id = leaveRequestId,
				Message = "Leave request cancelled successfully"
			};
		}

		private static int CalculateDays(DateTime startDate, DateTime endDate, bool isHalfDay)
		{
			var days = (endDate.Date - startDate.Date).Days + 1;
			return isHalfDay ? (int)Math.Ceiling(days / 2.0) : days;
		}

		private static string GenerateAttachmentUrl(Guid leaveId)
		{
			return $"/api/leave-management/{leaveId}/attachment";
		}
	}
}
