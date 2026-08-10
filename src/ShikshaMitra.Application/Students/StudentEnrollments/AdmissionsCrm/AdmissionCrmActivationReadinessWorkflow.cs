using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmActivationReadinessWorkflow(
	IRepository<AdmissionCrmNumberSeries, Guid> seriesRepository,
	AdmissionCrmActivationApplicantReadiness applicantReadiness,
	AdmissionCrmActivationPolicyReadiness policyReadiness,
	AdmissionCrmActivationRouteReadiness routeReadiness,
	AdmissionCrmActivationPlacementReadiness placementReadiness) : ApplicationService, ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<AdmissionCrmActivationPreviewDto> PreviewAsync(
		AdmissionInquiryLead lead,
		PreviewAdmissionCrmActivationDto input,
		string operation)
	{
		var items = new List<AdmissionCrmActivationReadinessItemDto>();
		await applicantReadiness.EvaluateAsync(lead, input, items);
		Add(items, "policy", "Policy evaluation", await policyReadiness.IsReadyAsync(lead),
			"Applicable policy rules are satisfied.", "Blocking policy rules are missing or unresolved.");
		var route = await routeReadiness.EvaluateAsync(lead);
		Add(items, "admission_route", "Admission route eligibility", route.Ready, route.Detail, route.Detail);
		var placement = await placementReadiness.ValidateAsync(lead, input, operation);
		Add(items, "placement", "Academic placement", placement.Valid, placement.Detail, placement.Detail);
		var series = await seriesRepository.FindAsync(input.GrNumberSeriesId);
		var seriesReady = series != null && series.TenantId == CurrentTenant.Id &&
			series.AcademicYearId == lead.AcademicYearId && series.IdentifierType == AdmissionCrmIdentifierType.GrNumber &&
			series.Status == AdmissionCrmNumberSeriesStatus.Published;
		Add(items, "numbering", "GR number series", seriesReady,
			"Published GR series is available.", "A published GR series is required.");
		var identityReady = lead.DateOfBirth.HasValue && !string.IsNullOrWhiteSpace(lead.PrimaryContactNo);
		Add(items, "student_identity", "Required applicant data", identityReady,
			"Date of birth and contact are available.", "Date of birth and primary contact are required.");
		if (input.RequireStudentAccount)
			Add(items, "student_access", "Student account input", !string.IsNullOrWhiteSpace(lead.Email),
				"Student email is available.", "Student email is required for access provisioning.");
		foreach (var handoff in input.ServiceHandoffs)
		{
			var valid = !string.IsNullOrWhiteSpace(handoff.DestinationModule) && handoff.ConsentConfirmed;
			Add(items, $"service_handoff_{handoff.Type}", $"{handoff.Type} service handoff", valid,
				$"A minimum-data request will be sent to {handoff.DestinationModule}.",
				"The optional service request needs a destination and confirmed consent before dispatch.", false);
		}

		return new AdmissionCrmActivationPreviewDto
		{
			LeadId = lead.Id, Items = items,
			IsReady = items.All(x => !x.Blocking || x.Status == AdmissionCrmActivationStepStatus.Completed),
			ExistingStudentId = lead.StudentId?.ToString(),
			GrNumberPreview = series == null ? string.Empty :
				$"{series.Prefix}{series.NextSequence.ToString().PadLeft(series.Padding, '0')}{series.Suffix}",
			DivisionStrength = placement.Strength, DivisionOccupancy = placement.Occupancy
		};
	}

	public async Task EnsureCurrentAsync(AdmissionCrmActivation activation, AdmissionInquiryLead lead, string operation)
	{
		var input = new PreviewAdmissionCrmActivationDto
		{
			EducationBoardId = activation.EducationBoardId, AcademicMediumId = activation.AcademicMediumId,
			AcademicMediumStageId = activation.AcademicMediumStageId, StandardId = activation.StandardId,
			DivisionId = activation.DivisionId, GrNumberSeriesId = activation.GrNumberSeriesId,
			RollNumber = activation.RollNumber, RequireStudentAccount = activation.RequireStudentAccount,
			RequireGuardianAccount = activation.RequireGuardianAccount,
			ServiceHandoffs = JsonSerializer.Deserialize<List<AdmissionCrmServiceHandoffRequestDto>>(
				activation.ServiceHandoffRequestsJson, JsonOptions) ?? []
		};
		var preview = await PreviewAsync(lead, input, operation);
		if (!preview.IsReady)
			throw new Volo.Abp.BusinessException("AdmissionCrm:ActivationReadinessChanged")
				.WithData("BlockingItems", string.Join(",", preview.Items
					.Where(x => x.Blocking && x.Status == AdmissionCrmActivationStepStatus.Failed).Select(x => x.Key)));
	}

	private static void Add(
		List<AdmissionCrmActivationReadinessItemDto> items,
		string key,
		string label,
		bool passed,
		string success,
		string failure,
		bool blocking = true) => items.Add(new AdmissionCrmActivationReadinessItemDto
		{
			Key = key, Label = label, Blocking = blocking, Detail = passed ? success : failure,
			Status = passed ? AdmissionCrmActivationStepStatus.Completed : AdmissionCrmActivationStepStatus.Failed
		});
}
