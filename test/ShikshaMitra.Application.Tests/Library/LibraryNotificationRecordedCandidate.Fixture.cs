using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	private sealed record RecordedCandidate(Context Context, NotificationClassContext Placement, Guid MemberId,
		Guid SourceId, bool Reservation, Guid? LaterStaffStageId);

	private async Task<RecordedCandidate> SeedRecordedCandidateAsync(Context context, bool staff, bool reservation)
	{
		var placement = await SeedNotificationClassesAsync(context);
		var person = staff ? await SeedNotificationCandidateStaffAsync(context, placement) : (context.StudentId, (Guid?)null);
		var sourceId = await WithUnitOfWorkAsync<Guid>(async () =>
		{
			var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
			var copy = await copies.GetAsync(context.CopyId);
			if (reservation)
			{
				copy.Status = BookCopyStatus.UnderRepair;
				await copies.UpdateAsync(copy, true);
				var request = await GetRequiredService<LibraryReservationCoordinator>().CreateAsync(copy.BookId, person.Item1,
					placement.YearId, new(2026, 11, 17), new(2026, 11, 18), LibraryOperationActor.User(context.ActorId));
				request.State.ShouldBe(LibraryReservationState.Waiting);
				if (staff) await AssertCandidateStaffCaptureAsync(request.BorrowerScopeCaptureId, person.Item1, request.Id,
					LibraryBorrowerScopeSourceKind.Reservation, placement.YearId);
				return request.Id;
			}
			var loan = BookIssue.CreateCivil(Guid.NewGuid(), context.TenantId, copy.Id, copy.AccessionNumber, person.Item1, staff ? 1 : 0,
				new(2026, 11, 10), new(2026, 11, 19), new(placement.YearId, staff ? null : placement.BoardId,
					staff ? null : placement.MediumId, staff ? null : placement.StageId, staff ? null : placement.OriginalClassId,
					null, "{}", "{}", "{}", context.ActorId, new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero)));
			if (staff)
			{
				loan.BindStaffScopeCapture(await GetRequiredService<LibraryStaffScopeCaptureService>().CaptureAsync(
					LibraryBorrowerScopeSourceKind.Loan, loan.Id, person.Item1, placement.YearId,
					LibraryBorrowerScopeProvenance.CapturedAtOperation, LibraryOperationActor.User(context.ActorId)));
				await AssertCandidateStaffCaptureAsync(loan.BorrowerScopeCaptureId, person.Item1, loan.Id,
					LibraryBorrowerScopeSourceKind.Loan, placement.YearId);
			}
			await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
			copy.Status = BookCopyStatus.Issued;
			await copies.UpdateAsync(copy, true);
			return loan.Id;
		});
		return new(context, placement, person.Item1, sourceId, reservation, person.Item2);
	}

	private async Task AssertCandidateStaffCaptureAsync(Guid? id, Guid employeeId, Guid sourceId,
		LibraryBorrowerScopeSourceKind kind, Guid yearId)
	{
		id.ShouldNotBeNull();
		var capture = await GetRequiredService<IRepository<LibraryBorrowerScopeCapture, Guid>>().GetAsync(id.Value);
		capture.IsComplete.ShouldBeTrue();
		capture.SourceKind.ShouldBe(kind); capture.SourceId.ShouldBe(sourceId); capture.CanonicalEmployeeId.ShouldBe(employeeId);
		capture.AcademicYearId.ShouldBe(yearId); capture.Version.ShouldBe(1);
		var children = await GetRequiredService<IRepository<LibraryBorrowerAssignmentSnapshot, Guid>>().GetListAsync(row => row.CaptureId == id.Value);
		var assignments = await GetRequiredService<IRepository<ShikshaMitra.HR.Employees.EmployeeOrganizationAssignment, Guid>>()
			.GetListAsync(row => row.EmployeeId == employeeId);
		children.Count.ShouldBe(2);
		children.Select(row => row.OrganizationAssignmentId).Order().ShouldBe(assignments.Select(row => row.Id).Order());
	}

	private async Task MoveRecordedCandidateAsync(RecordedCandidate fixture)
	{
		if (fixture.LaterStaffStageId.HasValue) await MoveNotificationCandidateStaffAsync(fixture);
		else await MoveNotificationStudentAsync(fixture.Context, fixture.Placement, fixture.Placement.NextClassId);
	}

	private async Task SetRecordedCandidateScopeAsync(RecordedCandidate fixture, bool currentPlacement)
	{
		if (fixture.LaterStaffStageId.HasValue) await SetNotificationCandidateStaffScopeAsync(fixture, currentPlacement);
		else await AssignNotificationClassAsync(fixture.Context, fixture.Placement,
			currentPlacement ? fixture.Placement.NextClassId : fixture.Placement.OriginalClassId);
		await WithUnitOfWorkAsync(async () =>
		{
			var scope = await GetRequiredService<IUserScopeService>().GetUserScopeAsync(fixture.Context.ActorId);
			var allowed = fixture.Reservation
				? await GetRequiredService<LibraryStoredReservationScope>().CanReadAsync(fixture.SourceId, scope)
				: await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(fixture.SourceId, scope);
			allowed.ShouldBe(!currentPlacement, "The committed source must retain its original scope independently of the current person.");
			var currentIds = await GetRequiredService<ILibraryAccessService>().GetMemberIdsAsync(scope);
			if (currentPlacement) currentIds.ShouldContain(fixture.MemberId);
			else currentIds.ShouldNotContain(fixture.MemberId);
		});
	}

	private static SendLibraryNotificationRequest RecordedCandidateRequest(RecordedCandidate fixture) => new()
	{
		EventType = fixture.Reservation ? LibraryNotificationEventTypes.ReservationConfirmed : LibraryNotificationEventTypes.BookIssueConfirmation,
		Channel = LibraryNotificationChannels.Email, Subject = "Recorded Library notice", Message = "Please review this Library record.",
		RelatedEntityType = fixture.Reservation ? nameof(BookReservation) : nameof(BookIssue), RelatedEntityId = fixture.SourceId,
		Recipients = [new() { MemberId = fixture.MemberId }]
	};
}
