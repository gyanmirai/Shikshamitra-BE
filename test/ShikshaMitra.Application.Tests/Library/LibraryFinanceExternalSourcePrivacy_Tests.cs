using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Theory]
	[InlineData("Collect")]
	[InlineData("Waive")]
	[InlineData("Refund")]
	public async Task Legacy_Command_Source_Classification_Should_Require_Current_Payer_And_Year_Scope(string command)
	{
		var context = await SeedPrivacySourceAsync();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Fixture.TenantId);
		var restricted = await ScopedSettlementOperatorAsync(context.Fixture);
		using var actor = SignIn(restricted);
		await AssertRealSettlementGrantAsync(restricted, command);

		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			InvokeLegacySourceCommandAsync(restricted, context, command, context.Fixture.PersonId, context.Fixture.YearId)));
		await AssertPrivacyMoneyUnchangedAsync();
	}

	[Theory]
	[InlineData("Collect")]
	[InlineData("Waive")]
	[InlineData("Refund")]
	public async Task Legacy_Command_Source_Classification_Should_Remain_Visible_To_An_Authorized_Payer_Operator(string command)
	{
		var context = await SeedPrivacySourceAsync();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Fixture.TenantId);
		using var actor = SignIn(context.Fixture);
		await AssertRealSettlementGrantAsync(context.Fixture, command);

		var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			InvokeLegacySourceCommandAsync(context.Fixture, context, command, context.Fixture.PersonId, context.Fixture.YearId)));

		error.Code.ShouldBe(LibraryFinanceErrorCodes.SettlementRequiresFinance);
		await AssertPrivacyMoneyUnchangedAsync();
	}

	[Theory]
	[InlineData("Collect", false)]
	[InlineData("Collect", true)]
	[InlineData("Refund", false)]
	[InlineData("Refund", true)]
	public async Task Legacy_Command_Source_Classification_Should_Reject_A_Different_Selected_Payer_Or_Year(
		string command, bool wrongYear)
	{
		var context = await SeedPrivacySourceAsync();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Fixture.TenantId);
		using var actor = SignIn(context.Fixture);
		await AssertRealSettlementGrantAsync(context.Fixture, command);
		var different = await DifferentSelectedContextAsync(context.Fixture);

		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			InvokeLegacySourceCommandAsync(context.Fixture, context, command,
				wrongYear ? context.Fixture.PersonId : different.StudentId,
				wrongYear ? different.YearId : context.Fixture.YearId)));
		await AssertPrivacyMoneyUnchangedAsync();
	}

	private async Task<PrivacySource> SeedPrivacySourceAsync()
	{
		var fixture = await SeedAsync(FinanceExternalPayerKind.Student, false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = SignIn(fixture);
		var posted = await WithUnitOfWorkAsync(() => Service(fixture).PostAsync(Post(fixture)));
		await SeedNormallyEligiblePaidStudentChargeAsync(fixture, posted);
		return await WithUnitOfWorkAsync(async () =>
		{
			await GrantSettlementActionsAsync($"external-finance-{fixture.ActorId:N}");
			var allocation = (await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>()
				.GetListAsync(row => row.ChargeId == posted.FinanceChargeId)).Single();
			var receipt = (await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>()
				.GetListAsync(row => row.FeeCollectionTransactionId == allocation.FeeRecordMetadataId)).Single();
			return new PrivacySource(fixture, posted.FinanceChargeId, allocation.Id, receipt.Id);
		});
	}

	private Task<Fixture> ScopedSettlementOperatorAsync(Fixture fixture) => WithUnitOfWorkAsync(async () =>
	{
		var userId = Guid.NewGuid();
		var role = new IdentityRole(Guid.NewGuid(), $"scoped-cashier-{userId:N}", fixture.TenantId);
		await GetRequiredService<IIdentityRoleRepository>().InsertAsync(role, true);
		var user = new IdentityUser(userId, $"scoped-{userId:N}", $"{userId:N}@example.test", fixture.TenantId);
		user.AddRole(role.Id);
		await GetRequiredService<IIdentityUserRepository>().InsertAsync(user, true);
		await GrantSettlementActionsAsync(role.Name);
		var year = new AcademicYear(Guid.NewGuid(), fixture.TenantId, "Different authorized collection year",
			new DateTime(2025, 4, 1), new DateTime(2026, 3, 31));
		await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(
			Guid.NewGuid(), fixture.TenantId, userId: userId, academicYearId: year.Id,
			description: "Finance actions restricted to a different actual school year"), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
		return fixture with { ActorId = userId };
	});

	private async Task GrantSettlementActionsAsync(string roleName)
	{
		foreach (var action in SettlementActions)
			await GetRequiredService<IPermissionManager>().SetAsync(action, "R", roleName, true);
	}

	private async Task AssertRealSettlementGrantAsync(Fixture fixture, string command)
	{
		var checker = (IPermissionChecker)new FinanceServices(ServiceProvider, fixture).GetService(typeof(IPermissionChecker))!;
		(await checker.IsGrantedAsync(SettlementAction(command))).ShouldBeTrue();
	}

	private async Task InvokeLegacySourceCommandAsync(Fixture actor, PrivacySource source, string command,
		Guid selectedStudentId, Guid selectedYearId)
	{
		var services = new FinanceServices(ServiceProvider, actor);
		if (command == "Collect")
		{
			var workflow = ActivatorUtilities.CreateInstance<CollectFeeWorkflow>(services);
			workflow.LazyServiceProvider = new AbpLazyServiceProvider(services);
			await workflow.CollectFeeAsync(new CollectFeeInput
			{
				StudentId = selectedStudentId, AcademicYearId = selectedYearId,
				IdempotencyKey = Guid.NewGuid().ToString("N"),
				ChargePayments = [new() { ChargeId = source.ChargeId, Amount = 1d }],
				Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 1d }]
			});
			return;
		}
		if (command == "Waive")
		{
			var waiver = ActivatorUtilities.CreateInstance<FeeWaiverAppService>(services);
			waiver.LazyServiceProvider = new AbpLazyServiceProvider(services);
			await waiver.ApplyAsync(new() { SourceType = "Charge", SourceId = source.ChargeId,
				Reason = "Authorized source classification check", IdempotencyKey = Guid.NewGuid().ToString("N") });
			return;
		}
		await RefundService(actor).CreateAsync(new CreateFeeRefundRequestDto
		{
			StudentId = selectedStudentId, AcademicYearId = selectedYearId, OriginalReceiptId = source.ReceiptId,
			RefundMethod = "Cash", Reason = "Authorized source classification check", IdempotencyKey = Guid.NewGuid().ToString("N"),
			Lines = [new() { ChargeId = source.ChargeId, SourceAllocationId = source.AllocationId,
				SourceReceiptId = source.ReceiptId, Amount = 1m }]
		});
	}

	private Task<(Guid StudentId, Guid YearId)> DifferentSelectedContextAsync(Fixture fixture) => WithUnitOfWorkAsync(async () =>
	{
		var existing = await GetRequiredService<IRepository<Student, Guid>>().GetAsync(fixture.PersonId);
		var other = Student.CreateDirectRegistration(Guid.NewGuid(), fixture.TenantId, "Different", null, "Payer",
			fixture.YearId, existing.AcademicBoardId!.Value, existing.AcademicMediumId, existing.AcademicMediumStageId!.Value,
			existing.StandardId, existing.DivisionId, new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero), GenderEnum.Female,
			primaryContactNo: "9999999998", email: "different-payer@example.test");
		other.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
		await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(other, true);
		var year = new AcademicYear(Guid.NewGuid(), fixture.TenantId, "Different selected year",
			new DateTime(2024, 4, 1), new DateTime(2025, 3, 31));
		await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
		return (other.Id, year.Id);
	});

	private Task AssertPrivacyMoneyUnchangedAsync() => WithUnitOfWorkAsync(async () =>
	{
		(await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().CountAsync()).ShouldBe(1);
		(await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().CountAsync()).ShouldBe(1);
		(await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().CountAsync()).ShouldBe(2);
	});

	private static string SettlementAction(string command) => command switch
	{
		"Collect" => ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add,
		"Waive" => ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver,
		_ => ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add
	};
	private static readonly string[] SettlementActions = [SettlementAction("Collect"), SettlementAction("Waive"), SettlementAction("Refund")];
	private sealed record PrivacySource(Fixture Fixture, Guid ChargeId, Guid AllocationId, Guid ReceiptId);
}
