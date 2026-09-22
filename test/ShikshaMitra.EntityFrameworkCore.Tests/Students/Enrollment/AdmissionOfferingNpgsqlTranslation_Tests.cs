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
	public void Offering_readiness_predicates_translate_to_PostgreSQL_without_connecting()
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
		var rows = from offering in context.Set<AdmissionOffering>()
				   join cycle in context.Set<AdmissionCycle>() on offering.AdmissionCycleId equals cycle.Id
				   join form in context.Set<AdmissionFormDefinition>() on offering.ApplicationFormDefinitionId equals form.Id
				   join workflowValue in context.Set<AdmissionWorkflowDefinition>() on offering.WorkflowDefinitionId equals workflowValue.Id into workflowGroup
				   from workflow in workflowGroup.DefaultIfEmpty()
				   select new { Offering = offering, Cycle = cycle, Form = form, Workflow = workflow };
		var ready = rows.Where(row => !incompatible.Contains(row.Offering.Id) &&
			row.Workflow != null &&
			(row.Workflow.Status == AdmissionWorkflowStatus.Published ||
				row.Workflow.Status == AdmissionWorkflowStatus.Superseded) &&
			(!row.Offering.OpenDate.HasValue || row.Offering.OpenDate.Value.Date >= row.Cycle.StartDate.Date) &&
			(!row.Offering.CloseDate.HasValue || row.Offering.CloseDate.Value.Date <= row.Cycle.EndDate.Date) &&
			(!row.Offering.OpenDate.HasValue || !row.Offering.CloseDate.HasValue || row.Offering.OpenDate.Value.Date <= row.Offering.CloseDate.Value.Date));
		var sql = ready.Where(row =>
			(row.Offering.OpenDate.HasValue ? row.Offering.OpenDate.Value.Date : row.Cycle.StartDate.Date) <= today &&
			(row.Offering.CloseDate.HasValue ? row.Offering.CloseDate.Value.Date : row.Cycle.EndDate.Date) >= today)
			.Select(row => row.Offering.Id).ToQueryString();
		sql.ShouldContain("SELECT");
		sql.ShouldContain("WHERE");
		sql.ShouldNotContain("AdmissionFormFieldDefinitions");
		sql.ShouldContain("OpenDate");
		sql.ShouldContain("CloseDate");
		sql.ShouldContain("date_trunc");

		var incompleteSql = rows.Where(row =>
			row.Workflow == null ||
			(row.Workflow.Status != AdmissionWorkflowStatus.Published &&
				row.Workflow.Status != AdmissionWorkflowStatus.Superseded))
			.Select(row => row.Offering.Id)
			.ToQueryString();
		incompleteSql.ShouldContain("SELECT");
		incompleteSql.ShouldContain("Status");
	}
}
