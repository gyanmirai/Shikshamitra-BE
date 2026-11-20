using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library;

public partial class LibraryReleaseGateApi_Tests
{
	private async Task<(Guid ParentBookId, Guid ReservationId, Guid ReservationBookId)> SeedMismatchedReservationPairAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var category = await services.GetRequiredService<IRepository<BookCategory, Guid>>().InsertAsync(
			new BookCategory($"Reservation {Guid.NewGuid():N}", "", Factory.SeededAcademicYearId, tenantId), true);
		var section = await services.GetRequiredService<IRepository<LibrarySection, Guid>>().InsertAsync(
			new(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId, "Reading", 1), true);
		var shelf = await services.GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(
			new(Guid.NewGuid(), section.Id, "A1"), true);
		async Task<Book> AddBookAsync(string title)
		{
			var book = await services.GetRequiredService<IRepository<Book, Guid>>().InsertAsync(
				new(title, "", "Known author", "", "", "", category.Id, Language.English,
					"", 0m, 0, 1, Factory.SeededAcademicYearId, tenantId), true);
			await services.GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(
				new(Guid.NewGuid(), book.Id, 1, $"RES-{Guid.NewGuid():N}", section.Id, shelf.Id, tenantId), true);
			return book;
		}
		var parent = await AddBookAsync("Requested reservation parent");
		var other = await AddBookAsync("Actual reservation parent");
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenantId, "Reservation", null, "Borrower",
			Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId,
			Factory.SeededStandardId, Factory.SeededDivisionId, new(2014, 1, 2, 0, 0, 0, TimeSpan.Zero), GenderEnum.Male, "8888888888");
		student.AdditionalDetail!.IsLibraryMember = true;
		await services.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
		var reservation = await services.GetRequiredService<IRepository<BookReservation, Guid>>().InsertAsync(
			new(Guid.NewGuid(), other.Id, student.Id, new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc),
				new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), tenantId), true);
		await unit.CompleteAsync();
		return (parent.Id, reservation.Id, other.Id);
	}

	private async Task AssertReservationPreservedAsync(Guid id, Guid expectedBookId)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var reservation = await services.GetRequiredService<IRepository<BookReservation, Guid>>().GetAsync(id);
		reservation.BookId.ShouldBe(expectedBookId);
		reservation.State.ShouldBe(LibraryReservationState.NeedsReview);
	}
}
