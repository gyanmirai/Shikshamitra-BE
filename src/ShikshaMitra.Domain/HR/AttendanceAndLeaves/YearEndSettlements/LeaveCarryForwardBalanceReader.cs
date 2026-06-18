using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;

public class LeaveCarryForwardBalanceReader(
	IRepository<LeaveYearEndSettlementItem, Guid> settlementItemRepository,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<IReadOnlyDictionary<Guid, decimal>> GetCarryInByRuleAsync(
		Guid employeeId,
		Guid academicYearId,
		IReadOnlyCollection<Guid> leavePolicyRuleIds,
		CancellationToken cancellationToken = default)
	{
		if (leavePolicyRuleIds.Count == 0)
		{
			return new Dictionary<Guid, decimal>();
		}

		var ruleIds = leavePolicyRuleIds.ToList();
		var query = await settlementItemRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(query.Where(x =>
			x.EmployeeId == employeeId &&
			x.TargetAcademicYearId == academicYearId &&
			x.TargetLeavePolicyRuleId.HasValue &&
			ruleIds.Contains(x.TargetLeavePolicyRuleId.Value) &&
			x.Status == LeaveYearEndSettlementItemStatus.Finalized &&
			x.CarriedDays > 0m), cancellationToken);

		return rows
			.GroupBy(x => x.TargetLeavePolicyRuleId!.Value)
			.ToDictionary(x => x.Key, x => x.Sum(item => item.CarriedDays));
	}
}
