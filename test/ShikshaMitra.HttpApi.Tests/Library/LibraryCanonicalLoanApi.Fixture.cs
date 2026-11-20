using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Students;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library;

public partial class LibraryCanonicalLoanApi_Tests
{
	private async Task<LibraryCheckoutRequest> CheckoutRequestAsync()
	{
		var (copyId, memberId) = await SeedLoanContextAsync();
		var input = new LibraryCheckoutRequest
		{
			CopyId = copyId, MemberId = memberId, IssueDate = new(2026, 11, 17),
			RequestedDueDate = new(2026, 11, 30), IdempotencyKey = Guid.NewGuid().ToString("N")
		};
		var plan = await PostAndReadAsync<LibraryIssuePlanDto>("/api/library/circulation/issues/preview", new LibraryIssuePlanRequest
		{
			CopyId = copyId, MemberId = memberId, IssueDate = input.IssueDate, RequestedDueDate = input.RequestedDueDate
		});
		input.Revision = plan.Revision;
		return input;
	}

	private async Task<(Guid CopyId, Guid MemberId)> SeedLoanContextAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var label = $"Loan API {Guid.NewGuid():N}"[..24];
		var category = await services.GetRequiredService<IRepository<BookCategory, Guid>>().InsertAsync(
			new BookCategory(label, "HTTP loan workflow", Factory.SeededAcademicYearId, tenantId), true);
		var section = await services.GetRequiredService<IRepository<LibrarySection, Guid>>().InsertAsync(
			new LibrarySection(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId, label, 1), true);
		var shelf = await services.GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(new LibraryShelf(Guid.NewGuid(), section.Id, "A"), true);
		var book = await services.GetRequiredService<IRepository<Book, Guid>>().InsertAsync(
			new Book(label, "", "Library author", "", "", "", category.Id, Language.English, "", 0m, 0, 1,
				Factory.SeededAcademicYearId, tenantId), true);
		var copy = await services.GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(
			new BookCopy(Guid.NewGuid(), book.Id, 1, $"LOAN-{Guid.NewGuid():N}", section.Id, shelf.Id, tenantId), true);
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenantId, "Library", null, label,
			Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId,
			Factory.SeededStandardId, Factory.SeededDivisionId, new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero), GenderEnum.Male, "9999999999");
		student.AdditionalDetail!.IsLibraryMember = true;
		await services.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
		await services.GetRequiredService<IRepository<LibraryMember, Guid>>().InsertAsync(
			new LibraryMember(Guid.NewGuid(), tenantId, student.Id, $"BOR-{Guid.NewGuid():N}", true), true);
		await unit.CompleteAsync();
		return (copy.Id, student.Id);
	}
}
