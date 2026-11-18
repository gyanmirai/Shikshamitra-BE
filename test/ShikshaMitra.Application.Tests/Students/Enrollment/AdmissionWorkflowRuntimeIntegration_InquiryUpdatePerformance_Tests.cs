using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public Task Inquiry_update_reuses_loaded_form_and_family_and_returns_the_canonical_detail(bool includeFormValues, bool archivedForm) =>
		RunCertificationScenarioAsync(async () =>
		{
			var (offering, _, target) = await CreatePendingFamilyConfirmationFixtureAsync();
			if (archivedForm)
				await WithUnitOfWorkAsync(async () =>
				{
					var form = await _formRepository.GetAsync(offering.InquiryFormDefinitionId!.Value);
					form.Archive();
					await _formRepository.UpdateAsync(form, true);
				});
			var service = GetRequiredService<IAdmissionInquiryAppService>();
			var current = await service.GetAsync(target.Id);
			var input = SiblingContactUpdate(current, current.NormalizedPhone);
			input.SourceDetail = "Inquiry update regression";
			if (includeFormValues) input.FormValues = PublicSiblingInput(offering, target.ApplicantName).FormValues;
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				AdmissionInquiryDto result;
				using (var capture = new AcademicQueryCounter(db.Database.GetDbConnection()))
				{
					result = await service.UpdateAsync(target.Id, input);
					var reads = new Dictionary<string, int>();
					foreach (var table in new[] {
						"AppAdmissionFormDefinitions", "AppAdmissionFormFields",
						"AppAdmissionFormSections", "AppAdmissionFamilies", "AppAdmissionGuardians" })
						reads[table] = DirectReadCount(capture, table);
					Console.WriteLine($"Inquiry update includeFormValues={includeFormValues}; archived={archivedForm}; all commands={capture.Count}; reads={JsonSerializer.Serialize(reads)}");
					foreach (var (table, count) in reads)
						count.ShouldBeLessThanOrEqualTo(1,
							$"The update response must reuse the {table} snapshot already read by form validation in the same operation.");
					result.Id.ShouldBe(current.Id);
					result.FamilyId.ShouldBe(current.FamilyId);
					result.SourceDetail.ShouldBe(input.SourceDetail);
					result.ConcurrencyStamp.ShouldNotBe(current.ConcurrencyStamp);
				}
				result.FormDefinition!.Status.ShouldBe(archivedForm ? AdmissionFormStatus.Archived : AdmissionFormStatus.Published);
				// Compare at the original response boundary, before SQLite rematerializes
				// DateTime values in a later unit with different serialization kinds.
				var canonical = await service.GetAsync(target.Id);
				var expected = JsonSerializer.SerializeToElement(canonical);
				var actual = JsonSerializer.SerializeToElement(result);
				foreach (var property in expected.EnumerateObject())
					actual.GetProperty(property.Name).GetRawText().ShouldBe(property.Value.GetRawText(),
						$"The optimized response must preserve the canonical {property.Name} field.");
			});
		});
}
