using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Charge_Write_Authority_Should_Amend_An_Exact_Linked_Assessment_Through_Recorded_Staff_Scope(bool originalScope)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var (fixture, loan, source) = await SeedMovedStaffFinanceSourceAsync();
		if (originalScope) await SetReaderStageAsync(fixture, fixture.Context.StageId);
		else await SetActualFinanceReaderStageAsync(fixture, fixture.LaterStageId);
		await AssertChargeHandoffScopeAsync(fixture, loan.Id, storedLoan: originalScope, currentEmployee: !originalScope);
		await GrantChargeWriteAuthorityAsync(ShikshaMitraSubmodulePermissions.Library.Charges.Update);
		await GetRequiredService<ILibraryAccessService>().GetContextAsync(fixture.Context.Catalog.YearId);
		var service = GetRequiredService<ILibraryChargeAppService>();
		var input = new UpdateChargeRequest
		{
			ExpectedSourceVersion = source.SourceVersion, Amount = 100.75m,
			Remarks = "Reviewed an amendment against the exact recorded source."
		};
		if (originalScope)
		{
			(await service.GetAsync(source.SourceId)).BookIssueId.ShouldBe(loan.Id);
			await WithUnitOfWorkAsync(() => service.UpdateAsync(source.SourceId, input));
		}
		else
		{
			var readDenied = await Should.ThrowAsync<BusinessException>(() => service.GetAsync(source.SourceId));
			readDenied.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			var writeDenied = await Should.ThrowAsync<BusinessException>(() =>
				WithUnitOfWorkAsync(() => service.UpdateAsync(source.SourceId, input)));
			writeDenied.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
		await WithUnitOfWorkAsync(async () =>
		{
			var preserved = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(source.SourceId);
			preserved.Amount.ShouldBe(originalScope ? 100.75m : 75m);
			preserved.MemberId.ShouldBe(fixture.EmployeeId);
			preserved.BookIssueId.ShouldBe(loan.Id);
			preserved.AcademicYearId.ShouldBe(fixture.Context.Catalog.YearId);
			preserved.FinanceSourceLinkId.ShouldBeNull();
			preserved.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
			if (!originalScope) preserved.GetAssessmentVersion().ShouldBe(source.SourceVersion);
		});
	}

	[Theory]
	[InlineData("Update", false)]
	[InlineData("Delete", false)]
	[InlineData("Update", true)]
	[InlineData("Delete", true)]
	public async Task Charge_Write_Authority_Should_Check_The_Independent_Direct_Assessment_Year(string operation, bool allowAssessmentYear)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loan = await CheckoutStaffAsync(fixture);
		var (otherYearSource, otherYearId) = await NewStaffAssessmentYearSourceAsync(fixture, loan.Id);
		var direct = await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var template = await repository.GetAsync(otherYearSource.SourceId);
			var actorId = GetRequiredService<ICurrentUser>().Id!.Value;
			var assessment = LibraryCharge.CreateConfirmed(Guid.NewGuid(), fixture.EmployeeId, 1, template.ChargeCategoryId,
				null, 75m, template.SchoolChargeDate!.Value, template.SchoolDueDate!.Value, otherYearId,
				LibraryAcademicYearProvenance.ExplicitAssessmentContext,
				JsonSerializer.Serialize(new { AcademicYearId = otherYearId, ActorId = actorId,
					Reason = "Confirmed a new direct assessment in this actual billing year under initial tenant-wide authority." }),
				JsonSerializer.Serialize(new { template.SchoolChargeDate, template.SchoolDueDate, ActorId = actorId,
					Reason = "Explicit school dates recorded for this direct assessment." }),
				"Direct assessment with an independently confirmed actual billing year.", GetRequiredService<ICurrentTenant>().Id!.Value);
			await repository.InsertAsync(assessment, true);
			return (assessment.Id, Version: assessment.GetAssessmentVersion());
		});
		await SetReaderStageAsync(fixture, fixture.Context.StageId);
		await GrantChargeWriteAuthorityAsync(operation == "Update"
			? ShikshaMitraSubmodulePermissions.Library.Charges.Update : ShikshaMitraSubmodulePermissions.Library.Charges.Delete);
		(await GetRequiredService<ILibraryAccessService>().EnsureMemberAsync(fixture.EmployeeId)).ShouldBe(fixture.EmployeeId);
		if (allowAssessmentYear)
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var actorId = GetRequiredService<ICurrentUser>().Id!.Value;
				await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new(Guid.NewGuid(),
					GetRequiredService<ICurrentTenant>().Id, userId: actorId, academicYearId: otherYearId,
					description: "Independent actual assessment-year write authority"), true);
				await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actorId);
			});
			await GetRequiredService<ILibraryAccessService>().GetContextAsync(otherYearId);
		}
		else
		{
			var deniedYear = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<ILibraryAccessService>().GetContextAsync(otherYearId));
			deniedYear.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
		var service = GetRequiredService<ILibraryChargeAppService>();
		if (!allowAssessmentYear)
		{
			var deniedRead = await Should.ThrowAsync<BusinessException>(() => service.GetAsync(direct.Id));
			deniedRead.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
		async Task WriteAsync()
		{
			if (operation == "Update") await WithUnitOfWorkAsync(() => service.UpdateAsync(direct.Id, new()
			{
				ExpectedSourceVersion = direct.Version, Amount = 100.75m, Remarks = "Reviewed the actual selected billing year."
			}));
			else await WithUnitOfWorkAsync(() => service.DeleteAsync(direct.Id));
		}
		if (allowAssessmentYear) await WriteAsync();
		else
		{
			var deniedWrite = await Should.ThrowAsync<BusinessException>(WriteAsync);
			deniedWrite.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
		await WithUnitOfWorkAsync(async () =>
		{
			var saved = await GetRequiredService<IRepository<LibraryCharge, Guid>>().FindAsync(direct.Id);
			if (allowAssessmentYear && operation == "Delete") saved.ShouldBeNull();
			else
			{
				saved.ShouldNotBeNull();
				saved.Amount.ShouldBe(allowAssessmentYear ? 100.75m : 75m);
				saved.AcademicYearId.ShouldBe(otherYearId);
				saved.BookIssueId.ShouldBeNull();
				saved.FinanceSourceLinkId.ShouldBeNull();
				if (!allowAssessmentYear) saved.GetAssessmentVersion().ShouldBe(direct.Version);
			}
		});
	}

	private async Task GrantChargeWriteAuthorityAsync(string action)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var actorId = GetRequiredService<ICurrentUser>().Id!.Value;
			foreach (var permission in new[] { ShikshaMitraSubmodulePermissions.Library.Charges.View, action })
				await GetRequiredService<IPermissionManager>().SetAsync(permission, "U", actorId.ToString(), true);
		});
		var checker = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionChecker>(ServiceProvider);
		(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
		(await checker.IsGrantedAsync(action)).ShouldBeTrue();
	}
}
