using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShikshaMitra.Common.Middleware;
using ShikshaMitra.Localization;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionFormLifecycleProblemDetails_Tests
{
	[Theory]
	[InlineData(AdmissionErrorCodes.OfferingFormNotPublished, "Reason", "Inquiry Form 2026 is archived. Restore it or select a published replacement.", "Restore")]
	[InlineData(AdmissionErrorCodes.OfferingWorkflowNotPublished, "Reason", "Admission workflow 'Standard admission' is archived. Publish an eligible version and select it in this offering.", "Publish")]
	[InlineData(AdmissionFormErrorCodes.RestoreInvalidDefinition, "Errors", "Missing required section", "Clone")]
	[InlineData(AdmissionFormErrorCodes.ArchivedRequired, "Reason", "", "archived")]
	[InlineData(AdmissionFormErrorCodes.DraftAlreadyExists, "DraftId", "00000000-0000-0000-0000-000000000001", "existing draft")]
	[InlineData(AdmissionFormErrorCodes.VersionSourceRequired, "Reason", "", "already a draft")]
	[InlineData(AdmissionFormErrorCodes.VersionCodeImmutable, "Reason", "", "original form code")]
	public async Task Form_lifecycle_business_error_returns_readable_recovery_in_problem_json(string code, string field, string value, string expected)
	{
		var assembly = typeof(ShikshaMitraDomainSharedModule).Assembly;
		using var resource = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames()
			.Single(name => name.EndsWith("Localization.ShikshaMitra.en.json")))!;
		using var messages = await JsonDocument.ParseAsync(resource);
		var template = messages.RootElement.GetProperty("Texts").GetProperty(code).GetString()!;
		var localizer = Substitute.For<IStringLocalizer<ShikshaMitraResource>>();
		localizer[code].Returns(new LocalizedString(code, template));
		var resolver = new ApiProblemDetailsErrorDetailResolver(localizer);
		var http = new DefaultHttpContext();
		http.Request.Path = "/api/student/enrollment/admissions/forms/00000000-0000-0000-0000-000000000001/restore";
		http.Response.Body = new MemoryStream();
		await new ApiProblemDetailsMiddleware(_ => throw new BusinessException(code).WithData(field, value),
			NullLogger<ApiProblemDetailsMiddleware>.Instance, resolver).InvokeAsync(http);
		http.Response.StatusCode.ShouldBe(400);
		http.Response.Body.Position = 0;
		using var json = await JsonDocument.ParseAsync(http.Response.Body);
		var detail = json.RootElement.GetProperty("detail").GetString().ShouldNotBeNull();
		detail.ShouldContain(expected);
		detail.ShouldNotContain("Business rule violated");
		detail.ShouldNotContain("{Reason}");
		detail.ShouldNotContain("{Errors}");
	}
}
