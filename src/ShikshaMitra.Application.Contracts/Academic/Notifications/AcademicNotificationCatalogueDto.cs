using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Notifications;

public sealed record AcademicNotificationCatalogueEventDto(NotificationEventDefinition Definition,
	NotificationSourceAvailability Availability, NotificationSourceAvailability ManualAvailability)
{
	public string? AvailabilityDetail { get; init; }
}
public sealed record AcademicNotificationChannelDto(CommunicationChannelType Channel, bool IsConfigured);
public sealed record AcademicNotificationCatalogueDto(IReadOnlyList<AcademicNotificationCatalogueEventDto> Events,
	IReadOnlyList<AcademicNotificationChannelDto> Channels);
public sealed record InitializeAcademicNotificationDefaultsDto(string EventKey);

public interface IAcademicNotificationCatalogueAppService : IApplicationService
{
	Task<AcademicNotificationCatalogueDto> GetAsync(CancellationToken cancellationToken = default);
}
