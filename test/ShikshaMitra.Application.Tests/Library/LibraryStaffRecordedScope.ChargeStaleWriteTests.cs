using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Fact]
	public async Task Charge_Update_Should_Reject_An_Actual_Stale_Form_Without_Overwriting_An_Accepted_Assessment()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var id = await SeedVersionedDirectAssessmentAsync();
		var service = GetRequiredService<ILibraryChargeAppService>();
		var firstForm = await WithUnitOfWorkAsync(() => service.GetAsync(id));
		var secondForm = await WithUnitOfWorkAsync(() => service.GetAsync(id));
		firstForm.SourceVersion.ShouldNotBeNullOrWhiteSpace();
		secondForm.SourceVersion.ShouldBe(firstForm.SourceVersion);
		await WithUnitOfWorkAsync(() => service.UpdateAsync(id, new()
		{
			ExpectedSourceVersion = firstForm.SourceVersion, Amount = 100.75m,
			Remarks = "First operator's accepted assessment amendment."
		}));
		var accepted = await WithUnitOfWorkAsync(() => service.GetAsync(id));
		accepted.Amount.ShouldBe(100.75m);
		accepted.SourceVersion.ShouldNotBe(firstForm.SourceVersion);

		var denied = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			service.UpdateAsync(id, new()
			{
				ExpectedSourceVersion = secondForm.SourceVersion, Amount = 5m,
				Remarks = "A second form opened before the accepted amendment."
			})));

		denied.Code.ShouldBe(LibraryFinanceErrorCodes.SourceChanged);
		var preserved = await WithUnitOfWorkAsync(() => service.GetAsync(id));
		preserved.Amount.ShouldBe(100.75m);
		preserved.Remarks.ShouldBe(accepted.Remarks);
		preserved.SourceVersion.ShouldBe(accepted.SourceVersion);
		preserved.FinanceSourceLinkId.ShouldBeNull();
		preserved.BookIssueId.ShouldBeNull();
	}

	[Fact]
	public async Task Charge_Update_Should_Accept_A_Fresh_Actual_Source_Version_After_An_Earlier_Amendment()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var id = await SeedVersionedDirectAssessmentAsync();
		var service = GetRequiredService<ILibraryChargeAppService>();
		var original = await WithUnitOfWorkAsync(() => service.GetAsync(id));
		await WithUnitOfWorkAsync(() => service.UpdateAsync(id, new()
		{
			ExpectedSourceVersion = original.SourceVersion, Amount = 100.75m,
			Remarks = "First reviewed amendment."
		}));
		var latest = await WithUnitOfWorkAsync(() => service.GetAsync(id));
		latest.SourceVersion.ShouldNotBe(original.SourceVersion);

		await WithUnitOfWorkAsync(() => service.UpdateAsync(id, new()
		{
			ExpectedSourceVersion = latest.SourceVersion, Amount = 110.75m,
			Remarks = "Reviewed amendment against the newly loaded version."
		}));

		var updated = await WithUnitOfWorkAsync(() => service.GetAsync(id));
		updated.Amount.ShouldBe(110.75m);
		updated.SourceVersion.ShouldNotBe(latest.SourceVersion);
		updated.MemberId.ShouldBe(original.MemberId);
		updated.AcademicYearId.ShouldBe(original.AcademicYearId);
		updated.SchoolChargeDate.ShouldBe(original.SchoolChargeDate);
		updated.SchoolDueDate.ShouldBe(original.SchoolDueDate);
		updated.FinanceSourceLinkId.ShouldBeNull();
	}

	private async Task<Guid> SeedVersionedDirectAssessmentAsync()
	{
		var fixture = await SeedStaffAsync();
		_ = await ActualChargeEditorLookupsAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Library.Charges.Update);
		return await WithUnitOfWorkAsync(async () =>
		{
			var tenantId = GetRequiredService<ICurrentTenant>().Id!.Value;
			var actorId = GetRequiredService<ICurrentUser>().Id!.Value;
			var category = new LibraryChargeCategory(Guid.NewGuid(), tenantId, fixture.Context.Catalog.YearId,
				"Versioned direct assessment", 75m, false, "Explicit source for concurrent form tests.");
			await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(category, true);
			var charge = LibraryCharge.CreateConfirmed(Guid.NewGuid(), fixture.EmployeeId, 1, category.Id, null,
				75m, new(2026, 11, 17), new(2026, 11, 30), fixture.Context.Catalog.YearId,
				LibraryAcademicYearProvenance.ExplicitAssessmentContext,
				JsonSerializer.Serialize(new { AcademicYearId = fixture.Context.Catalog.YearId, ActorId = actorId,
					Reason = "Confirmed actual assessment year before both forms were opened." }),
				JsonSerializer.Serialize(new { SchoolChargeDate = "2026-11-17", SchoolDueDate = "2026-11-30",
					ActorId = actorId, Reason = "Confirmed explicit school dates." }),
				"Original versioned assessment.", tenantId);
			await GetRequiredService<IRepository<LibraryCharge, Guid>>().InsertAsync(charge, true);
			return charge.Id;
		});
	}
}
