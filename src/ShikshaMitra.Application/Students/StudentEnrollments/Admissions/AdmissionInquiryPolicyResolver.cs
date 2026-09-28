using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionInquiryPolicyContext(
	AdmissionWorkflowStageDefinition? Definition,
	AdmissionInquiryPolicy Policy,
	string SnapshotJson);

[RemoteService(false)]
public class AdmissionInquiryPolicyResolver(
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionWorkflowStageDefinition, Guid> stages,
	IRepository<AdmissionIntakeProfile, Guid> profiles) : ApplicationService, ITransientDependency
{
	private static readonly JsonSerializerOptions SnapshotOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	public async Task<AdmissionInquiryPolicyContext?> ResolveAsync(Guid? offeringId)
	{
		if (!offeringId.HasValue) return null;
		var offering = await offerings.GetAsync(offeringId.Value);
		if (offering.InquiryIntakeProfileId.HasValue)
		{
			var profile = await profiles.GetAsync(offering.InquiryIntakeProfileId.Value);
			if (profile.Kind != AdmissionIntakeProfileKind.Inquiry)
				throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration);
			try
			{
				var rules = JsonSerializer.Deserialize<AdmissionInquiryIntakeRules>(profile.ConfigurationJson,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
					throw new ArgumentException("Inquiry settings are missing.");
				rules.Validate();
				var profilePolicy = new AdmissionInquiryPolicy
				{
					Version = 1, Enabled = true,
					RequireLinkedInquiry = offering.EntryMode == AdmissionEntryMode.Inquiry,
					ApplicationStartRequirement = rules.ApplicationStartRequirement,
					DuplicateRule = rules.DuplicateRule,
					FirstContactDueInHours = rules.FirstContactDueInHours,
					QualificationDueInDays = rules.QualificationDueInDays,
					FollowUpDueInDays = rules.FollowUpDueInDays,
					MaxOpenFollowUps = rules.MaxOpenFollowUps,
					RequireGuardianEmail = rules.RequireGuardianEmail,
					RequireApplicantDateOfBirth = rules.RequireApplicantDateOfBirth,
					AllowedSources = rules.AllowedSources,
					RequireSourceDetailFor = rules.RequireSourceDetailFor
				};
				profilePolicy.Validate();
				return new AdmissionInquiryPolicyContext(null, profilePolicy, JsonSerializer.Serialize(profilePolicy, SnapshotOptions));
			}
			catch (Exception error) when (error is ArgumentException or JsonException or InvalidOperationException)
			{
				throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration, error.Message);
			}
		}
		if (!offering.WorkflowDefinitionId.HasValue) return null;
		var definitions = await stages.GetListAsync(item =>
			item.WorkflowDefinitionId == offering.WorkflowDefinitionId.Value &&
			item.StageType == AdmissionWorkflowStageType.Inquiry);
		var managed = definitions.Select(definition => (Definition: definition, Policy: Read(definition)))
			.Where(item => item.Policy != null).ToList();
		if (managed.Count == 0) return null;
		if (managed.Count != 1 || !managed[0].Definition.IsEntryStage)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidPolicy,
				"A managed Inquiry must be the single configured entry Inquiry stage.");
		var policy = managed[0].Policy!;
		return new AdmissionInquiryPolicyContext(managed[0].Definition, policy,
			JsonSerializer.Serialize(policy, SnapshotOptions));
	}

	public static AdmissionInquiryPolicy? Read(AdmissionWorkflowStageDefinition definition)
	{
		try { return AdmissionInquiryPolicy.Read(definition.ConfigurationJson); }
		catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
		{
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidPolicy, exception.Message);
		}
	}

	public static AdmissionInquiryPolicy ReadSnapshot(string snapshotJson)
	{
		try
		{
			var policy = JsonSerializer.Deserialize<AdmissionInquiryPolicy>(snapshotJson,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
				throw new ArgumentException("Inquiry policy snapshot is missing.");
			policy.Validate();
			return policy;
		}
		catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
		{
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidPolicy, exception.Message);
		}
	}
}
