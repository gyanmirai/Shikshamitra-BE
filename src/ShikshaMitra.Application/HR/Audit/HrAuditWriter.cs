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

namespace ShikshaMitra.HR.Audit;

public class HrAuditWriter(
	IRepository<HrAuditEvent, Guid> repository,
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

	public async Task WriteAsync(
		Guid? academicYearId,
		string entityType,
		Guid entityId,
		string entityName,
		string action,
		string summary,
		string? reason = null,
		IReadOnlyList<HrAuditChangedFieldDto>? changedFields = null,
		object? context = null,
		string? correlationId = null,
		Guid? sourceAuditLogId = null)
	{
		var actorName = await ResolveActorNameAsync();
		var entity = new HrAuditEvent(
			guidGenerator.Create(),
			currentTenant.Id,
			academicYearId,
			entityType,
			entityId,
			entityName,
			action,
			currentUser.Id,
			actorName,
			clock.Now,
			summary,
			reason,
			changedFields is { Count: > 0 } ? JsonSerializer.Serialize(changedFields, JsonOptions) : null,
			context is null ? null : JsonSerializer.Serialize(context, JsonOptions),
			correlationId,
			sourceAuditLogId);

		await repository.InsertAsync(entity, autoSave: false);
	}

	public static List<HrAuditChangedFieldDto> Diff<T>(T before, T after, params string[] ignoredFields)
	{
		var ignored = DefaultIgnoredDiffFields.Concat(ignoredFields).Select(x => x.ToLowerInvariant()).ToHashSet();
		using var beforeDoc = JsonDocument.Parse(JsonSerializer.Serialize(before, JsonOptions));
		using var afterDoc = JsonDocument.Parse(JsonSerializer.Serialize(after, JsonOptions));
		var beforeValues = beforeDoc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetRawText());
		var afterValues = afterDoc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetRawText());
		return beforeValues.Keys.Union(afterValues.Keys)
			.Where(x => !ignored.Contains(x.ToLowerInvariant()))
			.Where(x => !string.Equals(beforeValues.GetValueOrDefault(x), afterValues.GetValueOrDefault(x), StringComparison.Ordinal))
			.Select(x => new HrAuditChangedFieldDto
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
