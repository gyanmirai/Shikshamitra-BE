using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReservationPolicyContext_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task A_Valid_Class_Should_Supply_Its_Real_Stage_For_A_Stage_Specific_Reservation_Policy()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		await GetRequiredService<ILibraryCalendarAppService>().UpdateSettingsAsync(new() { TimeZoneId = "America/New_York" });
		var policies = GetRequiredService<ILibraryBorrowingPolicyAppService>();
		var draft = await policies.CreateAsync(context.Catalog.YearId, new()
		{
			Name = "Middle school pickup policy", Role = 0, EffectiveFrom = new(2026, 4, 1), EducationMediumStageId = context.StageId,
			Terms = new() { MaxBooksAtTime = 4, ReservationLimit = 2, LoanDays = 7, DayCountingMode = 0,
				MaxRenewals = 0, RenewalDays = 0, PickupWindowDays = 2 }
		});
		var published = await policies.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp! });
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<Student, Guid>>();
			var student = await repository.GetAsync(context.StudentId);
			typeof(Student).GetProperty(nameof(Student.AcademicMediumStageId))!.SetValue(student, null);
			await repository.UpdateAsync(student, true);
		});

		var (facts, decision) = await WithUnitOfWorkAsync(async () =>
		{
			var facts = await GetRequiredService<LibraryReservationEligibilityReader>().RequireAsync(context.StudentId, context.Catalog.YearId);
			var decision = await GetRequiredService<LibraryBorrowingPolicyResolver>().ResolveAsync(new(context.Catalog.YearId, 0,
				new(2026, 11, 17), context.Catalog.CategoryId, context.Catalog.SectionId, facts.StageId, facts.StandardId));
			return (facts, decision);
		});

		(decision.Policy?.Id).ShouldBe(published.Id);
		facts.StageId.ShouldBe(context.StageId);
	}
}
