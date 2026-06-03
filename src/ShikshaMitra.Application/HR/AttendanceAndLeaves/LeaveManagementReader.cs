using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Linq;
using static ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementRequestRules;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveManagementReader(
	IEmployeeLeaveRepository employeeLeaveRepository,
	IAcademicYearRepository academicYearRepository,
	LeaveActorAccessService leaveActorAccessService,
	LeaveSubmitterResolver leaveSubmitterResolver,
	LeaveMediumStageResolver leaveMediumStageResolver,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<PagedLeaveManagementResultDto> GetListAsync(LeaveManagementGetListInput input)
	{
		var actorContext = await leaveActorAccessService.ResolveContextAsync();
		if (input.EmployeeId.HasValue)
		{
			await leaveActorAccessService.EnsureActorCanAccessEmployeeAsync(input.EmployeeId.Value);
			return await GetListInternalAsync(input, input.EmployeeId.Value, scopedEmployeeIds: null);
		}

		var scopedEmployeeIds = await leaveActorAccessService.ResolveScopedEmployeeIdsAsync(actorContext);
		return await GetListInternalAsync(input, scopedEmployeeId: null, scopedEmployeeIds);
	}

	public async Task<PagedLeaveManagementResultDto> GetMyListAsync(LeaveManagementGetListInput input)
	{
		var actorEmployeeId = await leaveActorAccessService.ResolveCurrentActorEmployeeIdStrictAsync();
		return await GetListInternalAsync(input, actorEmployeeId, scopedEmployeeIds: null);
	}

	public async Task<LeaveRequestDetailsDto> GetAsync(Guid leaveRequestId)
	{
		var query = await employeeLeaveRepository.WithDetailsAsync(
			x => x.Employee!,
			x => x.LeavePolicy!,
			x => x.LeavePolicy!.LeaveType!,
			x => x.LeavePolicy!.AcademicYear!);

		var leave = await asyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.Id == leaveRequestId))
			?? throw new EntityNotFoundException(typeof(EmployeeLeave), leaveRequestId);
		await leaveActorAccessService.EnsureActorCanAccessEmployeeAsync(leave.EmployeeId);

		var details = await MapToDetailsDtoAsync(leave);
		var academicYearId = leave.LeavePolicy?.AcademicYearId;
		if (academicYearId.HasValue)
		{
			var lookup = await leaveMediumStageResolver.ResolveAsync([leave], academicYearId.Value);
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

	public async Task<LeaveManagementCalendarResultDto> GetCalendarAsync(LeaveManagementCalendarInput input)
	{
		ValidateCalendarPaging(input);
		var academicYear = await academicYearRepository.FindAsync(input.AcademicYearId)
			?? throw new EntityNotFoundException(typeof(AcademicYear), input.AcademicYearId);

		var range = ResolveCalendarDateRange(input, academicYear);
		var rangeStartOffset = new DateTimeOffset(
			range.Start.Year, range.Start.Month, range.Start.Day, 0, 0, 0, input.RangeStart.Offset);
		var rangeEndOffset = new DateTimeOffset(
			range.End.Year, range.End.Month, range.End.Day, 23, 59, 59, 999, input.RangeEnd.Offset);

		var actorContext = await leaveActorAccessService.ResolveContextAsync();
		var scopedEmployeeIds = await leaveActorAccessService.ResolveScopedEmployeeIdsAsync(actorContext);

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
			input.PageSize,
			employeeId: null,
			employeeIds: scopedEmployeeIds);

		var detailQuery = await employeeLeaveRepository.WithDetailsAsync(
			x => x.Employee!,
			x => x.LeavePolicy!,
			x => x.LeavePolicy!.AcademicYear!,
			x => x.LeavePolicy!.LeaveType!);

		var leaves = await asyncExecuter.ToListAsync(
			detailQuery.Where(x => pageIds.Contains(x.Id)));

		var idOrder = pageIds.Select((id, index) => (id, index)).ToDictionary(t => t.id, t => t.index);
		leaves = leaves.OrderBy(l => idOrder[l.Id]).ToList();
		var employeeMediumStageLookup = await leaveMediumStageResolver.ResolveAsync(
			leaves,
			input.AcademicYearId);

		var items = leaves
			.Select(leave => LeaveManagementProjection.ToCalendarItemDto(leave, employeeMediumStageLookup))
			.ToList();

		return new LeaveManagementCalendarResultDto
		{
			TotalCount = totalCount,
			Items = items
		};
	}

	private async Task<PagedLeaveManagementResultDto> GetListInternalAsync(
		LeaveManagementGetListInput input,
		Guid? scopedEmployeeId,
		IReadOnlyCollection<Guid>? scopedEmployeeIds)
	{
		ValidateListPaging(input);
		var academicYear = await academicYearRepository.FindAsync(input.AcademicYearId)
			?? throw new EntityNotFoundException(typeof(AcademicYear), input.AcademicYearId);
		var range = ResolveListDateRange(input, academicYear);

		var skipCount = (input.Page - 1) * input.PageSize;

		var filters = new LeaveManagementListFilters(
			input.AcademicYearId,
			input.Status,
			input.LeavePolicyId,
			input.LeaveTypeMasterId);

		var (totalCount, pageIds) = await employeeLeaveRepository.GetPagedIdsForManagementListAsync(
			range.Start,
			range.End,
			filters,
			input.Search,
			skipCount,
			input.PageSize,
			scopedEmployeeId,
			scopedEmployeeIds);

		var detailQuery = await employeeLeaveRepository.WithDetailsAsync(
			x => x.Employee!,
			x => x.LeavePolicy!,
			x => x.LeavePolicy!.AcademicYear!,
			x => x.LeavePolicy!.LeaveType!);

		var leaveRequests = await asyncExecuter.ToListAsync(
			detailQuery.Where(x => pageIds.Contains(x.Id)));

		var idOrder = pageIds.Select((id, index) => (id, index)).ToDictionary(t => t.id, t => t.index);
		leaveRequests = leaveRequests.OrderBy(l => idOrder[l.Id]).ToList();
		var submitterLookup = await leaveSubmitterResolver.ResolveForLeavesAsync(leaveRequests);

		var items = leaveRequests
			.Select(leave =>
			{
				var submitter = submitterLookup.TryGetValue(leave.Id, out var resolvedSubmitter)
					? resolvedSubmitter
					: ("System", (string?)null);
				return LeaveManagementProjection.ToManagementDto(leave, submitter);
			})
			.ToList();

		return new PagedLeaveManagementResultDto { TotalCount = totalCount, Items = items };
	}

	private async Task<LeaveRequestDetailsDto> MapToDetailsDtoAsync(EmployeeLeave leave)
	{
		var submitterLookup = await leaveSubmitterResolver.ResolveByCreatorIdsAsync(
			leave.CreatorId.HasValue
				? [leave.CreatorId.Value]
				: []);
		var submitter = leave.CreatorId.HasValue && submitterLookup.TryGetValue(leave.CreatorId.Value, out var resolvedSubmitter)
			? resolvedSubmitter
			: ("System", (string?)null);

		return LeaveManagementProjection.ToDetailsDto(leave, submitter);
	}
}
