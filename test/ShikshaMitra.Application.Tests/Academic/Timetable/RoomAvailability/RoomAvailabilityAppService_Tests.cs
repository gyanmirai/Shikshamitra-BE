using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public class RoomAvailabilityAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly IRoomAvailabilityAppService _roomAvailabilityAppService;
    private readonly IRepository<Room, Guid> _roomRepository;
    private readonly IRepository<RoomReservation, Guid> _reservationRepository;
    private readonly IRepository<TenantSettings.RoomsManagement.Buildings.Building, Guid> _buildingRepository;
    private readonly IRepository<TenantSettings.RoomsManagement.Buildings.Floor, Guid> _floorRepository;
    private readonly IRepository<TenantSettings.RoomsManagement.RoomTypes.RoomType, Guid> _roomTypeRepository;
    private readonly IRepository<UserScopeAssignment, Guid> _userScopeAssignmentRepository;
    private readonly IUserScopeService _userScopeService;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    public RoomAvailabilityAppService_Tests()
    {
        _roomAvailabilityAppService = GetRequiredService<IRoomAvailabilityAppService>();
        _roomRepository = GetRequiredService<IRepository<Room, Guid>>();
        _reservationRepository = GetRequiredService<IRepository<RoomReservation, Guid>>();
        _buildingRepository = GetRequiredService<IRepository<TenantSettings.RoomsManagement.Buildings.Building, Guid>>();
        _floorRepository = GetRequiredService<IRepository<TenantSettings.RoomsManagement.Buildings.Floor, Guid>>();
        _roomTypeRepository = GetRequiredService<IRepository<TenantSettings.RoomsManagement.RoomTypes.RoomType, Guid>>();
        _userScopeAssignmentRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
        _userScopeService = GetRequiredService<IUserScopeService>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task ApproveReservation_Should_Throw_AuthorizationException_For_Unauthorized_Role()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (room, reservation) = await SeedRoomAndReservationAsync(userId);

        // Act & Assert
        using (_currentPrincipalAccessor.Change(BuildPrincipal(userId, "teacher")))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(async () =>
            {
                await _roomAvailabilityAppService.ApproveReservationAsync(reservation.Id);
            });
        }
    }

    [Fact]
    public async Task RejectReservation_Should_Throw_AuthorizationException_For_Unauthorized_Role()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (room, reservation) = await SeedRoomAndReservationAsync(userId);

        // Act & Assert
        using (_currentPrincipalAccessor.Change(BuildPrincipal(userId, "teacher")))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(async () =>
            {
                await _roomAvailabilityAppService.RejectReservationAsync(reservation.Id, "Reason for rejection");
            });
        }
    }

    [Fact]
    public async Task ApproveReservation_Should_Throw_UserFriendlyException_For_Self_Approval()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (room, reservation) = await SeedRoomAndReservationAsync(userId);

        // Act & Assert (Admin role but they created the reservation)
        using (_currentPrincipalAccessor.Change(BuildPrincipal(userId, "admin")))
        {
            await Should.ThrowAsync<UserFriendlyException>(async () =>
            {
                await _roomAvailabilityAppService.ApproveReservationAsync(reservation.Id);
            });
        }
    }

    [Fact]
    public async Task CreateReservation_Should_AutoApprove_For_Admin_Role()
    {
        // Arrange
        var creatorUserId = Guid.NewGuid();
        var (room, _) = await SeedRoomAndReservationAsync(creatorUserId);
        var adminUserId = Guid.NewGuid();

        using (_currentPrincipalAccessor.Change(BuildPrincipal(adminUserId, "admin")))
        {
            // Act
            var created = await _roomAvailabilityAppService.CreateReservationAsync(new CreateRoomReservationDto
            {
                RoomId = room.Id,
                Date = DateTime.UtcNow.Date,
                StartTime = new TimeSpan(12, 0, 0),
                EndTime = new TimeSpan(13, 0, 0),
                Purpose = "Yoga session for all",
                ReservationType = RoomReservationType.Event
            });

            // Assert
            created.Status.ShouldBe(RoomReservationStatus.Approved);
            created.CreatorId.ShouldBe(adminUserId);
        }
    }

    [Fact]
    public async Task CreateReservation_Should_Allow_InstituteWide_Booking_For_Scoped_Hod()
    {
        var seedUserId = Guid.NewGuid();
        var (room, _) = await SeedRoomAndReservationAsync(seedUserId);
        var hodUserId = Guid.NewGuid();

        await _userScopeAssignmentRepository.InsertAsync(
            new UserScopeAssignment(
                Guid.NewGuid(),
                null,
                userId: hodUserId,
                description: "Organization-wide room reservation test"),
            autoSave: true);
        await _userScopeService.InvalidateUserScopeCacheAsync(hodUserId);

        using (_currentPrincipalAccessor.Change(BuildPrincipal(hodUserId, "HOD")))
        {
            var created = await _roomAvailabilityAppService.CreateReservationAsync(new CreateRoomReservationDto
            {
                RoomId = room.Id,
                Date = DateTime.UtcNow.Date,
                StartTime = new TimeSpan(12, 0, 0),
                EndTime = new TimeSpan(13, 0, 0),
                Purpose = "Yoga session",
                ReservationType = RoomReservationType.Class
            });

            created.Status.ShouldBe(RoomReservationStatus.PendingApproval);

            var visibleReservations = await _roomAvailabilityAppService.GetReservationsAsync(
                new GetReservationsInputDto());
            visibleReservations.ShouldContain(item => item.Id == created.Id);
        }
    }

    [Fact]
    public async Task RejectReservation_Should_Throw_UserFriendlyException_For_Self_Rejection()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (room, reservation) = await SeedRoomAndReservationAsync(userId);

        // Act & Assert (Admin role but they created the reservation)
        using (_currentPrincipalAccessor.Change(BuildPrincipal(userId, "admin")))
        {
            await Should.ThrowAsync<UserFriendlyException>(async () =>
            {
                await _roomAvailabilityAppService.RejectReservationAsync(reservation.Id, "Reason for rejection");
            });
        }
    }

    [Fact]
    public async Task ApproveReservation_Should_Succeed_For_Authorized_Non_Creator()
    {
        // Arrange
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();
        var (room, reservation) = await SeedRoomAndReservationAsync(creatorUserId);

        // Act
        using (_currentPrincipalAccessor.Change(BuildPrincipal(approverUserId, "admin")))
        {
            await _roomAvailabilityAppService.ApproveReservationAsync(reservation.Id);
        }

        // Assert
        var updated = await _reservationRepository.GetAsync(reservation.Id);
        updated.Status.ShouldBe(RoomReservationStatus.Approved);
        updated.ApprovedByUserId.ShouldBe(approverUserId);
    }

    [Fact]
    public async Task RejectReservation_Should_Succeed_For_Authorized_Non_Creator()
    {
        // Arrange
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();
        var (room, reservation) = await SeedRoomAndReservationAsync(creatorUserId);

        // Act
        using (_currentPrincipalAccessor.Change(BuildPrincipal(approverUserId, "HOD")))
        {
            await _roomAvailabilityAppService.RejectReservationAsync(reservation.Id, "Conflict resolved elsewhere");
        }

        // Assert
        var updated = await _reservationRepository.GetAsync(reservation.Id);
        updated.Status.ShouldBe(RoomReservationStatus.Rejected);
        updated.RejectionReason.ShouldBe("Conflict resolved elsewhere");
        updated.ApprovedByUserId.ShouldBe(approverUserId);
    }

    private async Task<(Room room, RoomReservation reservation)> SeedRoomAndReservationAsync(Guid creatorUserId)
    {
        // Build mock principal to set CreatorId during InsertAsync
        using (_currentPrincipalAccessor.Change(BuildPrincipal(creatorUserId, "teacher")))
        {
            var building = new TenantSettings.RoomsManagement.Buildings.Building
            {
                BuildingName = "Test Building",
                TotalFloors = 1
            };
            SetEntityId(building, Guid.NewGuid());
            await _buildingRepository.InsertAsync(building, autoSave: true);

            var floor = new TenantSettings.RoomsManagement.Buildings.Floor
            {
                BuildingId = building.Id,
                FloorName = "1st Floor",
                FloorNo = 1
            };
            SetEntityId(floor, Guid.NewGuid());
            await _floorRepository.InsertAsync(floor, autoSave: true);

            var roomType = new TenantSettings.RoomsManagement.RoomTypes.RoomType
            {
                RoomTypeName = "Classroom",
                ShortName = "CLS"
            };
            SetEntityId(roomType, Guid.NewGuid());
            await _roomTypeRepository.InsertAsync(roomType, autoSave: true);

            var room = new Room
            {
                RoomName = "Test Room D101",
                RoomTypeId = roomType.Id,
                BuildingId = building.Id,
                FloorId = floor.Id,
                Capacity = 30
            };
            SetEntityId(room, Guid.NewGuid());
            await _roomRepository.InsertAsync(room, autoSave: true);

            var reservation = new RoomReservation(
                Guid.NewGuid(),
                null,
                room.Id,
                DateTime.UtcNow.Date,
                new TimeSpan(10, 0, 0),
                new TimeSpan(11, 0, 0),
                "Class Presentation",
                RoomReservationType.Class
            );
            await _reservationRepository.InsertAsync(reservation, autoSave: true);

            return (room, reservation);
        }
    }

    private static void SetEntityId<TEntity>(TEntity entity, Guid id)
        where TEntity : Entity<Guid>
    {
        typeof(TEntity).GetProperty(nameof(Entity<Guid>.Id))?.SetValue(entity, id);
    }

    private ClaimsPrincipal BuildPrincipal(Guid userId, string roleName)
    {
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, userId.ToString()),
            new(AbpClaimTypes.UserName, $"user-{userId:N}"),
            new(AbpClaimTypes.Email, $"user-{userId:N}@test.local"),
            new(AbpClaimTypes.Role, roleName)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Fact]
    public async Task SuggestRooms_Should_Respect_Pagination_And_Limits()
    {
        // Arrange: Seed 10 rooms
        var userId = Guid.NewGuid();
        var building = new TenantSettings.RoomsManagement.Buildings.Building
        {
            BuildingName = "Suggestion Building",
            TotalFloors = 1
        };
        SetEntityId(building, Guid.NewGuid());
        await _buildingRepository.InsertAsync(building, autoSave: true);

        var floor = new TenantSettings.RoomsManagement.Buildings.Floor
        {
            BuildingId = building.Id,
            FloorName = "1st Floor",
            FloorNo = 1
        };
        SetEntityId(floor, Guid.NewGuid());
        await _floorRepository.InsertAsync(floor, autoSave: true);

        var roomType = new TenantSettings.RoomsManagement.RoomTypes.RoomType
        {
            RoomTypeName = "Classroom",
            ShortName = "CLS"
        };
        SetEntityId(roomType, Guid.NewGuid());
        await _roomTypeRepository.InsertAsync(roomType, autoSave: true);

        for (int i = 1; i <= 10; i++)
        {
            var room = new Room
            {
                RoomName = $"Room-{i}",
                RoomTypeId = roomType.Id,
                BuildingId = building.Id,
                FloorId = floor.Id,
                Capacity = 30 + i
            };
            SetEntityId(room, Guid.NewGuid());
            await _roomRepository.InsertAsync(room, autoSave: true);
        }

        // Act & Assert 1: Default call should return all (up to 50)
        var resultAll = await _roomAvailabilityAppService.SuggestRoomsAsync(new SuggestedRoomRequestDto
        {
            Date = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            RequiredCapacity = 20
        });
        resultAll.Count.ShouldBeGreaterThanOrEqualTo(10);

        // Act & Assert 2: Limit parameter = 3
        var resultLimited = await _roomAvailabilityAppService.SuggestRoomsAsync(new SuggestedRoomRequestDto
        {
            Date = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            RequiredCapacity = 20,
            Limit = 3
        });
        resultLimited.Count.ShouldBe(3);

        // Act & Assert 3: Pagination with Page = 2, Limit = 4
        var page1 = await _roomAvailabilityAppService.SuggestRoomsAsync(new SuggestedRoomRequestDto
        {
            Date = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            RequiredCapacity = 20,
            Page = 1,
            Limit = 4
        });
        var page2 = await _roomAvailabilityAppService.SuggestRoomsAsync(new SuggestedRoomRequestDto
        {
            Date = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            RequiredCapacity = 20,
            Page = 2,
            Limit = 4
        });

        page1.Count.ShouldBe(4);
        page2.Count.ShouldBe(4);
        page1[0].RoomId.ShouldNotBe(page2[0].RoomId);
    }
}
