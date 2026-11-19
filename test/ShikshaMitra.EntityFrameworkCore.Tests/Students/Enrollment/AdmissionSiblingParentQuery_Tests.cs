using System;
using System.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionSiblingParentQuery_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Parent_contact_query_is_provider_backed_parameterized_and_composable_without_opening_a_connection(bool postgres)
	{
		var builder = new DbContextOptionsBuilder<ShikshaMitraDbContext>();
		if (postgres) builder.UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=translation_only;Timeout=1");
		else builder.UseSqlite("Data Source=:memory:");
		using var context = new ShikshaMitraDbContext(builder.Options) { LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>() };
		var sourceApplicantId = Guid.NewGuid();
		var tenantId = Guid.NewGuid();
		var query = EfCoreAdmissionApplicantRepository.BuildParentContactQuery(context, tenantId,
			["parent@example.com", "x' OR 1=1 --@example.com"], ["9876543210"]);
		var sql = query.Where(applicant => applicant.Id == sourceApplicantId).Select(applicant => applicant.FamilyId).ToQueryString();
		sql.ShouldContain(postgres ? "jsonb_array_elements_text" : "json_each");
		sql.ShouldContain("AdmissionStudentField.Father.Email");
		sql.ShouldContain("AdmissionStudentField.Mother.Email");
		sql.ShouldContain("TenantId"); sql.ShouldContain("IsDeleted"); sql.ShouldContain("ExistingStudentId");
		sql.ShouldContain("@p0"); sql.ShouldContain("@p1");
		sql.ShouldNotContain("LIMIT 2000");
		context.Database.GetDbConnection().State.ShouldBe(ConnectionState.Closed);
	}
}
