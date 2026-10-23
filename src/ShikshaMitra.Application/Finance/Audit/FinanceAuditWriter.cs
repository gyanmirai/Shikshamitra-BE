using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Audit;

public class FinanceAuditWriter(
	IRepository<FinanceAuditEvent, Guid> repository,
	IRepository<IdentityUser, Guid> userRepository,
	ICurrentTenant currentTenant,
	ICurrentUser currentUser,
	IClock clock,
	IGuidGenerator guidGenerator) : ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly string[] DefaultIgnoredDiffFields =
	[
		"id",
		"tenantId",
		"creatorId",
		"creationTime",
		"lastModifierId",
		"lastModificationTime",
		"deleterId",
		"deletionTime",
		"isDeleted"
	];
	private string? actorNameCache;

	public async Task<FinanceAuditWriteResult> WriteAsync(
		Guid? academicYearId,
		string entityType,
		Guid entityId,
		string entityName,
		string action,
		string summary,
		FinanceAuditStudentContext? student = null,
		string? reason = null,
		IReadOnlyList<FinanceAuditChangedFieldDto>? changedFields = null,
		object? context = null,
		string? correlationId = null,
		Guid? sourceAuditLogId = null,
		Guid? eventId = null)
	{
		var subModuleCode = FinanceAuditSubModuleCodes.FromEntityType(entityType);
		var resolvedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
			? $"fc-{entityId:N}"
			: correlationId.Trim();
		var actorName = await ResolveActorNameAsync();
		var auditEventId = eventId ?? guidGenerator.Create();

		var entity = new FinanceAuditEvent(
			auditEventId,
			currentTenant.Id,
			academicYearId,
			subModuleCode,
			entityType,
			entityId,
			entityName,
			action,
			student?.StudentId,
			student?.StudentName,
			student?.GrNo,
			student?.RollNo,
			currentUser.Id,
			actorName,
			null,
			clock.Now,
			summary,
			reason,
			changedFields is { Count: > 0 } ? JsonSerializer.Serialize(changedFields, JsonOptions) : null,
			context is null ? null : JsonSerializer.Serialize(context, JsonOptions),
			resolvedCorrelationId,
			sourceAuditLogId);

		await repository.InsertAsync(entity, autoSave: false);

		return new FinanceAuditWriteResult
		{
			EventId = auditEventId,
			CorrelationId = resolvedCorrelationId
		};
	}

	public static List<FinanceAuditChangedFieldDto> Diff<T>(T before, T after, params string[] ignoredFields)
	{
		var ignored = DefaultIgnoredDiffFields.Concat(ignoredFields).Select(x => x.ToLowerInvariant()).ToHashSet();
		using var beforeDoc = JsonDocument.Parse(JsonSerializer.Serialize(before, JsonOptions));
		using var afterDoc = JsonDocument.Parse(JsonSerializer.Serialize(after, JsonOptions));
		var beforeValues = beforeDoc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetRawText());
		var afterValues = afterDoc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetRawText());
		return beforeValues.Keys.Union(afterValues.Keys)
			.Where(x => !ignored.Contains(x.ToLowerInvariant()))
			.Where(x => !string.Equals(beforeValues.GetValueOrDefault(x), afterValues.GetValueOrDefault(x), StringComparison.Ordinal))
			.Select(x => new FinanceAuditChangedFieldDto
			{
				Field = x,
				OldValue = Trim(beforeValues.GetValueOrDefault(x)),
				NewValue = Trim(afterValues.GetValueOrDefault(x))
			})
			.OrderBy(x => x.Field)
			.ToList();
	}

	private async Task<string?> ResolveActorNameAsync()
	{
		if (actorNameCache != null)
		{
			return actorNameCache;
		}

		if (!currentUser.Id.HasValue)
		{
			actorNameCache = currentUser.UserName;
			return actorNameCache;
		}

		var user = await userRepository.FindAsync(currentUser.Id.Value);
		actorNameCache = user?.Name ?? user?.UserName ?? currentUser.UserName;
		return actorNameCache;
	}

	private static string? Trim(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return value;
		}

		var normalized = value.Trim('"');
		return normalized.Length <= 500 ? normalized : normalized[..500] + "...";
	}
}
