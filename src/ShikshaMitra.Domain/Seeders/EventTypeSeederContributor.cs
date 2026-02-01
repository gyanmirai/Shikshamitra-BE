using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.EventTypes;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds baseline calendar event types required by the UI.
/// </summary>
public class EventTypeSeederContributor : IDataSeedContributor, ITransientDependency
{
    private static readonly Guid MeetingDateEventTypeId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    private readonly ICurrentTenant _currentTenant;
    private readonly IEventTypeRepository _eventTypeRepository;
    private readonly IDataFilter _dataFilter;

    public EventTypeSeederContributor(
        ICurrentTenant currentTenant,
        IEventTypeRepository eventTypeRepository,
        IDataFilter dataFilter)
    {
        _currentTenant = currentTenant;
        _eventTypeRepository = eventTypeRepository;
        _dataFilter = dataFilter;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // Disable tenant filter to check if record exists globally (PK is unique across all tenants)
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var existingType = await _eventTypeRepository.FindAsync(MeetingDateEventTypeId);
            if (existingType != null)
            {
                return;
            }
        }

        var meetingEventType = new EventType(
            MeetingDateEventTypeId,
            _currentTenant.Id,
            "Meeting Date",
            "#1E88E5",
            "Default meeting-date event type");

        await _eventTypeRepository.InsertAsync(meetingEventType, autoSave: true);
    }
}
