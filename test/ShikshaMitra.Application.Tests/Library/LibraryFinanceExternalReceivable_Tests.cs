using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData(FinanceExternalPayerKind.Student, false)]
	[InlineData(FinanceExternalPayerKind.Student, true)]
	[InlineData(FinanceExternalPayerKind.Employee, false)]
	[InlineData(FinanceExternalPayerKind.Employee, true)]
	public async Task External_Assessments_Should_Create_One_Finance_Charge_For_Real_Borrowers(
		FinanceExternalPayerKind payerKind, bool departed)
	{
		var fixture = await SeedAsync(payerKind, departed);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = SignIn(fixture);
		var result = await WithUnitOfWorkAsync(() => Service(fixture).PostAsync(Post(fixture)));

		result.Source.SourceId.ShouldBe(fixture.AssessmentId);
		result.Payer.Kind.ShouldBe(payerKind);
		result.Payer.StudentId.ShouldBe(payerKind == FinanceExternalPayerKind.Student ? fixture.PersonId : null);
		result.Payer.EmployeeId.ShouldBe(payerKind == FinanceExternalPayerKind.Employee ? fixture.PersonId : null);
		result.AcademicYearId.ShouldBe(fixture.YearId);
		result.Currency.ShouldBe("INR");
		result.OriginalAmount.ShouldBe(100m);
		result.OutstandingAmount.ShouldBe(0m);
		result.ApprovalStatus.ShouldBe(ChargeStatus.Pending);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<Charge, Guid>>().GetAsync(result.FinanceChargeId)).Amount.ShouldBe(100m);
			(await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(fixture.AssessmentId))
				.FinanceSourceLinkId.ShouldNotBeNull();
			(await GetRequiredService<IRepository<ChargeRequest, Guid>>().CountAsync()).ShouldBe(1);
			(await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().CountAsync()).ShouldBe(0);
		});
	}

	[Fact]
	public async Task An_Employee_Receivable_Audit_Should_Not_Be_Treated_As_Unscoped_Manual_Money()
	{
		var fixture = await SeedAsync(FinanceExternalPayerKind.Employee, false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		FinanceExternalReceivableDto posted;
		using (SignIn(fixture))
		{
			posted = await WithUnitOfWorkAsync(() => Service(fixture).PostAsync(Post(fixture)));
			var owner = await WithUnitOfWorkAsync(() => AuditService(fixture).GetEntityEventsAsync(
				FinanceAuditEntityTypes.FeeCharge, posted.FinanceChargeRequestId, new()));
			owner.TotalCount.ShouldBe(1);
		}
		var academicReader = await AcademicAuditReaderAsync(fixture);
		using (SignIn(academicReader))
		{
			var hidden = await WithUnitOfWorkAsync(() => AuditService(academicReader).GetEntityEventsAsync(
				FinanceAuditEntityTypes.FeeCharge, posted.FinanceChargeRequestId, new()));
			hidden.TotalCount.ShouldBe(0);
			hidden.Items.ShouldBeEmpty();
		}
	}

	[Fact]
	public async Task A_Reviewed_Historical_Due_Date_Should_Not_Be_Replaced_With_Today()
	{
		var fixture = await SeedAsync(FinanceExternalPayerKind.Student, true,
			reviewedDueDate: new DateOnly(2026, 11, 10));
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = SignIn(fixture);

		var result = await WithUnitOfWorkAsync(() => Service(fixture).PostAsync(Post(fixture)));

		result.AssessmentDate.ShouldBe(fixture.AssessmentDate);
		result.DueDate.ShouldBe(fixture.DueDate);
	}

	[Fact]
	public async Task An_Invalid_Legacy_Subcent_Assessment_Should_Not_Create_A_Finance_Obligation()
	{
		var fixture = await SeedAsync(FinanceExternalPayerKind.Student, false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = SignIn(fixture);
		await WithUnitOfWorkAsync(async () =>
		{
			var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var legacy = await charges.GetAsync(fixture.AssessmentId);
			typeof(LibraryCharge).GetProperty(nameof(LibraryCharge.Amount))!.SetValue(legacy, 0.001m);
			await charges.UpdateAsync(legacy, true);
		});
		var source = Post(fixture);
		await WithUnitOfWorkAsync(async () => source.Source.SourceVersion =
			(await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(fixture.AssessmentId)).ConcurrencyStamp);

		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => Service(fixture).PostAsync(source)));
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<ChargeRequest, Guid>>().CountAsync()).ShouldBe(0));
	}

	[Fact]
	public async Task Finance_External_Post_Should_Require_The_Current_Database_Action_Grant()
	{
		var fixture = await SeedAsync(FinanceExternalPayerKind.Student, false, grantFinanceAdd: false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = SignIn(fixture);

		await Should.ThrowAsync<AbpAuthorizationException>(() =>
			WithUnitOfWorkAsync(() => Service(fixture).PostAsync(Post(fixture))));
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<ChargeRequest, Guid>>().CountAsync()).ShouldBe(0));
	}

	[Fact]
	public async Task Finance_Grant_Should_Not_Allow_Posting_A_Library_Source_Outside_Current_User_Scope()
	{
		var fixture = await SeedAsync(FinanceExternalPayerKind.Student, false, allowSourceScope: false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = SignIn(fixture);

		var error = await Should.ThrowAsync<BusinessException>(() =>
			WithUnitOfWorkAsync(() => Service(fixture).PostAsync(Post(fixture))));

		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<ChargeRequest, Guid>>().CountAsync()).ShouldBe(0));
	}

	[Fact]
	public async Task Different_Retry_Keys_For_The_Same_Assessment_Should_Return_The_Original_Finance_Obligation()
	{
		var fixture = await SeedAsync(FinanceExternalPayerKind.Student, false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = SignIn(fixture);
		var service = Service(fixture);
		var first = await WithUnitOfWorkAsync(() => service.PostAsync(Post(fixture)));
		await WithUnitOfWorkAsync(async () =>
		{
			var bound = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(fixture.AssessmentId);
			bound.ExtraProperties.ShouldContainKey("FinanceBoundAssessmentVersion");
			bound.GetAssessmentVersion().ShouldBe(fixture.Version,
				"Binding must retain the accepted assessment version across the persisted source reload.");
		});
		var second = await WithUnitOfWorkAsync(() => service.PostAsync(Post(fixture)));

		second.FinanceChargeId.ShouldBe(first.FinanceChargeId);
		second.FinanceChargeRequestId.ShouldBe(first.FinanceChargeRequestId);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<ChargeRequest, Guid>>().CountAsync()).ShouldBe(1);
			(await GetRequiredService<IRepository<Charge, Guid>>().CountAsync()).ShouldBe(1);
			(await GetRequiredService<IRepository<FinanceExternalReceivableCommand, Guid>>().CountAsync()).ShouldBe(2);
		});
	}

	[Fact]
	public async Task Reusing_A_Retry_Key_With_A_Different_Finance_Mapping_Should_Be_Rejected()
	{
		var fixture = await SeedAsync(FinanceExternalPayerKind.Student, false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = SignIn(fixture);
		var service = Service(fixture);
		var input = Post(fixture);
		var original = await WithUnitOfWorkAsync(() => service.PostAsync(input));
		input.FinanceChargeCategoryId = fixture.OtherFinanceCategoryId;

		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => service.PostAsync(input)));
		var saved = await WithUnitOfWorkAsync(() => service.GetAsync(input.Source));
		saved.FinanceChargeId.ShouldBe(original.FinanceChargeId);
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<Charge, Guid>>().GetAsync(original.FinanceChargeId))
			.ChargeCategoryId.ShouldBe(fixture.FinanceCategoryId));
	}
}
