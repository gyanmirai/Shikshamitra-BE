using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Configurable_card_roll_number_and_prefix_round_trip_to_issued_artwork() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1);
		var studentId = fixture.Students.Single().Id;
		await WithUnitOfWorkAsync(async () =>
		{
			var students = GetRequiredService<IRepository<Student, Guid>>();
			var student = await students.GetAsync(studentId);
			student.AssignRollNo(23);
			await students.UpdateAsync(student);
		});
		var design = IcardDesign("Student roll number", fixture.Year, studentId);
		design.SchemaVersion = 2;
		design.Fields = ["RollNo"];
		design.LayoutJson = """{"width":428,"height":270,"widthMm":85.6,"heightMm":53.98,"front":{"elements":[{"id":"roll","type":"text","fieldKey":"RollNo","label":"Roll layer","value":"Enter text","prefix":"Roll #: ","x":20,"y":20,"w":388,"h":60,"style":{"fontSize":14,"showLabel":true}}]}}""";
		var saved = await fixture.Service.CreateTemplateAsync(design);
		var request = new StudentIdCardPreviewRequestDto { TemplateId = saved.Id, StudentIds = [studentId] };
		var preview = (await fixture.Service.PreviewAsync(request)).Single();
		preview.StudentSnapshot["RollNo"].ShouldBe("23");
		var issued = (await fixture.Service.IssueAsync(new() { TemplateId = saved.Id, StudentIds = [studentId], IdempotencyKey = "studio-roll-prefix" })).Single();
		issued.TemplateSnapshot.LayoutJson.ShouldBe(design.LayoutJson);
		var svg = StudentIdCardDesign.Artwork(issued, null, null).FrontSvg;
		svg.ShouldContain("Roll #: 23");
		svg.ShouldNotContain("Roll layer");
		svg.ShouldNotContain("Enter text");
	});

	[Fact]
	public void Configurable_card_rejects_an_overlong_prefix()
	{
		var template = new StudentIdCardTemplateDto
		{
			SchemaVersion = 2,
			LayoutJson = """{"width":428,"height":270,"widthMm":85.6,"heightMm":53.98,"front":{"elements":[{"id":"name","type":"text","fieldKey":"StudentName","prefix":"PREFIX","x":20,"y":20,"w":388,"h":60}]}}"""
				.Replace("PREFIX", new string('x', 151))
		};
		var exception = Should.Throw<BusinessException>(() => StudentIdCardDesign.Resolve(template));
		exception.Data["detail"].ShouldBeOfType<string>().ShouldContain("prefix");
	}

	[Fact]
	public void Configurable_student_hidden_logo_suppresses_the_logo_element()
	{
		var card = new StudentIdCardDto
		{
			TemplateSnapshot = new()
			{
				SchemaVersion = 2,
				Logo = new() { Mode = SchoolIdentityAssetMode.Hidden },
				LayoutJson = """{"width":428,"height":270,"widthMm":85.6,"heightMm":53.98,"front":{"elements":[{"id":"logo","type":"logo","x":20,"y":20,"w":50,"h":50}]}}"""
			}
		};
		StudentIdCardDesign.Artwork(card, null, null).FrontSvg.ShouldNotContain("LOGO");
	}

	[Fact]
	public Task Configurable_card_captures_the_medium_signature_and_reprints_the_issued_choice() => RunCertificationScenarioAsync(async () =>
	{
		using var actor = _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(
			BuildAdminPrincipal().Claims.Append(new Claim(AbpClaimTypes.TenantId, TestTenantId.ToString())), "Test")));
		var fixture = await IcardLibraryFixtureAsync(1);
		var student = fixture.Students.Single();
		var identity = GetRequiredService<ISchoolIdentityAppService>();
		var documentIdentity = GetRequiredService<SchoolIdentityDocumentService>();
		var pixels = SchoolIdentityTestImage.Png(64, 32);
		var mediumSignature = await identity.UploadAsync(SchoolIdentityAssetKind.Signature,
			student.AcademicBoardId, student.AcademicMediumId, "medium-signature.png", pixels);
		var profile = await identity.UpdateAsync(new()
		{
			BoardId = student.AcademicBoardId, MediumId = student.AcademicMediumId,
			Choices = new() { Signature = new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = mediumSignature.Id, PrincipalName = "Medium Principal", Designation = "Principal" } }
		});
		var design = IcardDesign("Signed student card", fixture.Year, student.Id);
		design.SchemaVersion = 2;
		design.BackSideEnabled = true;
		design.Signature = new() { Mode = SchoolIdentityAssetMode.Inherit };
		design.LayoutJson = """{"width":428,"height":270,"widthMm":85.6,"heightMm":53.98,"front":{"elements":[{"id":"name","type":"text","fieldKey":"StudentName","x":20,"y":20,"w":388,"h":60}]},"back":{"elements":[{"id":"signature","type":"signature","x":240,"y":180,"w":120,"h":32}]}}""";
		var saved = await fixture.Service.CreateTemplateAsync(design);
		saved.Signature.ShouldBe(design.Signature);
		var request = new StudentIdCardPreviewRequestDto { TemplateId = saved.Id, StudentIds = [student.Id] };
		var preview = (await fixture.Service.PreviewAsync(request)).Single();
		preview.TemplateSnapshot.SchoolIdentity!.SignatureAssetId.ShouldBe(mediumSignature.Id);
		var issued = (await fixture.Service.IssueAsync(new()
		{
			TemplateId = saved.Id, StudentIds = [student.Id], IdempotencyKey = "medium-signature-card"
		})).Single();
		await identity.UpdateAsync(new()
		{
			BoardId = student.AcademicBoardId, MediumId = student.AcademicMediumId, ConcurrencyStamp = profile.ConcurrencyStamp,
			Choices = new() { Signature = new() { Mode = SchoolIdentityAssetMode.Hidden } }
		});
		(await fixture.Service.PreviewAsync(request)).Single().TemplateSnapshot.SchoolIdentity!.SignatureAssetId.ShouldBeNull();
		var historical = (await fixture.Service.GetHistoryAsync(student.Id)).Single();
		historical.TemplateSnapshot.SchoolIdentity!.SignatureAssetId.ShouldBe(mediumSignature.Id);
		var assets = await WithUnitOfWorkAsync(() => documentIdentity.LoadAsync(historical.TemplateSnapshot.SchoolIdentity));
		var artwork = StudentIdCardDesign.Artwork(historical, null, null, assets.Signature);
		artwork.BackSvg!.ShouldContain("data:image/png;base64,");
		artwork.BackSvg!.ShouldContain("xMidYMid meet");
		(await fixture.Service.GetPdfAsync(issued.Id!.Value)).Content.Take(4).ToArray().ShouldBe("%PDF"u8.ToArray());
		saved.Signature = new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = mediumSignature.Id, PrincipalName = "Medium Principal", Designation = "Principal" };
		var scopedAsset = await Should.ThrowAsync<BusinessException>(() => fixture.Service.UpdateTemplateAsync(saved.Id!.Value, saved));
		scopedAsset.Code.ShouldBe(SchoolIdentityErrorCodes.AssetInvalid);
		var custom = await identity.UploadAsync(SchoolIdentityAssetKind.Signature, null, null, "template-signature.png", pixels);
		saved.Signature = new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = custom.Id, PrincipalName = "Template Principal", Designation = "Principal" };
		saved = await fixture.Service.UpdateTemplateAsync(saved.Id!.Value, saved);
		(await fixture.Service.PreviewAsync(request)).Single().TemplateSnapshot.SchoolIdentity!.SignatureAssetId.ShouldBe(custom.Id);
		saved.Signature = new() { Mode = SchoolIdentityAssetMode.Hidden };
		await fixture.Service.UpdateTemplateAsync(saved.Id!.Value, saved);
		(await fixture.Service.PreviewAsync(request)).Single().TemplateSnapshot.SchoolIdentity!.SignatureAssetId.ShouldBeNull();
	});

	[Fact]
	public Task Icard_templates_schema_two_roundtrips_and_issued_duplex_snapshots_survive_edits() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1);
		var student = fixture.Students.Single();
		var design = IcardDesign("Configurable classic identity", fixture.Year, student.Id);
		design.SchemaVersion = 2;
		design.PresetKey = "classic-identity";
		design.BackSideEnabled = true;
		design.LayoutJson = """{"orientation":"Horizontal","width":428,"height":270,"widthMm":85.6,"heightMm":53.98,"front":{"elements":[{"id":"name","type":"text","fieldKey":"StudentName","x":20,"y":20,"w":388,"h":60,"style":{"fontSize":14}}]},"back":{"elements":[{"id":"return","type":"text","fieldKey":"static","value":"Return to the school office.","x":20,"y":20,"w":388,"h":60,"style":{"fontSize":14}}]}}""";
		design.PrintSettingsJson = """{"pageSize":"Card","units":"mm","cardsPerPage":8,"sideMode":"both"}""";
		var saved = await fixture.Service.CreateTemplateAsync(design);
		saved.SchemaVersion.ShouldBe(2); saved.LayoutJson.ShouldBe(design.LayoutJson); saved.PrintSettingsJson.ShouldBe(design.PrintSettingsJson); saved.BackSideEnabled.ShouldBeTrue();
		var issued = (await fixture.Service.IssueAsync(new() { TemplateId = saved.Id, StudentIds = [student.Id], IdempotencyKey = "configurable-classic-duplex" })).Single();
		issued.TemplateSnapshot.LayoutJson.ShouldBe(design.LayoutJson);
		saved.LayoutJson = saved.LayoutJson!.Replace("Return to the school office.", "Updated office instructions.");
		await fixture.Service.UpdateTemplateAsync(saved.Id!.Value, saved);
		var frozen = (await fixture.Service.GetHistoryAsync(student.Id)).Single();
		frozen.TemplateSnapshot.LayoutJson.ShouldBe(design.LayoutJson);
		var pdf = (await fixture.Service.GetPdfAsync(issued.Id!.Value)).Content;
		Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page\b").Count.ShouldBe(2);
	});
}
