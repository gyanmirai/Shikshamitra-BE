using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task Roll_history_Postgres_bootstraps_from_jsonb_before_images_without_reusing_other_divisions_numbers()
	{
		var fixture = await SeedOnboardingWorklistReadModelsAsync();
		await InTenantAsync(async services =>
		{
			var item = await services.GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().GetAsync(fixture.Cases[0]);
			var changes = services.GetRequiredService<IRepository<StudentRecordChange, Guid>>();
			async Task Add(Guid division, int number, string section = "placement", bool applied = true, string? beforeJson = null)
			{
				var change = new StudentRecordChange(Guid.NewGuid(), TenantId, item.StudentId, factory.SeededAcademicYearId,
					fixture.Staff, section, "Correction", Guid.NewGuid().ToString("N"), "Synthetic roll-history regression",
					beforeJson ?? JsonSerializer.Serialize(new { divisionId = division, rollNumber = number, note = fixture.DivisionA.ToString("D") }),
					"{}", "{}", null, "[]", 0, DateTimeOffset.UtcNow.AddMinutes(-1));
				if (applied) change.Apply(DateTimeOffset.UtcNow);
				await changes.InsertAsync(change, true);
			}
			await Add(fixture.DivisionA, 73);
			await Add(fixture.DivisionB, 999); // Mentions A in another field, but belongs to B.
			await Add(fixture.DivisionA, 1111, section: "communication");
			await Add(fixture.DivisionA, 2222, applied: false);
			await Add(fixture.DivisionA, 0, beforeJson: "null");
			await Add(fixture.DivisionA, 0, beforeJson: "[]");
			(await services.GetRequiredService<StudentOnboardingRollRuleReader>()
				.LastIssuedAsync(factory.SeededAcademicYearId, factory.SeededStandardId, fixture.DivisionA)).ShouldBe(73);
			return true;
		});
		await InTenantAsync(async services =>
		{
			await services.GetRequiredService<StudentOnboardingRollRuleReader>()
				.RecordIssuedAsync(factory.SeededAcademicYearId, factory.SeededStandardId, fixture.DivisionA, 74);
			return true;
		});
		await InTenantAsync(async services =>
		{
			var reader = services.GetRequiredService<StudentOnboardingRollRuleReader>();
			(await reader.LastIssuedAsync(factory.SeededAcademicYearId, factory.SeededStandardId, fixture.DivisionA)).ShouldBe(74);
			await reader.RecordIssuedAsync(factory.SeededAcademicYearId, factory.SeededStandardId, fixture.DivisionA, 30);
			return true;
		});
		(await InTenantAsync(services => services.GetRequiredService<StudentOnboardingRollRuleReader>()
			.LastIssuedAsync(factory.SeededAcademicYearId, factory.SeededStandardId, fixture.DivisionA))).ShouldBe(74);
	}
}
