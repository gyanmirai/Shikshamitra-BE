using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionInquiryManagementModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Management_and_followups_have_tenant_concurrency_and_restrictive_links()
	{
		var model = GetRequiredService<ShikshaMitraDbContext>().Model;
		var management = model.FindEntityType(typeof(AdmissionInquiryManagement)).ShouldNotBeNull();
		var followUp = model.FindEntityType(typeof(AdmissionInquiryFollowUp)).ShouldNotBeNull();
		foreach (var entity in new[] { management, followUp })
		{
			entity.FindProperty("TenantId").ShouldNotBeNull();
			entity.FindProperty("ConcurrencyStamp")!.IsConcurrencyToken.ShouldBeTrue();
			entity.GetForeignKeys().ShouldAllBe(key => key.DeleteBehavior == DeleteBehavior.Restrict);
		}
		management.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Single().Name == "InquiryId");
		management.FindProperty("PolicySnapshotJson")!.GetColumnType().ShouldBe("text");
		followUp.FindProperty("Purpose")!.GetMaxLength().ShouldBe(512);
		followUp.FindProperty("CompletionNote")!.GetMaxLength().ShouldBe(1024);
	}
}
