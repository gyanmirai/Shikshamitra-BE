using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private Task<MemberChargeAliasFixture> SeedMemberChargeAliasesAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var memberships = GetRequiredService<IRepository<LibraryMember, Guid>>();
		var aliases = new[]
		{
			new LibraryMember(Guid.NewGuid(), owner.TenantId, owner.PersonId, $"MC-A-{Guid.NewGuid():N}"),
			new LibraryMember(Guid.NewGuid(), owner.TenantId, owner.PersonId, $"MC-H-{Guid.NewGuid():N}", false)
		};
		await memberships.InsertManyAsync(aliases, true);
		var students = GetRequiredService<IRepository<Student, Guid>>();
		var person = await students.GetAsync(owner.PersonId, includeDetails: false);
		person.AcademicBoardId.ShouldNotBeNull(); person.AcademicMediumStageId.ShouldNotBeNull();
		var other = Student.CreateDirectRegistration(Guid.NewGuid(), owner.TenantId, "Other", null, "Member",
			owner.YearId, person.AcademicBoardId!.Value, person.AcademicMediumId, person.AcademicMediumStageId!.Value,
			person.StandardId, person.DivisionId, new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero),
			GenderEnum.Female, primaryContactNo: "9888888888", email: "other-member@example.test");
		other.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
		other.AdditionalDetail!.IsLibraryMember = true;
		await students.InsertAsync(other, true);
		var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
		var initial = await charges.GetAsync(owner.AssessmentId);
		initial.ChargeDate.ShouldNotBeNull(); initial.DueDate.ShouldNotBeNull();
		var extras = aliases.Select((alias, index) => new LibraryCharge(Guid.NewGuid(), alias.Id, 0,
			initial.ChargeCategoryId, null, index == 0 ? 25.25m : 40.50m, initial.ChargeDate!.Value, initial.DueDate!.Value, owner.TenantId)).ToArray();
		var unrelated = new LibraryCharge(Guid.NewGuid(), other.Id, 0, initial.ChargeCategoryId, null,
			17.75m, initial.ChargeDate!.Value, initial.DueDate!.Value, owner.TenantId);
		await charges.InsertManyAsync(extras.Append(unrelated), true);
		var allowed = extras.Select(row => row.Id).Append(initial.Id).ToArray();
		var query = await charges.GetQueryableAsync();
		var expected = await GetRequiredService<IAsyncQueryableExecuter>().ToListAsync(query
			.Where(row => Enumerable.Contains(allowed, row.Id)).OrderByDescending(row => row.ChargeDate)
			.ThenByDescending(row => row.CreationTime).ThenBy(row => row.Id).Select(row => row.Id));
		return new MemberChargeAliasFixture(aliases.Select(row => row.Id).ToArray(), other.Id, unrelated.Id, expected.ToArray());
	});

	private sealed record MemberChargeAliasFixture(Guid[] Aliases, Guid OtherPersonId, Guid OtherAssessmentId, Guid[] ExpectedOrder);
}
