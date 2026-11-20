using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Mobile;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceChargeSettlementAccessService(
	IRepository<Charge, Guid> charges,
	IRepository<FinanceFeeAccount, Guid> accounts,
	FinanceScopeAccessService scope,
	MobileFinanceAccessService familyAccess,
	FinanceExternalSettlementGuard externalSettlementGuard,
	ICurrentTenant tenant,
	IAsyncQueryableExecuter queries) : ITransientDependency
{
	public async Task EnsureLegacySourcesAsync(IEnumerable<Guid> chargeIds, Guid? selectedStudentId = null,
		Guid? selectedYearId = null, Guid? selectedAccountId = null, bool includesLateFeeSelections = false)
	{
		var ids = chargeIds.Distinct().ToArray();
		if (ids.Length == 0) return;
		if (!tenant.Id.HasValue) throw Denied();
		var rows = await queries.ToListAsync((await charges.GetQueryableAsync())
			.Where(row => row.TenantId == tenant.Id && ids.Contains(row.Id))
			.Select(row => new { row.Id, row.StudentId, row.EmployeeId, row.AcademicYearId }));
		if (!includesLateFeeSelections && rows.Count != ids.Length) throw Denied();
		if (rows.Count == 0) return;
		var expectedStudent = selectedStudentId;
		if (selectedAccountId.HasValue)
		{
			var account = await accounts.FindAsync(selectedAccountId.Value, includeDetails: false);
			if (account == null || account.TenantId != tenant.Id || selectedYearId != account.AcademicYearId ||
				selectedStudentId.HasValue && selectedStudentId != account.StudentId) throw Denied();
			await scope.EnsureFeeAccountAccessibleAsync(account);
			expectedStudent = account.StudentId;
		}
		if (selectedYearId.HasValue && !expectedStudent.HasValue) throw Denied();
		if (rows.Any(row => selectedYearId.HasValue && row.AcademicYearId != selectedYearId ||
			expectedStudent.HasValue && (row.StudentId != expectedStudent || row.EmployeeId.HasValue))) throw Denied();
		foreach (var yearId in rows.Select(row => row.AcademicYearId).Distinct())
			await scope.EnsureCollectionYearAccessibleAsync(yearId);
		var studentIds = rows.Where(row => row.StudentId.HasValue).Select(row => row.StudentId!.Value).ToArray();
		var allowedStudents = studentIds.Length == 0 ? [] : await queries.ToListAsync(
			(await scope.GetScopedStudentIdsQueryAsync()).Where(id => studentIds.Contains(id)));
		var employeeIds = rows.Where(row => row.EmployeeId.HasValue).Select(row => row.EmployeeId!.Value).ToArray();
		var allowedEmployees = employeeIds.Length == 0 ? [] : await queries.ToListAsync(
			(await scope.GetScopedEmployeeIdsQueryAsync()).Where(id => employeeIds.Contains(id)));
		if (rows.Any(row => row.StudentId.HasValue == row.EmployeeId.HasValue ||
			row.StudentId.HasValue && !allowedStudents.Contains(row.StudentId.Value) ||
			row.EmployeeId.HasValue && !allowedEmployees.Contains(row.EmployeeId.Value))) throw Denied();
		await externalSettlementGuard.EnsureLegacyChargesAsync(rows.Select(row => row.Id));
	}

	private static AbpAuthorizationException Denied() => new("Finance charge source access denied.");

	public async Task EnsureVerifiedFamilySourcesAsync(IEnumerable<Guid> chargeIds, Guid studentId, Guid yearId)
	{
		var ids = chargeIds.Distinct().ToArray();
		if (ids.Length == 0) return;
		await familyAccess.RequireAsync(studentId, yearId, guardianOnly: true,
			permission: MobileFinancePermissions.RequestRefund);
		var matched = await queries.CountAsync((await charges.GetQueryableAsync()).Where(row =>
			row.TenantId == tenant.Id && ids.Contains(row.Id) && row.StudentId == studentId &&
			!row.EmployeeId.HasValue && row.AcademicYearId == yearId));
		if (matched != ids.Length) throw Denied();
		await externalSettlementGuard.EnsureLegacyChargesAsync(ids);
	}
}
