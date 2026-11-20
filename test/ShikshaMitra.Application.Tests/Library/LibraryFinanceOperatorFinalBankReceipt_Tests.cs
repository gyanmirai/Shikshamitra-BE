using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Operator_Final_Bank_Receipt_Get_And_Pdf_Should_Follow_The_Recorded_Source_After_Actual_Verification(bool currentPlacement)
	{
		var fixture = await SeedMovedVerifiedBankReceiptAsync();
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await SetFinalBankReceiptReaderScopeAsync(fixture, currentPlacement);
		await AssertFinalBankReceiptAuthorityAsync(fixture, currentPlacement);
		await AssertFinalBankReceiptLineageAsync(fixture);
		var services = new DownstreamRefundServices(new FinanceServices(ServiceProvider, owner, useStoredSource: true));
		var service = ActivatorUtilities.CreateInstance<FeeReceiptAppService>(services);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);

		// Real action grants are checked above. Manual construction isolates the
		// receipt data-scope body; separate HTTP tests own attribute/action denials.
		if (currentPlacement)
		{
			await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
				service.GetAsync(fixture.FinalReceiptId)));
			await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
				service.GetPdfAsync(fixture.FinalReceiptId)));
		}
		else
		{
			var detail = await WithUnitOfWorkAsync(() => service.GetAsync(fixture.FinalReceiptId));
			detail.Id.ShouldBe(fixture.FinalReceiptId);
			detail.LinkedProvisionalReceiptId.ShouldBe(fixture.Collection.ReceiptId);
			detail.FeeCollectionTransactionId.ShouldBe(fixture.Collection.CollectionId);
			detail.EmployeeId.ShouldBe(owner.PersonId);
			detail.StudentId.ShouldBeNull();
			detail.FinanceFeeAccountId.ShouldBeNull();
			detail.ReceiptKind.ShouldBe(FeeReceiptKind.FinalCharge);
			detail.ChargePaidAmount.ShouldBe(40.25m);
			var bytes = await WithUnitOfWorkAsync(() => service.GetPdfAsync(fixture.FinalReceiptId));
			bytes.Length.ShouldBeGreaterThan(100);
			Encoding.ASCII.GetString(bytes, 0, 4).ShouldBe("%PDF");
		}

		await AssertFinalBankReceiptLineageAsync(fixture);
	}

	private Task AssertFinalBankReceiptAuthorityAsync(MovedVerifiedBankReceiptFixture fixture, bool currentPlacement) =>
		WithUnitOfWorkAsync(async () =>
		{
			var owner = fixture.Settlement.Owner;
			var services = new DownstreamRefundServices(new FinanceServices(ServiceProvider, owner, useStoredSource: true));
			var permissions = services.GetRequiredService<IPermissionChecker>();
			(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
			(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View)).ShouldBeTrue();
			(await GetRequiredService<IUserScopeService>().IsAdminUserAsync(owner.ActorId)).ShouldBeFalse();
			var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
			scope.HasTenantWideScope().ShouldBeFalse();
			(await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(fixture.Source.LoanId, scope))
				.ShouldBe(!currentPlacement);
			(await GetRequiredService<IAsyncQueryableExecuter>().AnyAsync((await GetRequiredService<ILibraryAccessService>()
				.GetEmployeesAsync(scope)).Where(row => row.Id == owner.PersonId))).ShouldBe(currentPlacement);
			await services.GetRequiredService<ShikshaMitra.Finance.FinanceScopeAccessService>()
				.EnsureCollectionYearAccessibleAsync(owner.YearId);
			var sourceReader = services.GetRequiredService<IFinanceExternalReceivableSourceReader>();
			if (currentPlacement)
			{
				var denied = await Should.ThrowAsync<BusinessException>(() => sourceReader.ReadAuthorizedAsync(fixture.Settlement.Post.Source));
				denied.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			}
			else (await sourceReader.ReadAuthorizedAsync(fixture.Settlement.Post.Source)).Payer.EmployeeId.ShouldBe(owner.PersonId);
		});

	private Task AssertFinalBankReceiptLineageAsync(MovedVerifiedBankReceiptFixture fixture) => WithUnitOfWorkAsync(async () =>
	{
		var owner = fixture.Settlement.Owner;
		var receipts = GetRequiredService<IRepository<FeePaymentReceipt, Guid>>();
		var provisional = await receipts.GetAsync(fixture.Collection.ReceiptId);
		var final = await receipts.GetAsync(fixture.FinalReceiptId);
		final.Id.ShouldNotBe(provisional.Id);
		provisional.LinkedFinalReceiptId.ShouldBe(final.Id);
		final.LinkedProvisionalReceiptId.ShouldBe(provisional.Id);
		provisional.ReceiptKind.ShouldBe(FeeReceiptKind.ProvisionalCharge);
		provisional.VerificationStatus.ShouldBe(FeeReceiptVerificationStatus.Verified);
		final.ReceiptKind.ShouldBe(FeeReceiptKind.FinalCharge);
		final.EmployeeId.ShouldBe(owner.PersonId);
		final.StudentId.ShouldBeNull();
		final.FinanceFeeAccountId.ShouldBeNull();
		final.AcademicYearId.ShouldBe(owner.YearId);
		final.FeeCollectionTransactionId.ShouldBe(fixture.Collection.CollectionId);
		final.FeeCollectionTransactionId.ShouldBe(provisional.FeeCollectionTransactionId);
		final.ReceiptDate.ShouldBe(provisional.ReceiptDate);
		final.ReceiptDate.ShouldBe(fixture.Collection.SchoolReceiptDate);
		final.TotalPaidAmount.ShouldBe(40.25m);
		final.TotalPaidAmount.ShouldBe(provisional.TotalPaidAmount);
		final.Currency.ShouldBe("INR");
		final.PdfBlobName.ShouldNotBeNullOrWhiteSpace("Actual bank verification must generate the final document.");
		var link = (await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
			.GetListAsync(row => row.TenantId == owner.TenantId && row.SourceId == owner.AssessmentId)).Single();
		var commands = await GetRequiredService<IRepository<FinanceExternalReceivableCommand, Guid>>().GetListAsync(row =>
			row.TenantId == owner.TenantId && row.SourceLinkId == link.Id && row.Operation == "Collect");
		commands.Count.ShouldBe(1);
		commands[0].ResultEntityType.ShouldBe("Receipt");
		commands[0].ResultEntityId.ShouldBe(provisional.Id);
		commands[0].ResultEntityId.ShouldNotBe(final.Id);
		var header = await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetAsync(fixture.Collection.CollectionId);
		header.EmployeeId.ShouldBe(owner.PersonId);
		header.StudentId.ShouldBeNull();
		header.FinanceFeeAccountId.ShouldBeNull();
		header.TotalPaidFee.ShouldBe(40.25m);
		var allocation = (await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>()
			.GetListAsync(row => row.FeeRecordMetadataId == header.Id)).Single();
		allocation.ChargeId.ShouldBe(fixture.Settlement.Posted.FinanceChargeId);
		allocation.EmployeeId.ShouldBe(owner.PersonId);
		allocation.Amount.ShouldBe(40.25m);
		var assessment = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(owner.AssessmentId);
		assessment.BookIssueId.ShouldBe(fixture.Source.LoanId);
		assessment.AcademicYearId.ShouldBe(owner.YearId);
		assessment.GetAssessmentVersion().ShouldBe(fixture.Settlement.Post.Source.SourceVersion);
		(await receipts.CountAsync()).ShouldBe(2);
		(await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().CountAsync()).ShouldBe(1);
		(await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().CountAsync()).ShouldBe(1);
	});
}
