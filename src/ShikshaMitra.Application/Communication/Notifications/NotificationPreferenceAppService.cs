using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Validation;

namespace ShikshaMitra.Communication.Notifications;

[Authorize, RemoteService(false), DisableAuditing]
public class NotificationPreferenceAppService(NotificationSelfIdentity identity, NotificationPersonalStudentAccess students,
	IRepository<NotificationPreference, Guid> preferences, IEnumerable<INotificationEventDefinitionProvider> schemas,
	INotificationPersonalWriteLock writeLock)
	: ApplicationService, INotificationPreferenceAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private sealed record Configuration(IReadOnlyList<CommunicationChannelType> EnabledChannels,
		bool QuietHoursEnabled, string? QuietHoursStart, string? QuietHoursEnd);

	public virtual async Task<IReadOnlyList<NotificationPreferenceDto>> GetListAsync(CancellationToken cancellationToken = default)
	{
		var owner = await identity.RequireAsync(cancellationToken);
		var linked = await students.GetStudentIdsQueryAsync(owner);
		var rows = await AsyncExecuter.ToListAsync((await preferences.GetQueryableAsync()).Where(x => x.TenantId == owner.TenantId &&
			x.UserId == owner.UserId && !x.IsDeleted && (!x.StudentId.HasValue || linked.Contains(x.StudentId.Value)))
			.OrderBy(x => x.Category).ThenBy(x => x.StudentId), cancellationToken);
		return rows.Select(Map).ToArray();
	}

	[UnitOfWork(true)]
	public virtual async Task<NotificationPreferenceDto> SaveAsync(SaveNotificationPreferenceDto input, CancellationToken cancellationToken = default)
	{
		var owner = await identity.RequireAsync(cancellationToken);
		var validation = await new NotificationPreferenceValidator().ValidateAsync(input, cancellationToken);
		if (!validation.IsValid) throw new AbpValidationException("Invalid notification preferences.",
			validation.Errors.Select(x => new ValidationResult(x.ErrorMessage, [x.PropertyName])).ToList());
		if (!schemas.SelectMany(x => x.GetAll()).Any(x => x.EventKey.Split('.')[0] + "." + x.Category == input.Category))
			throw new BusinessException(NotificationErrorCodes.EventUnknown);
		if (input.StudentId is Guid studentId) await students.RequireAsync(owner, studentId, cancellationToken);
		var context = input.StudentId.HasValue ? $"student:{input.StudentId.Value:N}" : "user";
		await writeLock.AcquireAsync([$"preference:{owner.UserId:N}:{context}:{input.Category}"], cancellationToken);
		var row = await AsyncExecuter.FirstOrDefaultAsync((await preferences.GetQueryableAsync()).Where(x =>
			x.TenantId == owner.TenantId && x.UserId == owner.UserId && x.ContextKey == context && x.Category == input.Category && !x.IsDeleted), cancellationToken);
		if (row == null ? !string.IsNullOrEmpty(input.ConcurrencyStamp) : row.ConcurrencyStamp != input.ConcurrencyStamp)
			throw new AbpDbConcurrencyException();
		var configuration = new Configuration(input.EnabledChannels.Distinct().ToArray(), input.QuietHoursEnabled,
			input.QuietHoursEnabled ? input.QuietHoursStart : null, input.QuietHoursEnabled ? input.QuietHoursEnd : null);
		var isNew = row == null;
		row ??= new(GuidGenerator.Create(), owner.TenantId, owner.UserId, input.Category, input.StudentId);
		row.Configure(JsonSerializer.Serialize(configuration, JsonOptions), input.LanguageCode);
		if (isNew) await preferences.InsertAsync(row, autoSave: true, cancellationToken: cancellationToken);
		else await preferences.UpdateAsync(row, autoSave: true, cancellationToken: cancellationToken);
		return Map(row);
	}

	private static NotificationPreferenceDto Map(NotificationPreference row)
	{
		var configuration = JsonSerializer.Deserialize<Configuration>(row.ConfigurationJson, JsonOptions);
		return new(row.Category, row.StudentId, configuration?.EnabledChannels ?? [], row.LanguageCode,
			configuration?.QuietHoursEnabled ?? false, configuration?.QuietHoursStart, configuration?.QuietHoursEnd, row.ConcurrencyStamp);
	}
}
