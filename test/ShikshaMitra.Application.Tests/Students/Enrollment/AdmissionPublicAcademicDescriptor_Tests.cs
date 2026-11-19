using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Public_academic_choices_keep_source_descriptors_separate_from_offering_bounded_arrays() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var formId = offering.InquiryFormDefinitionId!.Value;
		var section = (await _formSectionRepository.GetListAsync(item => item.FormDefinitionId == formId)).First();
		var descriptors = new[]
		{
			(Code: "PUBLIC_BOARD_2", Json: "{\"source\":\"EducationBoards\"}", Value: offering.BoardId),
			(Code: "PUBLIC_MEDIUM_2", Json: "{\"source\":\"EducationMediums\",\"parentField\":\"PUBLIC_BOARD_2\"}", Value: offering.MediumId),
			(Code: "PUBLIC_CLASS_2", Json: "{\"source\":\"StandardsByMedium\",\"parentField\":\"PUBLIC_MEDIUM_2\"}", Value: offering.StandardId)
		};
		foreach (var spec in descriptors)
			await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), formId, section.Id, spec.Code,
				"Renamed " + spec.Code, AdmissionFormFieldType.Select, AdmissionFormBindingType.Custom, spec.Code, false, false, 20,
				null, null, null, null, null, spec.Json), autoSave: true);
		var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
		var link = await service.CreateLinkAsync(new() { AdmissionCycleId = offering.AdmissionCycleId, InquiryFormDefinitionId = formId, Name = "Academic descriptors", Source = AdmissionInquirySource.Website });
		using (_currentTenant.Change(null))
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
		{
			var context = await service.GetPublicAsync(link.Token, offering.Id);
			var fields = context.Form!.Sections.SelectMany(item => item.Fields).ToList();
			foreach (var spec in descriptors)
			{
				var field = fields.Single(item => item.Code == spec.Code);
				using var json = JsonDocument.Parse(JsonSerializer.Serialize(field));
				json.RootElement.GetProperty("OptionsSourceJson").GetString().ShouldBe(spec.Json);
				using var options = JsonDocument.Parse(field.OptionsJson!);
				options.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
				options.RootElement.EnumerateArray().Single().GetProperty("value").GetString().ShouldBe(spec.Value.ToString());
			}
		}
	});
}
