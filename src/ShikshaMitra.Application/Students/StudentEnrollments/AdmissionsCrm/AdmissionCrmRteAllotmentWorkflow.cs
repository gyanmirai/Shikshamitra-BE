using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRteAllotmentWorkflow(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	StudentScopeAccessService studentScopeAccessService,
	AdmissionCrmLeadAccessReader leadAccessReader,
	AdmissionCrmRouteDefinitionWorkflow definitionWorkflow,
	AdmissionCrmRouteDataValidator routeDataValidator,
	AdmissionCrmRouteAssignmentWorkflow assignmentWorkflow,
	AdmissionCrmRouteTimelineWriter timelineWriter) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmRteAllotmentImportResultDto> PreviewAsync(
		Guid academicYearId,
		ImportAdmissionCrmRteAllotmentsDto input)
	{
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(PreviewAsync));
		var results = new List<AdmissionCrmRteAllotmentImportItemResultDto>();
		var batchNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var row in input.Rows)
		{
			var item = CreateResult(row);
			try
			{
				var normalizedNumber = row.RouteData.AllotmentNumber?.Trim();
				if (string.IsNullOrWhiteSpace(normalizedNumber))
					throw new BusinessException("AdmissionCrm:RteAllotmentNumberRequired");
				if (!batchNumbers.Add(normalizedNumber))
					throw new BusinessException("AdmissionCrm:DuplicateRteAllotmentInImport");
				var lead = await ResolveLeadAsync(academicYearId, row, nameof(PreviewAsync));
				item.LeadId = lead.Id;
				if (lead.AcademicYearId != academicYearId)
					throw new BusinessException("AdmissionCrm:RteAllotmentCycleMismatch");
				var definition = await definitionWorkflow.GetAsync(row.RouteDefinitionId);
				if (definition.AcademicYearId != academicYearId || definition.RouteType != AdmissionCrmRouteType.RteEws)
					throw new BusinessException("AdmissionCrm:RteRouteDefinitionRequired");
				assignmentWorkflow.EnsureCanAssign(definition, lead);
				await routeDataValidator.ValidateAsync(definition.RouteType, row.RouteData, lead);
				AdmissionCrmRouteEvidenceValidator.EnsureValid(definition, row.EvidenceReferences);
				item.Success = true;
			}
			catch (BusinessException exception)
			{
				item.ErrorCode = exception.Code ?? exception.Message;
			}
			results.Add(item);
		}

		return CreateSummary(results);
	}

	public async Task<AdmissionCrmRteAllotmentImportResultDto> ImportAsync(
		Guid academicYearId,
		ImportAdmissionCrmRteAllotmentsDto input)
	{
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(ImportAsync));
		var results = new List<AdmissionCrmRteAllotmentImportItemResultDto>();
		var batchNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var row in input.Rows)
		{
			var item = CreateResult(row);
			try
			{
				var normalizedNumber = row.RouteData.AllotmentNumber?.Trim();
				if (string.IsNullOrWhiteSpace(normalizedNumber) || !batchNumbers.Add(normalizedNumber))
					throw new BusinessException("AdmissionCrm:DuplicateRteAllotmentInImport");
				var lead = await ResolveLeadAsync(academicYearId, row, nameof(ImportAsync));
				item.LeadId = lead.Id;
				if (lead.AcademicYearId != academicYearId)
					throw new BusinessException("AdmissionCrm:RteAllotmentCycleMismatch");
				var assignment = await assignmentWorkflow.AssignAsync(
					lead.Id,
					new AssignAdmissionCrmSpecialRouteDto
					{
						RouteDefinitionId = row.RouteDefinitionId,
						RouteData = row.RouteData,
						EvidenceReferences = row.EvidenceReferences,
						Reason = $"Government allotment import: {normalizedNumber}."
					},
					nameof(ImportAsync));
				item.Success = true;
				item.RouteAssignmentId = assignment.Id;
				await timelineWriter.AddAsync(
					lead,
					AdmissionCrmTimelineEventType.GovernmentAllotmentImported,
					"Government allotment imported",
					normalizedNumber,
					assignment.Id);
			}
			catch (BusinessException exception)
			{
				item.ErrorCode = exception.Code ?? exception.Message;
			}
			results.Add(item);
		}

		return CreateSummary(results);
	}

	private async Task<AdmissionInquiryLead> ResolveLeadAsync(
		Guid academicYearId,
		ImportAdmissionCrmRteAllotmentRowDto row,
		string operation)
	{
		if (row.LeadId.HasValue && row.LeadId.Value != Guid.Empty)
			return await leadAccessReader.GetAsync(row.LeadId.Value, operation);
		var reference = row.ApplicantReference?.Trim();
		if (string.IsNullOrWhiteSpace(reference))
			throw new BusinessException("AdmissionCrm:RteApplicantReferenceRequired");

		var normalizedContact = new string(reference.Where(char.IsDigit).ToArray());
		var normalizedEmail = reference.ToLowerInvariant();
		var queryable = await leadRepository.GetQueryableAsync();
		var query = queryable.Where(lead =>
			lead.TenantId == CurrentTenant.Id &&
			lead.AcademicYearId == academicYearId &&
			(lead.RegistrationFormNumber == reference ||
			 lead.NormalizedEmail == normalizedEmail ||
			 (normalizedContact.Length >= 7 && lead.NormalizedPrimaryContactNo == normalizedContact)));
		query = await studentScopeAccessService.ApplyCurrentUserAdmissionLeadScopeAsync(query);
		var candidates = await AsyncExecuter.ToListAsync(query);
		if (candidates.Count == 0)
			throw new BusinessException("AdmissionCrm:RteApplicantReferenceNotFound");
		if (candidates.Count > 1)
			throw new BusinessException("AdmissionCrm:RteApplicantReferenceAmbiguous");
		await studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(candidates[0], operation);
		return candidates[0];
	}

	private static AdmissionCrmRteAllotmentImportItemResultDto CreateResult(ImportAdmissionCrmRteAllotmentRowDto row) => new()
	{
		LeadId = row.LeadId,
		ApplicantReference = row.ApplicantReference,
		AllotmentNumber = row.RouteData.AllotmentNumber
	};

	private static AdmissionCrmRteAllotmentImportResultDto CreateSummary(
		IReadOnlyCollection<AdmissionCrmRteAllotmentImportItemResultDto> results) => new()
	{
		Succeeded = results.Count(item => item.Success),
		Failed = results.Count(item => !item.Success),
		Items = results.ToList()
	};
}
