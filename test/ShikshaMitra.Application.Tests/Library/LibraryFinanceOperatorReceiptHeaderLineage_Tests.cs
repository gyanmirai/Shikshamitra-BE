using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Operator_Receipt_Should_Reject_A_Same_Source_Same_Amount_Header_Redirection()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(context);
		using var actor = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		var firstInput = CashCollectionInput(context, form.Revision);
		var first = await WithUnitOfWorkAsync(() => TypedService(context.Owner).CollectAsync(firstInput));
		form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		var secondInput = CashCollectionInput(context, form.Revision);
		var second = await WithUnitOfWorkAsync(() => TypedService(context.Owner).CollectAsync(secondInput));
		first.CollectionId.ShouldNotBe(second.CollectionId);
		first.ReceiptId.ShouldNotBe(second.ReceiptId);
		first.ReceiptNumber.ShouldNotBe(second.ReceiptNumber);
		firstInput.IdempotencyKey.ShouldNotBe(secondInput.IdempotencyKey);
		firstInput.Revision.ShouldNotBe(secondInput.Revision);
		first.AcceptedAmount.ShouldBe(40.25m);
		second.AcceptedAmount.ShouldBe(40.25m);
		second.Receivable.OriginalAmount.ShouldBe(100.75m);
		second.Receivable.SettledPaymentAmount.ShouldBe(80.50m);
		second.Receivable.OutstandingAmount.ShouldBe(20.25m);
		var services = new DownstreamRefundServices(new FinanceServices(ServiceProvider, context.Owner, useStoredSource: true));
		await WithUnitOfWorkAsync(async () =>
		{
			var permissions = services.GetRequiredService<IPermissionChecker>();
			(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
			(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View)).ShouldBeTrue();
		});
		var receipts = ActivatorUtilities.CreateInstance<FeeReceiptAppService>(services);
		receipts.LazyServiceProvider = new AbpLazyServiceProvider(services);
		(await WithUnitOfWorkAsync(() => receipts.GetAsync(first.ReceiptId))).FeeCollectionTransactionId.ShouldBe(first.CollectionId);
		(await WithUnitOfWorkAsync(() => receipts.GetAsync(second.ReceiptId))).FeeCollectionTransactionId.ShouldBe(second.CollectionId);
		var evidence = await WithUnitOfWorkAsync(async () =>
		{
			var headers = GetRequiredService<IRepository<FeeRecordMetadata, Guid>>();
			var firstHeader = await headers.GetAsync(first.CollectionId);
			var secondHeader = await headers.GetAsync(second.CollectionId);
			var link = (await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
				.GetListAsync(row => row.TenantId == context.Owner.TenantId && row.SourceId == context.Owner.AssessmentId)).Single();
			var commands = await GetRequiredService<IRepository<FinanceExternalReceivableCommand, Guid>>()
				.GetListAsync(row => row.TenantId == context.Owner.TenantId && row.SourceLinkId == link.Id && row.Operation == "Collect");
			commands.Count.ShouldBe(2);
			var firstCommand = commands.Single(row => row.ResultEntityId == first.ReceiptId);
			var secondCommand = commands.Single(row => row.ResultEntityId == second.ReceiptId);
			firstCommand.ResultEntityType.ShouldBe("Receipt");
			secondCommand.ResultEntityType.ShouldBe("Receipt");
			firstHeader.ReceiptNumber.ShouldBe(first.ReceiptNumber);
			secondHeader.ReceiptNumber.ShouldBe(second.ReceiptNumber);
			firstHeader.IdempotencyKey.ShouldBe(firstInput.IdempotencyKey);
			secondHeader.IdempotencyKey.ShouldBe(secondInput.IdempotencyKey);
			firstHeader.CommandFingerprint.ShouldBe(firstCommand.CommandFingerprint);
			secondHeader.CommandFingerprint.ShouldBe(secondCommand.CommandFingerprint);
			firstCommand.CommandFingerprint.ShouldNotBe(secondCommand.CommandFingerprint);
			foreach (var header in new[] { firstHeader, secondHeader })
			{
				header.EmployeeId.ShouldBe(context.Owner.PersonId);
				header.StudentId.ShouldBeNull();
				header.FinanceFeeAccountId.ShouldBeNull();
				header.AcademicYearId.ShouldBe(context.Owner.YearId);
				header.Currency.ShouldBe("INR");
				header.TotalPaidFee.ShouldBe(40.25m);
				var allocation = (await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>()
					.GetListAsync(row => row.FeeRecordMetadataId == header.Id)).Single();
				allocation.ChargeId.ShouldBe(context.Posted.FinanceChargeId);
				allocation.EmployeeId.ShouldBe(context.Owner.PersonId);
				allocation.Amount.ShouldBe(40.25m);
			}
			(await headers.CountAsync()).ShouldBe(2);
			(await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().CountAsync()).ShouldBe(2);
			(await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().CountAsync()).ShouldBe(2);
			(await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().CountAsync()).ShouldBe(0);
			return (FirstCommandId: firstCommand.Id, SecondCommandId: secondCommand.Id,
				FirstFingerprint: firstCommand.CommandFingerprint, SecondFingerprint: secondCommand.CommandFingerprint,
				FirstResultJson: firstHeader.CommandResultJson, SecondResultJson: secondHeader.CommandResultJson);
		});

		// The only changed fact is an existing public, persisted receipt property.
		// Neither immutable source/command nor either real money header is modified.
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<FeePaymentReceipt, Guid>>();
			var receipt = await repository.GetAsync(first.ReceiptId);
			receipt.FeeCollectionTransactionId = second.CollectionId;
			await repository.UpdateAsync(receipt, true);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var redirected = await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().GetAsync(first.ReceiptId);
			redirected.FeeCollectionTransactionId.ShouldBe(second.CollectionId);
			redirected.ReceiptNumber.ShouldBe(first.ReceiptNumber);
			redirected.TotalPaidAmount.ShouldBe(40.25m);
			var commands = GetRequiredService<IRepository<FinanceExternalReceivableCommand, Guid>>();
			var original = await commands.GetAsync(evidence.FirstCommandId);
			original.ResultEntityId.ShouldBe(first.ReceiptId);
			original.CommandFingerprint.ShouldBe(evidence.FirstFingerprint);
			var other = await commands.GetAsync(evidence.SecondCommandId);
			other.ResultEntityId.ShouldBe(second.ReceiptId);
			other.CommandFingerprint.ShouldBe(evidence.SecondFingerprint);
			var headers = GetRequiredService<IRepository<FeeRecordMetadata, Guid>>();
			(await headers.GetAsync(first.CollectionId)).CommandResultJson.ShouldBe(evidence.FirstResultJson);
			(await headers.GetAsync(second.CollectionId)).CommandResultJson.ShouldBe(evidence.SecondResultJson);
			(await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(context.Owner.AssessmentId))
				.GetAssessmentVersion().ShouldBe(context.Post.Source.SourceVersion);
		});

		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => receipts.GetAsync(first.ReceiptId)));
	}
}
