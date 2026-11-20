using System;
using System.Net.Http;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using Shouldly;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	private static async Task AssertFinancePgRefundSourceListAsync(HttpClient client, Guid yearId,
		Guid sourceLinkId, Guid requestId)
	{
		var route = $"/api/fee-refunds?academicYearId={yearId:D}&maxResultCount=1";
		var selected = await FinancePgGetAsync<PagedResultDto<FeeRefundRequestDto>>(client,
			$"{route}&financeExternalSourceLinkId={sourceLinkId:D}");
		selected.TotalCount.ShouldBe(1);
		selected.Items.Count.ShouldBe(1);
		selected.Items[0].Id.ShouldBe(requestId);
		selected.Items[0].FinanceExternalSourceLinkId.ShouldBe(sourceLinkId);
		var next = await FinancePgGetAsync<PagedResultDto<FeeRefundRequestDto>>(client,
			$"{route}&financeExternalSourceLinkId={sourceLinkId:D}&skipCount=1");
		next.TotalCount.ShouldBe(1);
		next.Items.ShouldBeEmpty();
		var absent = await FinancePgGetAsync<PagedResultDto<FeeRefundRequestDto>>(client,
			$"{route}&financeExternalSourceLinkId={Guid.NewGuid():D}");
		absent.TotalCount.ShouldBe(0);
		absent.Items.ShouldBeEmpty();
	}
}
