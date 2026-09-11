using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionOfferingNpgsqlTranslation_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Eligibility_offering_readiness_predicates_translate_to_PostgreSQL_without_connecting()
	{
		// ToQueryString never opens this deliberately unusable connection or runs database commands.
		var options = new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=translation_only;Timeout=1")
			.Options;
		using var context = new ShikshaMitraDbContext(options)
		{
			LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>()
		};
		var incompatible = new List<Guid> { Guid.NewGuid() };
		var today = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
		var fields = context.Set<AdmissionFormFieldDefinition>();
		var rows = from offering in context.Set<AdmissionOffering>()
			join cycle in context.Set<AdmissionCycle>() on offering.AdmissionCycleId equals cycle.Id
			join form in context.Set<AdmissionFormDefinition>() on offering.ApplicationFormDefinitionId equals form.Id
			select new { Offering = offering, Cycle = cycle, Form = form };
		var ready = rows.Where(row => !incompatible.Contains(row.Offering.Id) &&
			(!row.Offering.OpenDate.HasValue || row.Offering.OpenDate.Value.Date >= row.Cycle.StartDate.Date) &&
			(!row.Offering.CloseDate.HasValue || row.Offering.CloseDate.Value.Date <= row.Cycle.EndDate.Date) &&
			(!row.Offering.OpenDate.HasValue || !row.Offering.CloseDate.HasValue || row.Offering.OpenDate.Value.Date <= row.Offering.CloseDate.Value.Date) &&
			fields.Any(field => field.FormDefinitionId == row.Form.Id && field.Required &&
				field.BindingType == AdmissionFormBindingType.System && field.BindingKey == "Applicant.DateOfBirth"));
		var sql = ready.Where(row =>
			(row.Offering.OpenDate.HasValue ? row.Offering.OpenDate.Value.Date : row.Cycle.StartDate.Date) <= today &&
			(row.Offering.CloseDate.HasValue ? row.Offering.CloseDate.Value.Date : row.Cycle.EndDate.Date) >= today)
			.Select(row => row.Offering.Id).ToQueryString();
		sql.ShouldContain("SELECT");
		sql.ShouldContain("WHERE");
		sql.ShouldContain("EXISTS");
		sql.ShouldContain("OpenDate");
		sql.ShouldContain("CloseDate");
		sql.ShouldContain("date_trunc");
	}
}
