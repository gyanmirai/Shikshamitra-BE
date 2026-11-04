using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NSubstitute;
using ShikshaMitra.Common.Middleware;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Auditing;
using Xunit;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionParentRegistrationApiContract_Tests
{
	[Fact]
	public void Parent_routes_are_anonymous_private_uncached_and_rate_limited()
	{
		var type = typeof(AdmissionParentRegistrationPublicController);
		type.GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe("api/public/admission-registration");
		type.GetCustomAttribute<AllowAnonymousAttribute>().ShouldNotBeNull();
		type.GetCustomAttribute<DisableAuditingAttribute>().ShouldNotBeNull();
		type.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore.ShouldBeTrue();
		type.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.ShouldBe("public-admission-registration");
		type.GetMethod("SaveAsync")!.GetCustomAttribute<HttpPutAttribute>()!.Template.ShouldBe("draft");
		type.GetMethod("HandInAsync")!.GetCustomAttribute<HttpPostAttribute>()!.Template.ShouldBe("hand-in");
		type.GetMethod("UploadAsync")!.GetCustomAttribute<HttpPostAttribute>()!.Template.ShouldBe("documents/{stageInstanceId:guid}/{requirementCode}");
		type.GetMethod("DownloadAsync")!.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe("files/{evidenceId:guid}");
		type.GetMethod("UploadAsync")!.GetParameters().Single(parameter => parameter.Name == "file").GetCustomAttribute<FromFormAttribute>().ShouldBeNull();
		((Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata)type.GetMethod("UploadAsync")!.GetCustomAttribute<RequestSizeLimitAttribute>()!).MaxRequestBodySize.ShouldBe(22 * 1024 * 1024);
	}
	[Fact]
	public async Task Missing_or_ambiguous_capability_header_never_delegates_and_valid_header_sets_private_response_headers()
	{
		var service = Substitute.For<IAdmissionParentRegistrationPublicAppService>();
		var context = new DefaultHttpContext();
		var controller = new AdmissionParentRegistrationPublicController(service) { ControllerContext = new() { HttpContext = context } };
		Should.Throw<BusinessException>(() => controller.GetAsync()).Code.ShouldBe(AdmissionParentRegistrationErrors.Unavailable);
		context.Request.Headers[AdmissionParentRegistrationPublicController.TokenHeader] = new Microsoft.Extensions.Primitives.StringValues(["one", "two"]);
		Should.Throw<BusinessException>(() => controller.GetAsync());
		await service.DidNotReceiveWithAnyArgs().GetAsync(default!);
		context.Request.Headers[AdmissionParentRegistrationPublicController.TokenHeader] = new string('A', 64);
		service.GetAsync(Arg.Any<string>()).Returns(new AdmissionParentPublicContextDto());
		await controller.GetAsync();
		await service.Received(1).GetAsync(new string('A', 64));
		context.Response.Headers.CacheControl.ToString().ShouldBe("no-store, private");
		context.Response.Headers["Referrer-Policy"].ToString().ShouldBe("no-referrer");
	}
	[Fact]
	public async Task Multipart_forwards_the_private_capability_and_snapshot_with_binary_content()
	{
		var service = Substitute.For<IAdmissionParentRegistrationPublicAppService>(); var context = new DefaultHttpContext();
		context.Request.Headers[AdmissionParentRegistrationPublicController.TokenHeader] = new string('A', 64);
		var controller = new AdmissionParentRegistrationPublicController(service) { ControllerContext = new() { HttpContext = context } };
		var bytes = new byte[] { 1, 2, 3 }; var stage = Guid.NewGuid();
		var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "proof.pdf") { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
		await controller.UploadAsync(stage, "PROOF", file, "snapshot", "retry-key", "Parent upload");
		await service.Received(1).UploadAsync(new string('A', 64), stage, "PROOF", Arg.Is<AdmissionParentUploadInput>(input =>
			input.SnapshotToken == "snapshot" && input.IdempotencyKey == "retry-key" && input.File.FileName == "proof.pdf" && input.File.ContentLength == 3));
	}
	[Fact]
	public void Staff_routes_preserve_existing_view_and_edit_actions_and_safe_conflict_status()
	{
		var type = typeof(AdmissionParentRegistrationController);
		type.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(Permissions.View);
		foreach (var method in new[] { "SendAsync", "EmailAsync", "CorrectionsAsync", "RevokeAsync", "TakeoverAsync", "ApplyAsync", "RetryAsync" })
			type.GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(Permissions.Edit);
		ApiProblemDetailsMapper.ResolveBusinessStatusCode(AdmissionParentRegistrationErrors.Conflict).ShouldBe(409);
		ApiProblemDetailsMapper.ResolveBusinessStatusCode(AdmissionParentRegistrationErrors.Invalid).ShouldBe(400);
		ApiProblemDetailsMapper.ResolveBusinessStatusCode(AdmissionParentRegistrationErrors.Unavailable).ShouldBe(404);
	}
	[Fact]
	public async Task Saved_email_change_is_a_scoped_staff_command_and_never_calls_send()
	{
		var method = typeof(AdmissionParentRegistrationController).GetMethod("EmailAsync")!;
		method.GetCustomAttribute<HttpPostAttribute>()!.Template.ShouldBe("email");
		method.GetCustomAttribute<AllowAnonymousAttribute>().ShouldBeNull();
		var service = Substitute.For<IAdmissionParentRegistrationAppService>();
		var id = Guid.NewGuid();
		var input = new AdmissionParentEmailInput { RecipientRelationship = "mother", ExpectedEmail = "old@example.test", Email = "new@example.test", ConcurrencyStamp = "reviewed", IdempotencyKey = "save-email" };
		service.ChangeEmailAsync(id, input).Returns(new AdmissionParentStaffContextDto { ApplicationId = id });
		await new AdmissionParentRegistrationController(service).EmailAsync(id, input);
		await service.Received(1).ChangeEmailAsync(id, input);
		await service.DidNotReceiveWithAnyArgs().SendAsync(default, default!);
	}
}
