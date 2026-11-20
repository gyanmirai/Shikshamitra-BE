using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Fact]
	public async Task Loan_Linked_Staff_Assessment_Should_Retain_Original_Recorded_Source_Authority_After_A_Move()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var (fixture, loan, source) = await SeedMovedStaffFinanceSourceAsync();
		await SetReaderStageAsync(fixture, fixture.Context.StageId);
		var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		(await WithUnitOfWorkAsync(() => GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loan.Id, scope))).ShouldBeTrue();
		(await WithUnitOfWorkAsync(async () => await GetRequiredService<IAsyncQueryableExecuter>().AnyAsync(
			(await GetRequiredService<ILibraryAccessService>().GetEmployeesAsync()).Where(row => row.Id == fixture.EmployeeId)))).ShouldBeFalse();
		var reader = await ActualFinanceSourceReaderAsync();

		var facts = await WithUnitOfWorkAsync(() => reader.ReadAuthorizedAsync(source));

		facts.Payer.Kind.ShouldBe(FinanceExternalPayerKind.Employee);
		facts.Payer.EmployeeId.ShouldBe(fixture.EmployeeId);
		facts.Payer.StudentId.ShouldBeNull();
		facts.AcademicYearId.ShouldBe(fixture.Context.Catalog.YearId);
		facts.Source.SourceId.ShouldBe(source.SourceId);
		facts.Source.SourceVersion.ShouldBe(source.SourceVersion);
	}

	[Fact]
	public async Task Current_Staff_Placement_Should_Not_Grant_An_Older_Loan_Linked_Assessment()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var (fixture, loan, source) = await SeedMovedStaffFinanceSourceAsync();
		await SetActualFinanceReaderStageAsync(fixture, fixture.LaterStageId);
		var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		(await WithUnitOfWorkAsync(() => GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loan.Id, scope))).ShouldBeFalse();
		(await WithUnitOfWorkAsync(async () => await GetRequiredService<IAsyncQueryableExecuter>().AnyAsync(
			(await GetRequiredService<ILibraryAccessService>().GetEmployeesAsync()).Where(row => row.Id == fixture.EmployeeId)))).ShouldBeTrue();
		var reader = await ActualFinanceSourceReaderAsync();

		var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => reader.ReadAuthorizedAsync(source)));

		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	private async Task<(StaffScopeFixture Fixture, LibraryLoanDto Loan, FinanceExternalSourceReference Source)> SeedMovedStaffFinanceSourceAsync()
	{
		var fixture = await SeedStaffAsync();
		// Put the later placement outside the original board as well as its stage;
		// current HR ancestor expansion must not accidentally satisfy the positive control.
		fixture = await WithUnitOfWorkAsync(async () =>
		{
			var tenant = GetRequiredService<ICurrentTenant>().Id;
			var board = new EducationBoard(Guid.NewGuid(), tenant, EducationBoardType.GSEB, fixture.Context.Catalog.YearId);
			await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
			var medium = new EducationMedium(Guid.NewGuid(), tenant, "Replacement staff medium", board.Id);
			await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
			var stage = new EducationMediumStage(Guid.NewGuid(), tenant, "Replacement staff stage", null, medium.Id);
			await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
			return fixture with { LaterStageId = stage.Id };
		});
		var loan = await CheckoutStaffAsync(fixture);
		var source = await WithUnitOfWorkAsync(async () =>
		{
			var tenant = GetRequiredService<ICurrentTenant>().Id!.Value;
			var actor = GetRequiredService<ICurrentUser>().Id!.Value;
			var category = new LibraryChargeCategory(Guid.NewGuid(), tenant, fixture.Context.Catalog.YearId,
				"Reviewed staff assessment", 75m, false, "Original loan-linked evidence");
			await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(category, true);
			var charge = new LibraryCharge(Guid.NewGuid(), fixture.EmployeeId, 1, category.Id, loan.Id, 75m,
				new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc), tenant);
			charge.ConfirmSourceContext(fixture.Context.Catalog.YearId, LibraryAcademicYearProvenance.ReviewedLegacy,
				JsonSerializer.Serialize(new { LoanId = loan.Id, fixture.EmployeeId, AcademicYearId = fixture.Context.Catalog.YearId, ActorId = actor,
					Reason = "Checked the original loan and assessment register." }),
				new(2026, 11, 17), new(2026, 11, 30), LibraryCivilDateProvenance.ReviewedLegacy, LibraryCivilDateProvenance.ReviewedLegacy,
				JsonSerializer.Serialize(new { ActorId = actor, Reason = "Checked the original assessment school dates." }), "INR");
			await GetRequiredService<IRepository<LibraryCharge, Guid>>().InsertAsync(charge, true);
			return new FinanceExternalSourceReference
			{
				Kind = FinanceExternalSourceKind.LibraryAssessment, SourceId = charge.Id, SourceVersion = charge.GetAssessmentVersion()
			};
		});
		await MoveStaffAsync(fixture);
		return (fixture, loan, source);
	}

	private Task SetActualFinanceReaderStageAsync(StaffScopeFixture fixture, Guid stageId) => WithUnitOfWorkAsync(async () =>
	{
		var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(stageId);
		var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(stage.EducationMediumId);
		var actor = GetRequiredService<ICurrentUser>().Id!.Value;
		var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await assignments.DeleteAsync(row => row.UserId == actor, true);
		await assignments.InsertAsync(new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, userId: actor,
			academicYearId: fixture.Context.Catalog.YearId, educationBoardId: medium.EducationBoardId, educationMediumId: medium.Id,
			educationMediumStageId: stageId), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
	});

	private async Task<LibraryFinanceSourceReader> ActualFinanceSourceReaderAsync()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var actor = GetRequiredService<ICurrentUser>().Id!.Value;
			(await GetRequiredService<IIdentityUserRepository>().GetAsync(actor)).TenantId.ShouldBe(GetRequiredService<ICurrentTenant>().Id);
			await GetRequiredService<IPermissionManager>().SetAsync(
				ShikshaMitraSubmodulePermissions.Library.Charges.View, "U", actor.ToString(), true);
		});
		var checker = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionChecker>(ServiceProvider);
		(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
		return ActivatorUtilities.CreateInstance<LibraryFinanceSourceReader>(ServiceProvider, checker);
	}
}
