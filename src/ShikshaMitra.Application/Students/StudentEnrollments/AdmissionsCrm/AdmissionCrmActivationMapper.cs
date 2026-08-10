using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmActivationMapper(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmIdentifierAllocation, Guid> identifierRepository,
	IRepository<AdmissionCrmServiceHandoff, Guid> handoffRepository,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<AdmissionCrmActivationDto> ActivationAsync(AdmissionCrmActivation x)
	{
		var lead = await leadRepository.GetAsync(x.LeadId);
		await scopeAccess.EnsureCanAccessAdmissionLeadAsync(lead, "MapActivationAsync");
		var identifierQuery = await identifierRepository.GetQueryableAsync();
		var identifiers = await AsyncExecuter.ToListAsync(identifierQuery.Where(y =>
			y.TenantId == CurrentTenant.Id && y.ActivationId == x.Id));
		var handoffQuery = await handoffRepository.GetQueryableAsync();
		var handoffs = await AsyncExecuter.ToListAsync(handoffQuery.Where(y =>
			y.TenantId == CurrentTenant.Id && y.ActivationId == x.Id));
		return new AdmissionCrmActivationDto
		{
			Id = x.Id, AcademicYearId = x.AcademicYearId, LeadId = x.LeadId, StudentId = x.StudentId,
			ActivationReference = x.ActivationReference, Status = x.Status, EducationBoardId = x.EducationBoardId,
			AcademicMediumId = x.AcademicMediumId, AcademicMediumStageId = x.AcademicMediumStageId,
			StandardId = x.StandardId, DivisionId = x.DivisionId, GrNumberSeriesId = x.GrNumberSeriesId,
			RollNumber = x.RollNumber, Gender = x.Gender, RequireStudentAccount = x.RequireStudentAccount,
			RequireGuardianAccount = x.RequireGuardianAccount,
			Steps = Deserialize<IReadOnlyList<AdmissionCrmActivationStepDto>>(x.StepsJson) ?? [],
			Identifiers = identifiers.Select(Identifier).ToList(), AttemptCount = x.AttemptCount,
			RepairStep = x.RepairStep, RepairReason = x.RepairReason, FirstDayReadyAt = x.FirstDayReadyAt,
			CancellationRequestedByUserId = x.CancellationRequestedByUserId,
			CancellationRequestedAt = x.CancellationRequestedAt, CancellationReason = x.CancellationReason,
			CancelledByUserId = x.CancelledByUserId, CancelledAt = x.CancelledAt,
			ServiceHandoffs = handoffs.Select(ServiceHandoff).ToList()
		};
	}

	public static AdmissionCrmNumberSeriesDto Series(AdmissionCrmNumberSeries x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, Code = x.Code, Name = x.Name,
		IdentifierType = x.IdentifierType, EducationBoardId = x.EducationBoardId,
		AcademicMediumId = x.AcademicMediumId, StandardId = x.StandardId, Prefix = x.Prefix,
		Suffix = x.Suffix, Padding = x.Padding, StartingSequence = x.StartingSequence,
		NextSequence = x.NextSequence, MaximumSequence = x.MaximumSequence,
		EffectiveFrom = x.EffectiveFrom, EffectiveUntil = x.EffectiveUntil, Status = x.Status,
		NextPreview = $"{x.Prefix}{x.NextSequence.ToString().PadLeft(x.Padding, '0')}{x.Suffix}",
		ConcurrencyStamp = x.ConcurrencyStamp
	};

	public static AdmissionCrmIdentifierAllocationDto Identifier(AdmissionCrmIdentifierAllocation x) => new()
	{
		Id = x.Id, IdentifierType = x.IdentifierType, Value = x.Value, Sequence = x.Sequence,
		Status = x.Status, AllocatedAt = x.AllocatedAt, CommittedAt = x.CommittedAt,
		IsManual = x.IsManual, AssignedByUserId = x.AssignedByUserId, LifecycleReason = x.LifecycleReason
	};

	public static AdmissionCrmServiceHandoffDto ServiceHandoff(AdmissionCrmServiceHandoff x) => new()
	{
		Id = x.Id, ActivationId = x.ActivationId, LeadId = x.LeadId, StudentId = x.StudentId,
		Type = x.Type, DestinationModule = x.DestinationModule, ConsentConfirmed = x.ConsentConfirmed,
		Payload = Deserialize<Dictionary<string, string?>>(x.PayloadJson) ?? new(), Status = x.Status,
		AttemptCount = x.AttemptCount, RequestedAt = x.RequestedAt, DispatchedAt = x.DispatchedAt,
		AcknowledgedAt = x.AcknowledgedAt, ExternalReference = x.ExternalReference, LastError = x.LastError
	};

	public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
	public static T? Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions);
	public static void UpsertStep(List<AdmissionCrmActivationStepDto> steps, string key,
		AdmissionCrmActivationStepStatus status, string detail)
	{
		steps.RemoveAll(x => x.Key == key);
		steps.Add(new AdmissionCrmActivationStepDto { Key = key, Status = status, Detail = detail, UpdatedAt = DateTime.UtcNow });
	}
}
