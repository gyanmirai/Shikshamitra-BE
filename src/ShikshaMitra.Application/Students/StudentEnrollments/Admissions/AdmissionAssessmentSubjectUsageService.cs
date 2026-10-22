using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Protect live workflow references; historical attempts keep their own subject snapshots.</summary>
[RemoteService(false)]
public class AdmissionAssessmentSubjectUsageService(IRepository<AdmissionWorkflowStageDefinition, Guid> stages,
	IRepository<AdmissionWorkflowDefinition, Guid> workflows) : ApplicationService, ITransientDependency
{
	public async Task<Dictionary<Guid, int>> CountAsync(IReadOnlyCollection<Guid> subjectIds)
	{
		var counts = subjectIds.Distinct().ToDictionary(id => id, _ => 0);
		if (counts.Count == 0) return counts;
		var workflowQuery = await workflows.GetQueryableAsync();
		var configurations = await AsyncExecuter.ToListAsync((await stages.GetQueryableAsync())
			.Where(stage => stage.StageType == AdmissionWorkflowStageType.Assessment &&
				workflowQuery.Any(workflow => workflow.Id == stage.WorkflowDefinitionId && workflow.TenantId == CurrentTenant.Id && workflow.Status != AdmissionWorkflowStatus.Archived))
			.Select(stage => stage.ConfigurationJson));
		foreach (var configuration in configurations)
			foreach (var id in ReadSubjectIds(configuration).Where(counts.ContainsKey)) counts[id]++;
		return counts;
	}

	internal static HashSet<Guid> ReadSubjectIds(string? configuration)
	{
		var ids = new HashSet<Guid>();
		if (string.IsNullOrWhiteSpace(configuration)) return ids;
		try
		{
			using var document = JsonDocument.Parse(configuration);
			if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("assessment", out var assessment) ||
				assessment.ValueKind != JsonValueKind.Object || !TryGetPolicyProperty(assessment, "classProfiles", out var profiles) || profiles.ValueKind != JsonValueKind.Array) return ids;
			foreach (var profile in profiles.EnumerateArray())
			{
				if (profile.ValueKind != JsonValueKind.Object || !TryGetPolicyProperty(profile, "subjects", out var subjects) || subjects.ValueKind != JsonValueKind.Array) continue;
				foreach (var subject in subjects.EnumerateArray())
					if (subject.ValueKind == JsonValueKind.Object && TryGetPolicyProperty(subject, "subjectId", out var id) && id.ValueKind == JsonValueKind.String && id.TryGetGuid(out var value)) ids.Add(value);
			}
		}
		catch (JsonException) { /* Invalid drafts cannot be published and contain no valid reference graph. */ }
		return ids;
	}

	private static bool TryGetPolicyProperty(JsonElement element, string name, out JsonElement value)
	{
		value = default;
		var found = false;
		// Match the case-insensitive policy deserializer, including its last-property-wins behavior.
		foreach (var property in element.EnumerateObject())
			if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; found = true; }
		return found;
	}
}
