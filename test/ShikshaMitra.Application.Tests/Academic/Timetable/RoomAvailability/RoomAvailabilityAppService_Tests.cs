using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
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
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<IdentityRole, Guid> _roleRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
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
        _employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
        _roleRepository = GetRequiredService<IRepository<IdentityRole, Guid>>();
        _identityUserRepository = GetRequiredService<IRepository<IdentityUser, Guid>>();
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

    [Fact]
    public async Task Reporting_Manager_Should_Receive_Review_Actions_For_A_Direct_Report_Request()
    {
        var requesterUserId = Guid.NewGuid();
        var reportingManagerUserId = Guid.NewGuid();
        var (_, reservation) = await SeedRoomAndReservationAsync(requesterUserId);
        await SeedReportingRelationshipAsync(reportingManagerUserId, requesterUserId);

        await _userScopeAssignmentRepository.InsertAsync(
            new UserScopeAssignment(
                Guid.NewGuid(),
                null,
                userId: reportingManagerUserId,
                description: "Room reservation reporting manager test"),
            autoSave: true);
        await _userScopeService.InvalidateUserScopeCacheAsync(reportingManagerUserId);

        using (_currentPrincipalAccessor.Change(BuildPrincipal(reportingManagerUserId, "teacher")))
        {
            var queue = await _roomAvailabilityAppService.GetReservationsAsync(
                new GetReservationsInputDto { IsApprovalView = true });

            var queuedReservation = queue.Single(item => item.Id == reservation.Id);
            queuedReservation.CanReview.ShouldBeTrue();

            await _roomAvailabilityAppService.ApproveReservationAsync(reservation.Id);
        }

        var updated = await _reservationRepository.GetAsync(reservation.Id);
        updated.Status.ShouldBe(RoomReservationStatus.Approved);
        updated.ApprovedByUserId.ShouldBe(reportingManagerUserId);
    }

    private async Task SeedReportingRelationshipAsync(Guid managerUserId, Guid requesterUserId)
    {
        var manager = await CreateEmployeeAsync(managerUserId, "Manager");
        var requester = await CreateEmployeeAsync(requesterUserId, "Requester");
        requester.AssignReportingManager(manager.Id);
        await _employeeRepository.UpdateAsync(requester, autoSave: true);
    }

    private async Task<Employee> CreateEmployeeAsync(Guid userId, string firstName)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var roleId = Guid.NewGuid();
        await _identityUserRepository.InsertAsync(
            new IdentityUser(
                userId,
                $"room-review-{suffix[..8]}",
                $"{suffix[..12]}@test.local",
                tenantId: null),
            autoSave: true);
        await _roleRepository.InsertAsync(
            new IdentityRole(roleId, $"room-review-{suffix[..8]}", tenantId: null),
            autoSave: true);

        var employee = new Employee(
            Guid.NewGuid(),
            tenantId: null,
            userId: userId,
            employeeCode: $"ROOM{suffix[..10]}",
            basicInfo: new EmployeeBasicInfo(
                ProfilePictureBlobName: null,
                LastName: "Test",
                FirstName: firstName,
                MiddleName: null,
                ShortName: firstName,
                EmployeeStatusType: EmployeeStatusType.Active,
                DateOfBirth: new DateOnly(1990, 1, 1),
                FamilyCast: null,
                CastType: null,
                GenderType: null,
                Religion: null,
                MaritalStatus: null,
                BloodGroup: null,
                MobileNo: null,
                SecondaryMobileNo: null,
                Email: $"{suffix[..12]}@test.local",
                PanNo: $"ABCDE{suffix[..4].ToUpperInvariant()}F",
                AdharNo: $"1{Math.Abs((long)suffix.GetHashCode()):D11}"[..12],
                Address1: null,
                Address2: null,
                Address3: null,
                Department: "Academics",
                Designation: "Teacher",
                DateOfJoining: new DateOnly(2025, 1, 1),
                EmployeeGroup: null,
                Qualification: null,
                RoleId: roleId,
                IdentificationMark1: null,
                City: null,
                State: null,
                Country: null,
                PinCode: null,
                ShiftId: null),
            bankDetails: new BankDetails(null, null, null, null, null),
            workExperiences: null);

        return await _employeeRepository.InsertAsync(employee, autoSave: true);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Batched_Room_Checks_Should_Match_Individual_Checks(bool blockedByCalendar)
    {
        var (reservedRoom, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
        var (maintenanceRoom, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
        var (freeRoom, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
        var date = new DateTime(2030, 1, 7);
        var conflictService = GetRequiredService<RoomConflictService>();
        var maintenanceRepository = GetRequiredService<IRepository<RoomMaintenanceBlock, Guid>>();
        var calendarRepository = GetRequiredService<IRepository<ShikshaMitra.Academic.Calendar.AcademicCalendar, Guid>>();
        await WithUnitOfWorkAsync(async () =>
        {
            await _reservationRepository.InsertAsync(new RoomReservation(Guid.NewGuid(), null,
                reservedRoom.Id, date, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0),
                "Reserved for class", RoomReservationType.Class));
            await maintenanceRepository.InsertAsync(new RoomMaintenanceBlock(Guid.NewGuid(), null,
                maintenanceRoom.Id, "Repair", "Electrical repair", MaintenanceSeverity.High,
                date.AddHours(8), date.AddHours(12)));
            if (blockedByCalendar)
            {
                var calendar = new ShikshaMitra.Academic.Calendar.AcademicCalendar(Guid.NewGuid(), null,
                    Guid.NewGuid(), null, "Room check holiday", date, date.AddDays(7));
                calendar.Publish(Guid.NewGuid());
                calendar.Days.Add(new ShikshaMitra.Academic.Calendar.AcademicCalendarDay(Guid.NewGuid(), null,
                    calendar.Id, date, ShikshaMitra.Academic.Calendar.DayType.Holiday, "Holiday"));
                calendar.Days.Add(new ShikshaMitra.Academic.Calendar.AcademicCalendarDay(Guid.NewGuid(), null,
                    calendar.Id, date.AddDays(1), ShikshaMitra.Academic.Calendar.DayType.Holiday, "Holiday"));
                await calendarRepository.InsertAsync(calendar);
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var rooms = new[] { reservedRoom, maintenanceRoom, freeRoom };
            var batch = await conflictService.CheckRoomsAsync(rooms, date, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0));
            foreach (var room in rooms)
            {
                var individual = await conflictService.CheckConflictsAsync(room.Id, date,
                    new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0));
                System.Text.Json.JsonSerializer.Serialize(batch[room.Id])
                    .ShouldBe(System.Text.Json.JsonSerializer.Serialize(individual));
            }
            batch[reservedRoom.Id].CanSave.ShouldBeFalse();
            batch[maintenanceRoom.Id].CanSave.ShouldBeFalse();
            batch[freeRoom.Id].CanSave.ShouldBe(!blockedByCalendar);
        });
    }

    [Fact]
    public async Task Batched_Room_Periods_Should_Match_Individual_Checks()
    {
        var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
        var date = new DateTime(2030, 1, 7);
        var maintenanceRepository = GetRequiredService<IRepository<RoomMaintenanceBlock, Guid>>();
        await WithUnitOfWorkAsync(async () =>
        {
            await _reservationRepository.InsertAsync(new RoomReservation(Guid.NewGuid(), null,
                room.Id, date, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0),
                "Reserved for class", RoomReservationType.Class));
            await maintenanceRepository.InsertAsync(new RoomMaintenanceBlock(Guid.NewGuid(), null,
                room.Id, "Repair", "Electrical repair", MaintenanceSeverity.High,
                date.AddHours(12), date.AddHours(13)));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var periods = new List<(TimeSpan Start, TimeSpan End)>
            {
                (new TimeSpan(8, 0, 0), new TimeSpan(9, 0, 0)),
                (new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0)),
                (new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0)),
                (new TimeSpan(13, 0, 0), new TimeSpan(14, 0, 0))
            };
            var conflictService = GetRequiredService<RoomConflictService>();
            var batch = await conflictService.CheckRoomPeriodsAsync(room, date, periods);

            for (var index = 0; index < periods.Count; index++)
            {
                var period = periods[index];
                var individual = await conflictService.CheckConflictsAsync(room.Id, date,
                    period.Start, period.End);
                System.Text.Json.JsonSerializer.Serialize(batch[index])
                    .ShouldBe(System.Text.Json.JsonSerializer.Serialize(individual));
            }

            batch[0].CanSave.ShouldBeTrue();
            batch[1].CanSave.ShouldBeFalse();
            batch[2].CanSave.ShouldBeFalse();
            batch[3].CanSave.ShouldBeTrue();
        });
    }

    [Fact]
    public async Task Room_Schedule_Should_Keep_Reservation_Maintenance_And_Free_Periods()
    {
        var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
        var date = new DateTime(2030, 1, 7);
        var maintenanceRepository = GetRequiredService<IRepository<RoomMaintenanceBlock, Guid>>();
        await WithUnitOfWorkAsync(async () =>
        {
            await _reservationRepository.InsertAsync(new RoomReservation(Guid.NewGuid(), null,
                room.Id, date, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0),
                "Reserved for class", RoomReservationType.Class));
            await maintenanceRepository.InsertAsync(new RoomMaintenanceBlock(Guid.NewGuid(), null,
                room.Id, "Repair", "Electrical repair", MaintenanceSeverity.High,
                date.AddHours(12), date.AddHours(13)));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var schedule = await _roomAvailabilityAppService.GetRoomScheduleAsync(room.Id, date);
            schedule.Slots.Count.ShouldBe(18);
            schedule.Slots.Single(slot => slot.Period == "P3").Status.ShouldBe("free");
            schedule.Slots.Single(slot => slot.Period == "P4").Status.ShouldBe("class");
            schedule.Slots.Single(slot => slot.Period == "P7").Status.ShouldBe("maintenance");
            schedule.Slots.Single(slot => slot.Period == "P8").Status.ShouldBe("free");
        });
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
        var namedSuggestion = resultAll.Single(result => result.RoomName == "Room-1");
        namedSuggestion.BuildingName.ShouldBe("Suggestion Building");
        namedSuggestion.FloorName.ShouldBe("1st Floor");
        namedSuggestion.Type.ShouldBe("Classroom");

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
