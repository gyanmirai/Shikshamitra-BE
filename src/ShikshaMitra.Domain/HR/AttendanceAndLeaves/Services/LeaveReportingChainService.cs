using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveReportingChainService(IEmployeeRepository employeeRepository) : DomainService
{
	private const int MaxReportingHierarchyDepth = 64;

	public async Task EnsureActorMayChangeLeaveStatusAsync(
		Guid leaveRequesterEmployeeId,
		Guid actorEmployeeId,
		CancellationToken cancellationToken = default)
	{
		if (leaveRequesterEmployeeId == actorEmployeeId)
		{
			throw new BusinessException(HRErrorCodes.LeaveApproverNotInReportingChain);
		}

		var approverIds = await BuildApproverEmployeeIdSetAsync(leaveRequesterEmployeeId, cancellationToken);
		if (!approverIds.Contains(actorEmployeeId))
		{
			throw new BusinessException(HRErrorCodes.LeaveApproverNotInReportingChain);
		}
	}

	private async Task<HashSet<Guid>> BuildApproverEmployeeIdSetAsync(
		Guid requesterEmployeeId,
		CancellationToken cancellationToken)
	{
		var approvers = new HashSet<Guid>();
		var requester = await employeeRepository.FindAsync(requesterEmployeeId, includeDetails: false, cancellationToken);
		if (requester == null)
		{
			return approvers;
		}

		Guid? walker = requester.ReportingManagerId;
		var visited = new HashSet<Guid> { requesterEmployeeId };
		var depth = 0;

		while (walker != null && depth < MaxReportingHierarchyDepth)
		{
			depth++;
			if (!visited.Add(walker.Value))
			{
				throw new BusinessException(HRErrorCodes.LeaveReportingManagerCycle);
			}

			approvers.Add(walker.Value);
			var next = await employeeRepository.FindAsync(walker.Value, includeDetails: false, cancellationToken);
			if (next == null)
			{
				break;
			}

			walker = next.ReportingManagerId;
		}

		if (depth >= MaxReportingHierarchyDepth && walker != null)
		{
			throw new BusinessException(HRErrorCodes.LeaveReportingManagerCycle);
		}

		return approvers;
	}
}
