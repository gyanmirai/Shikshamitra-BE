using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Concessions;

public class ConcessionFinancialConcurrency_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ShikshaMitraDbContext _dbContext;

	public ConcessionFinancialConcurrency_Tests()
	{
		_dbContext = GetRequiredService<ShikshaMitraDbContext>();
	}

	[Fact]
	public void Due_fee_concurrency_stamp_must_be_an_ef_concurrency_token()
	{
		var entityType = _dbContext.Model.FindEntityType(typeof(DueFee));
		var concurrencyStamp = entityType?.FindProperty(nameof(DueFee.ConcurrencyStamp));

		entityType.ShouldNotBeNull();
		concurrencyStamp.ShouldNotBeNull();
		concurrencyStamp.IsConcurrencyToken.ShouldBeTrue();
	}

	[Fact]
	public void Concession_request_concurrency_stamp_must_be_an_ef_concurrency_token()
	{
		var entityType = _dbContext.Model.FindEntityType(typeof(ConcessionRequest));
		var concurrencyStamp = entityType?.FindProperty(nameof(ConcessionRequest.ConcurrencyStamp));

		entityType.ShouldNotBeNull();
		concurrencyStamp.ShouldNotBeNull();
		concurrencyStamp.IsConcurrencyToken.ShouldBeTrue();
	}
}
