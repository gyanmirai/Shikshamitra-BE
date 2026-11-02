using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Numeric_gr_allocation_reuses_collision_metadata_within_its_serializable_operation() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOnboardingWorklistFixtureAsync();
			var series = await WithUnitOfWorkAsync(async () =>
			{
				var created = new StudentIdentifierSeries(Guid.NewGuid(), TestTenantId, fixture.Cases[0].AcademicYearId,
					fixture.Offering.StandardId, StudentIdentifierType.GrNumber, "PERF_NUMERIC", "Numeric read fixture", "", "", 1, 1, 20,
					DateTime.UtcNow.AddDays(-1), null);
				await GetRequiredService<IRepository<StudentIdentifierSeries, Guid>>().InsertAsync(created, true);
				for (var index = 1; index <= 2; index++)
					await GetRequiredService<IRepository<StudentIdentifierAllocation, Guid>>().InsertAsync(new(
						Guid.NewGuid(), TestTenantId, fixture.Cases[index].Id, fixture.Cases[index].StudentId,
						created.Id, StudentIdentifierType.GrNumber, index, $"000{index}", DateTime.UtcNow), true);
				return created;
			});
			var service = GetRequiredService<IStudentOnboardingAppService>();
			(await service.GetAvailableIdentifierSeriesAsync(fixture.Cases[0].Id)).ShouldContain(item => item.Id == series.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				var assigned = await service.AssignGrNumberAsync(fixture.Cases[0].Id, new()
				{
					NumberSeriesId = series.Id, ConcurrencyStamp = fixture.Cases[0].ConcurrencyStamp
				});
				assigned.GrNumber.ShouldBe("3", "Leading-zero reservations consume the same numeric GR values.");
				var scans = capture.ReadCommands.Count(sql => Regex.IsMatch(sql,
					"SELECT\\s+\"[^\"]+\"\\.\"Value\"\\s+FROM\\s+\"AppStudentIdentifierAllocations\"", RegexOptions.CultureInvariant));
				Console.WriteLine($"SQLite numeric GR allocation: {capture.ReadCount} reads; reserved-value scans={scans}.");
				scans.ShouldBe(1, "Repeated candidates and the final collision check share the same transaction-local numeric metadata.");
			});
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentIdentifierSeries, Guid>>().GetAsync(series.Id))).NextSequence.ShouldBe(4);
			var result = await service.GetAsync(fixture.Cases[0].Id);
			(await service.AssignGrNumberAsync(result.Id, new() { NumberSeriesId = series.Id, ConcurrencyStamp = result.ConcurrencyStamp })).GrNumber.ShouldBe("3");
		});
}
