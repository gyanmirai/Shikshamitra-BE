using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.Finance.Audit;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.Finance.Audit;

public class FinanceAuditTrailAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IFinanceAuditTrailAppService _service;

	public FinanceAuditTrailAppService_Tests()
	{
		_service = GetRequiredService<IFinanceAuditTrailAppService>();
	}

	[Fact]
	public async Task GetByCorrelationAsync_Should_Throw_When_CorrelationId_Empty()
	{
		await Should.ThrowAsync<BusinessException>(async () =>
			await _service.GetByCorrelationAsync(" ", new PagedAndSortedResultRequestDto()));
	}
}
