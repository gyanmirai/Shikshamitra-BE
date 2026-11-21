using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundAppService
{
	private Task EnsureLegacyRefundSourcesAsync(IEnumerable<Guid?> chargeIds, Guid? studentId = null,
		Guid? yearId = null, Guid? accountId = null, bool familyAuthorized = false)
	{
		var ids = chargeIds.Where(id => id.HasValue).Select(id => id!.Value);
		var access = LazyServiceProvider.LazyGetRequiredService<FinanceChargeSettlementAccessService>();
		if (!familyAuthorized) return access.EnsureLegacySourcesAsync(ids, studentId, yearId, accountId);
		if (!studentId.HasValue || !yearId.HasValue) throw new AbpAuthorizationException();
		return access.EnsureVerifiedFamilySourcesAsync(ids, studentId.Value, yearId.Value);
	}

	private Task EnsureLegacyRefundSourcesAsync(FeeRefundRequest request, bool familyAuthorized = false) =>
		EnsureLegacyRefundSourcesAsync(request.Lines.Select(line => line.ChargeId), request.StudentId,
			request.AcademicYearId, request.FinanceFeeAccountId, familyAuthorized);
}
