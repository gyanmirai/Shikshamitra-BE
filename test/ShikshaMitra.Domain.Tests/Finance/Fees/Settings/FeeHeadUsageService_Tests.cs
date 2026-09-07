using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Domain.Finance.Fees.Settings.Managers;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Entities;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Domain.Tests.Finance.Fees.Settings;

public class FeeHeadUsageService_Tests
{
	[Fact]
	public async Task Should_Report_Every_Persisted_Downstream_Reference_With_Its_Identifier()
	{
		var feeHeadId = Guid.NewGuid();
		var requestId = Guid.NewGuid();
		var dueFeeId = Guid.NewGuid();
		var feeStructure = new FeeStructureDetail(
			Guid.NewGuid(), null, Guid.NewGuid(), feeHeadId, 100, false, false, null, null);
		var dueFee = new DueFee(
			dueFeeId, null, Guid.NewGuid(), feeHeadId, MonthEnum.April, 2026, false, 100, DateTime.UtcNow);
		var collection = new FeeCollectionRecord(Guid.NewGuid()) { FeeHeadId = feeHeadId };
		var concessionMapping = new ConcessionFeeHeadMapping(
			Guid.NewGuid(), null, requestId, feeHeadId, 100, 10, 90, DiscountBasis.FlatAmount, 10);
		var concessionAllocation = new ConcessionDueFeeAllocation(
			Guid.NewGuid(), null, requestId, dueFeeId, feeHeadId, 10);
		var source = new FeeAdjustmentSourceLine(
			Guid.NewGuid(), requestId, dueFeeId, feeHeadId, MonthEnum.April, 2026, 5);
		var target = new FeeAdjustmentTargetAllocation(
			Guid.NewGuid(), requestId, dueFeeId, feeHeadId, MonthEnum.April, 2026, 5, 1, DateTime.UtcNow);
		var policyBuilder = new CollectionPolicyUpdateModelBuilder
		{
			LateFeeApplyOnFeeHeads = [feeHeadId]
		};
		var policy = new CollectionPolicy(Guid.NewGuid(), null, Guid.NewGuid(), policyBuilder.Build());

		var policyRepository = Repository([policy]);
		policyRepository.GetListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns([policy]);
		var dataFilter = Substitute.For<IDataFilter>();
		dataFilter.Disable<ISoftDelete>().Returns(Substitute.For<IDisposable>());
		var service = new FeeHeadUsageService(
			Repository([feeStructure]),
			Repository([dueFee]),
			Repository([collection]),
			Repository([concessionMapping]),
			Repository([concessionAllocation]),
			Repository([source]),
			Repository([target]),
			policyRepository,
			dataFilter);

		var usage = await service.GetUsageAsync([feeHeadId]);

		dataFilter.Received(1).Disable<ISoftDelete>();
		usage[feeHeadId].Count.ShouldBe(8);
		foreach (var referenceId in new[]
		{
			feeStructure.Id,
			dueFee.Id,
			collection.Id,
			concessionMapping.Id,
			concessionAllocation.Id,
			source.Id,
			target.Id,
			policy.Id
		})
		{
			usage[feeHeadId].ShouldContain(value =>
				value.Contains(referenceId.ToString("D"), StringComparison.OrdinalIgnoreCase));
		}
	}

	private static IRepository<TEntity, Guid> Repository<TEntity>(List<TEntity> items)
		where TEntity : class, IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<TEntity, Guid>>();
		repository.GetListAsync(
				Arg.Any<Expression<Func<TEntity, bool>>>(),
				Arg.Any<bool>(),
				Arg.Any<CancellationToken>())
			.Returns(call =>
			{
				var predicate = call.Arg<Expression<Func<TEntity, bool>>>().Compile();
				return items.Where(predicate).ToList();
			});
		return repository;
	}
}
