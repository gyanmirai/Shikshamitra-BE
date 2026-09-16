using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectUsageBatching_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Subject_And_Elective_Lists_Should_Keep_Query_Count_Bounded_And_Usage_Separate()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var tenantId = Guid.NewGuid();
			using (GetRequiredService<ICurrentTenant>().Change(tenantId))
			{
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(stageId);
				var subjects = GetRequiredService<IRepository<Subject, Guid>>();
				var groups = GetRequiredService<IRepository<ElectiveGroup, Guid>>();
				var subjectService = GetRequiredService<ISubjectAppService>();
				var groupService = GetRequiredService<IElectiveGroupAppService>();
				var first = new Subject(Guid.NewGuid(), tenantId, "First", "#00A76F", stage.EducationMediumId);
				await subjects.InsertAsync(first, autoSave: true);
				var firstGroup = new ElectiveGroup(Guid.NewGuid(), tenantId, "First group", "#00A76F", stage.EducationMediumId);
				firstGroup.AddSubject(first.Id);
				await groups.InsertAsync(firstGroup, autoSave: true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				var connection = db.Database.GetDbConnection();
				int subjectQueryCount;
				int groupQueryCount;
				using (var counter = new AcademicQueryCounter(connection))
				{
					var initial = await subjectService.GetListAsync(stage.EducationMediumId);
					initial.Single(subject => subject.Id == first.Id).UsedInElectiveGroupsCount.ShouldBe(1);
					subjectQueryCount = counter.Count;
				}
				using (var counter = new AcademicQueryCounter(connection))
				{
					await groupService.GetListAsync(stage.EducationMediumId);
					groupQueryCount = counter.Count;
				}

				for (var index = 0; index < 5; index++)
				{
					var subject = new Subject(Guid.NewGuid(), tenantId, $"Additional {index}", "#00A76F", stage.EducationMediumId);
					await subjects.InsertAsync(subject, autoSave: true);
					var group = new ElectiveGroup(Guid.NewGuid(), tenantId, $"Additional group {index}", "#00A76F", stage.EducationMediumId);
					group.AddSubject(subject.Id);
					await groups.InsertAsync(group, autoSave: true);
				}
				var unused = new Subject(Guid.NewGuid(), tenantId, "Unused", "#00A76F", stage.EducationMediumId);
				await subjects.InsertAsync(unused, autoSave: true);

				using (var counter = new AcademicQueryCounter(connection))
				{
					var result = await subjectService.GetListAsync(stage.EducationMediumId);
					result.Single(subject => subject.Id == first.Id).UsedInElectiveGroupsCount.ShouldBe(1);
					var unusedDto = result.Single(subject => subject.Id == unused.Id);
					unusedDto.UsedInElectiveGroupsCount.ShouldBe(0);
					unusedDto.IsUsed.ShouldBeFalse();
					unusedDto.CanDelete.ShouldBeTrue();
					subjectQueryCount.ShouldBeGreaterThan(0);
					counter.Count.ShouldBe(subjectQueryCount);
				}
				using (var counter = new AcademicQueryCounter(connection))
				{
					var result = await groupService.GetListAsync(stage.EducationMediumId);
					result.Count.ShouldBe(6);
					result.ShouldAllBe(group => !group.IsUsed && group.CanDelete);
					groupQueryCount.ShouldBeGreaterThan(0);
					counter.Count.ShouldBe(groupQueryCount);
				}
			}
		});
	}
}
