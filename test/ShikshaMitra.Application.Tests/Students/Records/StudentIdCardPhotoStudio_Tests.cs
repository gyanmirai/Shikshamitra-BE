using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common.IdentityCards;
using ShikshaMitra.Students.Records;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.ICards;
using Volo.Abp.Authorization;
using Volo.Abp.Auditing;
using System.Reflection;
using ShikshaMitra.Security.UserScope;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public void Icard_photo_audit_contract_excludes_pixels_and_preserves_request_metadata()
	{
		foreach (var type in new[] { typeof(IssueStudentIdCardsDto), typeof(StudentIdCardsPdfRequestDto), typeof(EmployeeICardPdfRequest) })
			type.GetProperty("PhotoEdits")!.GetCustomAttribute<DisableAuditingAttribute>().ShouldNotBeNull();
		typeof(IdentityCardPhotoDto).GetProperty("ImageData")!.GetCustomAttribute<DisableAuditingAttribute>().ShouldNotBeNull();
		typeof(StudentIdCard).GetProperty("StudentSnapshotJson")!.GetCustomAttribute<DisableAuditingAttribute>().ShouldNotBeNull();
		typeof(IssueStudentIdCardsDto).GetProperty("StudentIds")!.GetCustomAttribute<DisableAuditingAttribute>().ShouldBeNull();
		typeof(StudentIdCard).GetProperty("Status")!.GetCustomAttribute<DisableAuditingAttribute>().ShouldBeNull();
	}
	[Fact]
	public Task Icard_photo_edits_freeze_only_their_own_snapshot_and_preserve_originals() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(2);
		var original = Convert.FromBase64String(IdentityCardStudio_Tests.Raster().Split(',')[1]);
		var edited = IdentityCardStudio_Tests.Raster(3, 3);
		var blob = "studio/synthetic-original.png";
		await GetRequiredService<IBlobContainer>().SaveAsync(blob, original, overrideExisting: true);
		await WithUnitOfWorkAsync(async () => { var student = await GetRequiredService<IStudentRepository>().GetAsync(fixture.Students[0].Id); student.SetPhotoBlobName(blob); await GetRequiredService<IStudentRepository>().UpdateAsync(student, true); });
		var source = await fixture.Service.GetPhotoAsync(fixture.Students[0].Id);
		source.RecordId.ShouldBe(fixture.Students[0].Id); source.ImageData.ShouldBe(IdentityCardStudio_Tests.Raster()); source.SourceHash.Length.ShouldBe(64);
		var template = IcardDesign("Frozen studio photos", fixture.Year, fixture.Students.Select(s => s.Id).ToArray()); template.ShowPhoto = true;
		var saved = await fixture.Service.CreateTemplateAsync(template);
		var input = JsonSerializer.Deserialize<IssueStudentIdCardsDto>(JsonSerializer.Serialize(new { templateId = saved.Id, studentIds = fixture.Students.Select(s => s.Id), idempotencyKey = "photo-studio-freeze", photoEdits = new[] { new { recordId = fixture.Students[0].Id, sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original)).ToLowerInvariant(), imageData = edited } } }), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
		var issued = await fixture.Service.IssueAsync(input);
		var rows = await WithUnitOfWorkAsync(async () => await GetRequiredService<IRepository<StudentIdCard, Guid>>().GetListAsync(c => c.IdempotencyKey.StartsWith("photo-studio-freeze:")));
		rows.Single(c => c.StudentId == fixture.Students[0].Id).StudentSnapshotJson.ShouldContain("_ProcessedPhotoData");
		rows.Single(c => c.StudentId == fixture.Students[1].Id).StudentSnapshotJson.ShouldNotContain("_ProcessedPhotoData");
		JsonSerializer.Serialize(issued).ShouldNotContain("base64");
		JsonSerializer.Serialize(await fixture.Service.GetTemplatesAsync()).ShouldNotContain("base64");
		(await GetRequiredService<IBlobContainer>().GetAllBytesAsync(blob)).ShouldBe(original);
		await GetRequiredService<IBlobContainer>().DeleteAsync(blob);
		(await fixture.Service.GetPdfAsync(issued.Single(c => c.StudentId == fixture.Students[0].Id).Id!.Value)).Content.Length.ShouldBeGreaterThan(100);
		(await fixture.Service.ReprintAsync(issued.Single(c => c.StudentId == fixture.Students[0].Id).Id!.Value, new() { Reason = "Synthetic reprint proof" })).Content.Length.ShouldBeGreaterThan(100);
	});
	[Fact]
	public Task Icard_photo_edits_reject_stale_source_or_unselected_record() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1); var id = fixture.Students[0].Id;
		var saved = await fixture.Service.CreateTemplateAsync(IcardDesign("Photo validation", fixture.Year, id));
		foreach (var recordId in new[] { id, Guid.NewGuid() })
		{
			var input = JsonSerializer.Deserialize<StudentIdCardsPdfRequestDto>(JsonSerializer.Serialize(new { templateId = saved.Id, studentIds = new[] { id }, photoEdits = new[] { new { recordId, sourceHash = new string('0', 64), imageData = IdentityCardStudio_Tests.Raster() } } }), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
			await Should.ThrowAsync<BusinessException>(() => fixture.Service.ExportPdfAsync(input));
		}
	});
	[Fact]
	public Task Icard_photo_read_requires_current_student_scope_before_blob_access() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1);
		var actor = Guid.NewGuid(); await EnsureTestIdentityUserAsync(actor, "photo-no-scope-" + actor.ToString("N"));
		using (_principalAccessor.Change(ProfilePrincipal(actor)))
			(await Should.ThrowAsync<BusinessException>(() => fixture.Service.GetPhotoAsync(fixture.Students[0].Id))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<AbpAuthorizationException>(() => fixture.Service.GetPhotoAsync(fixture.Students[0].Id));
	});
	[Fact]
	public Task Icard_photo_employee_overrides_keep_profiles_and_templates_private_and_enforce_scope() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1);
		var employee = await CreateAssessmentInterviewerAsync();
		var original = Convert.FromBase64String(IdentityCardStudio_Tests.Raster().Split(',')[1]);
		var blob = await WithUnitOfWorkAsync(() => GetRequiredService<EmployeeDocumentManager>().UploadProfilePictureAsync(employee.Id, "original.png", original));
		var service = GetRequiredService<IEmployeeICardTemplateAppService>();
		var saved = await service.CreateAsync(new() { AcademicYearId = fixture.Year, Name = "Employee studio proof", Width = 428, Height = 270,
			SchemaVersion = 2, LogoMode = ShikshaMitra.TenantSettings.SchoolIdentity.SchoolIdentityAssetMode.Hidden,
			LayoutJson = """{"width":428,"height":270,"widthMm":85.6,"heightMm":53.98,"front":{"elements":[{"id":"photo","type":"photo","fieldKey":"photo","x":20,"y":20,"w":60,"h":80}]}}""",
			PrintSettingsJson = """{"pageSize":"Card","units":"mm"}""", FieldsJson = """[{"name":"photo","enabled":true}]""" });
		var source = await service.GetPhotoAsync(employee.Id); source.RecordId.ShouldBe(employee.Id);
		source.ImageData = IdentityCardStudio_Tests.Raster(3, 3);
		(await service.GenerateEmployeeICardsPdfAsync(saved.Id, new() { EmployeeIds = [employee.Id], PhotoEdits = [source] })).Length.ShouldBeGreaterThan(100);
		(await GetRequiredService<IBlobContainer>().GetAllBytesAsync(blob)).ShouldBe(original);
		JsonSerializer.Serialize(await service.GetAsync(saved.Id)).ShouldNotContain("base64");
		source.SourceHash = new string('0', 64);
		(await Should.ThrowAsync<BusinessException>(() => service.GenerateEmployeeICardsPdfAsync(saved.Id, new() { EmployeeIds = [employee.Id], PhotoEdits = [source] }))).Code.ShouldBe(IdentityCardErrorCodes.PhotoSourceChanged);
		var actor = Guid.NewGuid(); await EnsureTestIdentityUserAsync(actor, "staff-photo-no-scope-" + actor.ToString("N"));
		using (_principalAccessor.Change(ProfilePrincipal(actor)))
		{
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetPhotoAsync(employee.Id));
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GenerateEmployeeICardsPdfAsync(saved.Id, new() { EmployeeIds = [employee.Id] }));
		}
	});
}
