using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.CashManagement;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private async Task<MovedTypedCashFixture> SeedMovedTypedCashAsync()
	{
		MovedCashSource? movement = null;
		var settlement = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee, prepareSource: async owner =>
		{
			movement = await PrepareMovedCashLoanSourceAsync(owner);
		});
		var source = movement ?? throw new InvalidOperationException("The real recorded cash source must be captured before posting.");
		var owner = settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(
			ShikshaMitraSubmodulePermissions.Finance.Accounts_CashManagement.View,
			"R", $"external-finance-{owner.ActorId:N}", true));
		await ConfigureAndApproveTypedAsync(settlement);
		var form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(settlement.Post.Source));
		var collected = await WithUnitOfWorkAsync(() => TypedService(owner).CollectAsync(CashCollectionInput(settlement, form.Revision)));
		return await WithUnitOfWorkAsync(async () =>
		{
			var allocation = (await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>()
				.GetListAsync(row => row.FeeRecordMetadataId == collected.CollectionId)).Single();
			var cash = (await GetRequiredService<IRepository<CashCollection, Guid>>()
				.GetListAsync(row => row.SourceId == collected.CollectionId)).Single();
			cash.EmployeeId.ShouldBe(owner.PersonId);
			cash.StudentId.ShouldBeNull();
			cash.FinanceFeeAccountId.ShouldBeNull();
			cash.Amount.ShouldBe(40.25m);
			cash.PaymentInstrumentId.ShouldNotBeNull();
			var assignments = GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.TenantId == owner.TenantId && row.EmployeeId == owner.PersonId, true);
			await assignments.InsertAsync(new(Guid.NewGuid(), owner.TenantId, owner.PersonId, source.Current.StageId), true);

			// A genuine unrelated Student makes the existing manual-context predicate
			// true for the replacement scope. This Student is never a payer/alias and
			// receives no fee assignment. The Employee payment remains exclusively Employee.
			var standard = new Standard(Guid.NewGuid(), owner.TenantId, "5", 10, source.Current.StageId, null);
			await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
			var student = Student.CreateDirectRegistration(Guid.NewGuid(), owner.TenantId, "Independent", null, "School student",
				owner.YearId, source.Current.BoardId, source.Current.MediumId, source.Current.StageId, standard.Id, null,
				new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero), GenderEnum.Female, "9999999998");
			student.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
			await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
			return new MovedTypedCashFixture(settlement, collected, allocation.Id, cash.Id, source, student.Id);
		});
	}

	private Task<MovedCashSource> PrepareMovedCashLoanSourceAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		owner.PayerKind.ShouldBe(FinanceExternalPayerKind.Employee);
		var original = await CreateMovedCashPlacementAsync(owner, EducationBoardType.CBSE, "Original recorded cash");
		var current = await CreateMovedCashPlacementAsync(owner, EducationBoardType.GSEB, "Replacement cash");
		original.BoardId.ShouldNotBe(current.BoardId);
		original.MediumId.ShouldNotBe(current.MediumId);
		var organization = new EmployeeOrganizationAssignment(Guid.NewGuid(), owner.TenantId, owner.PersonId, original.StageId);
		await GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>().InsertAsync(organization, true);
		var category = new BookCategory("Recorded staff cash source", "Named historical source authority", owner.YearId, owner.TenantId);
		await GetRequiredService<IRepository<BookCategory, Guid>>().InsertAsync(category, true);
		var section = new LibrarySection(Guid.NewGuid(), owner.TenantId, owner.YearId, "Recorded Library", 1);
		await GetRequiredService<IRepository<LibrarySection, Guid>>().InsertAsync(section, true);
		var shelf = new LibraryShelf(Guid.NewGuid(), section.Id, "A1");
		await GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(shelf, true);
		var book = new Book("Recorded staff cash obligation", "", "Library author", "", "", "", category.Id,
			Language.English, "", 0m, 0, 1, owner.YearId, owner.TenantId);
		await GetRequiredService<IRepository<Book, Guid>>().InsertAsync(book, true);
		var copy = new BookCopy(Guid.NewGuid(), book.Id, 1, $"CS-{Guid.NewGuid():N}", section.Id, shelf.Id, owner.TenantId)
			{ Status = BookCopyStatus.Issued };
		await GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(copy, true);
		var employee = await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(owner.PersonId, includeDetails: false);
		var policy = JsonSerializer.Serialize(new { Fixture = "Named historical loan input", Purpose = "Recorded staff cash authority" });
		var borrower = JsonSerializer.Serialize(new { Version = 1, Id = employee.Id,
			MemberName = employee.EmployeeName, MemberIdentifier = employee.EmployeeCode, Role = 1, AcademicYearId = owner.YearId });
		var calendar = JsonSerializer.Serialize(new { Fixture = "Explicit historical school dates", IssueDate = "2026-11-10", DueDate = "2026-11-24" });
		var loan = BookIssue.CreateCivil(Guid.NewGuid(), owner.TenantId, copy.Id, copy.AccessionNumber, owner.PersonId, 1,
			new(2026, 11, 10), new(2026, 11, 24), new(owner.YearId, null, null, null, null, null,
				policy, borrower, calendar, owner.ActorId, new DateTimeOffset(GetRequiredService<IClock>().Now.ToUniversalTime())));
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
		return new MovedCashSource(loan.Id, owner.YearId, capture.Id, organization.Id, original, current);
	});

	private async Task<MovedCashPlacement> CreateMovedCashPlacementAsync(Fixture owner, EducationBoardType type, string label)
	{
		var board = new EducationBoard(Guid.NewGuid(), owner.TenantId, type, owner.YearId);
		await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
		var medium = new EducationMedium(Guid.NewGuid(), owner.TenantId, label, board.Id);
		await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
		var stage = new EducationMediumStage(Guid.NewGuid(), owner.TenantId, label, null, medium.Id);
		await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
		return new(board.Id, medium.Id, stage.Id);
	}

	private Task SetMovedCashReaderScopeAsync(MovedTypedCashFixture fixture, bool currentPlacement) => WithUnitOfWorkAsync(async () =>
	{
		var owner = fixture.Settlement.Owner;
		var placement = currentPlacement ? fixture.Source.Current : fixture.Source.Original;
		var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await assignments.DeleteAsync(row => row.TenantId == owner.TenantId && row.UserId == owner.ActorId, true);
		await assignments.InsertAsync(new(Guid.NewGuid(), owner.TenantId, userId: owner.ActorId,
			academicYearId: fixture.Source.LoanYearId, educationBoardId: placement.BoardId, educationMediumId: placement.MediumId,
			educationMediumStageId: placement.StageId, description: "Current actual cash reader authority"), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(owner.ActorId);
	});

	private CashManagementAppService MovedCashService(Fixture owner)
	{
		var services = new DownstreamRefundServices(new FinanceServices(ServiceProvider, owner, useStoredSource: true));
		var service = ActivatorUtilities.CreateInstance<CashManagementAppService>(services);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		return service;
	}

	private Task AssertMovedCashAuthorityAsync(MovedTypedCashFixture fixture, bool currentPlacement) => WithUnitOfWorkAsync(async () =>
	{
		var owner = fixture.Settlement.Owner;
		(await GetRequiredService<IUserScopeService>().IsAdminUserAsync(owner.ActorId)).ShouldBeFalse();
		var services = new DownstreamRefundServices(new FinanceServices(ServiceProvider, owner, useStoredSource: true));
		var authorization = services.GetRequiredService<IAuthorizationService>();
		(await authorization.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
		(await authorization.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Accounts_CashManagement.View)).ShouldBeTrue();
		var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		scope.HasTenantWideScope().ShouldBeFalse();
		(await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(fixture.Source.LoanId, scope)).ShouldBe(!currentPlacement);
		(await GetRequiredService<IAsyncQueryableExecuter>().AnyAsync((await GetRequiredService<ILibraryAccessService>()
			.GetEmployeesAsync(scope)).Where(row => row.Id == owner.PersonId))).ShouldBe(currentPlacement);
		var financeScope = services.GetRequiredService<ShikshaMitra.Finance.FinanceScopeAccessService>();
		(await financeScope.IsAcademicContextAccessibleAsync(owner.YearId, null, null, null, null)).ShouldBe(currentPlacement);
		(await GetRequiredService<IAsyncQueryableExecuter>().AnyAsync((await financeScope.GetScopedStudentIdsQueryAsync())
			.Where(id => id == fixture.UnrelatedStudentId))).ShouldBe(currentPlacement);
		var capture = await GetRequiredService<IRepository<LibraryBorrowerScopeCapture, Guid>>().GetAsync(fixture.Source.CaptureId);
		capture.IsComplete.ShouldBeTrue();
		capture.CanonicalEmployeeId.ShouldBe(owner.PersonId);
		capture.AcademicYearId.ShouldBe(fixture.Source.LoanYearId);
		var tuple = (await GetRequiredService<IRepository<LibraryBorrowerAssignmentSnapshot, Guid>>()
			.GetListAsync(row => row.CaptureId == capture.Id)).Single();
		tuple.OrganizationAssignmentId.ShouldBe(fixture.Source.OriginalAssignmentId);
		tuple.BoardId.ShouldBe(fixture.Source.Original.BoardId);
		tuple.MediumId.ShouldBe(fixture.Source.Original.MediumId);
		tuple.StageId.ShouldBe(fixture.Source.Original.StageId);
		var reader = services.GetRequiredService<IFinanceExternalReceivableSourceReader>();
		if (currentPlacement)
		{
			var denied = await Should.ThrowAsync<BusinessException>(() => reader.ReadAuthorizedAsync(fixture.Settlement.Post.Source));
			denied.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
		else (await reader.ReadAuthorizedAsync(fixture.Settlement.Post.Source)).Payer.EmployeeId.ShouldBe(owner.PersonId);
	});

	private Task AssertMovedCashOriginalSourceIntactAsync(MovedTypedCashFixture fixture) => WithUnitOfWorkAsync(async () =>
	{
		var owner = fixture.Settlement.Owner;
		var cash = await GetRequiredService<IRepository<CashCollection, Guid>>().GetAsync(fixture.CashId);
		cash.TenantId.ShouldBe(owner.TenantId);
		cash.AcademicYearId.ShouldBe(owner.YearId);
		cash.SourceId.ShouldBe(fixture.Collection.CollectionId);
		cash.EmployeeId.ShouldBe(owner.PersonId);
		cash.StudentId.ShouldBeNull();
		cash.FinanceFeeAccountId.ShouldBeNull();
		cash.Amount.ShouldBe(40.25m);
		cash.Status.ShouldBe(0);
		var instrument = await GetRequiredService<IRepository<PaymentMethodRecord, Guid>>().GetAsync(cash.PaymentInstrumentId!.Value);
		instrument.FeeRecordMetadataId.ShouldBe(cash.SourceId);
		instrument.EmployeeId.ShouldBe(owner.PersonId);
		instrument.PaymentMethod.ShouldBe(PaymentMethodEnum.Cash);
		instrument.Amount.ShouldBe(cash.Amount);
		var header = await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetAsync(cash.SourceId);
		header.EmployeeId.ShouldBe(owner.PersonId);
		header.StudentId.ShouldBeNull();
		header.FinanceFeeAccountId.ShouldBeNull();
		header.TotalPaidFee.ShouldBe(40.25m);
		var allocation = await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().GetAsync(fixture.AllocationId);
		allocation.FeeRecordMetadataId.ShouldBe(header.Id);
		allocation.EmployeeId.ShouldBe(owner.PersonId);
		allocation.ChargeId.ShouldBe(fixture.Settlement.Posted.FinanceChargeId);
		var charge = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(owner.AssessmentId);
		charge.BookIssueId.ShouldBe(fixture.Source.LoanId);
		charge.AcademicYearId.ShouldBe(owner.YearId);
		(await GetRequiredService<IRepository<CashCollection, Guid>>().CountAsync()).ShouldBe(1);
		(await GetRequiredService<IRepository<StudentFeeStructure, Guid>>()
			.CountAsync(row => row.StudentId == fixture.UnrelatedStudentId)).ShouldBe(0);
	});

	private sealed record MovedCashPlacement(Guid BoardId, Guid MediumId, Guid StageId);
	private sealed record MovedCashSource(Guid LoanId, Guid LoanYearId, Guid CaptureId, Guid OriginalAssignmentId,
		MovedCashPlacement Original, MovedCashPlacement Current);
	private sealed record MovedTypedCashFixture(TypedSettlement Settlement, FinanceExternalCollectionDto Collection,
		Guid AllocationId, Guid CashId, MovedCashSource Source, Guid UnrelatedStudentId);
}

