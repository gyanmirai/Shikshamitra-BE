using System;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public class AdmissionFinanceHandoffDependency_Tests
	: ShikshaMitraApplicationTestBase<AdmissionFinanceHandoffDependencyTestModule>
{
	[Fact]
	public async Task Finalization_without_a_prepared_fee_account_does_not_construct_collection_services_or_create_financial_records()
	{
		using var unit = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var accounts = GetRequiredService<IRepository<FinanceFeeAccount, Guid>>();
		var before = await accounts.GetCountAsync();
		using var scope = ServiceProvider.GetRequiredService<ILifetimeScope>().BeginLifetimeScope(builder =>
		{
			foreach (var type in new[] { typeof(IAdmissionFinanceContextReader), typeof(IDueFeeRepository),
				typeof(IRepository<FeeRecordMetadata, Guid>), typeof(IRepository<FeeCollectionDueFeeAllocation, Guid>),
				typeof(IRepository<FeePaymentReceipt, Guid>), typeof(IRepository<PaymentMethodRecord, Guid>),
				typeof(IReadOnlyRepository<AcademicYear, Guid>), typeof(IRepository<FeeStructureDetail, Guid>),
				typeof(IRepository<FeeHead, Guid>) })
				builder.Register<object>(_ => throw new InvalidOperationException($"Unused fee ledger dependency constructed: {type.Name}"))
					.As(type);
		});
		var handoff = scope.Resolve<IAdmissionFinanceIntegration>();
		await handoff.LinkFinalizedStudentAsync(Guid.NewGuid(), Guid.NewGuid());
		await unit.SaveChangesAsync();
		(await accounts.GetCountAsync()).ShouldBe(before);
	}
}
