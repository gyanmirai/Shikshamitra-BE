using System;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Buffers.Binary;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private static byte[] PortalPhotoPng => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Portal_photo_applies_and_retries_without_staff_scope(bool parent) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync();
		var account = await LinkProfilePortalAsync(fixture.Student.Id, parent);
		using var principal = _principalAccessor.Change(ProfilePrincipal(account.UserId));
		var service = GetRequiredService<IMobileStudentPhotoAppService>();
		var info = await service.GetInfoAsync(fixture.Student.Id);
		var draft = await service.UploadAsync(fixture.Student.Id, new() { FileName = "photo.png", ContentType = "image/png", Content = PortalPhotoPng });
		var input = new SubmitStudentPhotoChangeDto { PhotoDraftId = draft.Id, ExpectedSnapshot = info.ExpectedSnapshot!, ClientKey = Guid.NewGuid().ToString("N") };
		var change = await service.SubmitAsync(fixture.Student.Id, input);
		change.Status.ShouldBe("Applied");
		(await service.SubmitAsync(fixture.Student.Id, input)).Id.ShouldBe(change.Id);
		(await service.GetPhotoAsync(fixture.Student.Id))!.Content.ShouldBe(PortalPhotoPng);
		input.ClientKey = Guid.NewGuid().ToString("N");
		(await Should.ThrowAsync<BusinessException>(() => service.SubmitAsync(fixture.Student.Id, input))).Code.ShouldBe("Students:Records:StaleSnapshot");
		var refreshed = await service.GetInfoAsync(fixture.Student.Id);
		refreshed.Version.ShouldNotBe(info.Version);
		var replacement = await service.UploadAsync(fixture.Student.Id, new() { FileName = "replacement.png", ContentType = "image/png", Content = PortalPhotoPng });
		input.PhotoDraftId = replacement.Id;
		input.ExpectedSnapshot = refreshed.ExpectedSnapshot!;
		(await service.SubmitAsync(fixture.Student.Id, input)).Status.ShouldBe("Applied");
		(await service.GetInfoAsync(fixture.Student.Id)).Version.ShouldNotBe(refreshed.Version);
	});

	[Fact]
	public Task Portal_photo_draft_ownership_and_idempotency_are_bound_to_requester() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync();
		var owner = await LinkProfilePortalAsync(fixture.Student.Id, true);
		var other = await LinkProfilePortalAsync(fixture.Student.Id, false);
		var service = GetRequiredService<IMobileStudentPhotoAppService>();
		SubmitStudentPhotoChangeDto input;
		using (_principalAccessor.Change(ProfilePrincipal(owner.UserId)))
		{
			var info = await service.GetInfoAsync(fixture.Student.Id);
			var draft = await service.UploadAsync(fixture.Student.Id, new() { FileName = "photo.png", ContentType = "image/png", Content = PortalPhotoPng });
			input = new() { PhotoDraftId = draft.Id, ExpectedSnapshot = info.ExpectedSnapshot!, ClientKey = Guid.NewGuid().ToString("N") };
		}
		using (_principalAccessor.Change(ProfilePrincipal(other.UserId)))
		{
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetDraftAsync(fixture.Student.Id, input.PhotoDraftId));
			(await Should.ThrowAsync<BusinessException>(() => service.SubmitAsync(fixture.Student.Id, input))).Code.ShouldBe("Students:Records:InvalidPhotoDraft");
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.SubmitAsync(Guid.NewGuid(), input));
		}
		using (_principalAccessor.Change(ProfilePrincipal(owner.UserId))) await service.SubmitAsync(fixture.Student.Id, input);
		using (_principalAccessor.Change(ProfilePrincipal(other.UserId)))
			(await Should.ThrowAsync<BusinessException>(() => service.SubmitAsync(fixture.Student.Id, input))).Code.ShouldBe("Students:Records:IdempotencyConflict");
	});

	[Fact]
	public Task Portal_photo_revoked_association_blocks_drafts_and_changes() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync();
		var account = await LinkProfilePortalAsync(fixture.Student.Id, true);
		using var principal = _principalAccessor.Change(ProfilePrincipal(account.UserId));
		var service = GetRequiredService<IMobileStudentPhotoAppService>();
		var draft = await service.UploadAsync(fixture.Student.Id, new() { FileName = "photo.png", ContentType = "image/png", Content = PortalPhotoPng });
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().DeleteAsync(account.LinkId, true));
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetDraftAsync(fixture.Student.Id, draft.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetPhotoAsync(fixture.Student.Id));
	});

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public Task Portal_photo_ownership_allows_access_when_legacy_permissions_are_denied(bool parent, bool denyView) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync();
		var account = await LinkProfilePortalAsync(fixture.Student.Id, parent);
		using var principal = _principalAccessor.Change(ProfilePrincipal(account.UserId));
		var checker = Substitute.For<IPermissionChecker>();
		checker.IsGrantedAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>()).Returns(true);
		checker.IsGrantedAsync(Arg.Any<ClaimsPrincipal>(), denyView ? ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.View : ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.Add).Returns(false);
		var provider = Substitute.For<IServiceProvider>();
		provider.GetService(Arg.Any<Type>()).Returns(call => (Type)call[0] == typeof(IPermissionChecker) ? checker : ServiceProvider.GetService((Type)call[0]));
		var authority = ActivatorUtilities.CreateInstance<StudentPortalPhotoAuthority>(provider);
		authority.LazyServiceProvider = new AbpLazyServiceProvider(ServiceProvider);
		await WithUnitOfWorkAsync(async () => (await authority.EnsureAsync(fixture.Student.Id, true)).ShouldNotBeNull());
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Portal_photo_applies_immediately_despite_school_approval_and_masking(bool parent) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync();
		var policy = StudentRecordsPolicy.Starter();
		policy.Changes["Photo"].Mode = "Approval";
		policy.Changes["Photo"].Stages = [new() { RoleNames = ["PortalPhotoReviewer"] }];
		policy.Access.Masks = [new() { FieldGroup = "Identity", AllowedRoleNames = ["school-staff"] }];
		await SetInlineFormPolicyAsync(policy, 2);
		var account = await LinkProfilePortalAsync(fixture.Student.Id, parent);
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(fixture.Student.Id);
			student.SetPhotoBlobName("synthetic/old.png");
			await _studentRepository.UpdateAsync(student, true);
		});
		StudentPhotoChangeDto change;
		using (_principalAccessor.Change(ProfilePrincipal(account.UserId)))
		{
			var photos = GetRequiredService<IMobileStudentPhotoAppService>();
			var info = await photos.GetInfoAsync(fixture.Student.Id);
			info.CanView.ShouldBeTrue(); info.CanUpdate.ShouldBeTrue();
			info.RequiresApproval.ShouldBeFalse();
			var draft = await photos.UploadAsync(fixture.Student.Id, new() { FileName = "photo.png", ContentType = "image/png", Content = PortalPhotoPng });
			var input = new SubmitStudentPhotoChangeDto { PhotoDraftId = draft.Id, ExpectedSnapshot = info.ExpectedSnapshot!, ClientKey = Guid.NewGuid().ToString("N") };
			change = await photos.SubmitAsync(fixture.Student.Id, input);
			change.Status.ShouldBe("Applied");
			(await photos.SubmitAsync(fixture.Student.Id, input)).Id.ShouldBe(change.Id);
		}
		using (_principalAccessor.Change(ProfilePrincipal(account.UserId)))
		{
			var info = await GetRequiredService<IMobileStudentPhotoAppService>().GetInfoAsync(fixture.Student.Id);
			info.PendingChange!.Status.ShouldBe("Applied");
		}
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(fixture.Student.Id);
			student.PhotoBlobName.ShouldNotBe("synthetic/old.png");
			(await GetRequiredService<IRepository<StudentRecordChange, Guid>>().GetAsync(change.Id)).RequiredApprovals.ShouldBe(0);
		});
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Portal_photo_does_not_require_completed_records_onboarding(bool parent) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync();
		var policy = StudentRecordsPolicy.Starter();
		policy.Provisioning.Mode = "Summary";
		await SetInlineFormPolicyAsync(policy, 2);
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().DeleteAsync(x => x.StudentId == fixture.Student.Id, true));
		await WithUnitOfWorkAsync(async () =>
		{
			(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<StudentRecordsAccessService>().EnsureWritableAsync(fixture.Student.Id)))
				.Code.ShouldBe("Students:Records:OnboardingIncomplete");
		});
		var account = await LinkProfilePortalAsync(fixture.Student.Id, parent);
		using var principal = _principalAccessor.Change(ProfilePrincipal(account.UserId));
		var photos = GetRequiredService<IMobileStudentPhotoAppService>();
		var info = await photos.GetInfoAsync(fixture.Student.Id);
		info.CanView.ShouldBeTrue(); info.CanUpdate.ShouldBeTrue(); info.ExpectedSnapshot.ShouldNotBeNullOrWhiteSpace();
		var draft = await photos.UploadAsync(fixture.Student.Id, new() { FileName = "photo.png", ContentType = "image/png", Content = PortalPhotoPng });
		(await photos.SubmitAsync(fixture.Student.Id, new() { PhotoDraftId = draft.Id, ExpectedSnapshot = info.ExpectedSnapshot!, ClientKey = Guid.NewGuid().ToString("N") })).Status.ShouldBe("Applied");
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Portal_photo_denies_inactive_account_and_foreign_tenant(bool parent) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync();
		var account = await LinkProfilePortalAsync(fixture.Student.Id, parent);
		using var principal = _principalAccessor.Change(ProfilePrincipal(account.UserId));
		var photos = GetRequiredService<IMobileStudentPhotoAppService>();
		using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
			await Should.ThrowAsync<AbpAuthorizationException>(() => photos.GetInfoAsync(fixture.Student.Id));
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IRepository<IdentityUser, Guid>>();
			var user = await users.GetAsync(account.UserId);
			user.SetIsActive(false);
			await users.UpdateAsync(user, true);
		});
		await Should.ThrowAsync<AbpAuthorizationException>(() => photos.GetInfoAsync(fixture.Student.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(() => photos.UploadAsync(fixture.Student.Id, new() { FileName = "photo.png", ContentType = "image/png", Content = PortalPhotoPng }));
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Portal_photo_old_approval_requests_do_not_block_or_overwrite_new_photo(bool portalOrigin) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync();
		var account = await LinkProfilePortalAsync(fixture.Student.Id, true);
		var reviewer = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(reviewer, $"legacy-photo-review-{reviewer:N}");
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: reviewer), true);
			var roles = GetRequiredService<IdentityRoleManager>();
			(await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "PortalPhotoReviewer", TestTenantId))).Succeeded.ShouldBeTrue();
			(await GetRequiredService<IdentityUserManager>().AddToRoleAsync((await GetRequiredService<IdentityUserManager>().FindByIdAsync(reviewer.ToString()))!, "PortalPhotoReviewer")).Succeeded.ShouldBeTrue();
		});
		var oldId = Guid.NewGuid();
		var photos = GetRequiredService<IMobileStudentPhotoAppService>();
		using (_principalAccessor.Change(ProfilePrincipal(account.UserId)))
		{
			var oldDraft = await photos.UploadAsync(fixture.Student.Id, new() { FileName = "old.png", ContentType = "image/png", Content = PortalPhotoPng });
			await WithUnitOfWorkAsync(async () =>
			{
				var old = new StudentRecordChange(oldId, TestTenantId, fixture.Student.Id, fixture.Student.AcademicYearId!.Value, account.UserId,
					"photo", "Photo", Guid.NewGuid().ToString("N"), "Legacy approval photo", await GetRequiredService<StudentRecordPayloadService>().CaptureAsync(fixture.Student.Id, "photo"),
					System.Text.Json.JsonSerializer.Serialize(new StudentRecordPhotoPayload { PhotoDraftId = oldDraft.Id }, StudentRecordPayloadService.JsonOptions),
					StudentRecordsPolicy.Starter().ToJson(), null, "[{\"roleNames\":[\"PortalPhotoReviewer\"]}]", 1, DateTimeOffset.UtcNow.AddMinutes(-1));
				if (portalOrigin) old.SetProperty("Origin", MobileStudentPhotoAppService.PortalOrigin);
				await GetRequiredService<IRepository<StudentRecordChange, Guid>>().InsertAsync(old, true);
			});
			var info = await photos.GetInfoAsync(fixture.Student.Id);
			info.PendingChange.ShouldBeNull();
			var draft = await photos.UploadAsync(fixture.Student.Id, new() { FileName = "new.png", ContentType = "image/png", Content = PortalPhotoPng });
			(await photos.SubmitAsync(fixture.Student.Id, new() { PhotoDraftId = draft.Id, ExpectedSnapshot = info.ExpectedSnapshot!, ClientKey = Guid.NewGuid().ToString("N") })).Status.ShouldBe("Applied");
		}
		string? official = null;
		await WithUnitOfWorkAsync(async () =>
		{
			official = (await _studentRepository.GetAsync(fixture.Student.Id)).PhotoBlobName;
			(await GetRequiredService<IRepository<StudentRecordChange, Guid>>().GetAsync(oldId)).Status.ShouldBe("Pending");
		});
		using (_principalAccessor.Change(ProfilePrincipal(reviewer)))
		{
			var records = GetRequiredService<IStudentRecordChangeAppService>();
			var pending = await records.GetAsync(oldId);
			var result = await records.ApproveAsync(oldId, new() { ConcurrencyStamp = pending.ConcurrencyStamp, Reason = "Reviewed legacy photo" });
			result.Status.ShouldBe("Conflict");
			result.ExceptionCode.ShouldBe("Students:Records:StaleSnapshot");
		}
		await WithUnitOfWorkAsync(async () => (await _studentRepository.GetAsync(fixture.Student.Id)).PhotoBlobName.ShouldBe(official));
	});

	[Theory]
	[InlineData("photo.gif", "image/gif")]
	[InlineData("photo.webp", "image/webp")]
	[InlineData("photo.png", "image/jpeg")]
	[InlineData("photo.jpg", "image/jpeg")]
	public void Portal_photo_rejects_unsupported_or_mismatched_uploads(string name, string mime)
	{
		Should.Throw<BusinessException>(() => MobileStudentPhotoAppService.ValidateUpload(new() { FileName = name, ContentType = mime, Content = PortalPhotoPng }));
	}

	[Fact]
	public void Portal_photo_upload_limit_is_inclusive_and_rejects_empty_or_signature_only_files()
	{
		var atLimit = new byte[3 * 1024 * 1024];
		var chunkOffset = PortalPhotoPng.Length - 12;
		PortalPhotoPng.AsSpan(0, chunkOffset).CopyTo(atLimit);
		var paddingLength = atLimit.Length - PortalPhotoPng.Length - 12;
		BinaryPrimitives.WriteUInt32BigEndian(atLimit.AsSpan(chunkOffset, 4), (uint)paddingLength);
		"tEXt"u8.CopyTo(atLimit.AsSpan(chunkOffset + 4));
		atLimit[chunkOffset + 8] = (byte)'x';
		BinaryPrimitives.WriteUInt32BigEndian(atLimit.AsSpan(chunkOffset + 8 + paddingLength, 4), StudentPhotoImageValidation.Crc32(atLimit.AsSpan(chunkOffset + 4, paddingLength + 4)));
		PortalPhotoPng.AsSpan(PortalPhotoPng.Length - 12).CopyTo(atLimit.AsSpan(atLimit.Length - 12));
		MobileStudentPhotoAppService.ValidateUpload(new() { FileName = "photo.png", ContentType = "image/png", Content = atLimit });
		foreach (var bytes in new[] { Array.Empty<byte>(), new byte[] { 137, 80, 78, 71 }, new byte[3 * 1024 * 1024 + 1] })
			Should.Throw<BusinessException>(() => MobileStudentPhotoAppService.ValidateUpload(new() { FileName = "photo.png", ContentType = "image/png", Content = bytes }));
	}

	[Fact]
	public void Portal_photo_rejects_fabricated_images_and_corrupt_png_chunks()
	{
		var fabricated = new byte[45];
		PortalPhotoPng.AsSpan(0, 8).CopyTo(fabricated);
		"IHDR"u8.CopyTo(fabricated.AsSpan(12));
		"IEND"u8.CopyTo(fabricated.AsSpan(37));
		var corrupt = PortalPhotoPng;
		corrupt[45] ^= 1;
		foreach (var bytes in new[] { fabricated, corrupt })
			Should.Throw<BusinessException>(() => MobileStudentPhotoAppService.ValidateUpload(new() { FileName = "photo.png", ContentType = "image/png", Content = bytes }));
		Should.Throw<BusinessException>(() => MobileStudentPhotoAppService.ValidateUpload(new() { FileName = "photo.jpg", ContentType = "image/jpeg", Content = [255, 216, 255, 0, 0, 0, 0, 0, 0, 0, 255, 217] }));
	}

	[Fact]
	public void Portal_photo_accepts_real_jpeg_from_existing_cross_platform_image_engine()
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var jpeg = Document.Create(document => document.Page(page => { page.Size(8, 8); page.Content().Background("#4499AA"); }))
			.GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Jpeg, RasterDpi = 72 }).Single();
		MobileStudentPhotoAppService.ValidateUpload(new() { FileName = "photo.jpeg", ContentType = "image/jpeg", Content = jpeg });
		var scan = Enumerable.Range(2, jpeg.Length - 3).First(i => jpeg[i] == 0xff && jpeg[i + 1] == 0xda);
		var scanEnd = scan + 2 + BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(scan + 2, 2));
		var truncated = new byte[scanEnd + 2];
		jpeg.AsSpan(0, scanEnd).CopyTo(truncated);
		truncated[^2] = 0xff; truncated[^1] = 0xd9;
		Should.Throw<BusinessException>(() => MobileStudentPhotoAppService.ValidateUpload(new() { FileName = "broken.jpg", ContentType = "image/jpeg", Content = truncated }));
		Should.Throw<BusinessException>(() => MobileStudentPhotoAppService.ValidateUpload(new() { FileName = "partial.jpg", ContentType = "image/jpeg", Content = jpeg[..(scanEnd + 1)] }));
	}

	[Fact]
	public void Portal_photo_rejects_bounded_headers_with_corrupt_pixel_data_and_oversized_dimensions()
	{
		var bytes = PortalPhotoPng;
		// This fixture's IDAT is 11 bytes; preserve the chunk CRC while invalidating its deflate stream.
		bytes.AsSpan(41, 11).Clear();
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(52, 4), StudentPhotoImageValidation.Crc32(bytes.AsSpan(37, 15)));
		Should.Throw<BusinessException>(() => MobileStudentPhotoAppService.ValidateUpload(new() { FileName = "broken.png", ContentType = "image/png", Content = bytes }));
		bytes = PortalPhotoPng;
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16, 4), 50000);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(29, 4), StudentPhotoImageValidation.Crc32(bytes.AsSpan(12, 17)));
		Should.Throw<BusinessException>(() => MobileStudentPhotoAppService.ValidateUpload(new() { FileName = "huge.png", ContentType = "image/png", Content = bytes }));
	}
}
