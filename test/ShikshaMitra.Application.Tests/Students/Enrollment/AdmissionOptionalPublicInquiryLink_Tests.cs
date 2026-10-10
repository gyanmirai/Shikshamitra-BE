using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public void Optional_public_link_migration_should_only_relax_assignment_column_and_guard_rollback()
	{
		var migration = new ShikshaMitra.Migrations.MakePublicInquiryAssignmentOptional();
		var column = migration.UpOperations.ShouldHaveSingleItem()
			.ShouldBeOfType<Microsoft.EntityFrameworkCore.Migrations.Operations.AlterColumnOperation>();
		column.Table.ShouldBe("AppAdmissionPublicInquiryLinks");
		column.Name.ShouldBe("CounsellorAssignmentProfileId");
		column.IsNullable.ShouldBeTrue();
		column.OldColumn.IsNullable.ShouldBeFalse();
		migration.DownOperations.First().ShouldBeOfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>()
			.Sql.ShouldContain("RAISE EXCEPTION");
		migration.DownOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.AlterColumnOperation>()
			.ShouldHaveSingleItem().DefaultValue.ShouldBeNull();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Manual_public_link_should_persist_without_setup_and_capture_unassigned_inquiry(bool offeringHasSetup) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			if (offeringHasSetup)
			{
				var setup = await GetRequiredService<IAdmissionIntakeSetupAppService>().CreateAsync(ValidSetupInput("Optional public routing"));
				await LinkInputAsync(offering, setup);
			}
			else await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.ConfigureIntake(null, null);
				await _offeringRepository.UpdateAsync(stored, true);
			});
			var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
			(await service.GetLinkOptionsAsync(offering.AdmissionCycleId)).ShouldContain(option =>
				option.CounsellorAssignmentProfileId == null && option.InquiryFormDefinitionId == offering.InquiryFormDefinitionId);
			var link = await service.CreateLinkAsync(new CreateAdmissionPublicInquiryLinkDto
			{
				AdmissionCycleId = offering.AdmissionCycleId, InquiryFormDefinitionId = offering.InquiryFormDefinitionId,
				Name = "Manual inquiry link", Source = AdmissionInquirySource.Website
			});
			link.CounsellorAssignmentProfileId.ShouldBeNull();
			var storedLink = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>().GetAsync(link.Id));
			storedLink.CounsellorAssignmentProfileId.ShouldBeNull();
			var input = new SubmitPublicAdmissionInquiryDto
			{
				AdmissionOfferingId = offering.Id, BoardId = offering.BoardId,
				MediumId = offering.MediumId, StandardId = offering.StandardId,
				ApplicantFirstName = "Aarav", GuardianFirstName = "Meera", GuardianPhone = "9876543210",
				ConsentToContact = true,
				FormValues =
				[
					new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"Aarav\"" },
					new() { FieldCode = "APPLICANT_DOB", ValueJson = "\"2018-04-15T00:00:00Z\"" },
					new() { FieldCode = "APPLICANT_GENDER", ValueJson = "\"Male\"" },
					new() { FieldCode = "GUARDIAN_MOBILE", ValueJson = "\"9876543210\"" }
				]
			};
			using (_currentTenant.Change(null))
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
			{
				(await service.GetPublicAsync(link.Token, offering.Id)).Form!.Id.ShouldBe(offering.InquiryFormDefinitionId!.Value);
				await service.SubmitPublicAsync(link.Token, input);
				await service.SubmitPublicAsync(link.Token, input);
			}
			var matches = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetListAsync(item =>
				item.AdmissionCycleId == offering.AdmissionCycleId && item.NormalizedPhone == input.GuardianPhone));
			matches.Count.ShouldBe(1);
			matches.Single().AdmissionOfferingId.ShouldBe(offering.Id);
			matches.Single().AssignedCounsellorId.ShouldBeNull();
		});

	[Fact]
	public Task Manual_public_link_should_require_published_form_and_creator_offering_scope() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
			var input = new CreateAdmissionPublicInquiryLinkDto { AdmissionCycleId = offering.AdmissionCycleId, Name = "Manual inquiry" };
			(await Should.ThrowAsync<BusinessException>(() => service.CreateLinkAsync(input))).Code.ShouldBe(AdmissionIntakeProfileErrorCodes.InvalidConfiguration);
			input.InquiryFormDefinitionId = offering.InquiryFormDefinitionId;
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(
				[new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString())], "test"))))
				(await Should.ThrowAsync<BusinessException>(() => service.CreateLinkAsync(input))).Code.ShouldBe(AdmissionErrorCodes.PublicInquiryOfferingRequired);
			(await GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>().GetListAsync()).ShouldBeEmpty();
		});
}
