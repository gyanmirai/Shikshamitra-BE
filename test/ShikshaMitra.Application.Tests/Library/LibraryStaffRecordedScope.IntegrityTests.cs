using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
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
	public async Task A_Staff_Reservation_Should_Retain_Original_Assignment_Authority_Until_An_Explicit_Review()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var app = GetRequiredService<ILibraryReservationAppService>();
		var request = await app.CreateAsync(new() { BookId = fixture.Context.Catalog.BookId, MemberId = fixture.EmployeeId,
			AcademicYearId = fixture.Context.Catalog.YearId, RequestedDate = new(2026, 11, 17), RequestExpiryDate = new(2026, 12, 1) });
		await MoveStaffAsync(fixture);
		await SetReaderStageAsync(fixture, fixture.Context.StageId);

		(await app.GetAsync(request.Id, fixture.Context.Catalog.YearId)).Id.ShouldBe(request.Id);
		(await app.GetListAsync(fixture.Context.Catalog.BookId, fixture.Context.Catalog.YearId)).TotalCount.ShouldBe(1);
		await SetReaderStageAsync(fixture, fixture.LaterStageId);
		(await app.GetListAsync(fixture.Context.Catalog.BookId, fixture.Context.Catalog.YearId)).TotalCount.ShouldBe(0);
		(await Should.ThrowAsync<BusinessException>(() => app.GetAsync(request.Id, fixture.Context.Catalog.YearId))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Fact]
	public async Task Foreign_Staff_Loan_Should_Remain_Invisible_And_A_Foreign_Capture_Link_Should_Be_Rejected_By_The_Composite_Foreign_Key()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var ownLoan = await CheckoutStaffAsync(fixture);
		LibraryLoanDto foreign;
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider)) foreign = await CheckoutStaffAsync(await SeedStaffAsync());
		await SetReaderStageAsync(fixture, fixture.Context.StageId);
		var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();

		(await WithUnitOfWorkAsync(() => GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(foreign.Id, scope))).ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<ILibraryLoanAppService>().GetAsync(foreign.Id))).Code.ShouldBe(LibraryCirculationErrorCodes.LoanNotFound);
		await Should.ThrowAsync<DbUpdateException>(() => WithUnitOfWorkAsync(async () =>
		{
			var source = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(ownLoan.Id);
			typeof(BookIssue).GetProperty(nameof(BookIssue.BorrowerScopeCaptureId))!.SetValue(source, foreign.BorrowerScopeCaptureId);
			await GetRequiredService<IRepository<BookIssue, Guid>>().UpdateAsync(source, true);
		}));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Persisted_Staff_Capture_And_Assignment_Evidence_Should_Reject_Ef_Updates(bool child)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loan = await CheckoutStaffAsync(fixture);

		await Should.ThrowAsync<InvalidOperationException>(() => WithUnitOfWorkAsync(async () =>
		{
			if (child)
			{
				var repository = GetRequiredService<IRepository<LibraryBorrowerAssignmentSnapshot, Guid>>();
				var row = (await repository.GetListAsync(item => item.CaptureId == loan.BorrowerScopeCaptureId)).First();
				typeof(LibraryBorrowerAssignmentSnapshot).GetProperty(nameof(LibraryBorrowerAssignmentSnapshot.StageId))!.SetValue(row, fixture.LaterStageId);
				await repository.UpdateAsync(row, true);
			}
			else
			{
				var repository = GetRequiredService<IRepository<LibraryBorrowerScopeCapture, Guid>>();
				var row = await repository.GetAsync(loan.BorrowerScopeCaptureId!.Value);
				typeof(LibraryBorrowerScopeCapture).GetProperty(nameof(LibraryBorrowerScopeCapture.SourceName))!.SetValue(row, "Changed after capture");
				await repository.UpdateAsync(row, true);
			}
		}));
	}

	[Fact]
	public async Task Staff_Capture_Actor_Constraint_Should_Reject_A_System_Actor_Without_A_System_Purpose()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await Should.ThrowAsync<DbUpdateException>(() => WithUnitOfWorkAsync(async () =>
		{
			var capture = new LibraryBorrowerScopeCapture(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id!.Value,
				LibraryBorrowerScopeSourceKind.Loan, Guid.NewGuid(), fixture.EmployeeId, fixture.Context.Catalog.YearId, 1,
				LibraryBorrowerScopeProvenance.CapturedAtOperation, LibraryOperationActor.User(GetRequiredService<ICurrentUser>().Id!.Value),
				new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero), []);
			typeof(LibraryBorrowerScopeCapture).GetProperty(nameof(LibraryBorrowerScopeCapture.ActorKind))!.SetValue(capture, LibraryActorKind.System);
			typeof(LibraryBorrowerScopeCapture).GetProperty(nameof(LibraryBorrowerScopeCapture.ActorId))!.SetValue(capture, null);
			await GetRequiredService<IRepository<LibraryBorrowerScopeCapture, Guid>>().InsertAsync(capture, true);
		}));
	}
}
