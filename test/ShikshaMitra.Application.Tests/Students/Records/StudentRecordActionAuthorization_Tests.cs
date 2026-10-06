using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Students.Records;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Unproxied direct calls must enforce each action before touching repositories.</summary>
public class StudentRecordActionAuthorization_Tests
{
	[Theory]
	[InlineData("Students.Records.Changes.View")]
	[InlineData("Students.Records.Changes.Create")]
	public async Task Admission_profile_review_requires_exact_records_permissions_before_repository_access(string permission)
	{
		var service = new StudentRecordChangeAppService(null!, null!, null!, null!, null!, null!, null!, null!, null!)
			{ LazyServiceProvider = Denying(permission) };
		(await Should.ThrowAsync<AbpAuthorizationException>(() => service.CreateAdmissionReviewAsync(new()))).Data["PolicyName"].ShouldBe(permission);
	}

	[Fact]
	public async Task Admission_photo_review_requires_records_create_permission_before_upload()
	{
		const string permission = "Students.Records.Changes.Create";
		var service = new StudentRecordPhotoAppService(null!, null!, null!, null!, null!) { LazyServiceProvider = Denying(permission) };
		(await Should.ThrowAsync<AbpAuthorizationException>(() => service.UploadForAdmissionReviewAsync(Guid.NewGuid(), new() { FileName = "photo.png", ContentType = "image/png", Content = [] }))).Data["PolicyName"].ShouldBe(permission);
	}

	private static AbpLazyServiceProvider Denying(string deniedPermission)
	{
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
			.Returns(call => Task.FromResult(call.ArgAt<string>(2) == deniedPermission ? AuthorizationResult.Failed() : AuthorizationResult.Success()));
		return new AbpLazyServiceProvider(new ServiceCollection().AddSingleton<IAuthorizationService>(authorization).BuildServiceProvider());
	}
	[Theory]
	[InlineData("issue", "Students.Records.StudentICard.Add")]
	[InlineData("template", "Students.Records.StudentICard.Add")]
	[InlineData("reprint", "Students.Records.StudentICard.Add")]
	[InlineData("void", "Students.Records.StudentICard.Delete")]
	[InlineData("download", "Students.Records.StudentICard.View")]
	public async Task Record_action_ID_direct_service_rejects_denied_exact_permission(string action, string deniedPermission)
	{
		var service = new StudentIdCardAppService(null!, null!, null!, null!, null!, null!, null!) { LazyServiceProvider = Denying(deniedPermission) };
		Func<Task> call = action switch
		{
			"issue" => () => service.IssueAsync(new()), "template" => () => service.SaveTemplateAsync(new()),
			"reprint" => () => service.ReprintAsync(Guid.NewGuid(), new() { Reason = "Replacement print" }),
			"void" => () => service.VoidAsync(Guid.NewGuid(), new() { Reason = "Invalid issued card" }),
			_ => () => service.GetPdfAsync(Guid.NewGuid())
		};
		(await Should.ThrowAsync<AbpAuthorizationException>(call)).Data["PolicyName"].ShouldBe(deniedPermission);
	}
	[Theory]
	[InlineData("issue", "Students.Records.Certificates.Add")]
	[InlineData("duplicate", "Students.Records.Certificates.Add")]
	[InlineData("reprint", "Students.Records.Certificates.Add")]
	[InlineData("void", "Students.Records.Certificates.Delete")]
	[InlineData("download", "Students.Records.Certificates.View")]
	public async Task Record_action_certificate_direct_service_rejects_denied_exact_permission(string action, string deniedPermission)
	{
		var service = new StudentCertificateAppService(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!) { LazyServiceProvider = Denying(deniedPermission) };
		Func<Task> call = action switch
		{
			"issue" => () => service.GenerateAsync(new()), "duplicate" => () => service.DuplicateAsync(Guid.NewGuid(), new() { Reason = "Additional numbered copy" }),
			"reprint" => () => service.ReprintAsync(Guid.NewGuid(), new() { Reason = "Replacement print" }),
			"void" => () => service.VoidAsync(Guid.NewGuid(), new() { Reason = "Invalid issued certificate" }),
			_ => () => service.GeneratePdfAsync(Guid.NewGuid())
		};
		(await Should.ThrowAsync<AbpAuthorizationException>(call)).Data["PolicyName"].ShouldBe(deniedPermission);
	}
	[Fact]
	public async Task Record_action_photo_direct_service_rejects_upload_without_create_permission()
	{
		const string permission = "Students.Records.Changes.Create";
		var service = new StudentRecordPhotoAppService(null!, null!, null!, null!, null!) { LazyServiceProvider = Denying(permission) };
		(await Should.ThrowAsync<AbpAuthorizationException>(() => service.UploadAsync(Guid.NewGuid(), new() { FileName = "photo.png", ContentType = "image/png", Content = [] }))).Data["PolicyName"].ShouldBe(permission);
	}
	[Fact]
	public async Task Record_action_photo_direct_service_rejects_download_without_view_permission()
	{
		const string permission = "Students.Records.Changes.View";
		var service = new StudentRecordPhotoAppService(null!, null!, null!, null!, null!) { LazyServiceProvider = Denying(permission) };
		(await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(Guid.NewGuid(), Guid.NewGuid()))).Data["PolicyName"].ShouldBe(permission);
	}
	[Fact]
	public async Task Record_action_guardian_direct_service_rejects_manage_denial_before_any_link_mutation()
	{
		const string deniedPermission = "Students.Records.GuardianAccess.Manage";
		var service = new StudentGuardianAccessAppService(null!, null!, null!, null!, null!, null!, null!) { LazyServiceProvider = Denying(deniedPermission) };
		(await Should.ThrowAsync<AbpAuthorizationException>(() => service.ResolveAsync(Guid.NewGuid(), new()))).Data["PolicyName"].ShouldBe(deniedPermission);
	}
}
