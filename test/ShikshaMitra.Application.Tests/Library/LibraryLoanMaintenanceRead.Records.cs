using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Library;

public partial class LibraryLoanMaintenanceRead_Tests
{
	private Task<Guid> SeedCivilLoanAsync(LibraryCopyLifecycleTestContext fixture, bool otherBorrower = false,
		bool legacyReturned = false) =>
		WithUnitOfWorkAsync<Guid>(async () =>
		{
			var tenant = GetRequiredService<ICurrentTenant>().Id;
			var standardId = otherBorrower ? fixture.OtherClassId : fixture.BorrowerClassId;
			var borrowerId = fixture.StudentId;
			if (otherBorrower)
			{
				var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenant, "Other", null, "Borrower",
					fixture.Catalog.YearId, fixture.BoardId, fixture.MediumId, fixture.StageId, standardId, null,
					new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero), GenderEnum.Male, "9999999998");
				student.AdditionalDetail!.IsLibraryMember = true;
				await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
				borrowerId = student.Id;
			}
			var policy = JsonSerializer.Serialize(new LibraryBorrowingPolicySnapshotDto
			{
				IsLegacyManualDate = true, Role = 0, Terms = new() { MaxBooksAtTime = 4, ReservationLimit = 1 }
			});
			var borrower = JsonSerializer.Serialize(new
			{
				Version = 1, Id = borrowerId, Role = 0, AcademicYearId = fixture.Catalog.YearId,
				MemberName = "Recorded borrower", MemberIdentifier = "RECORDED-CARD",
				AcademicContext = new { AcademicBoardId = fixture.BoardId, AcademicMediumId = fixture.MediumId,
					AcademicMediumStageId = fixture.StageId, StandardId = standardId, DivisionId = (Guid?)null }
			});
			var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
			var copy = await copies.GetAsync(fixture.Catalog.CopyId);
			var loan = BookIssue.CreateCivil(Guid.NewGuid(), tenant, copy.Id, copy.AccessionNumber, borrowerId, 0,
				new(2026, 11, 17), new(2026, 11, 24), new(fixture.Catalog.YearId, fixture.BoardId, fixture.MediumId,
					fixture.StageId, standardId, null, policy, borrower, "{}", GetRequiredService<ICurrentUser>().Id!.Value,
					new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero)));
			if (legacyReturned)
			{
				// Persisted legacy return evidence; the factory's confirmed issue/due values remain intact.
				loan.Status = BookIssueStatus.Returned;
				loan.ReturnDate = new DateTime(2026, 11, 18);
			}
			await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
			copy.Status = legacyReturned ? BookCopyStatus.Available : BookCopyStatus.Issued;
			await copies.UpdateAsync(copy, true);
			return loan.Id;
		});

	private Task AssertCivilReviewShapeAsync(Guid loanId, bool legacyReturned) => WithUnitOfWorkAsync(async () =>
	{
		var loan = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(loanId);
		loan.SchoolIssueDate.ShouldBe(new DateOnly(2026, 11, 17));
		loan.SchoolDueDate.ShouldBe(new DateOnly(2026, 11, 24));
		loan.OriginalSchoolDueDate.ShouldBe(new DateOnly(2026, 11, 24));
		loan.IssueDateProvenance.ShouldBe(LibraryCivilDateProvenance.ExplicitSchoolDate);
		loan.DueDateProvenance.ShouldBe(LibraryCivilDateProvenance.ExplicitSchoolDate);
		loan.SchoolReturnDate.ShouldBeNull(); ((int)loan.ReturnDateProvenance).ShouldBe(0);
		loan.Status.ShouldBe(legacyReturned ? BookIssueStatus.Returned : BookIssueStatus.Issued);
		loan.ReturnDate.ShouldBe(legacyReturned ? new DateTime(2026, 11, 18) : (DateTime?)null);
		(await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(loan.BookCopyId)).Status
			.ShouldBe(legacyReturned ? BookCopyStatus.Available : BookCopyStatus.Issued);
	});

	private Task RestrictToRecordedClassAsync(LibraryCopyLifecycleTestContext fixture, bool includeOtherYear = false) =>
		WithUnitOfWorkAsync(async () =>
		{
			var actor = GetRequiredService<ICurrentUser>().Id!.Value;
			var tenant = GetRequiredService<ICurrentTenant>().Id;
			var scopes = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await scopes.DeleteAsync(row => row.UserId == actor, true);
			await scopes.InsertAsync(new(Guid.NewGuid(), tenant, userId: actor, academicYearId: fixture.Catalog.YearId,
				educationBoardId: fixture.BoardId, educationMediumId: fixture.MediumId,
				educationMediumStageId: fixture.StageId, standardId: fixture.BorrowerClassId), true);
			if (includeOtherYear) await scopes.InsertAsync(new(Guid.NewGuid(), tenant, userId: actor,
				academicYearId: fixture.Catalog.OriginYearId), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
		});

	private Task<Guid> AddYearAsync(string name, int startYear) => WithUnitOfWorkAsync<Guid>(async () =>
	{
		var id = Guid.NewGuid();
		await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(new(id,
			GetRequiredService<ICurrentTenant>().Id, name, new DateTime(startYear, 4, 1), new DateTime(startYear + 1, 3, 31)), true);
		return id;
	});
}
