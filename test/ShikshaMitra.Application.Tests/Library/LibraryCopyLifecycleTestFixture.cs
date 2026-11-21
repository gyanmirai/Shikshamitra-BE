using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.Library;

internal sealed record LibraryCopyLifecycleTestContext(LibraryPolicyTestContext Catalog, Guid BoardId, Guid MediumId,
	Guid StageId, Guid BorrowerClassId, Guid OtherClassId, Guid StudentId, Guid OtherShelfId, Guid? LoanId, Guid? ChargeId);

internal static class LibraryCopyLifecycleTestFixture
{
	public static IDisposable EnterSchool(IServiceProvider services)
	{
		var tenantId = Guid.NewGuid();
		var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		var principal = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()), new Claim(AbpClaimTypes.TenantId, tenantId.ToString())
		], "LibraryCopyLifecycleTest")));
		return new SchoolScope(tenant, principal);
	}

	public static async Task<LibraryCopyLifecycleTestContext> SeedAsync(IServiceProvider services, bool onLoan = false)
	{
		var catalog = await LibraryPolicyTestFixture.SeedAsync(services);
		var tenantId = services.GetRequiredService<ICurrentTenant>().Id;
		var board = new EducationBoard(Guid.NewGuid(), tenantId, EducationBoardType.CBSE, catalog.YearId);
		await services.GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
		var medium = new EducationMedium(Guid.NewGuid(), tenantId, "English", board.Id);
		await services.GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
		var stage = new EducationMediumStage(Guid.NewGuid(), tenantId, "Middle school", null, medium.Id);
		await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
		var borrowerClass = new Standard(Guid.NewGuid(), tenantId, "5", 10, stage.Id, null);
		var otherClass = new Standard(Guid.NewGuid(), tenantId, "6", 11, stage.Id, null);
		await services.GetRequiredService<IRepository<Standard, Guid>>().InsertManyAsync([borrowerClass, otherClass], true);
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenantId, "Library", null, "Borrower", catalog.YearId,
			board.Id, medium.Id, stage.Id, borrowerClass.Id, null,
			new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero), GenderEnum.Male, "9999999999");
		student.AdditionalDetail!.IsLibraryMember = true;
		await services.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
		var otherShelf = new LibraryShelf(Guid.NewGuid(), catalog.SectionId, "B1");
		await services.GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(otherShelf, true);
		Guid? loanId = null;
		Guid? chargeId = null;
		if (onLoan)
		{
			var copies = services.GetRequiredService<IRepository<BookCopy, Guid>>();
			var copy = await copies.GetAsync(catalog.CopyId);
			var loan = new BookIssue(Guid.NewGuid(), copy.Id, copy.AccessionNumber, student.Id, 0,
				new DateTime(2026, 11, 1), new DateTime(2026, 11, 8), tenantId);
			await services.GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
			copy.Status = BookCopyStatus.Issued;
			await copies.UpdateAsync(copy, true);
			var category = new LibraryChargeCategory(Guid.NewGuid(), tenantId, catalog.YearId, "Existing assessment", 50m, false, "Existing pending assessment");
			await services.GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(category, true);
			var charge = new LibraryCharge(Guid.NewGuid(), student.Id, 0, category.Id, loan.Id, 50m,
				new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), tenantId);
			await services.GetRequiredService<IRepository<LibraryCharge, Guid>>().InsertAsync(charge, true);
			loanId = loan.Id;
			chargeId = charge.Id;
		}
		return new(catalog, board.Id, medium.Id, stage.Id, borrowerClass.Id, otherClass.Id,
			student.Id, otherShelf.Id, loanId, chargeId);
	}

	private sealed class SchoolScope(IDisposable tenant, IDisposable principal) : IDisposable
	{
		public void Dispose()
		{
			principal.Dispose();
			tenant.Dispose();
		}
	}
}
