using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Operator_History_Should_Require_Current_Authority_For_The_Recorded_Employee_Source_Before_Counts_And_Pages(bool currentPlacement)
	{
		var fixture = await SeedMovedTypedCashAsync();
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await SetMovedCashReaderScopeAsync(fixture, currentPlacement);
		await AssertMovedCashAuthorityAsync(fixture, currentPlacement);
		var permissions = new FinanceServices(ServiceProvider, owner, useStoredSource: true).GetRequiredService<IPermissionChecker>();
		(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View)).ShouldBeTrue();
		var service = OperatorService(owner);
		if (currentPlacement)
		{
			var denied = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
				service.GetHistoryAsync(fixture.Settlement.Post.Source, page: 1, pageSize: 1)));
			denied.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
		else
		{
			var first = await WithUnitOfWorkAsync(() => service.GetHistoryAsync(fixture.Settlement.Post.Source, page: 1, pageSize: 2));
			var second = await WithUnitOfWorkAsync(() => service.GetHistoryAsync(fixture.Settlement.Post.Source, page: 2, pageSize: 2));
			var end = await WithUnitOfWorkAsync(() => service.GetHistoryAsync(fixture.Settlement.Post.Source, page: 3, pageSize: 2));
			first.TotalCount.ShouldBe(4); second.TotalCount.ShouldBe(4); end.TotalCount.ShouldBe(4);
			first.Items.Count.ShouldBe(2); second.Items.Count.ShouldBe(2); end.Items.ShouldBeEmpty();
			var rows = first.Items.Concat(second.Items).ToList();
			rows.Select(row => row.Id).Distinct().Count().ShouldBe(4);
			rows.Select(row => row.Operation).OrderBy(value => value).ShouldBe(new[] { "Post", "BindPolicy", "Approve", "Collect" }.OrderBy(value => value));
			var receipt = rows.Single(row => row.Operation == "Collect");
			receipt.ResultKind.ShouldBe("Receipt"); receipt.ResultId.ShouldBe(fixture.Collection.ReceiptId);
			receipt.ResultId.ShouldNotBe(fixture.Collection.CollectionId);
			receipt.CanOpenResult.ShouldBeTrue();
			await WithUnitOfWorkAsync(async () =>
			{
				var link = (await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
					.GetListAsync(row => row.SourceId == fixture.Settlement.Post.Source.SourceId)).Single();
				var commands = await GetRequiredService<IRepository<FinanceExternalReceivableCommand, Guid>>().GetListAsync(row => row.SourceLinkId == link.Id);
				rows.Select(row => row.Id).OrderBy(id => id).ShouldBe(commands.Select(row => row.Id).OrderBy(id => id));
			});
		}
		await AssertMovedCashOriginalSourceIntactAsync(fixture);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Operator_Receipt_Detail_And_Pdf_Should_Use_Recorded_Source_Authority_Instead_Of_Only_Current_Employee_Placement(bool currentPlacement)
	{
		var fixture = await SeedMovedTypedCashAsync();
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await SetMovedCashReaderScopeAsync(fixture, currentPlacement);
		await AssertMovedCashAuthorityAsync(fixture, currentPlacement);
		var services = new DownstreamRefundServices(new FinanceServices(ServiceProvider, owner, useStoredSource: true));
		(await services.GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View)).ShouldBeTrue();
		// The real action grant is an explicit precondition. Manual construction
		// isolates the current stored-source body check; it is not a substitute for
		// separate HTTP action-permission tests.
		var service = ActivatorUtilities.CreateInstance<FeeReceiptAppService>(services);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		if (currentPlacement)
		{
			await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => service.GetAsync(fixture.Collection.ReceiptId)));
			await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => service.GetPdfAsync(fixture.Collection.ReceiptId)));
		}
		else
		{
			var detail = await WithUnitOfWorkAsync(() => service.GetAsync(fixture.Collection.ReceiptId));
			detail.Id.ShouldBe(fixture.Collection.ReceiptId); detail.EmployeeId.ShouldBe(owner.PersonId);
			detail.StudentId.ShouldBeNull(); detail.FinanceFeeAccountId.ShouldBeNull();
			detail.ChargePaidAmount.ShouldBe(40.25m);
			var bytes = await WithUnitOfWorkAsync(() => service.GetPdfAsync(fixture.Collection.ReceiptId));
			bytes.Length.ShouldBeGreaterThan(100);
			Encoding.ASCII.GetString(bytes, 0, 4).ShouldBe("%PDF");
		}
		await AssertMovedCashOriginalSourceIntactAsync(fixture);
	}
}
