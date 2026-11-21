using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private async Task<MemberChargeYears> SeedMemberChargeYearsAsync()
	{
		var owner = await SeedAsync(FinanceExternalPayerKind.Employee, departed: false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		owner = await ConfirmStoredAssessmentAsync(owner);
		return await WithUnitOfWorkAsync(async () =>
		{
			var placement = await CreateMovedCashPlacementAsync(owner, EducationBoardType.CBSE, "Member year current target");
			await GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>().InsertAsync(
				new(Guid.NewGuid(), owner.TenantId, owner.PersonId, placement.StageId), true);
			var year = new AcademicYear(Guid.NewGuid(), owner.TenantId, "Explicit assessment2027-28",
				new DateTime(2027, 4, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2028, 3, 31, 0, 0, 0, DateTimeKind.Utc));
			await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
			var category = new LibraryChargeCategory(Guid.NewGuid(), owner.TenantId, year.Id,
				"Separate saved assessment", 35.50m, false, "Explicitly reviewed2027 assessment context.");
			await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(category, true);
			var otherId = Guid.NewGuid();
			var other = LibraryCharge.CreateConfirmed(otherId, owner.PersonId, 1, category.Id, null, 35.50m,
				new(2027, 11, 17), new(2027, 11, 24), year.Id, LibraryAcademicYearProvenance.ExplicitAssessmentContext,
				JsonSerializer.Serialize(new { SourceId = otherId, AcademicYearId = year.Id, ActorId = owner.ActorId,
					Reason = "Reviewed this explicit assessment's own billing year." }),
				JsonSerializer.Serialize(new { SourceId = otherId, ActorId = owner.ActorId,
					AssessmentDate = "2027-11-17", DueDate = "2027-11-24", Reason = "Explicit school dates." }),
				"Independent saved assessment year.", owner.TenantId);
			var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			await charges.InsertAsync(other, true);
			var current = await charges.GetAsync(owner.AssessmentId);
			var unknown = new LibraryCharge(Guid.NewGuid(), owner.PersonId, 1, current.ChargeCategoryId, null, 12.25m,
				new DateTime(2001, 1, 1), new DateTime(2001, 1, 8), owner.TenantId);
			await charges.InsertAsync(unknown, true);
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.TenantId == owner.TenantId && row.UserId == owner.ActorId, true);
			await assignments.InsertAsync(new(Guid.NewGuid(), owner.TenantId, userId: owner.ActorId, academicYearId: owner.YearId,
				educationBoardId: placement.BoardId, educationMediumId: placement.MediumId,
				educationMediumStageId: placement.StageId, description: "Actual2026 member target scope"), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(owner.ActorId);
			return new MemberChargeYears(owner, year.Id, other.Id, unknown.Id);
		});
	}

	private Task AddMemberChargeYearAuthorityAsync(Fixture owner, Guid yearId) => WithUnitOfWorkAsync(async () =>
	{
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new(Guid.NewGuid(), owner.TenantId,
			userId: owner.ActorId, academicYearId: yearId, description: "Explicit additional assessment-year authority"), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(owner.ActorId);
	});

	private sealed record MemberChargeYears(Fixture Owner, Guid OtherYearId, Guid OtherAssessmentId, Guid UnknownAssessmentId);
}

