using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Typed_Moved_Employee_Refund_Detail_Should_Retain_Original_Recorded_Loan_Authority()
	{
		var fixture = await SeedMovedTypedRefundAsync();
		var owner = fixture.Refund.Source.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await SetMovedRefundReaderScopeAsync(fixture, currentPlacement: false);
		await AssertMovedRefundReaderAuthorityAsync(fixture, currentPlacement: false);

		var detail = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetAsync(fixture.Refund.Submitted.Id));

		detail.Id.ShouldBe(fixture.Refund.Submitted.Id);
		detail.EmployeeId.ShouldBe(owner.PersonId);
		detail.StudentId.ShouldBeNull();
		detail.FinanceExternalSourceLinkId.ShouldBe(fixture.Refund.Submitted.FinanceExternalSourceLinkId);
		detail.AcademicYearId.ShouldBe(owner.YearId);
		detail.TotalAmount.ShouldBe(15.25m);
		detail.CalculatedAmount.ShouldBe(15m);
		detail.ProcessedAmount.ShouldBe(0m);
		detail.OriginalReceiptId.ShouldBe(fixture.Refund.Source.Collection.ReceiptId);
	}

	[Fact]
	public async Task Typed_Moved_Employee_Refund_Detail_Should_Deny_Current_Placement_Only_Authority()
	{
		var fixture = await SeedMovedTypedRefundAsync();
		var owner = fixture.Refund.Source.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await SetMovedRefundReaderScopeAsync(fixture, currentPlacement: true);
		await AssertMovedRefundReaderAuthorityAsync(fixture, currentPlacement: true);

		var denied = await Record.ExceptionAsync(() => WithUnitOfWorkAsync(() =>
			DownstreamRefundService(owner).GetAsync(fixture.Refund.Submitted.Id)));
		denied.ShouldNotBeNull();
		if (denied is BusinessException business) business.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		else denied.ShouldBeOfType<AbpAuthorizationException>();

		await AssertTypedRefundStillSubmittedAsync(fixture.Refund);
	}

	[Fact]
	public async Task Typed_Moved_Employee_Refund_List_Should_Include_Original_Recorded_Source_In_Counts_And_Pages()
	{
		var fixture = await SeedMovedTypedRefundAsync();
		var owner = fixture.Refund.Source.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await SetMovedRefundReaderScopeAsync(fixture, currentPlacement: false);
		await AssertMovedRefundReaderAuthorityAsync(fixture, currentPlacement: false);

		var first = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = owner.YearId, WorkflowVersion = 1, MaxResultCount = 1, SkipCount = 0
		}));
		var second = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = owner.YearId, WorkflowVersion = 1, MaxResultCount = 1, SkipCount = 1
		}));

		first.TotalCount.ShouldBe(1);
		first.Items.Single().Id.ShouldBe(fixture.Refund.Submitted.Id);
		first.Items.Single().EmployeeId.ShouldBe(owner.PersonId);
		first.Items.Single().FinanceExternalSourceLinkId.ShouldBe(fixture.Refund.Submitted.FinanceExternalSourceLinkId);
		second.TotalCount.ShouldBe(1);
		second.Items.ShouldBeEmpty();
	}

	[Fact]
	public async Task Typed_Moved_Employee_Refund_List_Should_Exclude_Current_Placement_Only_Source_From_Counts_And_Pages()
	{
		var fixture = await SeedMovedTypedRefundAsync();
		var owner = fixture.Refund.Source.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await SetMovedRefundReaderScopeAsync(fixture, currentPlacement: true);
		await AssertMovedRefundReaderAuthorityAsync(fixture, currentPlacement: true);

		var first = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = owner.YearId, WorkflowVersion = 1, MaxResultCount = 1, SkipCount = 0
		}));
		var second = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = owner.YearId, WorkflowVersion = 1, MaxResultCount = 1, SkipCount = 1
		}));

		first.TotalCount.ShouldBe(0);
		first.Items.ShouldBeEmpty();
		second.TotalCount.ShouldBe(0);
		second.Items.ShouldBeEmpty();
		await AssertTypedRefundStillSubmittedAsync(fixture.Refund);
	}

	private async Task<MovedTypedRefundFixture> SeedMovedTypedRefundAsync()
	{
		MovedRefundSource? movement = null;
		var refund = await SeedSubmittedTypedRefundAsync(FinanceExternalPayerKind.Employee, prepareSource: async sourceOwner =>
		{
			movement = await PrepareMovedRefundLoanSourceAsync(sourceOwner);
		});
		var source = movement ?? throw new InvalidOperationException("The original recorded loan source was not prepared before posting.");
		var owner = refund.Source.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await WithUnitOfWorkAsync(async () =>
		{
			var charge = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(owner.AssessmentId);
			charge.BookIssueId.ShouldBe(source.LoanId);
			charge.FinanceSourceLinkId.ShouldBe(refund.Submitted.FinanceExternalSourceLinkId);
			charge.AcademicYearId.ShouldBe(owner.YearId);
			refund.Submitted.Status.ShouldBe(FeeRefundStatus.Submitted);
			var assignments = GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.TenantId == owner.TenantId && row.EmployeeId == owner.PersonId, true);
			await assignments.InsertAsync(new(Guid.NewGuid(), owner.TenantId, owner.PersonId, source.Current.StageId), true);
		});
		return new(refund, source);
	}

	private Task<MovedRefundSource> PrepareMovedRefundLoanSourceAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		owner.PayerKind.ShouldBe(FinanceExternalPayerKind.Employee);
		var original = await CreateMovedRefundPlacementAsync(owner, EducationBoardType.CBSE, "Original recorded staff");
		var current = await CreateMovedRefundPlacementAsync(owner, EducationBoardType.GSEB, "Replacement staff");
		original.BoardId.ShouldNotBe(current.BoardId);
		original.MediumId.ShouldNotBe(current.MediumId);
		var originalAssignment = new EmployeeOrganizationAssignment(Guid.NewGuid(), owner.TenantId, owner.PersonId, original.StageId);
		await GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>().InsertAsync(originalAssignment, true);
		var category = new BookCategory("Recorded staff refund source", "Explicit historical source for authorization", owner.YearId, owner.TenantId);
		await GetRequiredService<IRepository<BookCategory, Guid>>().InsertAsync(category, true);
		var section = new LibrarySection(Guid.NewGuid(), owner.TenantId, owner.YearId, "Recorded Library", 1);
		await GetRequiredService<IRepository<LibrarySection, Guid>>().InsertAsync(section, true);
		var shelf = new LibraryShelf(Guid.NewGuid(), section.Id, "A1");
		await GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(shelf, true);
		var book = new Book("Recorded staff obligation", "", "Library author", "", "", "", category.Id,
			Language.English, "", 0m, 0, 1, owner.YearId, owner.TenantId);
		await GetRequiredService<IRepository<Book, Guid>>().InsertAsync(book, true);
		var copy = new BookCopy(Guid.NewGuid(), book.Id, 1, $"RF-{Guid.NewGuid():N}", section.Id, shelf.Id, owner.TenantId)
			{ Status = BookCopyStatus.Issued };
		await GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(copy, true);
		var policyEvidence = JsonSerializer.Serialize(new { Fixture = "Explicit historical loan input", Purpose = "Recorded staff scope authorization" });
		var employee = await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(owner.PersonId, includeDetails: false);
		var borrowerEvidence = JsonSerializer.Serialize(new
		{
			Version = 1, Id = employee.Id, MemberName = employee.EmployeeName, MemberIdentifier = employee.EmployeeCode,
			Role = 1, AcademicYearId = owner.YearId
		});
		var calendarEvidence = JsonSerializer.Serialize(new { Fixture = "Explicit historical school dates", IssueDate = "2026-11-10", DueDate = "2026-11-24" });
		var loan = BookIssue.CreateCivil(Guid.NewGuid(), owner.TenantId, copy.Id, copy.AccessionNumber, owner.PersonId, 1,
			new(2026, 11, 10), new(2026, 11, 24), new(owner.YearId, null, null, null, null, null,
				policyEvidence, borrowerEvidence, calendarEvidence, owner.ActorId,
				new DateTimeOffset(GetRequiredService<IClock>().Now.ToUniversalTime())));
		var capture = await GetRequiredService<LibraryStaffScopeCaptureService>().CaptureAsync(LibraryBorrowerScopeSourceKind.Loan,
			loan.Id, owner.PersonId, owner.YearId, LibraryBorrowerScopeProvenance.CapturedAtOperation, LibraryOperationActor.User(owner.ActorId));
		capture.IsComplete.ShouldBeTrue();
		capture.ExpectedAssignmentCount.ShouldBe(1);
		loan.BindStaffScopeCapture(capture);
		await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
		var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
		var charge = await charges.GetAsync(owner.AssessmentId);
		charge.FinanceSourceLinkId.ShouldBeNull();
		charge.MemberId.ShouldBe(owner.PersonId);
		charge.MemberRole.ShouldBe(1);
		charge.BookIssueId = loan.Id;
		await charges.UpdateAsync(charge, true);
		return new MovedRefundSource(loan.Id, owner.YearId, capture.Id, originalAssignment.Id, original, current);
	});

	private async Task<MovedRefundPlacement> CreateMovedRefundPlacementAsync(Fixture owner, EducationBoardType type, string label)
	{
		var board = new EducationBoard(Guid.NewGuid(), owner.TenantId, type, owner.YearId);
		await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
		var medium = new EducationMedium(Guid.NewGuid(), owner.TenantId, label, board.Id);
		await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
		var stage = new EducationMediumStage(Guid.NewGuid(), owner.TenantId, label, null, medium.Id);
		await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
		return new(board.Id, medium.Id, stage.Id);
	}

	private Task SetMovedRefundReaderScopeAsync(MovedTypedRefundFixture fixture, bool currentPlacement) => WithUnitOfWorkAsync(async () =>
	{
		var owner = fixture.Refund.Source.Settlement.Owner;
		var placement = currentPlacement ? fixture.Source.Current : fixture.Source.Original;
		var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await assignments.DeleteAsync(row => row.TenantId == owner.TenantId && row.UserId == owner.ActorId, true);
		await assignments.InsertAsync(new(Guid.NewGuid(), owner.TenantId, userId: owner.ActorId,
			academicYearId: fixture.Source.LoanYearId, educationBoardId: placement.BoardId, educationMediumId: placement.MediumId,
			educationMediumStageId: placement.StageId, description: "Current real reader scope for the recorded refund source"), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(owner.ActorId);
	});

	private Task AssertMovedRefundReaderAuthorityAsync(MovedTypedRefundFixture fixture, bool currentPlacement) => WithUnitOfWorkAsync(async () =>
	{
		var owner = fixture.Refund.Source.Settlement.Owner;
		(await GetRequiredService<IUserScopeService>().IsAdminUserAsync(owner.ActorId)).ShouldBeFalse();
		var services = new DownstreamRefundServices(new FinanceServices(ServiceProvider, owner, useStoredSource: true));
		var authorization = services.GetRequiredService<IAuthorizationService>();
		(await authorization.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
		(await authorization.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)).ShouldBeTrue();
		var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		(await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(fixture.Source.LoanId, scope)).ShouldBe(!currentPlacement);
		(await GetRequiredService<IAsyncQueryableExecuter>().AnyAsync((await GetRequiredService<ILibraryAccessService>()
			.GetEmployeesAsync(scope)).Where(row => row.Id == owner.PersonId))).ShouldBe(currentPlacement);
		var loan = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(fixture.Source.LoanId);
		loan.AcademicYearId.ShouldBe(fixture.Source.LoanYearId);
		loan.MemberId.ShouldBe(owner.PersonId);
		loan.BorrowerScopeCaptureId.ShouldBe(fixture.Source.CaptureId);
		var capture = await GetRequiredService<IRepository<LibraryBorrowerScopeCapture, Guid>>().GetAsync(fixture.Source.CaptureId);
		capture.IsComplete.ShouldBeTrue();
		capture.SourceId.ShouldBe(loan.Id);
		capture.CanonicalEmployeeId.ShouldBe(owner.PersonId);
		capture.AcademicYearId.ShouldBe(fixture.Source.LoanYearId);
		capture.ExpectedAssignmentCount.ShouldBe(1);
		var tuples = await GetRequiredService<IRepository<LibraryBorrowerAssignmentSnapshot, Guid>>()
			.GetListAsync(row => row.CaptureId == capture.Id && row.TenantId == owner.TenantId);
		tuples.Count.ShouldBe(capture.ExpectedAssignmentCount);
		tuples.Single().OrganizationAssignmentId.ShouldBe(fixture.Source.OriginalAssignmentId);
		tuples.Single().StageId.ShouldBe(fixture.Source.Original.StageId);
		tuples.Single().MediumId.ShouldBe(fixture.Source.Original.MediumId);
		tuples.Single().BoardId.ShouldBe(fixture.Source.Original.BoardId);
		tuples.Single().AcademicYearId.ShouldBe(fixture.Source.LoanYearId);
		var reader = services.GetRequiredService<IFinanceExternalReceivableSourceReader>();
		if (currentPlacement)
		{
			var denied = await Should.ThrowAsync<BusinessException>(() => reader.ReadAuthorizedAsync(fixture.Refund.Source.Settlement.Post.Source));
			denied.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
		else
		{
			var facts = await reader.ReadAuthorizedAsync(fixture.Refund.Source.Settlement.Post.Source);
			facts.Payer.EmployeeId.ShouldBe(owner.PersonId);
			facts.AcademicYearId.ShouldBe(owner.YearId);
			facts.Source.SourceId.ShouldBe(owner.AssessmentId);
		}
	});

	private sealed record MovedRefundPlacement(Guid BoardId, Guid MediumId, Guid StageId);
	private sealed record MovedRefundSource(Guid LoanId, Guid LoanYearId, Guid CaptureId, Guid OriginalAssignmentId,
		MovedRefundPlacement Original, MovedRefundPlacement Current);
	private sealed record MovedTypedRefundFixture(DownstreamRefundFixture Refund, MovedRefundSource Source);
}
