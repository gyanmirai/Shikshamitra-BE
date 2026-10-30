using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HttpApi.Controllers.Students.Portal;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Records;

public class MobileStudentPhotoApiContract_Tests
{
	[Fact]
	public void Photo_routes_preserve_profile_read_and_separate_view_from_add()
	{
		var controller = typeof(MobileStudentPhotoController);
		controller.GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe("api/mobile/v1/students/{studentId:guid}/profile/photo");
		controller.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.View);
		foreach (var method in new[] { nameof(MobileStudentPhotoController.UploadAsync), nameof(MobileStudentPhotoController.SubmitAsync) })
			controller.GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.Add);
		controller.GetMethods().SelectMany(x => x.GetCustomAttributes<HttpDeleteAttribute>()).ShouldBeEmpty();
		typeof(MobileStudentProfileController).GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBeNull();
	}

	[Fact]
	public async Task Multipart_upload_bounds_actual_stream_even_when_reported_length_is_false()
	{
		var service = Substitute.For<IMobileStudentPhotoAppService>();
		var file = Substitute.For<IFormFile>();
		file.Length.Returns(1);
		file.OpenReadStream().Returns(new MemoryStream(new byte[3 * 1024 * 1024 + 1]));
		var controller = new MobileStudentPhotoController(service) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
		(await Should.ThrowAsync<BusinessException>(() => controller.UploadAsync(Guid.NewGuid(), file))).Code.ShouldBe("Students:Records:PhotoTooLarge");
		await service.DidNotReceiveWithAnyArgs().UploadAsync(default, default!);
	}

	[Fact]
	public async Task Multipart_upload_accepts_exact_limit_and_forwards_binary_content()
	{
		var bytes = new byte[3 * 1024 * 1024];
		var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "photo.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
		var service = Substitute.For<IMobileStudentPhotoAppService>();
		service.UploadAsync(Arg.Any<Guid>(), Arg.Any<UploadStudentPhotoDto>()).Returns(new StudentRecordPhotoDraftDto());
		var controller = new MobileStudentPhotoController(service) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
		await controller.UploadAsync(Guid.NewGuid(), file);
		await service.Received(1).UploadAsync(Arg.Any<Guid>(), Arg.Is<UploadStudentPhotoDto>(x => x.Content.Length == bytes.Length && x.ContentType == "image/jpeg"));
	}

	[Fact]
	public async Task Multipart_upload_rejects_extra_file_parts()
	{
		var service = Substitute.For<IMobileStudentPhotoAppService>();
		var file = new FormFile(new MemoryStream([1]), 0, 1, "file", "photo.png");
		var context = new DefaultHttpContext();
		context.Request.ContentType = "multipart/form-data; boundary=synthetic";
		context.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), new FormFileCollection { file, file });
		var controller = new MobileStudentPhotoController(service) { ControllerContext = new() { HttpContext = context } };
		(await Should.ThrowAsync<BusinessException>(() => controller.UploadAsync(Guid.NewGuid(), file))).Code.ShouldBe("Students:Records:InvalidPhoto");
		await service.DidNotReceiveWithAnyArgs().UploadAsync(default, default!);
	}
}
