using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Finance;

public partial class FinanceScopeAccessService
{
	public async Task EnsureReceiptAccessibleAsync(FeePaymentReceipt receipt, CancellationToken cancellationToken = default)
	{
		if (!CurrentTenant.Id.HasValue || receipt.TenantId != CurrentTenant.Id) throw new AbpAuthorizationException();
		await EnsureCollectionYearAccessibleAsync(receipt.AcademicYearId);
		if (receipt.EmployeeId.HasValue)
		{
			if (await EnsureExternalEmployeeReceiptAccessibleAsync(receipt, cancellationToken)) return;
			if (receipt.StudentId.HasValue || receipt.FinanceFeeAccountId.HasValue ||
				!await AsyncExecuter.AnyAsync((await GetScopedEmployeeIdsQueryAsync())
					.Where(id => id == receipt.EmployeeId.Value), cancellationToken)) throw new AbpAuthorizationException();
			return;
		}
		await EnsureCollectionPayerAccessibleAsync(receipt.StudentId, receipt.FinanceFeeAccountId, cancellationToken);
	}
}
