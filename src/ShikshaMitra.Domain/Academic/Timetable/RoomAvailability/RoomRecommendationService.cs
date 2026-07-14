using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public class RoomRecommendationService : DomainService
{
    private readonly IRepository<Room, Guid> _roomRepository;
    private readonly RoomConflictService _conflictService;

    public RoomRecommendationService(
        IRepository<Room, Guid> roomRepository,
        RoomConflictService conflictService)
    {
        _roomRepository = roomRepository;
        _conflictService = conflictService;
    }

    public async Task<List<SuggestedRoom>> SuggestRoomsAsync(
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        int requiredCapacity,
        List<string>? requiredFacilities = null,
        Guid? preferredBuildingId = null,
        Guid? standardId = null)
    {
        var allRooms = await _roomRepository.GetListAsync();
        var suggestions = new List<SuggestedRoom>();

        foreach (var room in allRooms)
        {
            var check = await _conflictService.CheckConflictsAsync(room.Id, date, startTime, endTime, null, null, standardId);
            
            if (check.OverallSeverity == "critical")
            {
                continue;
            }

            int score = 100;
            var badges = new List<string> { "availableFullDuration" };

            // 1. Capacity Alignment Penalty
            if (room.Capacity < requiredCapacity)
            {
                score -= 40;
                badges.Add("warning");
            }
            else
            {
                int excess = room.Capacity - requiredCapacity;
                if (excess == 0)
                {
                    score += 10;
                    badges.Add("capacityMatch");
                }
                else if (excess <= 10)
                {
                    score += 5;
                    badges.Add("capacityMatch");
                }
                else if (excess > 30)
                {
                    score -= 15;
                }
            }

            // 2. Facilities Matching
            if (requiredFacilities != null && requiredFacilities.Any())
            {
                int matched = room.Features?.Intersect(requiredFacilities).Count() ?? 0;
                int totalReq = requiredFacilities.Count;
                if (matched == totalReq)
                {
                    score += 15;
                    badges.Add("facilityMatch");
                }
                else
                {
                    score -= (totalReq - matched) * 15;
                }
            }

            // 3. Location preference
            if (preferredBuildingId.HasValue && room.BuildingId == preferredBuildingId.Value)
            {
                score += 10;
                badges.Add("nearClass");
            }

            score = Math.Clamp(score, 0, 100);

            suggestions.Add(new SuggestedRoom
            {
                RoomId = room.Id,
                RoomName = room.RoomName,
                BuildingId = room.BuildingId,
                FloorId = room.FloorId,
                Capacity = room.Capacity,
                Features = room.Features ?? new List<string>(),
                MatchScore = score,
                NeedsApproval = check.RequiresApproval,
                ConflictStatus = check.OverallSeverity,
                Badges = badges.Distinct().ToList(),
                Reason = score > 85 ? "Best match: fits capacity and required resources perfectly."
                       : score > 60 ? "Partial match: satisfies booking length, but facilities mismatch."
                       : "Lower match: room excess capacity or location mismatch."
            });
        }

        return suggestions.OrderByDescending(s => s.MatchScore).Take(5).ToList();
    }
}
