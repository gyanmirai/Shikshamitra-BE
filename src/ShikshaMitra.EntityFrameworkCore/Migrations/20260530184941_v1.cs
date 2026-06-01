using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AbpAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationName = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ImpersonatorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImpersonatorUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ImpersonatorTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImpersonatorTenantName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExecutionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecutionDuration = table.Column<int>(type: "integer", nullable: false),
                    ClientIpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ClientName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BrowserInfo = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    HttpMethod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Url = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Exceptions = table.Column<string>(type: "text", nullable: true),
                    Comments = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpBackgroundJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    JobArgs = table.Column<string>(type: "character varying(1048576)", maxLength: 1048576, nullable: false),
                    TryCount = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextTryTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastTryTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsAbandoned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Priority = table.Column<byte>(type: "smallint", nullable: false, defaultValue: (byte)15),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpBackgroundJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpBlobContainers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpBlobContainers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpClaimTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    IsStatic = table.Column<bool>(type: "boolean", nullable: false),
                    Regex = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RegexDescription = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ValueType = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpClaimTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpFeatureGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpFeatureGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ParentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DefaultValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsVisibleToClients = table.Column<bool>(type: "boolean", nullable: false),
                    IsAvailableToHost = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedProviders = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ValueType = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpFeatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpFeatureValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProviderKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpFeatureValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpLinkUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpLinkUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpOrganizationUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(95)", maxLength: 95, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityVersion = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpOrganizationUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbpOrganizationUnits_AbpOrganizationUnits_ParentId",
                        column: x => x.ParentId,
                        principalTable: "AbpOrganizationUnits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AbpPermissionGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpPermissionGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpPermissionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpPermissionGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ParentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MultiTenancySide = table.Column<byte>(type: "smallint", nullable: false),
                    Providers = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StateCheckers = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsStatic = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    EntityVersion = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpSecurityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationName = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    Identity = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    Action = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ClientIpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BrowserInfo = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpSecurityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Device = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeviceInfo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IpAddresses = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SignedIn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastAccessed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpSettingDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DefaultValue = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IsVisibleToClients = table.Column<bool>(type: "boolean", nullable: false),
                    Providers = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsInherited = table.Column<bool>(type: "boolean", nullable: false),
                    IsEncrypted = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpSettingDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProviderKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpTenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityVersion = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpTenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpUserDelegations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpUserDelegations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Surname = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SecurityStamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsExternal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PhoneNumber = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ShouldChangePasswordOnNextLogin = table.Column<bool>(type: "boolean", nullable: false),
                    EntityVersion = table.Column<int>(type: "integer", nullable: false),
                    LastPasswordChangeTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnnouncementTypeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAcademicYearPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAcademicYearPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAcademicYears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAcademicYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAttendanceDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MarkedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAttendanceDays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAutomatedNotificationsSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeAttendanceStatusType = table.Column<int>(type: "integer", nullable: false),
                    NotificationMethods = table.Column<int[]>(type: "integer[]", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAutomatedNotificationsSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppBookCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBookCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppBookIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookCopyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberRole = table.Column<int>(type: "integer", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ChargeCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    FineDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBookIssues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppBooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Isbn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Author = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Publisher = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Edition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Subject = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    ApplicableAge = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Pages = table.Column<int>(type: "integer", nullable: false),
                    NumberOfCopies = table.Column<int>(type: "integer", nullable: false),
                    CoverImage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppBorrowingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    MaxBooksAtTime = table.Column<int>(type: "integer", nullable: false),
                    ReservationLimit = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBorrowingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppBuildings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BuildingName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TotalFloors = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBuildings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppChapterProgressLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    ChangedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppChapterProgressLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppClassTeachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationBoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppClassTeachers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppConcessionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ApproverMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConcessionRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppDeductionComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ComponentType = table.Column<int>(type: "integer", nullable: false),
                    CalculationType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    SelectedDeductionComponentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DependOn = table.Column<int>(type: "integer", nullable: false),
                    VisibleInPayslip = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDeductionComponents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppDivisionPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDivisionPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppDocumentSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DocumentSize = table.Column<float>(type: "real", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DocumentTypes = table.Column<int[]>(type: "integer[]", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDocumentSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppEarningComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ComponentType = table.Column<int>(type: "integer", nullable: false),
                    CalculationType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    SelectedEarningComponentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DependOn = table.Column<int>(type: "integer", nullable: false),
                    VisibleInPayslip = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEarningComponents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppEducationBoardPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationBoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEducationBoardPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppEducationMediumPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEducationMediumPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppEducationMediumStagePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEducationMediumStagePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppEmployeeICardTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediumStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    BackgroundColor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    BackgroundImageBlobName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LogoBlobName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SchoolName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    SchoolAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ThemeColor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Orientation = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    PresetKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    QrCodeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    BarcodeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    BackSideEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    FieldsJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEmployeeICardTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppEntranceExamSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StudentLimit = table.Column<int>(type: "integer", nullable: false),
                    RoomNo = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEntranceExamSlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppEventTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true, comment: "Tenant ID for multi-tenancy"),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "Name of the event type"),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, comment: "Hex color code for UI representation"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "Optional description of the event type"),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEventTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppExamMarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarksObtained = table.Column<double>(type: "double precision", nullable: true),
                    IsAbsent = table.Column<bool>(type: "boolean", nullable: false),
                    Feedback = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppExamMarks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppExams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExamTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppExams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppExamSeatAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatNumber = table.Column<int>(type: "integer", nullable: false),
                    BenchId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRoomAssign = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppExamSeatAllocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppExamTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppExamTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppGradingScales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TotalMarks = table.Column<string>(type: "text", nullable: false),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppGradingScales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppHolidayTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppHolidayTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppHomeworkAssignmentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppHomeworkAssignmentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppHouseSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppHouseSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppInquiryPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreferenceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppInquiryPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppInstituteSetupExtraProperties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppInstituteSetupExtraProperties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppLectureSlotConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolHours = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PreviousStandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLectureSlotConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryChargeCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsFixed = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryChargeCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryCharges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberRole = table.Column<int>(type: "integer", nullable: false),
                    ChargeCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookIssueId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ChargeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PaidDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryCharges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberIdentifier = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppLibrarySections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    SectionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NumberOfShelf = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibrarySections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryTimings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    Day = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartTime = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EndTime = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryTimings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppNotificationInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiverName = table.Column<string>(type: "text", nullable: false),
                    NotificationTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceiverRole = table.Column<string>(type: "text", nullable: false),
                    NotificationType = table.Column<int>(type: "integer", nullable: false),
                    IsDelivered = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppNotificationInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPayElements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsEmployeeSpecific = table.Column<bool>(type: "boolean", nullable: false),
                    IsDependent = table.Column<bool>(type: "boolean", nullable: false),
                    Dependent_ElementId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Dependent_Percentage = table.Column<float>(type: "real", nullable: true),
                    Dependent_Amount = table.Column<float>(type: "real", nullable: true),
                    IsVisibleInPaySlip = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPayElements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPayrollComponentVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEarning = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveMonth = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ComponentType = table.Column<int>(type: "integer", nullable: false),
                    CalculationType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Percentage = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SelectedComponentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DependOn = table.Column<int>(type: "integer", nullable: false),
                    VisibleInPayslip = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPayrollComponentVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppProfessionalReviewCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppProfessionalReviewCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppProfessionalReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommunicationScore = table.Column<int>(type: "integer", nullable: false),
                    ProblemSolvingScore = table.Column<int>(type: "integer", nullable: false),
                    Comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Feedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppProfessionalReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppProfessionalTaxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    GenderType = table.Column<int>(type: "integer", nullable: false),
                    FromSalary = table.Column<double>(type: "double precision", nullable: false),
                    ToSalary = table.Column<double>(type: "double precision", nullable: false),
                    TaxAmount = table.Column<double>(type: "double precision", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppProfessionalTaxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppProvidentFunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EmployeePercentage = table.Column<float>(type: "real", nullable: false),
                    EmployerPercentage = table.Column<float>(type: "real", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppProvidentFunds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppRoomTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomTypeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRoomTypes", x => x.Id);
                });

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "AppSeedExecutionMarkers" (
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NULL,
                    "SeederName" character varying(128) NOT NULL,
                    "Version" character varying(64) NOT NULL,
                    "Profile" character varying(32) NOT NULL,
                    "Status" integer NOT NULL,
                    "StartedAt" timestamp with time zone NOT NULL,
                    "CompletedAt" timestamp with time zone NULL,
                    "ElapsedMilliseconds" bigint NULL,
                    "Error" character varying(4000) NULL,
                    "ExtraProperties" text NOT NULL,
                    "ConcurrencyStamp" character varying(40) NOT NULL,
                    "CreationTime" timestamp with time zone NOT NULL,
                    "CreatorId" uuid NULL,
                    "LastModificationTime" timestamp with time zone NULL,
                    "LastModifierId" uuid NULL,
                    CONSTRAINT "PK_AppSeedExecutionMarkers" PRIMARY KEY ("Id")
                );
                """);

            migrationBuilder.CreateTable(
                name: "AppShiftManagements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftName = table.Column<string>(type: "text", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppShiftManagements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStaffAttendanceDayLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    LockDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LockedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStaffAttendanceDayLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStandardPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStandardPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentElectiveSelections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectiveGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedSubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentPreference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentElectiveSelections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentLeaveTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaveTypeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentLeaveTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentManagementSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArrangementTye = table.Column<int>(type: "integer", nullable: true),
                    ArrangementBy = table.Column<int>(type: "integer", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentManagementSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTeacherSubjectAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppTeacherSubjectAllocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTimetables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppTimetables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppWeeklyLectureSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppWeeklyLectureSlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankName = table.Column<string>(type: "text", nullable: false),
                    BranchName = table.Column<string>(type: "text", nullable: false),
                    BranchAddress = table.Column<string>(type: "text", nullable: true),
                    AccountHolderName = table.Column<string>(type: "text", nullable: false),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    AccountNumber = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: true),
                    IfscCode = table.Column<string>(type: "text", nullable: false),
                    UpiId = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChargeCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargeCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChargeCollectionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    PaymentDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FeeRecordMetadataId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargeCollectionRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Charges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurposeDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AdditionalNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequestorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovalNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Charges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConcessionTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DiscountType = table.Column<int>(type: "integer", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConcessionTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DueFees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    IsOneTime = table.Column<bool>(type: "boolean", nullable: false),
                    OriginalAmount = table.Column<double>(type: "double precision", nullable: false),
                    NetAmount = table.Column<double>(type: "double precision", nullable: false),
                    PaidAmount = table.Column<double>(type: "double precision", nullable: false),
                    OutstandingAmount = table.Column<double>(type: "double precision", nullable: false),
                    RefundedAmount = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DueFees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeeCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeeCollectionDueFeeAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FeeRecordMetadataId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueFeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<double>(type: "numeric(18,2)", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeCollectionDueFeeAllocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeeCollectionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    PaymentDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FeeRecordMetadataId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeCollectionRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeePaymentReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FeeCollectionTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReceiptKind = table.Column<int>(type: "integer", nullable: false),
                    ReceiptScope = table.Column<int>(type: "integer", nullable: false),
                    VerificationStatus = table.Column<int>(type: "integer", nullable: false),
                    TotalPaidAmount = table.Column<double>(type: "numeric(18,2)", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PdfBlobName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LinkedProvisionalReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedFinalReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptBookId = table.Column<Guid>(type: "uuid", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeePaymentReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeeRecordMetadatas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalPaidFee = table.Column<double>(type: "double precision", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeRecordMetadatas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeeRefundRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    VoucherNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalAmount = table.Column<double>(type: "double precision", nullable: false),
                    RefundMethod = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ApproverNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProcessedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeRefundRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeeStructures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeStructureMetadataId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeStructures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceReceiptNumberSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceReceiptNumberSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_AttendanceImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ImportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportedByName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PayrollMonthYear = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    PayrollMonth = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    ValidRows = table.Column<int>(type: "integer", nullable: false),
                    InvalidRows = table.Column<int>(type: "integer", nullable: false),
                    CreateCount = table.Column<int>(type: "integer", nullable: false),
                    ReplaceCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessedRows = table.Column<int>(type: "integer", nullable: false),
                    CreatedCount = table.Column<int>(type: "integer", nullable: false),
                    ReplacedCount = table.Column<int>(type: "integer", nullable: false),
                    PreviewToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PreviewTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorsSummary = table.Column<string>(type: "jsonb", nullable: false),
                    WarningsSummary = table.Column<string>(type: "jsonb", nullable: false),
                    RunSource = table.Column<int>(type: "integer", nullable: false),
                    ExternalCorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IntegrationProvider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RunStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RunFinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsBatchLocked = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_AttendanceImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_AttendancePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkCalendarPolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    FullDayThresholdPercentage = table.Column<double>(type: "double precision", nullable: false),
                    HalfDayThresholdPercentage = table.Column<double>(type: "double precision", nullable: false),
                    AbsentThresholdPercentage = table.Column<double>(type: "double precision", nullable: false),
                    MissingPunchBehavior = table.Column<int>(type: "integer", nullable: false),
                    ManualCorrectionAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    GraceTime = table.Column<int>(type: "integer", nullable: false),
                    MaxLateAllowedPerMonth = table.Column<int>(type: "integer", nullable: false),
                    PenaltyType = table.Column<int>(type: "integer", nullable: false),
                    PenaltyCountThreshold = table.Column<int>(type: "integer", nullable: false),
                    EarlyExitEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EarlyExitGraceTime = table.Column<int>(type: "integer", nullable: false),
                    OvertimeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    OvertimeRate = table.Column<int>(type: "integer", nullable: true),
                    MinOvertimeMinutes = table.Column<int>(type: "integer", nullable: true),
                    HolidayWorkBehavior = table.Column<int>(type: "integer", nullable: false),
                    SandwichRuleEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PayrollPreviewEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_AttendancePolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorNameSnapshot = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ChangedFieldsJson = table.Column<string>(type: "text", nullable: true),
                    ContextJson = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SourceAuditLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_DashboardPreviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DashboardKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    PayloadVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    LastRefreshedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefreshStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefreshRequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_DashboardPreviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_PayrollAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MonthYear = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsEarning = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayrollAdjustments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_PayrollPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkCalendarPolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    DayCalculationType = table.Column<int>(type: "integer", nullable: false),
                    ManualDays = table.Column<int>(type: "integer", nullable: true),
                    ManualMonthDaysJson = table.Column<string>(type: "text", nullable: true),
                    SaturdayWorkPolicy = table.Column<int>(type: "integer", nullable: false),
                    HolidayOverridesJson = table.Column<string>(type: "text", nullable: true),
                    LockedMonthSettingsJson = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayrollPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_PayrollRunItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MonthYear = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmployeeName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmployeeIdentifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsTeaching = table.Column<bool>(type: "boolean", nullable: false),
                    BasicSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FullGrossSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousMonthNetPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NetPayDelta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NetPayDeltaPercent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    IssueCodesJson = table.Column<string>(type: "jsonb", nullable: false),
                    PresentDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HalfWorkingDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalWorkingDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayableDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProrationFactor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DayCalculationMode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UsedDefaultDayCalculation = table.Column<bool>(type: "boolean", nullable: false),
                    PayrollPolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayrollPolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    AttendancePolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttendancePolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    WorkCalendarPolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkCalendarPolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    HolidayImpactJson = table.Column<string>(type: "jsonb", nullable: false),
                    BasisDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayableUnits = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayrollAttendanceSource = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AttendancePolicyWarningsJson = table.Column<string>(type: "jsonb", nullable: false),
                    EarningComponentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    DeductionComponentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    PayrollAdjustmentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayrollRunItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_PayrollRunReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MonthYear = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayrollRunReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_PolicyAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AttendancePolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollPolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    AssignmentReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PolicyAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_PolicyVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ChangeAction = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChangeReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PolicyVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_ReportExportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportType = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    Format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FilterJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BlobName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_ReportExportJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_SalarySlips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthYear = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    TotalEarning = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProcessedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Earnings = table.Column<string>(type: "jsonb", nullable: false),
                    Deductions = table.Column<string>(type: "jsonb", nullable: false),
                    PayrollBasisDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PayableDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ProrationFactor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    PayrollDayCalculationMode = table.Column<int>(type: "integer", nullable: true),
                    PayrollPolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayrollPolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    AttendancePolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttendancePolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    WorkCalendarPolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkCalendarPolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_SalarySlips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_WorkCalendarHolidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkCalendarPolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FromDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ToDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HolidayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    CalendarHolidayId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsIncluded = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_WorkCalendarHolidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_WorkCalendarPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WeeklyOffPattern = table.Column<int>(type: "integer", nullable: false),
                    CustomWeeklyOffsJson = table.Column<string>(type: "text", nullable: true),
                    HolidaySyncMode = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_WorkCalendarPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientSecret = table.Column<string>(type: "text", nullable: true),
                    ClientType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConsentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    JsonWebKeySet = table.Column<string>(type: "text", nullable: true),
                    Permissions = table.Column<string>(type: "text", nullable: true),
                    PostLogoutRedirectUris = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedirectUris = table.Column<string>(type: "text", nullable: true),
                    Requirements = table.Column<string>(type: "text", nullable: true),
                    Settings = table.Column<string>(type: "text", nullable: true),
                    ClientUri = table.Column<string>(type: "text", nullable: true),
                    LogoUri = table.Column<string>(type: "text", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Descriptions = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Resources = table.Column<string>(type: "text", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethodRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    FeeRecordMetadataId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PaymentDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaymentDetails = table.Column<string>(type: "jsonb", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethodRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentBankDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IFSCCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BranchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountHolderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BankAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MICRCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SwiftCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentBankDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentCommunicationDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocalAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LocalAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LocalAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LocalAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LocalAddress_State_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LocalAddress_State_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    LocalAddress_Country_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LocalAddress_Country_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    LocalAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PermanentAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PermanentAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PermanentAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PermanentAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PermanentAddress_State_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PermanentAddress_State_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PermanentAddress_Country_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PermanentAddress_Country_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PermanentAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    EmergencyAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmergencyAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmergencyAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmergencyAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmergencyAddress_State_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmergencyAddress_State_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    EmergencyAddress_Country_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmergencyAddress_Country_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    EmergencyAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsPermanentSameAsLocal = table.Column<bool>(type: "boolean", nullable: false),
                    IsEmergencySameAsLocal = table.Column<bool>(type: "boolean", nullable: false),
                    EmergencyContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EmergencyContactRelation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCommunicationDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentDocumentSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentName = table.Column<string>(type: "text", nullable: false),
                    isRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentDocumentSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentFeeRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LockState = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentFeeRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentFeeStructures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeStructureStandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentFeeStructures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentMedicalRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    WeightKg = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    HeightCm = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    BodyMassIndex = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    PulseRate = table.Column<int>(type: "integer", nullable: true),
                    Hemoglobin = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    Allergies = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChildImmunization = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CovidVaccinationDoses = table.Column<int>(type: "integer", nullable: true),
                    ImmunizationRemarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BloodGroup = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ChronicConditions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CurrentMedications = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentMedicalRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentPreviousSchoolDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SchoolName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SchoolAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Board = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Medium = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastClassAttended = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TCNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DateOfLeaving = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PercentageObtained = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    AcademicYear = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReasonForLeaving = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPreviousSchoolDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TransactionType = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserScopeAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoleName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationBoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserScopeAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AbpAuditLogActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuditLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MethodName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Parameters = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExecutionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecutionDuration = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpAuditLogActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbpAuditLogActions_AbpAuditLogs_AuditLogId",
                        column: x => x.AuditLogId,
                        principalTable: "AbpAuditLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AbpEntityChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangeTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangeType = table.Column<byte>(type: "smallint", nullable: false),
                    EntityTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    EntityTypeFullName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpEntityChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbpEntityChanges_AbpAuditLogs_AuditLogId",
                        column: x => x.AuditLogId,
                        principalTable: "AbpAuditLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AbpBlobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", maxLength: 2147483647, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpBlobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbpBlobs_AbpBlobContainers_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "AbpBlobContainers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AbpOrganizationUnitRoles",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpOrganizationUnitRoles", x => new { x.OrganizationUnitId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AbpOrganizationUnitRoles_AbpOrganizationUnits_OrganizationU~",
                        column: x => x.OrganizationUnitId,
                        principalTable: "AbpOrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AbpOrganizationUnitRoles_AbpRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AbpRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AbpRoleClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ClaimValue = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbpRoleClaims_AbpRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AbpRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AbpTenantConnectionStrings",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpTenantConnectionStrings", x => new { x.TenantId, x.Name });
                    table.ForeignKey(
                        name: "FK_AbpTenantConnectionStrings_AbpTenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "AbpTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AbpUserClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ClaimValue = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbpUserClaims_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AbpUserLogins",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderKey = table.Column<string>(type: "character varying(196)", maxLength: 196, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpUserLogins", x => new { x.UserId, x.LoginProvider });
                    table.ForeignKey(
                        name: "FK_AbpUserLogins_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AbpUserOrganizationUnits",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpUserOrganizationUnits", x => new { x.OrganizationUnitId, x.UserId });
                    table.ForeignKey(
                        name: "FK_AbpUserOrganizationUnits_AbpOrganizationUnits_OrganizationU~",
                        column: x => x.OrganizationUnitId,
                        principalTable: "AbpOrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AbpUserOrganizationUnits_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AbpUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AbpUserRoles_AbpRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AbpRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AbpUserRoles_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AbpUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AbpUserTokens_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppEducationBoards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEducationBoards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEducationBoards_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppEmployeeLeaveTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEmployeeLeaveTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEmployeeLeaveTypes_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Poster = table.Column<string>(type: "text", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Venue = table.Column<string>(type: "text", nullable: true),
                    MeetingDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartTime = table.Column<string>(type: "text", nullable: false),
                    EndTime = table.Column<string>(type: "text", nullable: false),
                    IsRecurring = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RecurrenceJson = table.Column<string>(type: "text", nullable: true),
                    AudienceIds = table.Column<string>(type: "jsonb", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEvents_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RuleBasedDivisionIds = table.Column<string>(type: "jsonb", nullable: false),
                    RuleBasedRoleIds = table.Column<string>(type: "jsonb", nullable: false),
                    NameBasedDivisionIds = table.Column<string>(type: "jsonb", nullable: false),
                    NameBasedRoleIds = table.Column<string>(type: "jsonb", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppGroups_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppHolidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    HolidayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    HolidayTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AudienceIds = table.Column<string>(type: "jsonb", nullable: false),
                    Attachments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppHolidays_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppBookCopies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    CopyNumber = table.Column<int>(type: "integer", nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShelfId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBookCopies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppBookCopies_AppBooks_BookId",
                        column: x => x.BookId,
                        principalTable: "AppBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppBookReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReserveDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBookReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppBookReservations_AppBooks_BookId",
                        column: x => x.BookId,
                        principalTable: "AppBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppFloors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FloorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TotalRoom = table.Column<int>(type: "integer", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    FloorNo = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppFloors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppFloors_AppBuildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "AppBuildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppConcessionFeeHeadMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcessionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadConcessionAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PreConcessionFeeHeadAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PostConcessionFeeHeadAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UserSpecifiedConcessionAmountValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UserSpecifiedConcessionAmountType = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConcessionFeeHeadMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppConcessionFeeHeadMappings_AppConcessionRequests_Concessi~",
                        column: x => x.ConcessionRequestId,
                        principalTable: "AppConcessionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppConcessionRequestDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcessionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConcessionRequestDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppConcessionRequestDocuments_AppConcessionRequests_Concess~",
                        column: x => x.ConcessionRequestId,
                        principalTable: "AppConcessionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppExamDivisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppExamDivisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppExamDivisions_AppExams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "AppExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppExamSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalMarks = table.Column<double>(type: "double precision", nullable: true),
                    PassingMarks = table.Column<double>(type: "double precision", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GradeScalingId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppExamSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppExamSubjects_AppExams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "AppExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppGradeRanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GradingScaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MinMarks = table.Column<int>(type: "integer", nullable: false),
                    MaxMarks = table.Column<int>(type: "integer", nullable: false),
                    Grade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppGradeRanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppGradeRanges_AppGradingScales_GradingScaleId",
                        column: x => x.GradingScaleId,
                        principalTable: "AppGradingScales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppLectureSlotGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    LectureSlotConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalSlots = table.Column<int>(type: "integer", nullable: false),
                    WeekRangeStart = table.Column<int>(type: "integer", nullable: false),
                    WeekRangeEnd = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLectureSlotGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLectureSlotGroups_AppLectureSlotConfigs_LectureSlotConfi~",
                        column: x => x.LectureSlotConfigId,
                        principalTable: "AppLectureSlotConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryShelves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShelfName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryShelves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryShelves_AppLibrarySections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "AppLibrarySections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppEmployees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProfilePictureBlobName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    EmployeeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MiddleName = table.Column<string>(type: "text", nullable: true),
                    ShortName = table.Column<string>(type: "text", nullable: true),
                    EmployeeStatusType = table.Column<int>(type: "integer", nullable: true),
                    ChangeReason = table.Column<string>(type: "text", nullable: true),
                    StatusLastChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StatusEffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    FamilyCast = table.Column<string>(type: "text", nullable: true),
                    CastType = table.Column<int>(type: "integer", nullable: true),
                    GenderType = table.Column<int>(type: "integer", nullable: true),
                    Religion = table.Column<string>(type: "text", nullable: true),
                    MaritalStatus = table.Column<int>(type: "integer", nullable: true),
                    BloodGroup = table.Column<string>(type: "text", nullable: true),
                    MobileNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SecondaryMobileNo = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PanNo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AdharNo = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Address1 = table.Column<string>(type: "text", nullable: true),
                    Address2 = table.Column<string>(type: "text", nullable: true),
                    Address3 = table.Column<string>(type: "text", nullable: true),
                    Department = table.Column<string>(type: "text", nullable: true),
                    Designation = table.Column<string>(type: "text", nullable: true),
                    DateOfJoining = table.Column<DateOnly>(type: "date", nullable: true),
                    EmployeeGroup = table.Column<int>(type: "integer", nullable: true),
                    Qualification = table.Column<string>(type: "text", nullable: true),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentificationMark1 = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    StateName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StateIsoCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    CountryName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CountryIsoCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    PinCode = table.Column<string>(type: "text", nullable: true),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportingManagerId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankDetails_AccountHolderName = table.Column<string>(type: "text", nullable: true),
                    BankDetails_AccountNumber = table.Column<string>(type: "text", nullable: true),
                    BankDetails_BankName = table.Column<string>(type: "text", nullable: true),
                    BankDetails_BranchName = table.Column<string>(type: "text", nullable: true),
                    BankDetails_IfscCode = table.Column<string>(type: "text", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WorkExperiences = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEmployees_AbpRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AbpRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppEmployees_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppEmployees_AppEmployees_ReportingManagerId",
                        column: x => x.ReportingManagerId,
                        principalTable: "AppEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppEmployees_AppShiftManagements_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "AppShiftManagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppAllocatedTeachers",
                columns: table => new
                {
                    TeacherSubjectAllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAllocatedTeachers", x => new { x.TeacherSubjectAllocationId, x.TeacherId });
                    table.ForeignKey(
                        name: "FK_AppAllocatedTeachers_AppTeacherSubjectAllocations_TeacherSu~",
                        column: x => x.TeacherSubjectAllocationId,
                        principalTable: "AppTeacherSubjectAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppTimetableSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimetableId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    SlotType = table.Column<int>(type: "integer", nullable: false),
                    BreakType = table.Column<int>(type: "integer", nullable: true),
                    SubjectType = table.Column<int>(type: "integer", nullable: true),
                    ElectiveGroupId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppTimetableSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppTimetableSlots_AppTimetables_TimetableId",
                        column: x => x.TimetableId,
                        principalTable: "AppTimetables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppWeeklyLectureSlotSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    WeeklyLectureSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ElectiveGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    WeeklyLectures = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppWeeklyLectureSlotSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppWeeklyLectureSlotSubjects_AppWeeklyLectureSlots_WeeklyLe~",
                        column: x => x.WeeklyLectureSlotId,
                        principalTable: "AppWeeklyLectureSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConcessionTypeDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentName = table.Column<string>(type: "text", nullable: false),
                    DocumentSize = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConcessionTypeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConcessionTypeDocuments_ConcessionTypes_ConcessionTypeId",
                        column: x => x.ConcessionTypeId,
                        principalTable: "ConcessionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeeHeads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeHeads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeHeads_FeeCategories_FeeCategoryId",
                        column: x => x.FeeCategoryId,
                        principalTable: "FeeCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeeRefundLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeRefundRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueFeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeRefundLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeRefundLines_FeeRefundRequests_FeeRefundRequestId",
                        column: x => x.FeeRefundRequestId,
                        principalTable: "FeeRefundRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HR_AttendanceImportRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttendanceDate = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EmployeeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContactNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TimeIn = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TimeOut = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Errors = table.Column<string>(type: "jsonb", nullable: false),
                    Warnings = table.Column<string>(type: "jsonb", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_AttendanceImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HR_AttendanceImportRows_HR_AttendanceImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "HR_AttendanceImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictAuthorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Scopes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictAuthorizations_OpenIddictApplications_Application~",
                        column: x => x.ApplicationId,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppDocumentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaxSizeMB = table.Column<double>(type: "numeric(10,2)", nullable: false),
                    AcceptedFormats = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    StudentDocumentSettingId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDocumentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppDocumentTypes_StudentDocumentSettings_StudentDocumentSet~",
                        column: x => x.StudentDocumentSettingId,
                        principalTable: "StudentDocumentSettings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppIncomeCategoryTags",
                columns: table => new
                {
                    TransactionCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppIncomeCategoryTags", x => new { x.TransactionCategoryId, x.Id });
                    table.ForeignKey(
                        name: "FK_AppIncomeCategoryTags_TransactionCategories_TransactionCate~",
                        column: x => x.TransactionCategoryId,
                        principalTable: "TransactionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppTransactionCategoryTags",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppTransactionCategoryTags", x => new { x.TransactionId, x.Id });
                    table.ForeignKey(
                        name: "FK_AppTransactionCategoryTags_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AbpEntityPropertyChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityChangeId = table.Column<Guid>(type: "uuid", nullable: false),
                    NewValue = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    OriginalValue = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PropertyName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PropertyTypeFullName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpEntityPropertyChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbpEntityPropertyChanges_AbpEntityChanges_EntityChangeId",
                        column: x => x.EntityChangeId,
                        principalTable: "AbpEntityChanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppEducationMediums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EducationBoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEducationMediums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEducationMediums_AppEducationBoards_EducationBoardId",
                        column: x => x.EducationBoardId,
                        principalTable: "AppEducationBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppEmployeeLeavePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccrualPolicyType = table.Column<int>(type: "integer", nullable: false),
                    LeaveCount = table.Column<int>(type: "integer", nullable: false),
                    PaidType = table.Column<int>(type: "integer", nullable: false),
                    CarryForward = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Encashable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EncashmentTiming = table.Column<int>(type: "integer", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEmployeeLeavePolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEmployeeLeavePolicies_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppEmployeeLeavePolicies_AppEmployeeLeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "AppEmployeeLeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppGroupEmployeeRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectionType = table.Column<int>(type: "integer", nullable: false),
                    Field = table.Column<int>(type: "integer", nullable: false),
                    Operator = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppGroupEmployeeRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppGroupEmployeeRules_AppGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "AppGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberType = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppGroupMembers_AppGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "AppGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppGroupStudentRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectionType = table.Column<int>(type: "integer", nullable: false),
                    Field = table.Column<int>(type: "integer", nullable: false),
                    Operator = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppGroupStudentRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppGroupStudentRules_AppGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "AppGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    FloorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalBenches = table.Column<int>(type: "integer", nullable: false),
                    BenchCapacity = table.Column<int>(type: "integer", nullable: false),
                    BenchesPerRow = table.Column<int>(type: "integer", nullable: false),
                    BenchesPerColumn = table.Column<int>(type: "integer", nullable: false),
                    Features = table.Column<string>(type: "jsonb", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppRooms_AppBuildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "AppBuildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppRooms_AppFloors_FloorId",
                        column: x => x.FloorId,
                        principalTable: "AppFloors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppRooms_AppRoomTypes_RoomTypeId",
                        column: x => x.RoomTypeId,
                        principalTable: "AppRoomTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppExamSubjectDivisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamSubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    RoomIds = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppExamSubjectDivisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppExamSubjectDivisions_AppExamSubjects_ExamSubjectId",
                        column: x => x.ExamSubjectId,
                        principalTable: "AppExamSubjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppLectureSlotTimeSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LectureSlotGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    SlotType = table.Column<int>(type: "integer", nullable: false),
                    BreakType = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLectureSlotTimeSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLectureSlotTimeSlots_AppLectureSlotGroups_LectureSlotGro~",
                        column: x => x.LectureSlotGroupId,
                        principalTable: "AppLectureSlotGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppEmployeeAdditionalDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BplCardNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PersonWithDisability = table.Column<bool>(type: "boolean", nullable: false),
                    SecondLanguage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LibraryMember = table.Column<bool>(type: "boolean", nullable: false),
                    TransportMember = table.Column<bool>(type: "boolean", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEmployeeAdditionalDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEmployeeAdditionalDetails_AppEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AppEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppEmployeeAttendances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TimeIn = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    TimeOut = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    StatusType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EducationMediumStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEmployeeAttendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEmployeeAttendances_AppEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AppEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppEmployeeDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNameId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BlobName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEmployeeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEmployeeDocuments_AppEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AppEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppEmployeeFamilyMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Occupation = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContactNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Address1 = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Address2 = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Address3 = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    City = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StateName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StateIsoCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    CountryName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CountryIsoCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    PinCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEmployeeFamilyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEmployeeFamilyMembers_AppEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AppEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppEmployeePayrollComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ComponentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsFixed = table.Column<bool>(type: "boolean", nullable: false),
                    IsTemp = table.Column<bool>(type: "boolean", nullable: false),
                    ApplicableMonth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ComponentType = table.Column<int>(type: "integer", nullable: false),
                    DependOn = table.Column<int>(type: "integer", nullable: true),
                    IsEarning = table.Column<bool>(type: "boolean", nullable: false),
                    UsesMasterValue = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEmployeePayrollComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEmployeePayrollComponents_AppEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AppEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppEmployeePayrollDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BranchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountHolderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IfscCode = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    PfRule = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EpfNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UanNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PunchId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TotalWorkingHourPerMonth = table.Column<decimal>(type: "numeric(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEmployeePayrollDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEmployeePayrollDetails_AppEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AppEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppTimetableSlotSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimetableSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppTimetableSlotSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppTimetableSlotSubjects_AppTimetableSlots_TimetableSlotId",
                        column: x => x.TimetableSlotId,
                        principalTable: "AppTimetableSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppConcessionTypeDocumentFormats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionTypeDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConcessionTypeDocumentFormats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppConcessionTypeDocumentFormats_ConcessionTypeDocuments_Co~",
                        column: x => x.ConcessionTypeDocumentId,
                        principalTable: "ConcessionTypeDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeeStructureDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FeeStructureId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    IsOneTime = table.Column<bool>(type: "boolean", nullable: false),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false),
                    IsRefundable = table.Column<bool>(type: "boolean", nullable: false),
                    PayableMonth = table.Column<int>(type: "integer", nullable: true),
                    PayableYear = table.Column<int>(type: "integer", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeStructureDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeStructureDetails_FeeHeads_FeeHeadId",
                        column: x => x.FeeHeadId,
                        principalTable: "FeeHeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeStructureDetails_FeeStructures_FeeStructureId",
                        column: x => x.FeeStructureId,
                        principalTable: "FeeStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedemptionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId",
                        column: x => x.AuthorizationId,
                        principalTable: "OpenIddictAuthorizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppAssignmentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAssignmentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAssignmentTypes_AppEducationMediums_AcademicMediumId",
                        column: x => x.AcademicMediumId,
                        principalTable: "AppEducationMediums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppEducationMediumStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BannerPicture = table.Column<byte[]>(type: "bytea", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEducationMediumStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEducationMediumStages_AppEducationMediums_EducationMediu~",
                        column: x => x.EducationMediumId,
                        principalTable: "AppEducationMediums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppElectiveGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppElectiveGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppElectiveGroups_AppEducationMediums_AcademicMediumId",
                        column: x => x.AcademicMediumId,
                        principalTable: "AppEducationMediums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppHomeworkTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppHomeworkTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppHomeworkTypes_AppEducationMediums_AcademicMediumId",
                        column: x => x.AcademicMediumId,
                        principalTable: "AppEducationMediums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppSubjects_AppEducationMediums_EducationMediumId",
                        column: x => x.EducationMediumId,
                        principalTable: "AppEducationMediums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppEmployeeLeaves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublicId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    HalfDay = table.Column<bool>(type: "boolean", nullable: false),
                    StartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovedPaidType = table.Column<int>(type: "integer", nullable: true),
                    ApprovedLeaveUnits = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    Description = table.Column<string>(type: "text", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    Attachment = table.Column<byte[]>(type: "bytea", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEmployeeLeaves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEmployeeLeaves_AppEmployeeLeavePolicies_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "AppEmployeeLeavePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppEmployeeLeaves_AppEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AppEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationBoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    AssignmentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionType = table.Column<int>(type: "integer", nullable: false),
                    TotalMarks = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ChapterIdsJson = table.Column<string>(type: "text", nullable: true),
                    AttachmentsJson = table.Column<string>(type: "text", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAssignments_AppAssignmentTypes_AssignmentTypeId",
                        column: x => x.AssignmentTypeId,
                        principalTable: "AppAssignmentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppEmployeeOrganizationAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationMediumStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEmployeeOrganizationAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEmployeeOrganizationAssignments_AppEducationMediumStages~",
                        column: x => x.EducationMediumStageId,
                        principalTable: "AppEducationMediumStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppEmployeeOrganizationAssignments_AppEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AppEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppStandards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AgeLowerLimit = table.Column<int>(type: "integer", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStandards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStandards_AppEducationMediumStages_EducationMediumStageId",
                        column: x => x.EducationMediumStageId,
                        principalTable: "AppEducationMediumStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppStandards_AppShiftManagements_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "AppShiftManagements",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReceiptBooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediumStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Prefix = table.Column<string>(type: "text", nullable: false),
                    Suffix = table.Column<string>(type: "text", nullable: false),
                    StartingNumber = table.Column<long>(type: "bigint", nullable: false),
                    EndingNumber = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptBooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptBooks_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReceiptBooks_AppEducationBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "AppEducationBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReceiptBooks_AppEducationMediumStages_MediumStageId",
                        column: x => x.MediumStageId,
                        principalTable: "AppEducationMediumStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReceiptBooks_AppEducationMediums_MediumId",
                        column: x => x.MediumId,
                        principalTable: "AppEducationMediums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppHomeworkAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationBoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    HomeworkAssignmentTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentUrl = table.Column<string>(type: "text", nullable: true),
                    SubmissionType = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ChapterIdsJson = table.Column<string>(type: "text", nullable: true),
                    HomeworkTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttachmentsJson = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppHomeworkAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppHomeworkAssignments_AppHomeworkAssignmentTypes_HomeworkA~",
                        column: x => x.HomeworkAssignmentTypeId,
                        principalTable: "AppHomeworkAssignmentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppHomeworkAssignments_AppHomeworkTypes_HomeworkTypeId",
                        column: x => x.HomeworkTypeId,
                        principalTable: "AppHomeworkTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppElectiveGroupComponents",
                columns: table => new
                {
                    ElectiveGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppElectiveGroupComponents", x => new { x.ElectiveGroupId, x.SubjectId });
                    table.ForeignKey(
                        name: "FK_AppElectiveGroupComponents_AppElectiveGroups_ElectiveGroupId",
                        column: x => x.ElectiveGroupId,
                        principalTable: "AppElectiveGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppElectiveGroupComponents_AppSubjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "AppSubjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppAssignmentDivisionSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAssignmentDivisionSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAssignmentDivisionSchedules_AppAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "AppAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppDivisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Strength = table.Column<int>(type: "integer", nullable: false),
                    RoomNumber = table.Column<string>(type: "text", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDivisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppDivisions_AppStandards_StandardId",
                        column: x => x.StandardId,
                        principalTable: "AppStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppStandardSubjectAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectiveGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LearningGoals = table.Column<string>(type: "text", nullable: true),
                    Prerequisites = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubjectType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStandardSubjectAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStandardSubjectAssignments_AppElectiveGroups_ElectiveGro~",
                        column: x => x.ElectiveGroupId,
                        principalTable: "AppElectiveGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppStandardSubjectAssignments_AppStandards_StandardId",
                        column: x => x.StandardId,
                        principalTable: "AppStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppStandardSubjectAssignments_AppSubjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "AppSubjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeeStructureMetadatas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicBoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptBookId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeStructureMetadatas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeStructureMetadatas_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeStructureMetadatas_AppEducationBoards_AcademicBoardId",
                        column: x => x.AcademicBoardId,
                        principalTable: "AppEducationBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeStructureMetadatas_AppEducationMediums_AcademicMediumId",
                        column: x => x.AcademicMediumId,
                        principalTable: "AppEducationMediums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeStructureMetadatas_ReceiptBooks_ReceiptBookId",
                        column: x => x.ReceiptBookId,
                        principalTable: "ReceiptBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppHomeworkDivisionSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    HomeworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppHomeworkDivisionSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppHomeworkDivisionSchedules_AppHomeworkAssignments_Homewor~",
                        column: x => x.HomeworkId,
                        principalTable: "AppHomeworkAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentHomeworkAssignmentSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    HomeworkAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Feedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Marks = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TotalMarks = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SubmittedDocumentUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SubmittedContent = table.Column<string>(type: "text", maxLength: 2147483647, nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GradedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    GradedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentHomeworkAssignmentSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentHomeworkAssignmentSubmissions_AppAssignments_Assi~",
                        column: x => x.AssignmentId,
                        principalTable: "AppAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppStudentHomeworkAssignmentSubmissions_AppHomeworkAssignme~",
                        column: x => x.HomeworkAssignmentId,
                        principalTable: "AppHomeworkAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppStudents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    MiddleName = table.Column<string>(type: "text", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicBoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicMediumStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrNo = table.Column<int>(type: "integer", nullable: true),
                    RollNo = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AdmissionApprovalStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AdmissionApprovalRemarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FeeStructureId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommunicationDetailId = table.Column<Guid>(type: "uuid", nullable: true),
                    MedicalRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousSchoolDetailId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankDetailId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudents_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppStudents_AppDivisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "AppDivisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudents_AppEducationBoards_AcademicBoardId",
                        column: x => x.AcademicBoardId,
                        principalTable: "AppEducationBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudents_AppEducationMediums_AcademicMediumId",
                        column: x => x.AcademicMediumId,
                        principalTable: "AppEducationMediums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudents_AppStandards_StandardId",
                        column: x => x.StandardId,
                        principalTable: "AppStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudents_StudentBankDetails_BankDetailId",
                        column: x => x.BankDetailId,
                        principalTable: "StudentBankDetails",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppStudents_StudentCommunicationDetails_CommunicationDetail~",
                        column: x => x.CommunicationDetailId,
                        principalTable: "StudentCommunicationDetails",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppStudents_StudentMedicalRecords_MedicalRecordId",
                        column: x => x.MedicalRecordId,
                        principalTable: "StudentMedicalRecords",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppStudents_StudentPreviousSchoolDetails_PreviousSchoolDeta~",
                        column: x => x.PreviousSchoolDetailId,
                        principalTable: "StudentPreviousSchoolDetails",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppSubjectChapters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedSubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterNumber = table.Column<int>(type: "integer", nullable: false),
                    ChapterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChapterStartTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChapterEndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSubjectChapters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppSubjectChapters_AppStandardSubjectAssignments_AssignedSu~",
                        column: x => x.AssignedSubjectId,
                        principalTable: "AppStandardSubjectAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppSubjectTeachers",
                columns: table => new
                {
                    AssignedSubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSubjectTeachers", x => new { x.AssignedSubjectId, x.TeacherId });
                    table.ForeignKey(
                        name: "FK_AppSubjectTeachers_AppStandardSubjectAssignments_AssignedSu~",
                        column: x => x.AssignedSubjectId,
                        principalTable: "AppStandardSubjectAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppAttendanceEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttendanceDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MarkedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAttendanceEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAttendanceEntries_AppAttendanceDays_AttendanceDayId",
                        column: x => x.AttendanceDayId,
                        principalTable: "AppAttendanceDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppAttendanceEntries_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentAdditionalDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: true),
                    ProfilePictureUrl = table.Column<string>(type: "text", nullable: true),
                    DateOfInquiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InquiryNo = table.Column<string>(type: "text", nullable: false),
                    InquiryPreferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegistrationFormNo = table.Column<string>(type: "text", nullable: true),
                    RegistrationAmount = table.Column<double>(type: "double precision", nullable: true),
                    EntranceExamSlotId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntranceExamStartDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EntranceExamEndDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EntranceExamRoomNo = table.Column<int>(type: "integer", nullable: true),
                    EntranceExamTotalMarks = table.Column<double>(type: "double precision", nullable: true),
                    EntranceExamPassingMarks = table.Column<double>(type: "double precision", nullable: true),
                    EntranceExamObtainedMarks = table.Column<double>(type: "double precision", nullable: true),
                    EntranceExamPercentage = table.Column<double>(type: "double precision", nullable: true),
                    EntranceExamStatus = table.Column<int>(type: "integer", nullable: true),
                    EntranceExamResultDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AdharNo = table.Column<string>(type: "text", nullable: true),
                    ApparId = table.Column<string>(type: "text", nullable: true),
                    PanNo = table.Column<string>(type: "text", nullable: true),
                    PennNo = table.Column<string>(type: "text", nullable: true),
                    Religion = table.Column<int>(type: "integer", nullable: true),
                    FamilyCast = table.Column<string>(type: "text", nullable: false),
                    CastCategory = table.Column<int>(type: "integer", nullable: true),
                    Nationality = table.Column<string>(type: "text", nullable: true),
                    BirthPlace = table.Column<string>(type: "text", nullable: true),
                    MotherTongue = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    IsBPLStudent = table.Column<bool>(type: "boolean", nullable: true),
                    BPLCardNo = table.Column<string>(type: "text", nullable: true),
                    HaveDisability = table.Column<bool>(type: "boolean", nullable: true),
                    SecondLanguage = table.Column<string>(type: "text", nullable: true),
                    EmergencyContactNo = table.Column<string>(type: "text", nullable: true),
                    EmergencyRelationName = table.Column<string>(type: "text", nullable: true),
                    EmergencyRelationType = table.Column<string>(type: "text", nullable: true),
                    IsSingleParentChild = table.Column<bool>(type: "boolean", nullable: true),
                    IsSingleChild = table.Column<bool>(type: "boolean", nullable: true),
                    IsLibraryMember = table.Column<bool>(type: "boolean", nullable: true),
                    IsTransportMember = table.Column<bool>(type: "boolean", nullable: true),
                    PreviousSchool = table.Column<string>(type: "text", nullable: true),
                    LastClassPassed = table.Column<string>(type: "text", nullable: true),
                    HouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsGlobalHouseLeader = table.Column<bool>(type: "boolean", nullable: true),
                    IsDivisionHouseLeader = table.Column<bool>(type: "boolean", nullable: true),
                    StudentRegistrationState = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentAdditionalDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentAdditionalDetails_AppHouseSettings_HouseId",
                        column: x => x.HouseId,
                        principalTable: "AppHouseSettings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppStudentAdditionalDetails_AppInquiryPreferences_InquiryPr~",
                        column: x => x.InquiryPreferenceId,
                        principalTable: "AppInquiryPreferences",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppStudentAdditionalDetails_AppStudents_Id",
                        column: x => x.Id,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentContactDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrimaryContactNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SecondaryContactNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Address1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address3 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Country_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Country_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentContactDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentContactDetails_AppStudents_Id",
                        column: x => x.Id,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppStudentContactDetails_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppStudentDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlobName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentDocuments_AppDocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "AppDocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentDocuments_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentLeaves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentLeaveTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsHalfDay = table.Column<bool>(type: "boolean", nullable: false),
                    LeaveDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalDays = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Attachment = table.Column<byte[]>(type: "bytea", nullable: true),
                    AttachmentFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AttachmentContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentLeaves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentLeaves_AppStudentLeaveTypes_StudentLeaveTypeId",
                        column: x => x.StudentLeaveTypeId,
                        principalTable: "AppStudentLeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentLeaves_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: false),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentStatusHistories_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentFamilyDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FatherDetails_FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FatherDetails_DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FatherDetails_Occupation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FatherDetails_Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FatherDetails_ContactNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FatherDetails_OfficeNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FatherDetails_Qualification = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FatherDetails_YearOfPassing = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    FatherDetails_Designation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FatherDetails_Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FatherDetails_AadharNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Father_OfficeAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_OfficeAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Father_OfficeAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Father_OfficeAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_OfficeAddress_State_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_OfficeAddress_State_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Father_OfficeAddress_Country_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_OfficeAddress_Country_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Father_OfficeAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Father_LocalAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_LocalAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Father_LocalAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Father_LocalAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_LocalAddress_State_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_LocalAddress_State_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Father_LocalAddress_Country_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_LocalAddress_Country_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Father_LocalAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    MotherDetails_FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MotherDetails_DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MotherDetails_Occupation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MotherDetails_Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MotherDetails_ContactNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MotherDetails_OfficeNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MotherDetails_Qualification = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MotherDetails_YearOfPassing = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    MotherDetails_Designation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MotherDetails_Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MotherDetails_AadharNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Mother_OfficeAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_OfficeAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Mother_OfficeAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Mother_OfficeAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_OfficeAddress_State_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_OfficeAddress_State_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Mother_OfficeAddress_Country_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_OfficeAddress_Country_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Mother_OfficeAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Mother_LocalAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_LocalAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Mother_LocalAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Mother_LocalAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_LocalAddress_State_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_LocalAddress_State_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Mother_LocalAddress_Country_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_LocalAddress_Country_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Mother_LocalAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    GuardianDetails_FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GuardianDetails_DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GuardianDetails_Occupation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuardianDetails_Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    GuardianDetails_ContactNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    GuardianDetails_OfficeNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    GuardianDetails_Qualification = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuardianDetails_YearOfPassing = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    GuardianDetails_Designation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuardianDetails_Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GuardianDetails_AadharNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Guardian_OfficeAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_OfficeAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Guardian_OfficeAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Guardian_OfficeAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_OfficeAddress_State_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_OfficeAddress_State_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Guardian_OfficeAddress_Country_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_OfficeAddress_Country_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Guardian_OfficeAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Guardian_LocalAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_LocalAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Guardian_LocalAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Guardian_LocalAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_LocalAddress_State_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_LocalAddress_State_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Guardian_LocalAddress_Country_Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_LocalAddress_Country_IsoCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Guardian_LocalAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SiblingStudentIds = table.Column<string>(type: "text", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentFamilyDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentFamilyDetails_AppStudents_Id",
                        column: x => x.Id,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    NextReminderDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentNotes_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppChapterDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedSubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    BlobName = table.Column<string>(type: "text", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssignedSubjectChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppChapterDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppChapterDocuments_AppStandardSubjectAssignments_AssignedS~",
                        column: x => x.AssignedSubjectId,
                        principalTable: "AppStandardSubjectAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppChapterDocuments_AppSubjectChapters_AssignedSubjectChapt~",
                        column: x => x.AssignedSubjectChapterId,
                        principalTable: "AppSubjectChapters",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppChapterProgresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppChapterProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppChapterProgresses_AppSubjectChapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "AppSubjectChapters",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbpAuditLogActions_AuditLogId",
                table: "AbpAuditLogActions",
                column: "AuditLogId");

            migrationBuilder.CreateIndex(
                name: "IX_AbpAuditLogActions_TenantId_ServiceName_MethodName_Executio~",
                table: "AbpAuditLogActions",
                columns: new[] { "TenantId", "ServiceName", "MethodName", "ExecutionTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpAuditLogs_TenantId_ExecutionTime",
                table: "AbpAuditLogs",
                columns: new[] { "TenantId", "ExecutionTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpAuditLogs_TenantId_UserId_ExecutionTime",
                table: "AbpAuditLogs",
                columns: new[] { "TenantId", "UserId", "ExecutionTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpBackgroundJobs_IsAbandoned_NextTryTime",
                table: "AbpBackgroundJobs",
                columns: new[] { "IsAbandoned", "NextTryTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpBlobContainers_TenantId_Name",
                table: "AbpBlobContainers",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpBlobs_ContainerId",
                table: "AbpBlobs",
                column: "ContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_AbpBlobs_TenantId_ContainerId_Name",
                table: "AbpBlobs",
                columns: new[] { "TenantId", "ContainerId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpEntityChanges_AuditLogId",
                table: "AbpEntityChanges",
                column: "AuditLogId");

            migrationBuilder.CreateIndex(
                name: "IX_AbpEntityChanges_TenantId_EntityTypeFullName_EntityId",
                table: "AbpEntityChanges",
                columns: new[] { "TenantId", "EntityTypeFullName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpEntityPropertyChanges_EntityChangeId",
                table: "AbpEntityPropertyChanges",
                column: "EntityChangeId");

            migrationBuilder.CreateIndex(
                name: "IX_AbpFeatureGroups_Name",
                table: "AbpFeatureGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbpFeatures_GroupName",
                table: "AbpFeatures",
                column: "GroupName");

            migrationBuilder.CreateIndex(
                name: "IX_AbpFeatures_Name",
                table: "AbpFeatures",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbpFeatureValues_Name_ProviderName_ProviderKey",
                table: "AbpFeatureValues",
                columns: new[] { "Name", "ProviderName", "ProviderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbpLinkUsers_SourceUserId_SourceTenantId_TargetUserId_Targe~",
                table: "AbpLinkUsers",
                columns: new[] { "SourceUserId", "SourceTenantId", "TargetUserId", "TargetTenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbpOrganizationUnitRoles_RoleId_OrganizationUnitId",
                table: "AbpOrganizationUnitRoles",
                columns: new[] { "RoleId", "OrganizationUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpOrganizationUnits_Code",
                table: "AbpOrganizationUnits",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_AbpOrganizationUnits_ParentId",
                table: "AbpOrganizationUnits",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_AbpPermissionGrants_TenantId_Name_ProviderName_ProviderKey",
                table: "AbpPermissionGrants",
                columns: new[] { "TenantId", "Name", "ProviderName", "ProviderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbpPermissionGroups_Name",
                table: "AbpPermissionGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbpPermissions_GroupName",
                table: "AbpPermissions",
                column: "GroupName");

            migrationBuilder.CreateIndex(
                name: "IX_AbpPermissions_Name",
                table: "AbpPermissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbpRoleClaims_RoleId",
                table: "AbpRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AbpRoles_NormalizedName",
                table: "AbpRoles",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_AbpSecurityLogs_TenantId_Action",
                table: "AbpSecurityLogs",
                columns: new[] { "TenantId", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpSecurityLogs_TenantId_ApplicationName",
                table: "AbpSecurityLogs",
                columns: new[] { "TenantId", "ApplicationName" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpSecurityLogs_TenantId_Identity",
                table: "AbpSecurityLogs",
                columns: new[] { "TenantId", "Identity" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpSecurityLogs_TenantId_UserId",
                table: "AbpSecurityLogs",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpSessions_Device",
                table: "AbpSessions",
                column: "Device");

            migrationBuilder.CreateIndex(
                name: "IX_AbpSessions_SessionId",
                table: "AbpSessions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AbpSessions_TenantId_UserId",
                table: "AbpSessions",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpSettingDefinitions_Name",
                table: "AbpSettingDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbpSettings_Name_ProviderName_ProviderKey",
                table: "AbpSettings",
                columns: new[] { "Name", "ProviderName", "ProviderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbpTenants_Name",
                table: "AbpTenants",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AbpTenants_NormalizedName",
                table: "AbpTenants",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_AbpUserClaims_UserId",
                table: "AbpUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AbpUserLogins_LoginProvider_ProviderKey",
                table: "AbpUserLogins",
                columns: new[] { "LoginProvider", "ProviderKey" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpUserOrganizationUnits_UserId_OrganizationUnitId",
                table: "AbpUserOrganizationUnits",
                columns: new[] { "UserId", "OrganizationUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpUserRoles_RoleId_UserId",
                table: "AbpUserRoles",
                columns: new[] { "RoleId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpUsers_Email",
                table: "AbpUsers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_AbpUsers_NormalizedEmail",
                table: "AbpUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AbpUsers_NormalizedUserName",
                table: "AbpUsers",
                column: "NormalizedUserName");

            migrationBuilder.CreateIndex(
                name: "IX_AbpUsers_UserName",
                table: "AbpUsers",
                column: "UserName");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementTypes_Tenant_Medium_Name",
                table: "AnnouncementTypes",
                columns: new[] { "TenantId", "AcademicMediumId", "AnnouncementTypeName" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicYears_Name_TenantId_StartDate_EndDate",
                table: "AppAcademicYears",
                columns: new[] { "Name", "TenantId", "StartDate", "EndDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAllocatedTeachers_TeacherId",
                table: "AppAllocatedTeachers",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDivisionSchedules_AssignmentId",
                table: "AppAssignmentDivisionSchedules",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDivisionSchedules_AssignmentId_DivisionId",
                table: "AppAssignmentDivisionSchedules",
                columns: new[] { "AssignmentId", "DivisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDivisionSchedules_DivisionId",
                table: "AppAssignmentDivisionSchedules",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDivisionSchedules_PublishDate",
                table: "AppAssignmentDivisionSchedules",
                column: "PublishDate",
                filter: "\"PublishDate\" IS NOT NULL AND \"IsPublished\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_AcademicYearId",
                table: "AppAssignments",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_AssignmentTypeId",
                table: "AppAssignments",
                column: "AssignmentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_StandardId",
                table: "AppAssignments",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_SubjectId",
                table: "AppAssignments",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_Tenant_Standard_Subject",
                table: "AppAssignments",
                columns: new[] { "TenantId", "StandardId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TenantId",
                table: "AppAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentTypes_AcademicMediumId_Name",
                table: "AppAssignmentTypes",
                columns: new[] { "AcademicMediumId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceDay_Unique",
                table: "AppAttendanceDays",
                columns: new[] { "AcademicYearId", "StandardId", "DivisionId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAttendanceEntries_AttendanceDayId",
                table: "AppAttendanceEntries",
                column: "AttendanceDayId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEntry_Student",
                table: "AppAttendanceEntries",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceEntry_Unique",
                table: "AppAttendanceEntries",
                columns: new[] { "AttendanceDayId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppBookCopies_AccessionNumber",
                table: "AppBookCopies",
                column: "AccessionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppBookCopies_BookId",
                table: "AppBookCopies",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookIssues_AccessionNumber",
                table: "AppBookIssues",
                column: "AccessionNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookIssues_BookCopyId",
                table: "AppBookIssues",
                column: "BookCopyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookIssues_MemberId",
                table: "AppBookIssues",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookReservations_BookId",
                table: "AppBookReservations",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookReservations_MemberId",
                table: "AppBookReservations",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBooks_Isbn_Edition",
                table: "AppBooks",
                columns: new[] { "Isbn", "Edition" });

            migrationBuilder.CreateIndex(
                name: "IX_AppBorrowingRules_AcademicYearId_Role",
                table: "AppBorrowingRules",
                columns: new[] { "AcademicYearId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppChapterDocuments_AssignedSubjectChapterId",
                table: "AppChapterDocuments",
                column: "AssignedSubjectChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppChapterDocuments_AssignedSubjectId",
                table: "AppChapterDocuments",
                column: "AssignedSubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppChapterProgresses_ChapterId_DivisionId",
                table: "AppChapterProgresses",
                columns: new[] { "ChapterId", "DivisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppChapterProgressLogs_ChapterId_DivisionId",
                table: "AppChapterProgressLogs",
                columns: new[] { "ChapterId", "DivisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionFeeHeadMappings_ConcessionRequestId",
                table: "AppConcessionFeeHeadMappings",
                column: "ConcessionRequestId");

            migrationBuilder.CreateIndex(
                name: "UIX_ConcessionFeeHeadMappings_RequestId_FeeHeadId",
                table: "AppConcessionFeeHeadMappings",
                columns: new[] { "ConcessionRequestId", "FeeHeadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionRequestDocuments_ConcessionRequestId",
                table: "AppConcessionRequestDocuments",
                column: "ConcessionRequestId");

            migrationBuilder.CreateIndex(
                name: "UIX_ConcessionRequestDocuments_RequestId_DocumentId",
                table: "AppConcessionRequestDocuments",
                columns: new[] { "ConcessionRequestId", "DocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionRequests_ConcessionTypeId",
                table: "AppConcessionRequests",
                column: "ConcessionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionRequests_StudentId",
                table: "AppConcessionRequests",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionRequests_StudentId_Status",
                table: "AppConcessionRequests",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppConcessionTypeDocumentFormats_ConcessionTypeDocumentId",
                table: "AppConcessionTypeDocumentFormats",
                column: "ConcessionTypeDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DeductionComponents_DisplayOrder",
                table: "AppDeductionComponents",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_DeductionComponents_TenantId_IsActive",
                table: "AppDeductionComponents",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DeductionComponents_TenantId_Name",
                table: "AppDeductionComponents",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppDivisions_StandardId",
                table: "AppDivisions",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentTypes_StudentDocumentSettingId",
                table: "AppDocumentTypes",
                column: "StudentDocumentSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_TenantId_DisplayOrder",
                table: "AppDocumentTypes",
                columns: new[] { "TenantId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_TenantId_Name",
                table: "AppDocumentTypes",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EarningComponents_DisplayOrder",
                table: "AppEarningComponents",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_EarningComponents_TenantId_IsActive",
                table: "AppEarningComponents",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EarningComponents_TenantId_Name",
                table: "AppEarningComponents",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppEducationBoards_AcademicYearId",
                table: "AppEducationBoards",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEducationMediums_EducationBoardId",
                table: "AppEducationMediums",
                column: "EducationBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEducationMediumStages_EducationMediumId",
                table: "AppEducationMediumStages",
                column: "EducationMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppElectiveGroupComponents_SubjectId",
                table: "AppElectiveGroupComponents",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppElectiveGroups_AcademicMediumId",
                table: "AppElectiveGroups",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAdditionalDetails_EmployeeId_Unique",
                table: "AppEmployeeAdditionalDetails",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAdditionalDetails_TenantId",
                table: "AppEmployeeAdditionalDetails",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeeAttendances_EducationMediumStageId",
                table: "AppEmployeeAttendances",
                column: "EducationMediumStageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeeAttendances_EmployeeId",
                table: "AppEmployeeAttendances",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAttendances_Tenant_Date",
                table: "AppEmployeeAttendances",
                columns: new[] { "TenantId", "AttendanceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAttendances_Tenant_Date_Stage_Status",
                table: "AppEmployeeAttendances",
                columns: new[] { "TenantId", "AttendanceDate", "EducationMediumStageId", "StatusType" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAttendances_Tenant_Employee_Date",
                table: "AppEmployeeAttendances",
                columns: new[] { "TenantId", "EmployeeId", "AttendanceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_DocumentNameId",
                table: "AppEmployeeDocuments",
                columns: new[] { "EmployeeId", "DocumentNameId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFamilyMembers_EmployeeId",
                table: "AppEmployeeFamilyMembers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFamilyMembers_EmployeeId_RelationType_Unique",
                table: "AppEmployeeFamilyMembers",
                columns: new[] { "EmployeeId", "RelationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeeICardTemplates_TenantId_AcademicYearId_MediumSta~",
                table: "AppEmployeeICardTemplates",
                columns: new[] { "TenantId", "AcademicYearId", "MediumStageId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeeLeavePolicies_AcademicYearId",
                table: "AppEmployeeLeavePolicies",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeavePolicies_LeaveTypeId",
                table: "AppEmployeeLeavePolicies",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_IsActive",
                table: "AppEmployeeLeavePolicies",
                columns: new[] { "TenantId", "AcademicYearId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_PolicyName",
                table: "AppEmployeeLeavePolicies",
                columns: new[] { "TenantId", "AcademicYearId", "PolicyName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeeLeaves_EmployeeId",
                table: "AppEmployeeLeaves",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeeLeaves_LeaveTypeId",
                table: "AppEmployeeLeaves",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaves_Overlap",
                table: "AppEmployeeLeaves",
                columns: new[] { "TenantId", "EmployeeId", "Status", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaves_Tenant_PublicId",
                table: "AppEmployeeLeaves",
                columns: new[] { "TenantId", "PublicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeeLeaveTypes_AcademicYearId",
                table: "AppEmployeeLeaveTypes",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveTypes_TenantId_AcademicYearId_IsActive",
                table: "AppEmployeeLeaveTypes",
                columns: new[] { "TenantId", "AcademicYearId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveTypes_TenantId_AcademicYearId_Type",
                table: "AppEmployeeLeaveTypes",
                columns: new[] { "TenantId", "AcademicYearId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOrganizationAssignments_EducationMediumStageId",
                table: "AppEmployeeOrganizationAssignments",
                column: "EducationMediumStageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOrganizationAssignments_EmployeeId",
                table: "AppEmployeeOrganizationAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOrganizationAssignments_Tenant_Employee_Stage_Unique",
                table: "AppEmployeeOrganizationAssignments",
                columns: new[] { "TenantId", "EmployeeId", "EducationMediumStageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeePayrollComponents_EmployeeId",
                table: "AppEmployeePayrollComponents",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeePayrollComponents_EmployeeId_IsEarning",
                table: "AppEmployeePayrollComponents",
                columns: new[] { "EmployeeId", "IsEarning" });

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeePayrollDetails_EmployeeId",
                table: "AppEmployeePayrollDetails",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployees_ShiftId",
                table: "AppEmployees",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Email",
                table: "AppEmployees",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ReportingManagerId",
                table: "AppEmployees",
                column: "ReportingManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_RoleId",
                table: "AppEmployees",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_AdharNo_Unique",
                table: "AppEmployees",
                columns: new[] { "TenantId", "AdharNo" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_EmployeeCode_Unique",
                table: "AppEmployees",
                columns: new[] { "TenantId", "EmployeeCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_PanNo_Unique",
                table: "AppEmployees",
                columns: new[] { "TenantId", "PanNo" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_Status",
                table: "AppEmployees",
                columns: new[] { "TenantId", "EmployeeStatusType" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_Status_EffectiveDate",
                table: "AppEmployees",
                columns: new[] { "TenantId", "EmployeeStatusType", "StatusEffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "AppEmployees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEntranceExamSlots_AcademicYearId_StartDate_TenantId",
                table: "AppEntranceExamSlots",
                columns: new[] { "AcademicYearId", "StartDate", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppEvents_AcademicYearId_TenantId",
                table: "AppEvents",
                columns: new[] { "AcademicYearId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppEvents_EventTypeId",
                table: "AppEvents",
                column: "EventTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEvents_MeetingDate",
                table: "AppEvents",
                column: "MeetingDate");

            migrationBuilder.CreateIndex(
                name: "IX_AppEvents_TenantId",
                table: "AppEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEvents_Title_TenantId",
                table: "AppEvents",
                columns: new[] { "Title", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppEventTypes_Name_TenantId",
                table: "AppEventTypes",
                columns: new[] { "Name", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppEventTypes_TenantId",
                table: "AppEventTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamDivisions_ExamId",
                table: "AppExamDivisions",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamMarks_ExamId_StudentId_SubjectId",
                table: "AppExamMarks",
                columns: new[] { "ExamId", "StudentId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatAllocations_ExamId_StudentId",
                table: "AppExamSeatAllocations",
                columns: new[] { "ExamId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSubjectDivisions_ExamSubjectId",
                table: "AppExamSubjectDivisions",
                column: "ExamSubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSubjects_ExamId",
                table: "AppExamSubjects",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamTypes_Name_AcademicMediumId",
                table: "AppExamTypes",
                columns: new[] { "Name", "AcademicMediumId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppFloors_BuildingId",
                table: "AppFloors",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppGradeRanges_GradingScaleId",
                table: "AppGradeRanges",
                column: "GradingScaleId");

            migrationBuilder.CreateIndex(
                name: "AppGroupEmployeeRules_GroupId_SelectionType",
                table: "AppGroupEmployeeRules",
                columns: new[] { "GroupId", "SelectionType" });

            migrationBuilder.CreateIndex(
                name: "AppGroupMembers_GroupId",
                table: "AppGroupMembers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "AppGroupMembers_Unique",
                table: "AppGroupMembers",
                columns: new[] { "GroupId", "MemberType", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "AppGroupMembers_UserId",
                table: "AppGroupMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "AppGroups_AcademicYearId_Tenant",
                table: "AppGroups",
                columns: new[] { "AcademicYearId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "AppGroups_Name_Tenant",
                table: "AppGroups",
                columns: new[] { "Name", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "AppGroups_Tenant",
                table: "AppGroups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "AppGroupStudentRules_GroupId_SelectionType",
                table: "AppGroupStudentRules",
                columns: new[] { "GroupId", "SelectionType" });

            migrationBuilder.CreateIndex(
                name: "AppHolidays_AcademicYearId_Tenant",
                table: "AppHolidays",
                columns: new[] { "AcademicYearId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "AppHolidays_DateRange",
                table: "AppHolidays",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "AppHolidays_HolidayNameTenant",
                table: "AppHolidays",
                columns: new[] { "HolidayName", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "AppHolidays_Tenant",
                table: "AppHolidays",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "AppHolidays_TypeId",
                table: "AppHolidays",
                column: "HolidayTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppHolidayTypes_Name_TenantId",
                table: "AppHolidayTypes",
                columns: new[] { "Name", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppHolidayTypes_TenantId",
                table: "AppHolidayTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_AcademicYearId",
                table: "AppHomeworkAssignments",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_HomeworkAssignmentTypeId",
                table: "AppHomeworkAssignments",
                column: "HomeworkAssignmentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_HomeworkTypeId",
                table: "AppHomeworkAssignments",
                column: "HomeworkTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_StandardId",
                table: "AppHomeworkAssignments",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_SubjectId",
                table: "AppHomeworkAssignments",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_TenantId",
                table: "AppHomeworkAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkDivisionSchedules_DivisionId",
                table: "AppHomeworkDivisionSchedules",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkDivisionSchedules_HomeworkId",
                table: "AppHomeworkDivisionSchedules",
                column: "HomeworkId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkDivisionSchedules_HomeworkId_DivisionId",
                table: "AppHomeworkDivisionSchedules",
                columns: new[] { "HomeworkId", "DivisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkDivisionSchedules_PublishDate",
                table: "AppHomeworkDivisionSchedules",
                column: "PublishDate",
                filter: "\"PublishDate\" IS NOT NULL AND \"IsPublished\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkTypes_AcademicMediumId_Name",
                table: "AppHomeworkTypes",
                columns: new[] { "AcademicMediumId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppHouseSettings_AcademicMediumId",
                table: "AppHouseSettings",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppInquiryPreferences_TenantId_AcademicYearId_PreferenceName",
                table: "AppInquiryPreferences",
                columns: new[] { "TenantId", "AcademicYearId", "PreferenceName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLectureSlotConfigs_StandardId",
                table: "AppLectureSlotConfigs",
                column: "StandardId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLectureSlotGroups_LectureSlotConfigId",
                table: "AppLectureSlotGroups",
                column: "LectureSlotConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLectureSlotTimeSlots_LectureSlotGroupId",
                table: "AppLectureSlotTimeSlots",
                column: "LectureSlotGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCharges_BookIssueId",
                table: "AppLibraryCharges",
                column: "BookIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCharges_ChargeCategoryId",
                table: "AppLibraryCharges",
                column: "ChargeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCharges_MemberId",
                table: "AppLibraryCharges",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryShelves_SectionId",
                table: "AppLibraryShelves",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPayrollComponentVersions_TenantId_IsEarning_ComponentId_~",
                table: "AppPayrollComponentVersions",
                columns: new[] { "TenantId", "IsEarning", "ComponentId", "EffectiveMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_AppProfessionalReviewCycles_TenantId_Status",
                table: "AppProfessionalReviewCycles",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppProfessionalReviews_TenantId_CycleId_EmployeeId",
                table: "AppProfessionalReviews",
                columns: new[] { "TenantId", "CycleId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProvidentFunds_TenantId_Code",
                table: "AppProvidentFunds",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProvidentFunds_TenantId_Name",
                table: "AppProvidentFunds",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppRooms_BuildingId",
                table: "AppRooms",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppRooms_FloorId",
                table: "AppRooms",
                column: "FloorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppRooms_RoomTypeId",
                table: "AppRooms",
                column: "RoomTypeId");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SeedExecutionMarkers_Tenant_Seeder_Version_Profile"
                ON "AppSeedExecutionMarkers" ("TenantId", "SeederName", "Version", "Profile");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StaffAttendanceDayLocks_Tenant_Date",
                table: "AppStaffAttendanceDayLocks",
                columns: new[] { "TenantId", "LockDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStandards_EducationMediumStageId",
                table: "AppStandards",
                column: "EducationMediumStageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStandards_ShiftId",
                table: "AppStandards",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStandardSubjectAssignments_ElectiveGroupId",
                table: "AppStandardSubjectAssignments",
                column: "ElectiveGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStandardSubjectAssignments_StandardId",
                table: "AppStandardSubjectAssignments",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStandardSubjectAssignments_SubjectId",
                table: "AppStandardSubjectAssignments",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAdditionalDetails_HouseId",
                table: "AppStudentAdditionalDetails",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAdditionalDetails_InquiryPreferenceId",
                table: "AppStudentAdditionalDetails",
                column: "InquiryPreferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentContactDetails_StudentId",
                table: "AppStudentContactDetails",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocuments_DocumentTypeId",
                table: "AppStudentDocuments",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocuments_StudentId",
                table: "AppStudentDocuments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocuments_StudentId_DocumentTypeId",
                table: "AppStudentDocuments",
                columns: new[] { "StudentId", "DocumentTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentElectiveSelections_StudentId_ElectiveGroupId",
                table: "AppStudentElectiveSelections",
                columns: new[] { "StudentId", "ElectiveGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubmissions_AssignmentId",
                table: "AppStudentHomeworkAssignmentSubmissions",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubmissions_DivisionId",
                table: "AppStudentHomeworkAssignmentSubmissions",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubmissions_HomeworkAssignmentId",
                table: "AppStudentHomeworkAssignmentSubmissions",
                column: "HomeworkAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubmissions_Status",
                table: "AppStudentHomeworkAssignmentSubmissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubmissions_Student_Assignment",
                table: "AppStudentHomeworkAssignmentSubmissions",
                columns: new[] { "StudentId", "AssignmentId" },
                unique: true,
                filter: "\"AssignmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubmissions_Student_Homework",
                table: "AppStudentHomeworkAssignmentSubmissions",
                columns: new[] { "StudentId", "HomeworkAssignmentId" },
                unique: true,
                filter: "\"AssignmentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubmissions_StudentId",
                table: "AppStudentHomeworkAssignmentSubmissions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaves_IsDeleted",
                table: "AppStudentLeaves",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaves_LeaveDate",
                table: "AppStudentLeaves",
                column: "LeaveDate");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaves_Status",
                table: "AppStudentLeaves",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaves_StudentId",
                table: "AppStudentLeaves",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaves_StudentLeaveTypeId",
                table: "AppStudentLeaves",
                column: "StudentLeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaves_TenantId",
                table: "AppStudentLeaves",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaveTypes_IsActive",
                table: "AppStudentLeaveTypes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaveTypes_LeaveTypeName",
                table: "AppStudentLeaveTypes",
                column: "LeaveTypeName");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaveTypes_TenantId_LeaveTypeName",
                table: "AppStudentLeaveTypes",
                columns: new[] { "TenantId", "LeaveTypeName" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_AcademicBoardId",
                table: "AppStudents",
                column: "AcademicBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_AcademicMediumId",
                table: "AppStudents",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_AcademicYearId_Status",
                table: "AppStudents",
                columns: new[] { "AcademicYearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_AdmissionApprovalStatus_DivisionId",
                table: "AppStudents",
                columns: new[] { "AdmissionApprovalStatus", "DivisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_BankDetailId",
                table: "AppStudents",
                column: "BankDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_CommunicationDetailId",
                table: "AppStudents",
                column: "CommunicationDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_DivisionId",
                table: "AppStudents",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_MedicalRecordId",
                table: "AppStudents",
                column: "MedicalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_PreviousSchoolDetailId",
                table: "AppStudents",
                column: "PreviousSchoolDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_StandardId",
                table: "AppStudents",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentStatusHistories_StudentId",
                table: "AppStudentStatusHistories",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSubjectChapters_AssignedSubjectId",
                table: "AppSubjectChapters",
                column: "AssignedSubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSubjects_EducationMediumId",
                table: "AppSubjects",
                column: "EducationMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeacherSubjectAllocations_DivisionId",
                table: "AppTeacherSubjectAllocations",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeacherSubjectAllocations_StandardId",
                table: "AppTeacherSubjectAllocations",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeacherSubjectAllocations_StandardId_DivisionId_SubjectId",
                table: "AppTeacherSubjectAllocations",
                columns: new[] { "StandardId", "DivisionId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTeacherSubjectAllocations_SubjectId",
                table: "AppTeacherSubjectAllocations",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTimetables_StandardId_DivisionId",
                table: "AppTimetables",
                columns: new[] { "StandardId", "DivisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTimetableSlots_TimetableId",
                table: "AppTimetableSlots",
                column: "TimetableId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTimetableSlotSubjects_TimetableSlotId",
                table: "AppTimetableSlotSubjects",
                column: "TimetableSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_AppWeeklyLectureSlots_StandardId",
                table: "AppWeeklyLectureSlots",
                column: "StandardId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppWeeklyLectureSlotSubjects_WeeklyLectureSlotId",
                table: "AppWeeklyLectureSlotSubjects",
                column: "WeeklyLectureSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargeCollectionRecords_FeeRecordMetadataId",
                table: "ChargeCollectionRecords",
                column: "FeeRecordMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargeCollectionRecords_StudentId",
                table: "ChargeCollectionRecords",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_AcademicYearId_Status",
                table: "Charges",
                columns: new[] { "AcademicYearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_ChargeCategoryId",
                table: "Charges",
                column: "ChargeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_DueDate",
                table: "Charges",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_StudentId",
                table: "Charges",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionTypeDocuments_ConcessionTypeId",
                table: "ConcessionTypeDocuments",
                column: "ConcessionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DueFees_TenantId_StudentId_FeeHeadId_Month_Year",
                table: "DueFees",
                columns: new[] { "TenantId", "StudentId", "FeeHeadId", "Month", "Year" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_FeeCollectionDueFeeAllocations_DueFeeId",
                table: "FeeCollectionDueFeeAllocations",
                column: "DueFeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeCollectionDueFeeAllocations_FeeRecordMetadataId",
                table: "FeeCollectionDueFeeAllocations",
                column: "FeeRecordMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeCollectionRecords_FeeRecordMetadataId",
                table: "FeeCollectionRecords",
                column: "FeeRecordMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeCollectionRecords_StudentId",
                table: "FeeCollectionRecords",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeHeads_FeeCategoryId",
                table: "FeeHeads",
                column: "FeeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FeePaymentReceipts_FeeCollectionTransactionId",
                table: "FeePaymentReceipts",
                column: "FeeCollectionTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_FeePaymentReceipts_ListFilter",
                table: "FeePaymentReceipts",
                columns: new[] { "TenantId", "AcademicYearId", "BoardId", "EducationMediumId", "ReceiptKind" });

            migrationBuilder.CreateIndex(
                name: "IX_FeePaymentReceipts_ReconciliationFilter",
                table: "FeePaymentReceipts",
                columns: new[] { "TenantId", "AcademicYearId", "ReceiptKind", "VerificationStatus", "ReceiptDate" });

            migrationBuilder.CreateIndex(
                name: "UX_FeePaymentReceipts_TenantId_ReceiptNumber",
                table: "FeePaymentReceipts",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeeRecordMetadatas_StudentId_AcademicYearId",
                table: "FeeRecordMetadatas",
                columns: new[] { "StudentId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "UX_FeeRecordMetadatas_TenantId_ReceiptNumber",
                table: "FeeRecordMetadatas",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeeRefundLines_DueFeeId",
                table: "FeeRefundLines",
                column: "DueFeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeRefundLines_FeeRefundRequestId",
                table: "FeeRefundLines",
                column: "FeeRefundRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeRefundRequests_Student_Year_Status",
                table: "FeeRefundRequests",
                columns: new[] { "StudentId", "AcademicYearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_FeeRefundRequests_TenantId_VoucherNumber",
                table: "FeeRefundRequests",
                columns: new[] { "TenantId", "VoucherNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureDetails_FeeHeadId",
                table: "FeeStructureDetails",
                column: "FeeHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureDetails_FeeStructureId",
                table: "FeeStructureDetails",
                column: "FeeStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureMetadatas_AcademicBoardId",
                table: "FeeStructureMetadatas",
                column: "AcademicBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureMetadatas_AcademicMediumId",
                table: "FeeStructureMetadatas",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureMetadatas_AcademicYearId",
                table: "FeeStructureMetadatas",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureMetadatas_ReceiptBookId",
                table: "FeeStructureMetadatas",
                column: "ReceiptBookId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReceiptNumberSequences_Tenant_Year_Kind",
                table: "FinanceReceiptNumberSequences",
                columns: new[] { "TenantId", "Year", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportBatches_ExternalCorrelationId",
                table: "HR_AttendanceImportBatches",
                column: "ExternalCorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportBatches_ImportedAt",
                table: "HR_AttendanceImportBatches",
                column: "ImportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportBatches_PayrollMonthYear",
                table: "HR_AttendanceImportBatches",
                column: "PayrollMonthYear");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportBatches_RunSource",
                table: "HR_AttendanceImportBatches",
                column: "RunSource");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportBatches_Status",
                table: "HR_AttendanceImportBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportBatches_TenantId",
                table: "HR_AttendanceImportBatches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportRows_BatchId",
                table: "HR_AttendanceImportRows",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportRows_BatchId_RowNumber",
                table: "HR_AttendanceImportRows",
                columns: new[] { "BatchId", "RowNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportRows_TenantId",
                table: "HR_AttendanceImportRows",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendancePolicies_Tenant_AcademicYear_Default",
                table: "HR_AttendancePolicies",
                columns: new[] { "TenantId", "AcademicYearId", "IsDefault" },
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendancePolicies_Tenant_AcademicYear_Name",
                table: "HR_AttendancePolicies",
                columns: new[] { "TenantId", "AcademicYearId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_AuditEvents_Tenant_AcademicYear_Time",
                table: "HR_AuditEvents",
                columns: new[] { "TenantId", "AcademicYearId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_AuditEvents_Tenant_Action_Time",
                table: "HR_AuditEvents",
                columns: new[] { "TenantId", "Action", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_AuditEvents_Tenant_Entity_Time",
                table: "HR_AuditEvents",
                columns: new[] { "TenantId", "EntityType", "EntityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_DashboardPreviews_Status_RefreshStartedAt",
                table: "HR_DashboardPreviews",
                columns: new[] { "Status", "RefreshStartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_DashboardPreviews_Tenant_Key",
                table: "HR_DashboardPreviews",
                columns: new[] { "TenantId", "DashboardKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollAdjustments_Tenant_Month_Employee",
                table: "HR_PayrollAdjustments",
                columns: new[] { "TenantId", "MonthYear", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollPolicies_Tenant_AcademicYear_Default",
                table: "HR_PayrollPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "IsDefault" },
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollPolicies_Tenant_AcademicYear_Name",
                table: "HR_PayrollPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollRunItems_Month_ReviewStatus",
                table: "HR_PayrollRunItems",
                columns: new[] { "MonthYear", "ReviewStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollRunItems_Tenant_Month_Employee",
                table: "HR_PayrollRunItems",
                columns: new[] { "TenantId", "MonthYear", "EmployeeId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollRunItems_Tenant_Month_Employee_Locked",
                table: "HR_PayrollRunItems",
                columns: new[] { "TenantId", "MonthYear", "EmployeeId", "IsLocked" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollRunReviews_Month_Employee",
                table: "HR_PayrollRunReviews",
                columns: new[] { "MonthYear", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollRunReviews_Tenant_Month_Employee",
                table: "HR_PayrollRunReviews",
                columns: new[] { "TenantId", "MonthYear", "EmployeeId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PolicyAssignments_AttendancePolicy_Impact",
                table: "HR_PolicyAssignments",
                columns: new[] { "TenantId", "AcademicYearId", "IsActive", "AttendancePolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PolicyAssignments_PayrollPolicy_Impact",
                table: "HR_PolicyAssignments",
                columns: new[] { "TenantId", "AcademicYearId", "IsActive", "PayrollPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PolicyAssignments_Resolver",
                table: "HR_PolicyAssignments",
                columns: new[] { "AcademicYearId", "TargetType", "TargetKey", "EffectiveFrom", "EffectiveTo", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PolicyAssignments_Target_Active",
                table: "HR_PolicyAssignments",
                columns: new[] { "TenantId", "AcademicYearId", "TargetType", "TargetKey", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PolicyVersions_Tenant_Type_AcademicYear",
                table: "HR_PolicyVersions",
                columns: new[] { "TenantId", "PolicyType", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PolicyVersions_Tenant_Type_Policy_Version",
                table: "HR_PolicyVersions",
                columns: new[] { "TenantId", "PolicyType", "PolicyId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_ReportExportJobs_Tenant_Status_Expires",
                table: "HR_ReportExportJobs",
                columns: new[] { "TenantId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_ReportExportJobs_Tenant_User_Created",
                table: "HR_ReportExportJobs",
                columns: new[] { "TenantId", "RequestedByUserId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_SalarySlips_EmployeeId",
                table: "HR_SalarySlips",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_SalarySlips_EmployeeId_MonthYear",
                table: "HR_SalarySlips",
                columns: new[] { "EmployeeId", "MonthYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_SalarySlips_Status",
                table: "HR_SalarySlips",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HR_SalarySlips_Tenant_Month_Employee_Status",
                table: "HR_SalarySlips",
                columns: new[] { "TenantId", "MonthYear", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_SalarySlips_TenantId",
                table: "HR_SalarySlips",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_SalarySlips_Year_Month",
                table: "HR_SalarySlips",
                columns: new[] { "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_WorkCalendarHolidays_AcademicYear_Date",
                table: "HR_WorkCalendarHolidays",
                columns: new[] { "TenantId", "AcademicYearId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_WorkCalendarHolidays_AcademicYear_Range",
                table: "HR_WorkCalendarHolidays",
                columns: new[] { "TenantId", "AcademicYearId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_WorkCalendarHolidays_Policy_Date_Name",
                table: "HR_WorkCalendarHolidays",
                columns: new[] { "TenantId", "WorkCalendarPolicyId", "Date", "HolidayName" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_WorkCalendarPolicies_Tenant_AcademicYear_Name",
                table: "HR_WorkCalendarPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictApplications_ClientId",
                table: "OpenIddictApplications",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type",
                table: "OpenIddictAuthorizations",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictScopes_Name",
                table: "OpenIddictScopes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type",
                table: "OpenIddictTokens",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_AuthorizationId",
                table: "OpenIddictTokens",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ReferenceId",
                table: "OpenIddictTokens",
                column: "ReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethodRecords_FeeRecordMetadataId",
                table: "PaymentMethodRecords",
                column: "FeeRecordMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethodRecords_StudentId",
                table: "PaymentMethodRecords",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptBooks_AcademicYearId",
                table: "ReceiptBooks",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptBooks_BoardId",
                table: "ReceiptBooks",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptBooks_MediumId",
                table: "ReceiptBooks",
                column: "MediumId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptBooks_MediumStageId",
                table: "ReceiptBooks",
                column: "MediumStageId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBankDetails_AccountNo",
                table: "StudentBankDetails",
                column: "AccountNo");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBankDetails_IFSCCode",
                table: "StudentBankDetails",
                column: "IFSCCode");

            migrationBuilder.CreateIndex(
                name: "UX_StudentFeeRecords_StudentId_AcademicYearId",
                table: "StudentFeeRecords",
                columns: new[] { "StudentId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeStructures_StudentId",
                table: "StudentFeeStructures",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentNotes_StudentId",
                table: "StudentNotes",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserScopeAssignments_Lookup",
                table: "UserScopeAssignments",
                columns: new[] { "TenantId", "UserId", "IsActive", "IsDeleted" })
                .Annotation("Npgsql:IndexInclude", new[] { "EducationBoardId", "EducationMediumId", "EducationMediumStageId", "StandardId", "DivisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserScopeAssignments_Role",
                table: "UserScopeAssignments",
                columns: new[] { "TenantId", "RoleName", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UQ_UserScopeAssignment",
                table: "UserScopeAssignments",
                columns: new[] { "TenantId", "UserId", "RoleName", "AcademicYearId", "EducationBoardId", "EducationMediumId", "EducationMediumStageId", "StandardId", "DivisionId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbpAuditLogActions");

            migrationBuilder.DropTable(
                name: "AbpBackgroundJobs");

            migrationBuilder.DropTable(
                name: "AbpBlobs");

            migrationBuilder.DropTable(
                name: "AbpClaimTypes");

            migrationBuilder.DropTable(
                name: "AbpEntityPropertyChanges");

            migrationBuilder.DropTable(
                name: "AbpFeatureGroups");

            migrationBuilder.DropTable(
                name: "AbpFeatures");

            migrationBuilder.DropTable(
                name: "AbpFeatureValues");

            migrationBuilder.DropTable(
                name: "AbpLinkUsers");

            migrationBuilder.DropTable(
                name: "AbpOrganizationUnitRoles");

            migrationBuilder.DropTable(
                name: "AbpPermissionGrants");

            migrationBuilder.DropTable(
                name: "AbpPermissionGroups");

            migrationBuilder.DropTable(
                name: "AbpPermissions");

            migrationBuilder.DropTable(
                name: "AbpRoleClaims");

            migrationBuilder.DropTable(
                name: "AbpSecurityLogs");

            migrationBuilder.DropTable(
                name: "AbpSessions");

            migrationBuilder.DropTable(
                name: "AbpSettingDefinitions");

            migrationBuilder.DropTable(
                name: "AbpSettings");

            migrationBuilder.DropTable(
                name: "AbpTenantConnectionStrings");

            migrationBuilder.DropTable(
                name: "AbpUserClaims");

            migrationBuilder.DropTable(
                name: "AbpUserDelegations");

            migrationBuilder.DropTable(
                name: "AbpUserLogins");

            migrationBuilder.DropTable(
                name: "AbpUserOrganizationUnits");

            migrationBuilder.DropTable(
                name: "AbpUserRoles");

            migrationBuilder.DropTable(
                name: "AbpUserTokens");

            migrationBuilder.DropTable(
                name: "AnnouncementTypes");

            migrationBuilder.DropTable(
                name: "AppAcademicYearPermissions");

            migrationBuilder.DropTable(
                name: "AppAllocatedTeachers");

            migrationBuilder.DropTable(
                name: "AppAssignmentDivisionSchedules");

            migrationBuilder.DropTable(
                name: "AppAttendanceEntries");

            migrationBuilder.DropTable(
                name: "AppAutomatedNotificationsSettings");

            migrationBuilder.DropTable(
                name: "AppBookCategories");

            migrationBuilder.DropTable(
                name: "AppBookCopies");

            migrationBuilder.DropTable(
                name: "AppBookIssues");

            migrationBuilder.DropTable(
                name: "AppBookReservations");

            migrationBuilder.DropTable(
                name: "AppBorrowingRules");

            migrationBuilder.DropTable(
                name: "AppChapterDocuments");

            migrationBuilder.DropTable(
                name: "AppChapterProgresses");

            migrationBuilder.DropTable(
                name: "AppChapterProgressLogs");

            migrationBuilder.DropTable(
                name: "AppClassTeachers");

            migrationBuilder.DropTable(
                name: "AppConcessionFeeHeadMappings");

            migrationBuilder.DropTable(
                name: "AppConcessionRequestDocuments");

            migrationBuilder.DropTable(
                name: "AppConcessionTypeDocumentFormats");

            migrationBuilder.DropTable(
                name: "AppDeductionComponents");

            migrationBuilder.DropTable(
                name: "AppDivisionPermissions");

            migrationBuilder.DropTable(
                name: "AppDocumentSettings");

            migrationBuilder.DropTable(
                name: "AppEarningComponents");

            migrationBuilder.DropTable(
                name: "AppEducationBoardPermissions");

            migrationBuilder.DropTable(
                name: "AppEducationMediumPermissions");

            migrationBuilder.DropTable(
                name: "AppEducationMediumStagePermissions");

            migrationBuilder.DropTable(
                name: "AppElectiveGroupComponents");

            migrationBuilder.DropTable(
                name: "AppEmployeeAdditionalDetails");

            migrationBuilder.DropTable(
                name: "AppEmployeeAttendances");

            migrationBuilder.DropTable(
                name: "AppEmployeeDocuments");

            migrationBuilder.DropTable(
                name: "AppEmployeeFamilyMembers");

            migrationBuilder.DropTable(
                name: "AppEmployeeICardTemplates");

            migrationBuilder.DropTable(
                name: "AppEmployeeLeaves");

            migrationBuilder.DropTable(
                name: "AppEmployeeOrganizationAssignments");

            migrationBuilder.DropTable(
                name: "AppEmployeePayrollComponents");

            migrationBuilder.DropTable(
                name: "AppEmployeePayrollDetails");

            migrationBuilder.DropTable(
                name: "AppEntranceExamSlots");

            migrationBuilder.DropTable(
                name: "AppEvents");

            migrationBuilder.DropTable(
                name: "AppEventTypes");

            migrationBuilder.DropTable(
                name: "AppExamDivisions");

            migrationBuilder.DropTable(
                name: "AppExamMarks");

            migrationBuilder.DropTable(
                name: "AppExamSeatAllocations");

            migrationBuilder.DropTable(
                name: "AppExamSubjectDivisions");

            migrationBuilder.DropTable(
                name: "AppExamTypes");

            migrationBuilder.DropTable(
                name: "AppGradeRanges");

            migrationBuilder.DropTable(
                name: "AppGroupEmployeeRules");

            migrationBuilder.DropTable(
                name: "AppGroupMembers");

            migrationBuilder.DropTable(
                name: "AppGroupStudentRules");

            migrationBuilder.DropTable(
                name: "AppHolidays");

            migrationBuilder.DropTable(
                name: "AppHolidayTypes");

            migrationBuilder.DropTable(
                name: "AppHomeworkDivisionSchedules");

            migrationBuilder.DropTable(
                name: "AppIncomeCategoryTags");

            migrationBuilder.DropTable(
                name: "AppInstituteSetupExtraProperties");

            migrationBuilder.DropTable(
                name: "AppLectureSlotTimeSlots");

            migrationBuilder.DropTable(
                name: "AppLibraryChargeCategories");

            migrationBuilder.DropTable(
                name: "AppLibraryCharges");

            migrationBuilder.DropTable(
                name: "AppLibraryMembers");

            migrationBuilder.DropTable(
                name: "AppLibraryShelves");

            migrationBuilder.DropTable(
                name: "AppLibraryTimings");

            migrationBuilder.DropTable(
                name: "AppNotificationInfos");

            migrationBuilder.DropTable(
                name: "AppPayElements");

            migrationBuilder.DropTable(
                name: "AppPayrollComponentVersions");

            migrationBuilder.DropTable(
                name: "AppProfessionalReviewCycles");

            migrationBuilder.DropTable(
                name: "AppProfessionalReviews");

            migrationBuilder.DropTable(
                name: "AppProfessionalTaxes");

            migrationBuilder.DropTable(
                name: "AppProvidentFunds");

            migrationBuilder.DropTable(
                name: "AppRooms");

            migrationBuilder.DropTable(
                name: "AppSeedExecutionMarkers");

            migrationBuilder.DropTable(
                name: "AppStaffAttendanceDayLocks");

            migrationBuilder.DropTable(
                name: "AppStandardPermissions");

            migrationBuilder.DropTable(
                name: "AppStudentAdditionalDetails");

            migrationBuilder.DropTable(
                name: "AppStudentContactDetails");

            migrationBuilder.DropTable(
                name: "AppStudentDocuments");

            migrationBuilder.DropTable(
                name: "AppStudentElectiveSelections");

            migrationBuilder.DropTable(
                name: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropTable(
                name: "AppStudentLeaves");

            migrationBuilder.DropTable(
                name: "AppStudentManagementSettings");

            migrationBuilder.DropTable(
                name: "AppStudentStatusHistories");

            migrationBuilder.DropTable(
                name: "AppSubjectTeachers");

            migrationBuilder.DropTable(
                name: "AppTimetableSlotSubjects");

            migrationBuilder.DropTable(
                name: "AppTransactionCategoryTags");

            migrationBuilder.DropTable(
                name: "AppWeeklyLectureSlotSubjects");

            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropTable(
                name: "ChargeCategories");

            migrationBuilder.DropTable(
                name: "ChargeCollectionRecords");

            migrationBuilder.DropTable(
                name: "Charges");

            migrationBuilder.DropTable(
                name: "DueFees");

            migrationBuilder.DropTable(
                name: "FeeCollectionDueFeeAllocations");

            migrationBuilder.DropTable(
                name: "FeeCollectionRecords");

            migrationBuilder.DropTable(
                name: "FeePaymentReceipts");

            migrationBuilder.DropTable(
                name: "FeeRecordMetadatas");

            migrationBuilder.DropTable(
                name: "FeeRefundLines");

            migrationBuilder.DropTable(
                name: "FeeStructureDetails");

            migrationBuilder.DropTable(
                name: "FeeStructureMetadatas");

            migrationBuilder.DropTable(
                name: "FinanceReceiptNumberSequences");

            migrationBuilder.DropTable(
                name: "HR_AttendanceImportRows");

            migrationBuilder.DropTable(
                name: "HR_AttendancePolicies");

            migrationBuilder.DropTable(
                name: "HR_AuditEvents");

            migrationBuilder.DropTable(
                name: "HR_DashboardPreviews");

            migrationBuilder.DropTable(
                name: "HR_PayrollAdjustments");

            migrationBuilder.DropTable(
                name: "HR_PayrollPolicies");

            migrationBuilder.DropTable(
                name: "HR_PayrollRunItems");

            migrationBuilder.DropTable(
                name: "HR_PayrollRunReviews");

            migrationBuilder.DropTable(
                name: "HR_PolicyAssignments");

            migrationBuilder.DropTable(
                name: "HR_PolicyVersions");

            migrationBuilder.DropTable(
                name: "HR_ReportExportJobs");

            migrationBuilder.DropTable(
                name: "HR_SalarySlips");

            migrationBuilder.DropTable(
                name: "HR_WorkCalendarHolidays");

            migrationBuilder.DropTable(
                name: "HR_WorkCalendarPolicies");

            migrationBuilder.DropTable(
                name: "OpenIddictScopes");

            migrationBuilder.DropTable(
                name: "OpenIddictTokens");

            migrationBuilder.DropTable(
                name: "PaymentMethodRecords");

            migrationBuilder.DropTable(
                name: "StudentFamilyDetails");

            migrationBuilder.DropTable(
                name: "StudentFeeRecords");

            migrationBuilder.DropTable(
                name: "StudentFeeStructures");

            migrationBuilder.DropTable(
                name: "StudentNotes");

            migrationBuilder.DropTable(
                name: "UserScopeAssignments");

            migrationBuilder.DropTable(
                name: "AbpBlobContainers");

            migrationBuilder.DropTable(
                name: "AbpEntityChanges");

            migrationBuilder.DropTable(
                name: "AbpTenants");

            migrationBuilder.DropTable(
                name: "AbpOrganizationUnits");

            migrationBuilder.DropTable(
                name: "AppTeacherSubjectAllocations");

            migrationBuilder.DropTable(
                name: "AppAttendanceDays");

            migrationBuilder.DropTable(
                name: "AppBooks");

            migrationBuilder.DropTable(
                name: "AppSubjectChapters");

            migrationBuilder.DropTable(
                name: "AppConcessionRequests");

            migrationBuilder.DropTable(
                name: "ConcessionTypeDocuments");

            migrationBuilder.DropTable(
                name: "AppEmployeeLeavePolicies");

            migrationBuilder.DropTable(
                name: "AppEmployees");

            migrationBuilder.DropTable(
                name: "AppExamSubjects");

            migrationBuilder.DropTable(
                name: "AppGradingScales");

            migrationBuilder.DropTable(
                name: "AppGroups");

            migrationBuilder.DropTable(
                name: "TransactionCategories");

            migrationBuilder.DropTable(
                name: "AppLectureSlotGroups");

            migrationBuilder.DropTable(
                name: "AppLibrarySections");

            migrationBuilder.DropTable(
                name: "AppFloors");

            migrationBuilder.DropTable(
                name: "AppRoomTypes");

            migrationBuilder.DropTable(
                name: "AppHouseSettings");

            migrationBuilder.DropTable(
                name: "AppInquiryPreferences");

            migrationBuilder.DropTable(
                name: "AppDocumentTypes");

            migrationBuilder.DropTable(
                name: "AppAssignments");

            migrationBuilder.DropTable(
                name: "AppHomeworkAssignments");

            migrationBuilder.DropTable(
                name: "AppStudentLeaveTypes");

            migrationBuilder.DropTable(
                name: "AppTimetableSlots");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "AppWeeklyLectureSlots");

            migrationBuilder.DropTable(
                name: "FeeRefundRequests");

            migrationBuilder.DropTable(
                name: "FeeHeads");

            migrationBuilder.DropTable(
                name: "FeeStructures");

            migrationBuilder.DropTable(
                name: "ReceiptBooks");

            migrationBuilder.DropTable(
                name: "HR_AttendanceImportBatches");

            migrationBuilder.DropTable(
                name: "OpenIddictAuthorizations");

            migrationBuilder.DropTable(
                name: "AppStudents");

            migrationBuilder.DropTable(
                name: "AbpAuditLogs");

            migrationBuilder.DropTable(
                name: "AppStandardSubjectAssignments");

            migrationBuilder.DropTable(
                name: "ConcessionTypes");

            migrationBuilder.DropTable(
                name: "AppEmployeeLeaveTypes");

            migrationBuilder.DropTable(
                name: "AbpRoles");

            migrationBuilder.DropTable(
                name: "AbpUsers");

            migrationBuilder.DropTable(
                name: "AppExams");

            migrationBuilder.DropTable(
                name: "AppLectureSlotConfigs");

            migrationBuilder.DropTable(
                name: "AppBuildings");

            migrationBuilder.DropTable(
                name: "StudentDocumentSettings");

            migrationBuilder.DropTable(
                name: "AppAssignmentTypes");

            migrationBuilder.DropTable(
                name: "AppHomeworkAssignmentTypes");

            migrationBuilder.DropTable(
                name: "AppHomeworkTypes");

            migrationBuilder.DropTable(
                name: "AppTimetables");

            migrationBuilder.DropTable(
                name: "FeeCategories");

            migrationBuilder.DropTable(
                name: "OpenIddictApplications");

            migrationBuilder.DropTable(
                name: "AppDivisions");

            migrationBuilder.DropTable(
                name: "StudentBankDetails");

            migrationBuilder.DropTable(
                name: "StudentCommunicationDetails");

            migrationBuilder.DropTable(
                name: "StudentMedicalRecords");

            migrationBuilder.DropTable(
                name: "StudentPreviousSchoolDetails");

            migrationBuilder.DropTable(
                name: "AppElectiveGroups");

            migrationBuilder.DropTable(
                name: "AppSubjects");

            migrationBuilder.DropTable(
                name: "AppStandards");

            migrationBuilder.DropTable(
                name: "AppEducationMediumStages");

            migrationBuilder.DropTable(
                name: "AppShiftManagements");

            migrationBuilder.DropTable(
                name: "AppEducationMediums");

            migrationBuilder.DropTable(
                name: "AppEducationBoards");

            migrationBuilder.DropTable(
                name: "AppAcademicYears");
        }
    }
}
