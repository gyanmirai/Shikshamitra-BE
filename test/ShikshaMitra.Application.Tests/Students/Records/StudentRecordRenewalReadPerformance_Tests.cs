using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public Task Records_renewal_history_shares_page_policy_reads_and_preserves_target_year_privacy(bool masked, bool clearCurrentYear) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordRenewalFixtureAsync(requirePortals: false);
		var targetPolicy = StudentRecordsPolicy.Starter();
		if (masked) targetPolicy.Access.Masks.Add(new() { FieldGroup = "Identity", AllowedRoleNames = [] });
		await WithUnitOfWorkAsync(async () =>
		{
			var renewals = GetRequiredService<IRepository<StudentRecordRenewalCase, Guid>>();
			var original = await renewals.GetAsync(fixture.Renewal.Id);
			original.Cancel(); await renewals.UpdateAsync(original, true);
			for (var index = 0; index < 3; index++)
			{
				var history = new StudentRecordRenewalCase(Guid.NewGuid(), TestTenantId, original.StudentId, original.AcademicYearId,
					original.StandardId, original.DivisionId, original.RollNumber, null, original.PolicyJson,
					original.OnboardingRulesJson, original.SourceSnapshotJson, "renewal-read-history-" + Guid.NewGuid().ToString("N"));
				history.Cancel(); await renewals.InsertAsync(history, true);
			}
			var configuration = new StudentRecordsConfiguration(Guid.NewGuid(), TestTenantId, original.AcademicYearId, targetPolicy.ToJson());
			await GetRequiredService<IRepository<StudentRecordsConfiguration, Guid>>().InsertAsync(configuration, true);
			var version = new StudentRecordsPolicyVersion(Guid.NewGuid(), TestTenantId, configuration.Id, original.AcademicYearId, 1, targetPolicy.ToJson());
			await GetRequiredService<IRepository<StudentRecordsPolicyVersion, Guid>>().InsertAsync(version, true);
			configuration.Publish(version.Id);
			await GetRequiredService<IRepository<StudentRecordsConfiguration, Guid>>().UpdateAsync(configuration, true);
			if (clearCurrentYear)
			{
				var student = await _studentRepository.GetAsync(fixture.StudentId);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.Entry(student).Property(x => x.AcademicYearId).CurrentValue = null;
				await db.SaveChangesAsync();
			}
		});
		var one = await MeasureRenewalPolicyReadsAsync(() => fixture.Service.GetRenewalsAsync(new() { StudentId = fixture.StudentId, MaxResultCount = 1 }));
		var page = await MeasureRenewalPolicyReadsAsync(() => fixture.Service.GetRenewalsAsync(new() { StudentId = fixture.StudentId, MaxResultCount = 4 }));
		page.Result.TotalCount.ShouldBe(4); page.Result.Items.Count.ShouldBe(4);
		foreach (var item in page.Result.Items)
		{
			item.Status.ShouldBe("Cancelled"); item.Issues.ShouldBeEmpty();
			item.StudentName.ShouldBe(masked ? null : "Transition Student");
			JsonSerializer.Serialize(item).ShouldBe(JsonSerializer.Serialize(await fixture.Service.GetRenewalAsync(item.Id)));
		}
		Console.WriteLine($"Renewal history policy reads: one={one.Reads}, four={page.Reads}, target-year masked={masked}, current-year null={clearCurrentYear}.");
		page.Reads.ShouldBeLessThanOrEqualTo(one.Reads, "Current and target-year display policies must be read once per distinct year in this page, while every row retains its target scope and live readiness checks.");
	});

	private Task<(T Result, int Reads)> MeasureRenewalPolicyReadsAsync<T>(Func<Task<T>> read) => WithUnitOfWorkAsync(async () =>
	{
		var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		db.ChangeTracker.Clear();
		using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
		var result = await read();
		return (result, counter.ReadCommands.Count(sql => sql.Contains("\"AppStudentRecordsConfigurations\"", StringComparison.Ordinal) ||
			sql.Contains("\"AppStudentRecordsPolicyVersions\"", StringComparison.Ordinal)));
	});
}
