using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HttpApi.Controllers.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Xunit;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionDocuments;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class StudentAdmissionEvidenceApiContract_Tests
{
	[Fact]
	public void Onboarding_document_routes_require_onboarding_permissions_and_have_an_opt_in_runtime_flag()
	{
		foreach (var boundary in new[] { typeof(StudentOnboardingDocumentAppService), typeof(StudentOnboardingDocumentsController) })
		{
			boundary.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View);
			boundary.GetMethod(nameof(StudentOnboardingDocumentAppService.UploadAsync))!.GetCustomAttribute<AuthorizeAttribute>()!
				.Policy.ShouldBe(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
		}
		new StudentOnboardingCaseDto().DocumentUploadsEnabled.ShouldBeTrue();
	}

	[Fact]
	public void Student_evidence_requires_student_record_and_document_view_permissions_at_both_boundaries()
	{
		foreach (var boundary in new[] { typeof(StudentAdmissionEvidenceAppService), typeof(StudentAdmissionEvidenceController) })
		{
			var policies = boundary.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).ToArray();
			policies.ShouldContain(Permissions.View);
			policies.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		}
	}

	[Fact]
	public void Student_evidence_download_also_requires_the_separate_download_permission()
	{
		foreach (var boundary in new[] { typeof(StudentAdmissionEvidenceAppService), typeof(StudentAdmissionEvidenceController) })
			boundary.GetMethod(nameof(StudentAdmissionEvidenceAppService.DownloadAsync))!
				.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(Permissions.Download);
	}

	[Fact]
	public void Student_evidence_contract_does_not_expose_storage_keys_or_create_mutation_routes()
	{
		typeof(StudentAdmissionEvidenceController).GetCustomAttribute<RouteAttribute>()!.Template
			.ShouldBe("api/student/records/students/{studentId:guid}/admission-evidence");
		typeof(StudentAdmissionEvidenceDto).GetProperty("BlobName").ShouldBeNull();
		typeof(StudentAdmissionEvidenceDto).GetProperty("Sha256Hash").ShouldBeNull();
		typeof(IStudentAdmissionEvidenceAppService).GetMethods().Select(method => method.Name)
			.ShouldBe(["GetListAsync", "DownloadAsync"], ignoreOrder: true);
	}
}
