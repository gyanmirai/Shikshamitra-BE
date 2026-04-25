using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Identity;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
public class LeaveManagementAppService(
	IEmployeeLeaveRepository employeeLeaveRepository,
	IEmployeeRepository employeeRepository,
	EmployeeLeaveManager employeeLeaveManager,
	LeaveValidationService leaveValidationService,
	IAcademicYearRepository academicYearRepository,
	ILeavePolicyRepository leavePolicyRepository,
	IRepository<UserScopeAssignment, Guid> userScopeAssignmentRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<IdentityUser, Guid> identityUserRepository,
	IRepository<IdentityRole, Guid> identityRoleRepository,
	IEmployeeAuthorizationService employeeAuthorizationService,
	HRExcelService hrExcelService)
	: ApplicationService, ILeaveManagementAppService
{
	public async Task<PagedLeaveManagementResultDto> GetListAsync(LeaveManagementGetListInput input)
	{
		if (input.Page < 1)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidPage);
		}

		if (input.UseExportBatching)
		{
			if (input.PageSize < 1 || input.PageSize > 10_000)
			{
				throw new BusinessException(HRErrorCodes.LeaveInvalidPageSize);
			}
		}
		else if (input.PageSize < 10 || input.PageSize > 100)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidPageSize);
		}

		var academicYear = await academicYearRepository.FindAsync(input.AcademicYearId)
			?? throw new EntityNotFoundException(typeof(AcademicYear), input.AcademicYearId);

		var ayStart = academicYear.StartDate.Date;
		var ayEnd = academicYear.EndDate.Date;
		DateTime rangeStart;
		DateTime rangeEnd;

		if (input.MonthYearAnchor is { } anchor)
		{
			var year = anchor.Year;
			var month = anchor.Month;
			if (month is < 1 or > 12)
			{
				throw new BusinessException(HRErrorCodes.LeaveInvalidMonth);
			}

			if (year is < 1900 or > 2100)
			{
				throw new BusinessException(HRErrorCodes.LeaveInvalidYear);
			}

			var monthStart = new DateTime(year, month, 1).Date;
			var monthEnd = monthStart.AddMonths(1).AddDays(-1);
			rangeStart = monthStart > ayStart ? monthStart : ayStart;
			rangeEnd = monthEnd < ayEnd ? monthEnd : ayEnd;
			if (rangeStart > rangeEnd)
			{
				throw new BusinessException(HRErrorCodes.LeaveInvalidMonthForAcademicYear)
					.WithData("requestedMonth", anchor.ToString("yyyy-MM"))
					.WithData("academicYearName", academicYear.Name ?? string.Empty)
					.WithData("academicYearStart", ayStart.ToString("yyyy-MM-dd"))
					.WithData("academicYearEnd", ayEnd.ToString("yyyy-MM-dd"));
			}
		}
		else
		{
			rangeStart = ayStart;
			rangeEnd = ayEnd;
		}

		var skipCount = (input.Page - 1) * input.PageSize;

		var filters = new LeaveManagementListFilters(
			input.AcademicYearId,
			input.Status,
			input.LeavePolicyId,
			input.LeaveTypeMasterId);

		var (totalCount, pageIds) = await employeeLeaveRepository.GetPagedIdsForManagementListAsync(
			rangeStart,
			rangeEnd,
			filters,
			input.Search,
			skipCount,
			input.PageSize);

		var detailQuery = await employeeLeaveRepository.WithDetailsAsync(
			x => x.Employee,
			x => x.LeavePolicy,
			x => x.LeavePolicy!.AcademicYear,
			x => x.LeavePolicy!.LeaveType);

		var leaveRequests = await AsyncExecuter.ToListAsync(
			detailQuery.Where(x => pageIds.Contains(x.Id)));

		var idOrder = pageIds.Select((id, index) => (id, index)).ToDictionary(t => t.id, t => t.index);
		leaveRequests = leaveRequests.OrderBy(l => idOrder[l.Id]).ToList();

		var items = leaveRequests.Select(leave => new LeaveManagementDto
		{
			Id = leave.Id,
			PublicId = leave.PublicId,
			Name = leave.Employee?.EmployeeNameWithFather ?? "Unknown",
			EmployeeIdentifier = leave.Employee?.EmployeeCode ?? "Unknown",
			LeaveType = leave.LeavePolicy?.LeaveType?.Type ?? "Unknown",
			StartDate = leave.StartDate.DateTime,
			EndDate = leave.EndDate.DateTime,
			TotalDays = CalculateDays(leave.StartDate.DateTime, leave.EndDate.DateTime, leave.HalfDay),
			MediumStageName = leave.LeavePolicy?.AcademicYear?.Name ?? "",
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
			x => x.LeavePolicy,
			x => x.LeavePolicy!.LeaveType,
			x => x.LeavePolicy!.AcademicYear);

		var leave = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		var details = MapToDetailsDto(leave);
		var academicYearId = leave.LeavePolicy?.AcademicYearId;
		if (academicYearId.HasValue)
		{
			var lookup = await ResolveEmployeeMediumStageLookupAsync([leave], academicYearId.Value);
			if (leave.Employee?.UserId is Guid userId && lookup.TryGetValue(userId, out var mediumStage))
			{
				details.MediumStageId = mediumStage.MediumStageId;
				details.MediumStageName = mediumStage.MediumStageName;
			}
			else
			{
				details.MediumStageId = Guid.Empty;
				details.MediumStageName = string.Empty;
			}
		}

		return details;
	}

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
		{
			throw new BusinessException(HRErrorCodes.LeaveDatesOutsideAcademicYear);
		}

		var actorContext = await ResolveLeaveActorContextAsync();

		var policyId = input.ResolveLeavePolicyId();
		if (policyId == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);
		}

		Guid targetEmployeeId = input.ForEmployeeId
			?? actorContext.ActorEmployeeId
			?? throw new BusinessException(HRErrorCodes.LeaveTargetEmployeeRequired);

		await EnsureMayCreateLeaveOnBehalfAsync(targetEmployeeId, actorContext);

		var targetEmployee = await employeeRepository.FindAsync(targetEmployeeId)
			?? throw new EntityNotFoundException(typeof(Employee), targetEmployeeId);

		var resolvedPolicyId = await leaveValidationService.ValidateLeaveRequestAsync(
			targetEmployee.Id, policyId, input.StartDate, input.EndDate, academicYearId);

		byte[]? attachmentBytes = await ExtractAttachmentBytesAsync(attachment);

		var leave = await employeeLeaveManager.CreateAsync(
			targetEmployee.Id, resolvedPolicyId,
			new DateTimeOffset(input.StartDate), new DateTimeOffset(input.EndDate),
			input.Reason, false);

		if (attachmentBytes is { Length: > 0 })
		{
			leave.Attachment = attachmentBytes;
		}

		await employeeLeaveRepository.InsertAsync(leave);

		var reloaded = await employeeLeaveRepository.GetAsync(leave.Id);

		return new LeaveRequestCreatedDto
		{
			Id = reloaded.Id,
			PublicId = reloaded.PublicId,
			Status = "PENDING"
		};
	}

	public async Task<LeaveActionResultDto> ApproveAsync(Guid leaveRequestId, LeaveApprovalRequestDto? input)
	{
		var query = await employeeLeaveRepository.WithDetailsAsync(x => x.Employee, x => x.LeavePolicy);

		var leave = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		EnsureNotTerminalForApproverAction(leave.Status);

		if (!IsValidStatusTransition(leave.Status, EmployeeLeaveStatusType.Approved))
		{
			throw new BusinessException(HRErrorCodes.LeaveCannotActOnLeaveState);
		}

		var actorContext = await ResolveLeaveActorContextAsync();
		if (!actorContext.IsAdminBypass)
		{
			await leaveValidationService.EnsureActorMayChangeLeaveStatusAsync(
				leave.EmployeeId,
				actorContext.ActorEmployeeId!.Value);
		}

		await leaveValidationService.EnsureSufficientLeaveBalanceAsync(
			leave.EmployeeId,
			leave.LeaveTypeId,
			leave.StartDate.DateTime,
			leave.EndDate.DateTime);

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

	public async Task<LeaveActionResultDto> RejectAsync(Guid leaveRequestId, LeaveRejectionRequestDto input)
	{
		var query = await employeeLeaveRepository.WithDetailsAsync(x => x.Employee);

		var leave = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		EnsureNotTerminalForApproverAction(leave.Status);

		if (!IsValidStatusTransition(leave.Status, EmployeeLeaveStatusType.Rejected))
		{
			throw new BusinessException(HRErrorCodes.LeaveCannotActOnLeaveState);
		}

		var actorContext = await ResolveLeaveActorContextAsync();
		if (!actorContext.IsAdminBypass)
		{
			await leaveValidationService.EnsureActorMayChangeLeaveStatusAsync(
				leave.EmployeeId,
				actorContext.ActorEmployeeId!.Value);
		}

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

	public async Task<LeaveRequestDeleteDto> DeleteAsync(Guid leaveRequestId)
	{
		var query = await employeeLeaveRepository.GetQueryableAsync();

		var leave = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);

		if (leave.Status == EmployeeLeaveStatusType.Cancelled)
		{
			throw new BusinessException(HRErrorCodes.LeaveAlreadyCancelled);
		}

		var actorContext = await ResolveLeaveActorContextAsync();
		if (!actorContext.IsAdminBypass && leave.EmployeeId != actorContext.ActorEmployeeId!.Value)
		{
			throw new BusinessException(HRErrorCodes.LeaveDeleteOwnOnly);
		}

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
		{
			throw new BusinessException(HRErrorCodes.LeaveAttachmentNotFound);
		}

		var (contentType, extension) = DetectContentType(leave.Attachment);
		return new LeaveAttachmentDto
		{
			FileContent = leave.Attachment,
			ContentType = contentType,
			FileName = $"leave-attachment-{leaveRequestId:N}{extension}"
		};
	}

	public async Task<LeaveBalanceListDto> GetBalancesAsync(Guid academicYearId, Guid? forEmployeeId)
	{
		await academicYearRepository.GetAsync(academicYearId);

		var actorContext = await ResolveLeaveActorContextAsync();
		var targetId = forEmployeeId
			?? actorContext.ActorEmployeeId
			?? throw new BusinessException(HRErrorCodes.LeaveTargetEmployeeRequired);

		await employeeAuthorizationService.CheckEmployeeAccessAsync(targetId, requireOwnership: false);

		var policies = await leavePolicyRepository.GetListByAcademicYearIdAsync(
			academicYearId,
			includeInactive: false);

		var items = new List<LeaveBalanceItemDto>();

		foreach (var policy in policies)
		{
			var used = await employeeLeaveRepository.GetApprovedLeaveDaysAsync(targetId, policy.Id);
			var entitled = policy.LeaveCount;
			var remaining = Math.Max(0, entitled - used);

			items.Add(new LeaveBalanceItemDto
			{
				LeavePolicyId = policy.Id,
				PolicyName = policy.PolicyName,
				LeaveTypeMasterId = policy.LeaveTypeId,
				LeaveTypeName = policy.LeaveType?.Type ?? "",
				EntitledDays = entitled,
				UsedDays = used,
				RemainingDays = remaining
			});
		}

		return new LeaveBalanceListDto { Items = items };
	}

	public async Task<LeaveManagementCalendarResultDto> GetCalendarAsync(LeaveManagementCalendarInput input)
	{
		if (input.Page < 1)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidPage);
		}

		if (input.PageSize < 1 || input.PageSize > 500)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidPageSize);
		}

		var academicYear = await academicYearRepository.FindAsync(input.AcademicYearId)
			?? throw new EntityNotFoundException(typeof(AcademicYear), input.AcademicYearId);

		var rs = input.RangeStart.Date;
		var re = input.RangeEnd.Date;
		if (rs > re)
		{
			throw new BusinessException(HRErrorCodes.LeaveStartAfterEnd);
		}

		var ayStart = academicYear.StartDate.Date;
		var ayEnd = academicYear.EndDate.Date;
		var rangeStart = rs > ayStart ? rs : ayStart;
		var rangeEnd = re < ayEnd ? re : ayEnd;
		if (rangeStart > rangeEnd)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidMonthForAcademicYear)
				.WithData("requestedMonth", rs.ToString("yyyy-MM"))
				.WithData("academicYearName", academicYear.Name ?? string.Empty)
				.WithData("academicYearStart", ayStart.ToString("yyyy-MM-dd"))
				.WithData("academicYearEnd", ayEnd.ToString("yyyy-MM-dd"));
		}

		var rangeStartOffset = new DateTimeOffset(
			rangeStart.Year, rangeStart.Month, rangeStart.Day, 0, 0, 0, input.RangeStart.Offset);
		var rangeEndOffset = new DateTimeOffset(
			rangeEnd.Year, rangeEnd.Month, rangeEnd.Day, 23, 59, 59, 999, input.RangeEnd.Offset);

		var skip = (input.Page - 1) * input.PageSize;
		var filters = new LeaveManagementListFilters(
			input.AcademicYearId,
			input.Status,
			input.LeavePolicyId,
			input.LeaveTypeMasterId);

		var (totalCount, pageIds) = await employeeLeaveRepository.GetPagedIdsForCalendarAsync(
			rangeStartOffset,
			rangeEndOffset,
			filters,
			input.Search,
			skip,
			input.PageSize);

		var detailQuery = await employeeLeaveRepository.WithDetailsAsync(
			x => x.Employee,
			x => x.LeavePolicy,
			x => x.LeavePolicy!.AcademicYear,
			x => x.LeavePolicy!.LeaveType);

		var leaves = await AsyncExecuter.ToListAsync(
			detailQuery.Where(x => pageIds.Contains(x.Id)));

		var idOrder = pageIds.Select((id, index) => (id, index)).ToDictionary(t => t.id, t => t.index);
		leaves = leaves.OrderBy(l => idOrder[l.Id]).ToList();
		var employeeMediumStageLookup = await ResolveEmployeeMediumStageLookupAsync(
			leaves,
			input.AcademicYearId);

		var items = leaves.Select(leave => new LeaveManagementCalendarItemDto
		{
			MediumStageId = leave.Employee?.UserId is Guid userId && employeeMediumStageLookup.TryGetValue(userId, out var mediumStage)
				? mediumStage.MediumStageId
				: null,
			Id = leave.Id,
			PublicId = leave.PublicId,
			EmployeeId = leave.EmployeeId,
			EmployeeName = leave.Employee?.EmployeeNameWithFather ?? "Unknown",
			EmployeeCode = leave.Employee?.EmployeeCode ?? "Unknown",
			LeaveTypeName = leave.LeavePolicy?.LeaveType?.Type ?? "Unknown",
			LeaveTypeColor = leave.LeavePolicy?.LeaveType?.Color,
			StartDate = leave.StartDate.DateTime,
			EndDate = leave.EndDate.DateTime,
			TotalDays = CalculateDays(leave.StartDate.DateTime, leave.EndDate.DateTime, leave.HalfDay),
			Status = leave.Status.ToString().ToUpperInvariant(),
			MediumStageName = leave.Employee?.UserId is Guid mediumUserId && employeeMediumStageLookup.TryGetValue(mediumUserId, out var mediumStageName)
				? mediumStageName.MediumStageName
				: string.Empty
		}).ToList();

		return new LeaveManagementCalendarResultDto
		{
			TotalCount = totalCount,
			Items = items
		};
	}

	public async Task<byte[]> ExportToExcelAsync(LeaveManagementGetListInput input)
	{
		const int exportMax = 10_000;
		const int batch = 1_000;
		var allItems = new List<LeaveManagementDto>();
		var page = 1;

		while (allItems.Count < exportMax)
		{
			var paged = await GetListAsync(CloneListInputWithPage(input, page, batch));
			if (paged.Items.Count == 0)
			{
				break;
			}

			allItems.AddRange(paged.Items);
			if (allItems.Count >= paged.TotalCount || paged.Items.Count < batch)
			{
				break;
			}

			page++;
		}

		if (allItems.Count > exportMax)
		{
			allItems = allItems.Take(exportMax).ToList();
		}

		return hrExcelService.GenerateLeaveManagementExcel(allItems);
	}

	private static LeaveManagementGetListInput CloneListInputWithPage(LeaveManagementGetListInput input, int page, int pageSize)
	{
		return new LeaveManagementGetListInput
		{
			AcademicYearId = input.AcademicYearId,
			MonthYearAnchor = input.MonthYearAnchor,
			Search = input.Search,
			Page = page,
			PageSize = pageSize,
			UseExportBatching = true,
			Status = input.Status,
			LeavePolicyId = input.LeavePolicyId,
			LeaveTypeMasterId = input.LeaveTypeMasterId
		};
	}

	private async Task<Dictionary<Guid, (Guid MediumStageId, string MediumStageName)>> ResolveEmployeeMediumStageLookupAsync(
		IReadOnlyCollection<EmployeeLeave> leaves,
		Guid academicYearId)
	{
		var userIds = leaves
			.Select(x => x.Employee?.UserId)
			.Where(x => x.HasValue)
			.Select(x => x!.Value)
			.Distinct()
			.ToList();

		if (userIds.Count == 0)
		{
			return new Dictionary<Guid, (Guid MediumStageId, string MediumStageName)>();
		}

		var scopeAssignments = await AsyncExecuter.ToListAsync(
			(await userScopeAssignmentRepository.GetQueryableAsync())
				.Where(x =>
					x.IsActive &&
					!x.IsDeleted &&
					x.UserId.HasValue &&
					userIds.Contains(x.UserId.Value) &&
					x.EducationMediumStageId.HasValue &&
					(x.AcademicYearId == academicYearId || !x.AcademicYearId.HasValue))
				.Select(x => new
				{
					UserId = x.UserId!.Value,
					MediumStageId = x.EducationMediumStageId!.Value,
					x.AcademicYearId,
					x.CreationTime
				}));

		var selectedByUser = scopeAssignments
			.GroupBy(x => x.UserId)
			.Select(g => g
				.OrderByDescending(x => x.AcademicYearId == academicYearId)
				.ThenByDescending(x => x.CreationTime)
				.First())
			.ToList();

		var selectedByUserMap = selectedByUser.ToDictionary(x => x.UserId, x => x.MediumStageId);
		var missingUserIds = userIds.Where(x => !selectedByUserMap.ContainsKey(x)).ToList();
		if (missingUserIds.Count > 0)
		{
			var users = await identityUserRepository.GetListAsync(
				x => missingUserIds.Contains(x.Id),
				includeDetails: true);

			var roleIds = users
				.SelectMany(x => x.Roles)
				.Select(x => x.RoleId)
				.Distinct()
				.ToList();

			if (roleIds.Count > 0)
			{
				var roles = await identityRoleRepository.GetListAsync(x => roleIds.Contains(x.Id));
				var roleNamesById = roles
					.Where(x => roleIds.Contains(x.Id))
					.ToDictionary(x => x.Id, x => x.Name);
				var relevantRoleNames = roleNamesById.Values
					.Where(x => !string.IsNullOrWhiteSpace(x))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();

				if (relevantRoleNames.Count > 0)
				{
					var roleAssignments = await AsyncExecuter.ToListAsync(
						(await userScopeAssignmentRepository.GetQueryableAsync())
							.Where(x =>
								x.IsActive &&
								!x.IsDeleted &&
								x.RoleName != null &&
								relevantRoleNames.Contains(x.RoleName) &&
								x.EducationMediumStageId.HasValue &&
								(x.AcademicYearId == academicYearId || !x.AcademicYearId.HasValue))
							.Select(x => new
							{
								x.RoleName,
								MediumStageId = x.EducationMediumStageId!.Value,
								x.AcademicYearId,
								x.CreationTime
							}));

					var roleAssignmentByRoleName = roleAssignments
						.GroupBy(x => x.RoleName!, StringComparer.OrdinalIgnoreCase)
						.ToDictionary(
							g => g.Key,
							g => g
								.OrderByDescending(x => x.AcademicYearId == academicYearId)
								.ThenByDescending(x => x.CreationTime)
								.First(),
							StringComparer.OrdinalIgnoreCase);

					foreach (var user in users)
					{
						var mappedRoleNames = user.Roles
							.Select(x => roleNamesById.TryGetValue(x.RoleId, out var roleName) ? roleName : null)
							.Where(x => !string.IsNullOrWhiteSpace(x))
							.Cast<string>()
							.ToList();

						var resolvedStageIds = mappedRoleNames
							.Where(roleAssignmentByRoleName.ContainsKey)
							.Select(x => roleAssignmentByRoleName[x].MediumStageId)
							.Distinct()
							.ToList();

						// If multiple stage IDs resolve for the same user, keep it unset
						// to avoid arbitrary, order-dependent stage attribution.
						if (resolvedStageIds.Count == 1)
						{
							selectedByUserMap[user.Id] = resolvedStageIds[0];
						}
					}
				}
			}
		}

		if (selectedByUserMap.Count == 0)
		{
			return new Dictionary<Guid, (Guid MediumStageId, string MediumStageName)>();
		}

		var stageIds = selectedByUserMap.Values.Distinct().ToList();
		var stages = await AsyncExecuter.ToListAsync(
			(await educationMediumStageRepository.GetQueryableAsync())
				.Where(x => stageIds.Contains(x.Id))
				.Select(x => new { x.Id, x.Name }));
		var stageNames = stages.ToDictionary(x => x.Id, x => x.Name);

		var lookup = new Dictionary<Guid, (Guid MediumStageId, string MediumStageName)>();
		foreach (var item in selectedByUserMap)
		{
			if (stageNames.TryGetValue(item.Value, out var name))
			{
				lookup[item.Key] = (item.Value, name);
			}
		}

		return lookup;
	}

	private async Task EnsureMayCreateLeaveOnBehalfAsync(Guid targetEmployeeId, LeaveActorContext actorContext)
	{
		if (actorContext.IsAdminBypass)
		{
			return;
		}

		if (targetEmployeeId == actorContext.ActorEmployeeId)
		{
			return;
		}

		await leaveValidationService.EnsureActorMayChangeLeaveStatusAsync(
			targetEmployeeId,
			actorContext.ActorEmployeeId!.Value);
	}

	private async Task<LeaveActorContext> ResolveLeaveActorContextAsync()
	{
		var currentUserId = CurrentUser.Id
			?? throw new BusinessException(HRErrorCodes.LeaveUserNotAuthenticated);

		var actorEmployee = await employeeRepository.GetEmployeeByUserIdAsync(currentUserId);
		var isAdminBypass = IsAdminBypassUser();

		if (actorEmployee is null && !isAdminBypass)
		{
			throw new BusinessException(HRErrorCodes.LeaveEmployeeNotFound);
		}

		return new LeaveActorContext(isAdminBypass, actorEmployee?.Id);
	}

	private bool IsAdminBypassUser()
	{
		return CurrentUser.IsInRole("admin")
			|| CurrentUser.IsInRole("Administrator");
	}

	private readonly record struct LeaveActorContext(bool IsAdminBypass, Guid? ActorEmployeeId);

	private static LeaveRequestDetailsDto MapToDetailsDto(EmployeeLeave leave)
	{
		var policyId = leave.LeaveTypeId;
		return new LeaveRequestDetailsDto
		{
			Id = leave.Id,
			PublicId = leave.PublicId,
			MediumStageId = leave.LeavePolicy?.AcademicYearId ?? Guid.Empty,
			MediumStageName = leave.LeavePolicy?.AcademicYear?.Name ?? "",
			EmployeeId = leave.EmployeeId,
			EmployeeName = leave.Employee?.EmployeeNameWithFather ?? "Unknown",
			EmployeeCode = leave.Employee?.EmployeeCode ?? "Unknown",
			LeavePolicyId = policyId,
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
		if (attachment == null)
		{
			return null;
		}

		if (attachment is byte[] bytes)
		{
			return bytes;
		}

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
		{
			return null;
		}

		var length = (long)(lengthProp.GetValue(attachment) ?? 0L);
		var fileName = (string?)fileNameProp.GetValue(attachment);

		if (length == 0 || string.IsNullOrEmpty(fileName))
		{
			return null;
		}

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
		{
			throw new BusinessException(HRErrorCodes.LeaveFileTooLarge);
		}

		if (fileSize == 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveFileEmpty);
		}

		var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
		{
			throw new BusinessException(HRErrorCodes.LeaveFileInvalidExtension);
		}

		if (fileName.Length > 255)
		{
			throw new BusinessException(HRErrorCodes.LeaveFileNameTooLong);
		}

		if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveFileInvalidCharacters);
		}
	}

	private static void ValidateFileContent(byte[] fileBytes, string fileName)
	{
		if (fileBytes.Length < 4)
		{
			throw new BusinessException(HRErrorCodes.LeaveFileTooSmall);
		}

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
		{
			throw new BusinessException(HRErrorCodes.LeaveFileContentMismatch);
		}
	}

	private static (string ContentType, string Extension) DetectContentType(byte[] fileBytes)
	{
		if (fileBytes.Length >= 4 &&
		    fileBytes[0] == 0x25 && fileBytes[1] == 0x50 && fileBytes[2] == 0x44 && fileBytes[3] == 0x46)
		{
			return ("application/pdf", ".pdf");
		}

		if (fileBytes.Length >= 3 && fileBytes[0] == 0xFF && fileBytes[1] == 0xD8 && fileBytes[2] == 0xFF)
		{
			return ("image/jpeg", ".jpg");
		}

		if (fileBytes.Length >= 4 &&
		    fileBytes[0] == 0x89 && fileBytes[1] == 0x50 && fileBytes[2] == 0x4E && fileBytes[3] == 0x47)
		{
			return ("image/png", ".png");
		}

		return ("application/octet-stream", ".bin");
	}

	private static int CalculateDays(DateTime startDate, DateTime endDate, bool isHalfDay)
	{
		return LeaveDurationCalculator.CountLeaveDays(startDate, endDate, isHalfDay);
	}
}
