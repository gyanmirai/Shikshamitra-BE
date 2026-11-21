using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Setting.BorrowingRules;
using ShikshaMitra.Students;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	private static async Task<(Guid CopyId, Guid MemberId)> SeedCompetingContextAsync(LibraryPostgresApiTestFactory factory,
		Guid firstCopyId, Guid firstMemberId, bool quotaAcrossCopies)
	{
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		if (quotaAcrossCopies)
		{
			await services.GetRequiredService<IRepository<BorrowingRule, Guid>>().InsertAsync(
				new BorrowingRule(Guid.NewGuid(), tenantId, factory.YearId, 0, 1, 2), true);
			var copies = services.GetRequiredService<IRepository<BookCopy, Guid>>();
			var firstCopy = await copies.GetAsync(firstCopyId);
			var secondCopy = await copies.InsertAsync(new BookCopy(Guid.NewGuid(), firstCopy.BookId, 2,
				$"PG-COMPETE-{Guid.NewGuid():N}", firstCopy.SectionId, firstCopy.ShelfId, tenantId), true);
			var books = services.GetRequiredService<IRepository<Book, Guid>>();
			var book = await books.GetAsync(firstCopy.BookId);
			book.NumberOfCopies = 2;
			await books.UpdateAsync(book, true);
			await unit.CompleteAsync();
			return (secondCopy.Id, firstMemberId);
		}
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenantId, "Competing", null, "Borrower",
			factory.YearId, factory.BoardId, factory.MediumId, factory.StageId, factory.StandardId, null,
			new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero), GenderEnum.Male, "9999999998");
		student.AdditionalDetail!.IsLibraryMember = true;
		await services.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
		await services.GetRequiredService<IRepository<LibraryMember, Guid>>().InsertAsync(
			new LibraryMember(Guid.NewGuid(), tenantId, student.Id, $"PG-COMPETE-{Guid.NewGuid():N}", true), true);
		await unit.CompleteAsync();
		return (firstCopyId, student.Id);
	}

	private static async Task<bool> ObserveTwoAdvisoryWaitersAsync(LibraryMigrationPostgresFixture database)
	{
		var timer = Stopwatch.StartNew();
		while (timer.Elapsed < TimeSpan.FromSeconds(10))
		{
			var waiting = await database.ScalarAsync("""
				SELECT count(*) FROM pg_stat_activity WHERE datname=current_database()
				AND wait_event_type='Lock' AND wait_event='advisory';
				""");
			if (int.Parse(waiting!) >= 2) return true;
			await Task.Delay(50);
		}
		return false;
	}
}
