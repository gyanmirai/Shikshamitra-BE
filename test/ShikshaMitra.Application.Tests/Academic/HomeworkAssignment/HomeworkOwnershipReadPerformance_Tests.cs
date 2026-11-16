using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class HomeworkOwnershipReadPerformance_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public async Task Teacher_Ownership_Should_Filter_In_Sql_Before_Hydrating_The_Requested_Page()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			{
				await SeedAllocationAsync();
				var employee = await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(OldTeacher);
				var userId = employee.UserId!.Value;
				await GetRequiredService<IUserScopeAssignmentRepository>().DeleteAsync(row => row.UserId == userId, autoSave: true);
				var type = await GetRequiredService<HomeworkTypeManager>().CreateAsync(AcademicMediumId, "Ownership content");
				var repository = GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>();
				for (var index = 0; index < 45; index++)
				{
					var homework = new HomeworkAssignmentEntity(Guid.NewGuid(), StandardId, SubjectId,
						index < 2 ? $"Selected {index}" : $"Other {index}", new string('d', 12000), type.Id,
						SubmissionType.OnlineText, TenantId, educationMediumId: AcademicMediumId);
					homework.AddDivisionSchedule(new HomeworkDivisionSchedule(Guid.NewGuid(), homework.Id, DivisionId,
						null, DateTime.UtcNow.AddDays(1), TenantId));
					await repository.InsertAsync(homework, autoSave: true);
				}
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(
					new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, userId.ToString())], "Test")));
				using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
				var page = await GetRequiredService<IHomeworkAppService>().GetListAsync(AcademicMediumId,
					new() { StandardId = StandardId, DivisionId = DivisionId, SubjectId = SubjectId, Search = "Selected ", Page = 1, PageSize = 10 });
				page.TotalCount.ShouldBe(2);
				page.Items.Count.ShouldBe(2);
				db.ChangeTracker.Entries<HomeworkAssignmentEntity>().Count().ShouldBeLessThanOrEqualTo(2);
				var table = db.Model.FindEntityType(typeof(HomeworkAssignmentEntity))!.GetTableName()!;
				// EF counts the final unsuccessful Read as well as the two returned rows.
				capture.Reads.Where(read => read.Sql.Contains(table, StringComparison.Ordinal) &&
					read.Sql[..read.Sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase)].Contains("\"Description\"", StringComparison.Ordinal))
					.ShouldAllBe(read => read.Rows <= page.Items.Count + 1);
				await GetRequiredService<IIdentityUserRepository>().DeleteAsync(userId, autoSave: true);
				var denied = await GetRequiredService<IHomeworkAppService>().GetListAsync(AcademicMediumId,
					new() { StandardId = StandardId, DivisionId = DivisionId, SubjectId = SubjectId });
				denied.TotalCount.ShouldBe(0, "A missing identity must not retain employee subject ownership.");
			}
		});
	}

	[Fact]
	public async Task Division_Allocation_Ownership_Should_Require_Every_Target_And_A_Nonempty_Target_Set()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			{
				await SeedAllocationAsync();
				var employee = await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(NewTeacher);
				var userId = employee.UserId!.Value;
				await GetRequiredService<IUserScopeAssignmentRepository>().DeleteAsync(row => row.UserId == userId, autoSave: true);
				var allocation = new TeacherSubjectAllocation(Guid.NewGuid(), StandardId, DivisionId, SubjectId);
				allocation.Teachers.Add(new AllocatedTeacher(allocation.Id, NewTeacher));
				await GetRequiredService<IRepository<TeacherSubjectAllocation, Guid>>().InsertAsync(allocation, autoSave: true);
				var type = await GetRequiredService<HomeworkTypeManager>().CreateAsync(AcademicMediumId, "All targets");
				var otherDivision = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
					new Division(Guid.NewGuid(), TenantId, "Other target", StandardId, 40, "OTHER"), autoSave: true);
				var repository = GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>();
				Guid allowed = default;
				foreach (var count in new[] { 0, 1, 2 })
				{
					var task = new HomeworkAssignmentEntity(Guid.NewGuid(), StandardId, SubjectId, $"Targets {count}", "Content",
						type.Id, SubmissionType.OnlineText, TenantId, educationMediumId: AcademicMediumId);
					if (count > 0) task.AddDivisionSchedule(new HomeworkDivisionSchedule(Guid.NewGuid(), task.Id, DivisionId,
						null, DateTime.UtcNow.AddDays(1), TenantId));
					if (count > 1) task.AddDivisionSchedule(new HomeworkDivisionSchedule(Guid.NewGuid(), task.Id, otherDivision.Id,
						null, DateTime.UtcNow.AddDays(1), TenantId));
					await repository.InsertAsync(task, autoSave: true);
					if (count == 1) allowed = task.Id;
				}
				using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(
					new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, userId.ToString())], "Test")));
				var scoped = await GetRequiredService<HomeworkAppService>().ApplyCurrentUserScopeAsync(await repository.GetQueryableAsync());
				(await scoped.Select(row => row.Id).ToListAsync()).ShouldBe([allowed]);
			}
		});
	}
}
