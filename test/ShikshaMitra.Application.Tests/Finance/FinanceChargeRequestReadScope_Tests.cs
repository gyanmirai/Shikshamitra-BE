using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Finance;

public class FinanceChargeRequestReadScope_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Request_lists_and_counts_exclude_hidden_lines_and_unrelated_review_makers()
	{
		var tenantId = Guid.NewGuid();
		using (GetRequiredService<ICurrentTenant>().Change(tenantId))
		{
			var fixture = await WithUnitOfWorkAsync(async () =>
			{
				var year = await Insert(new AcademicYear(Guid.NewGuid(), tenantId, "Charge read scope year",
					new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)));
				var board = await Insert(await GetRequiredService<EducationBoardManager>()
					.CreateAsync(year.Id, EducationBoardType.CBSE));
				var medium = await Insert(await GetRequiredService<EducationMediumManager>()
					.CreateAsync(board.Id, "English"));
				var stage = await Insert(await GetRequiredService<EducationMediumStageManager>()
					.CreateAsync(medium.Id, "Primary"));
				var standard = await Insert(await GetRequiredService<StandardManager>()
					.CreateAsync(stage.Id, "Class 8", 12, null));
				var divisions = new[] { "A", "B", "C" };
				var students = new Student[3];
				for (var i = 0; i < divisions.Length; i++)
				{
					var division = await Insert(await GetRequiredService<DivisionManager>()
						.CreateAsync(standard.Id, divisions[i], 30));
					students[i] = await Insert(new Student(Guid.NewGuid(), tenantId, $"Line {i}", null,
						"Student", year.Id, board.Id, medium.Id, stage.Id, standard.Id, division.Id));
				}
				var category = await Insert(new ChargeCategory(Guid.NewGuid(), tenantId, year.Id,
					"Scope check", true, null));
				var maker = await Insert(new IdentityUser(Guid.NewGuid(), $"maker-{Guid.NewGuid():N}",
					$"{Guid.NewGuid():N}@example.test", tenantId));
				var unrelated = await Insert(new IdentityUser(Guid.NewGuid(), $"other-{Guid.NewGuid():N}",
					$"{Guid.NewGuid():N}@example.test", tenantId));
				var allowed = await CreateRequestAsync(year.Id, board.Id, medium.Id, category.Id,
					maker.Id, students[0], students[1]);
				await CreateRequestAsync(year.Id, board.Id, medium.Id, category.Id,
					unrelated.Id, students[1]);
				await CreateRequestAsync(year.Id, board.Id, medium.Id, category.Id,
					maker.Id, students[0], students[2]);
				return new ScopeFixture(year.Id, allowed.Id, maker.Id, students[0].Id, students[1].Id);
			});

			await WithUnitOfWorkAsync(async () =>
			{
				var users = await GetRequiredService<IRepository<IdentityUser, Guid>>().GetQueryableAsync();
				var familyIds = users.Where(user => user.Id == Guid.Empty).Select(user => user.Id);
				var scope = new ChargeRequestReadScope([fixture.OwnStudentId],
					[fixture.ReviewStudentId], [fixture.MakerId], familyIds);
				var repository = GetRequiredService<IChargeRequestRepository>();
				var visibleStudentIds = new[] { fixture.OwnStudentId, fixture.ReviewStudentId };
				var (items, total) = await repository.GetChargeRequestsPagedAsync(
					fixture.YearId, status: 0, page: 1, pageSize: 20,
					scopedStudentIds: visibleStudentIds, readScope: scope);
				total.ShouldBe(1);
				items.Single().Request.Id.ShouldBe(fixture.AllowedRequestId);
				var counts = await repository.GetStatusCountsAsync(fixture.YearId,
					scopedStudentIds: visibleStudentIds, readScope: scope);
				counts.Pending.ShouldBe(1);
			});
		}
	}

	private async Task<ChargeRequest> CreateRequestAsync(Guid yearId, Guid boardId, Guid mediumId,
		Guid categoryId, Guid makerId, params Student[] students)
	{
		var due = DateTimeOffset.UtcNow.AddDays(30);
		var request = await Insert(new ChargeRequest(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id,
			yearId, "Scoped charge", categoryId, "Scope verification", 100m, due,
			ChargePriority.Medium, boardId, mediumId, makerId, "{}"));
		foreach (var student in students)
			await Insert(new Charge(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id,
				yearId, request.Id, request.ChargeName, categoryId, student.Id, boardId,
				mediumId, student.StandardId!.Value, student.DivisionId!.Value,
				"Scope verification", 100m, due, ChargePriority.Medium, ChargeStatus.Pending, makerId));
		return request;
	}

	private async Task<T> Insert<T>(T entity) where T : class, IEntity<Guid> =>
		await GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, true);

	private sealed record ScopeFixture(Guid YearId, Guid AllowedRequestId, Guid MakerId,
		Guid OwnStudentId, Guid ReviewStudentId);
}
