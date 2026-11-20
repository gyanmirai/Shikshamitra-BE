using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Library.Setting.ChargeCategory;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCanonicalReturnAssessment_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData(50, true, false)]
	[InlineData(60, false, false)]
	[InlineData(60, false, true)]
	public async Task Return_Assessment_Should_Respect_The_Fixed_Category_Amount_And_Roll_Back_A_Rejected_Return(int amount, bool accepted, bool precedingValidAssessment)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider, onLoan: true));
		var categoryId = await WithUnitOfWorkAsync(async () =>
		{
			var fixedCategoryId = (await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(context.ChargeId!.Value)).ChargeCategoryId;
			var categories = GetRequiredService<IRepository<LibraryChargeCategory, Guid>>();
			var category = await categories.GetAsync(fixedCategoryId);
			category.IsFixed = true;
			category.Amount = 50m;
			await categories.UpdateAsync(category, true);
			return fixedCategoryId;
		});
		var service = GetRequiredService<ILibraryLoanAppService>();
		var before = await service.GetAsync(context.LoanId!.Value);
		var request = new LibraryLoanReturnRequest
		{
			Revision = before.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"), CopyId = context.Catalog.CopyId,
			SchoolReturnDate = new(2026, 11, 17), CopyStatus = (int)BookCopyStatus.Available,
			UnknownOriginalDatesReason = "Received the physical book; retained uncertain historic dates.",
			Assessments = [new()
			{
				ChargeCategoryId = categoryId, Amount = amount, SchoolChargeDate = new(2026, 11, 17), SchoolDueDate = new(2026, 11, 24),
				AcademicYearId = context.Catalog.YearId, AcademicYearReason = "New inspection assessment in this confirmed year.",
				Reason = "Fixed damage assessment after inspection."
			}]
		};
		if (precedingValidAssessment)
			request.Assessments.Insert(0, new()
			{
				ChargeCategoryId = categoryId, Amount = 50m, SchoolChargeDate = new(2026, 11, 17), SchoolDueDate = new(2026, 11, 24),
				AcademicYearId = context.Catalog.YearId, AcademicYearReason = "Confirmed year for the first inspection assessment.",
				Reason = "A valid first assessment must not be saved if the next assessment is invalid."
			});
		if (accepted)
		{
			var result = await service.ReturnAsync(before.Id, request);
			result.AssessmentIds.Count.ShouldBe(1);
			await WithUnitOfWorkAsync(async () =>
			{
				var charge = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(result.AssessmentIds[0]);
				charge.Amount.ShouldBe(50m);
				charge.FinanceSourceLinkId.ShouldBeNull();
			});
			return;
		}
		(await Should.ThrowAsync<BusinessException>(() => service.ReturnAsync(before.Id, request))).Code.ShouldBe(LibraryErrorCodes.InvalidCharge);
		(await service.GetAsync(before.Id)).Status.ShouldBe((int)BookIssueStatus.Issued);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.Catalog.CopyId)).Status.ShouldBe(BookCopyStatus.Issued);
			(await GetRequiredService<IRepository<LibraryCharge, Guid>>().CountAsync(row => row.BookIssueId == before.Id)).ShouldBe(1);
		});
	}

	[Fact]
	public async Task A_New_Return_Assessment_Should_Save_Its_Own_Explicit_Year_And_Remain_Unposted_Without_Finance_Setup()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider, onLoan: true));
		var categoryId = await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(context.ChargeId!.Value)).ChargeCategoryId);
		var service = GetRequiredService<ILibraryLoanAppService>();
		var before = await service.GetAsync(context.LoanId!.Value);

		var returned = await service.ReturnAsync(before.Id, new()
		{
			Revision = before.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"), CopyId = context.Catalog.CopyId,
			SchoolReturnDate = new(2026, 11, 17), CopyStatus = (int)BookCopyStatus.Available,
			UnknownOriginalDatesReason = "The old issue dates remain unknown; received and inspected the physical item.",
			Assessments = [new()
			{
				ChargeCategoryId = categoryId, Amount = 25m, SchoolChargeDate = new(2026, 11, 17), SchoolDueDate = new(2026, 11, 24),
				AcademicYearId = context.Catalog.YearId, AcademicYearReason = "New assessment in the selected current year; original loan year is unknown.",
				Reason = "Inspection found a damaged cover."
			}]
		});

		returned.Loan.Status.ShouldBe((int)BookIssueStatus.Returned);
		returned.Loan.AcademicYearId.ShouldBeNull();
		returned.AssessmentIds.Count.ShouldBe(1);
		await WithUnitOfWorkAsync(async () =>
		{
			var charge = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(returned.AssessmentIds[0]);
			charge.AcademicYearId.ShouldBe(context.Catalog.YearId);
			charge.AcademicYearProvenance.ShouldBe(LibraryAcademicYearProvenance.ExplicitAssessmentContext);
			charge.Amount.ShouldBe(25m);
			charge.SchoolDueDate.ShouldBe(new DateOnly(2026, 11, 24));
			charge.FinanceSourceLinkId.ShouldBeNull();
			charge.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
		});
	}
}
