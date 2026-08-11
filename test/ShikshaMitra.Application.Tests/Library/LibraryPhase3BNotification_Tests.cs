using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryPhase3BNotification_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IBookAppService _bookAppService;
	private readonly ILibraryNotificationAppService _notificationAppService;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<EducationBoard, Guid> _educationBoardRepository;
	private readonly IRepository<EducationMedium, Guid> _educationMediumRepository;
	private readonly IRepository<EducationMediumStage, Guid> _educationMediumStageRepository;
	private readonly IRepository<BookCategory, Guid> _bookCategoryRepository;
	private readonly IRepository<LibrarySection, Guid> _sectionRepository;
	private readonly IRepository<LibraryShelf, Guid> _shelfRepository;
	private readonly IRepository<LibraryChargeCategory, Guid> _chargeCategoryRepository;
	private readonly IRepository<LibraryMember, Guid> _libraryMemberRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<Book, Guid> _bookRepository;
	private readonly IRepository<BookCopy, Guid> _bookCopyRepository;
	private readonly IRepository<BookIssue, Guid> _bookIssueRepository;
	private readonly IRepository<LibraryNotificationHistory, Guid> _historyRepository;

	public LibraryPhase3BNotification_Tests()
	{
		_bookAppService = GetRequiredService<IBookAppService>();
		_notificationAppService = GetRequiredService<ILibraryNotificationAppService>();
		_academicYearRepository = GetRequiredService<IRepository<AcademicYear, Guid>>();
		_educationBoardRepository = GetRequiredService<IRepository<EducationBoard, Guid>>();
		_educationMediumRepository = GetRequiredService<IRepository<EducationMedium, Guid>>();
		_educationMediumStageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_bookCategoryRepository = GetRequiredService<IRepository<BookCategory, Guid>>();
		_sectionRepository = GetRequiredService<IRepository<LibrarySection, Guid>>();
		_shelfRepository = GetRequiredService<IRepository<LibraryShelf, Guid>>();
		_chargeCategoryRepository = GetRequiredService<IRepository<LibraryChargeCategory, Guid>>();
		_libraryMemberRepository = GetRequiredService<IRepository<LibraryMember, Guid>>();
		_studentRepository = GetRequiredService<IRepository<Student, Guid>>();
		_bookRepository = GetRequiredService<IRepository<Book, Guid>>();
		_bookCopyRepository = GetRequiredService<IRepository<BookCopy, Guid>>();
		_bookIssueRepository = GetRequiredService<IRepository<BookIssue, Guid>>();
		_historyRepository = GetRequiredService<IRepository<LibraryNotificationHistory, Guid>>();
	}

	[Fact]
	public async Task Settings_Should_Seed_Defaults_With_Disabled_Future_Channels()
	{
		var settings = await _notificationAppService.GetSettingsAsync();

		settings.Items.ShouldContain(x =>
			x.EventType == LibraryNotificationEventTypes.DueDateReminder &&
			x.Channel == LibraryNotificationChannels.InApp &&
			x.IsActive &&
			x.IsChannelAvailable);
		settings.Items.ShouldContain(x =>
			x.EventType == LibraryNotificationEventTypes.DueDateReminder &&
			x.Channel == LibraryNotificationChannels.Sms &&
			!x.IsActive &&
			!x.IsChannelAvailable);
		settings.Items.ShouldContain(x =>
			x.EventType == LibraryNotificationEventTypes.MembershipExpiring &&
			x.IsComingSoon);
		settings.Channels.ShouldContain(x => x.Channel == LibraryNotificationChannels.WhatsApp && !x.IsAvailable);
	}

	[Fact]
	public async Task Settings_Update_Should_Edit_Templates_And_Block_Disabled_Channel_Enable()
	{
		var settings = await _notificationAppService.GetSettingsAsync();
		var dueInApp = settings.Items.Single(x =>
			x.EventType == LibraryNotificationEventTypes.DueDateReminder &&
			x.Channel == LibraryNotificationChannels.InApp);

		var updated = await _notificationAppService.UpdateSettingsAsync(new UpdateLibraryNotificationSettingsRequest
		{
			Items =
			[
				new LibraryNotificationSettingUpdateDto
				{
					Id = dueInApp.Id,
					IsActive = false,
					SubjectTemplate = "Custom due reminder",
					MessageTemplate = "Custom message for {{memberName}}",
					ReminderOffsetDays = 3
				}
			]
		});

		var row = updated.Items.Single(x => x.Id == dueInApp.Id);
		row.IsActive.ShouldBeFalse();
		row.SubjectTemplate.ShouldBe("Custom due reminder");
		row.ReminderOffsetDays.ShouldBe(3);

		var dueSms = updated.Items.Single(x =>
			x.EventType == LibraryNotificationEventTypes.DueDateReminder &&
			x.Channel == LibraryNotificationChannels.Sms);

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_notificationAppService.UpdateSettingsAsync(new UpdateLibraryNotificationSettingsRequest
			{
				Items =
				[
					new LibraryNotificationSettingUpdateDto
					{
						Id = dueSms.Id,
						IsActive = true
					}
				]
			}));

		exception.Code.ShouldBe(LibraryErrorCodes.NotificationChannelDisabled);
	}

	[Fact]
	public async Task RunReminders_Should_Create_Due_Reminder_History_And_Dedupe_Repeated_Run()
	{
		var settings = await _notificationAppService.GetSettingsAsync();
		await _notificationAppService.UpdateSettingsAsync(new UpdateLibraryNotificationSettingsRequest
		{
			Items = settings.Items
				.Where(x => x.EventType == LibraryNotificationEventTypes.DueDateReminder &&
				            (x.Channel == LibraryNotificationChannels.InApp || x.Channel == LibraryNotificationChannels.Email))
				.Select(x => new LibraryNotificationSettingUpdateDto
				{
					Id = x.Id,
					IsActive = true,
					ReminderOffsetDays = 2
				})
				.ToList()
		});

		var context = await SeedLibraryContextAsync(email: null);
		var copy = await CreateBookCopyAsync(context, UniqueAccession("P3B-DUE"));
		var issueId = await SeedActiveIssueAsync(context.ActiveStudentId, copy, DateTime.UtcNow.Date.AddDays(2));

		var firstRun = await _notificationAppService.RunRemindersAsync(new LibraryNotificationRunRemindersRequest
		{
			RunDate = DateTime.UtcNow.Date
		});
		var secondRun = await _notificationAppService.RunRemindersAsync(new LibraryNotificationRunRemindersRequest
		{
			RunDate = DateTime.UtcNow.Date
		});

		firstRun.Items.ShouldContain(x =>
			x.EventType == LibraryNotificationEventTypes.DueDateReminder &&
			x.RelatedEntityId == issueId &&
			x.Channel == LibraryNotificationChannels.InApp &&
			x.Status == LibraryNotificationDeliveryStatuses.Sent);
		firstRun.Items.ShouldContain(x =>
			x.EventType == LibraryNotificationEventTypes.DueDateReminder &&
			x.RelatedEntityId == issueId &&
			x.Channel == LibraryNotificationChannels.Email &&
			x.Status == LibraryNotificationDeliveryStatuses.Skipped &&
			x.ErrorMessage == "Recipient email is missing.");
		secondRun.Items.ShouldNotContain(x =>
			x.EventType == LibraryNotificationEventTypes.DueDateReminder &&
			x.RelatedEntityId == issueId);

		await WithUnitOfWorkAsync(async () =>
		{
			var rows = await _historyRepository.GetListAsync(x =>
				x.EventType == LibraryNotificationEventTypes.DueDateReminder &&
				x.RelatedEntityId == issueId);
			rows.Count.ShouldBe(2);
		});
	}

	[Fact]
	public async Task Manual_Send_Should_Record_Missing_Email_As_Skipped()
	{
		var result = await _notificationAppService.SendAsync(new SendLibraryNotificationRequest
		{
			EventType = LibraryNotificationEventTypes.FineDueReminder,
			Channel = LibraryNotificationChannels.Email,
			Subject = "Manual fine reminder",
			Message = "Please clear your Library fine.",
			Recipients =
			[
				new LibraryNotificationRecipientRequest
				{
					RecipientName = "No Email Member",
					RecipientRole = LibraryNotificationRecipientRoles.Member
				}
			]
		});

		result.Attempted.ShouldBe(1);
		result.Skipped.ShouldBe(1);
		result.Items.Single().Status.ShouldBe(LibraryNotificationDeliveryStatuses.Skipped);
		result.Items.Single().ErrorMessage.ShouldBe("Recipient email is missing.");
	}

	private async Task<LibraryTestContext> SeedLibraryContextAsync(string? email)
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var academicYearId = Guid.NewGuid();
			var educationBoardId = Guid.NewGuid();
			var educationMediumId = Guid.NewGuid();
			var educationMediumStageId = Guid.NewGuid();
			var studentId = Guid.NewGuid();

			await _academicYearRepository.InsertAsync(
				new AcademicYear(
					academicYearId,
					tenantId: null,
					$"Library Phase3B {Guid.NewGuid():N}"[..30],
					new DateTime(2026, 4, 1),
					new DateTime(2027, 3, 31)),
				autoSave: true);

			await _educationBoardRepository.InsertAsync(
				new EducationBoard(educationBoardId, tenantId: null, EducationBoardType.CBSE, academicYearId),
				autoSave: true);

			await _educationMediumRepository.InsertAsync(
				new EducationMedium(educationMediumId, tenantId: null, "English", educationBoardId),
				autoSave: true);

			await _educationMediumStageRepository.InsertAsync(
				new EducationMediumStage(
					educationMediumStageId,
					Guid.Parse("11111111-1111-1111-1111-111111111111"),
					"Primary",
					bannerPicture: null,
					educationMediumId),
				autoSave: true);

			var student = Student.CreateForAdmission(
				studentId,
				tenantId: null,
				firstName: "Library",
				middleName: null,
				lastName: "Notifier",
				educationYearId: academicYearId,
				educationBoardId: educationBoardId,
				educationMediumId: educationMediumId,
				educationMediumStageId: educationMediumStageId,
				standardId: null,
				divisionId: null,
				dateOfBirth: new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero),
				gender: GenderEnum.Male,
				primaryContactNo: "9999999999",
				email: email);
			student.AdditionalDetail!.IsLibraryMember = true;
			await _studentRepository.InsertAsync(student, autoSave: true);

			var member = new LibraryMember(
				Guid.NewGuid(),
				tenantId: null,
				studentId,
				$"LIB-{Guid.NewGuid():N}"[..20],
				isActive: true);
			await _libraryMemberRepository.InsertAsync(member, autoSave: true);

			var category = new BookCategory(
				$"Phase3B {Guid.NewGuid():N}"[..30],
				"Phase 3B notification category",
				academicYearId,
				tenantId: null);
			await _bookCategoryRepository.InsertAsync(category, autoSave: true);

			var chargeCategory = new LibraryChargeCategory(
				Guid.NewGuid(),
				tenantId: null,
				academicYearId,
				"Late Fine",
				50m,
				isFixed: false,
				"Phase 3B charge");
			await _chargeCategoryRepository.InsertAsync(chargeCategory, autoSave: true);

			var section = new LibrarySection(
				Guid.NewGuid(),
				tenantId: null,
				academicYearId,
				$"P3B Section {Guid.NewGuid():N}"[..30],
				numberOfShelf: 1);
			await _sectionRepository.InsertAsync(section, autoSave: true);

			var shelf = new LibraryShelf(Guid.NewGuid(), section.Id, "P3B-A1");
			await _shelfRepository.InsertAsync(shelf, autoSave: true);

			return new LibraryTestContext(
				academicYearId,
				category.Id,
				shelf.SectionId,
				shelf.Id,
				chargeCategory.Id,
				member.Id,
				member.StudentId);
		});
	}

	private async Task<BookCopy> CreateBookCopyAsync(LibraryTestContext context, string accession)
	{
		var bookId = Guid.Parse((await _bookAppService.CreateAsync(context.AcademicYearId, new CreateBookRequest
		{
			Title = $"Phase 3B Book {Guid.NewGuid():N}",
			Isbn = $"ISBN-{Guid.NewGuid():N}",
			Author = "Library Test Author",
			Publisher = "Library Test Publisher",
			Edition = Guid.NewGuid().ToString("N")[..8],
			Subject = "Regression",
			CategoryId = context.CategoryId,
			Language = Language.English,
			ApplicableAge = "10+",
			Price = 100m,
			Pages = 120,
			NumberOfCopies = 1,
			Copies =
			[
				new BookCopyItem
				{
					CopyNumber = 1,
					AccessionNumber = accession,
					SectionId = context.SectionId,
					ShelfId = context.ShelfId
				}
			]
		})).Id);

		return await WithUnitOfWorkAsync(async () =>
		{
			var book = await _bookRepository.GetAsync(bookId);
			book.Title.ShouldNotBeNullOrWhiteSpace();
			return await _bookCopyRepository.GetAsync(x => x.AccessionNumber == accession);
		});
	}

	private async Task<Guid> SeedActiveIssueAsync(Guid memberId, BookCopy copy, DateTime dueDate)
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var issue = new BookIssue(
				Guid.NewGuid(),
				copy.Id,
				copy.AccessionNumber,
				memberId,
				memberRole: 0,
				DateTime.UtcNow.Date,
				dueDate,
				tenantId: null);
			await _bookIssueRepository.InsertAsync(issue, autoSave: true);

			var reloadedCopy = await _bookCopyRepository.GetAsync(copy.Id);
			reloadedCopy.Status = BookCopyStatus.Issued;
			await _bookCopyRepository.UpdateAsync(reloadedCopy, autoSave: true);

			return issue.Id;
		});
	}

	private static string UniqueAccession(string prefix)
	{
		return $"TST-{prefix}-{Guid.NewGuid():N}";
	}

	private sealed record LibraryTestContext(
		Guid AcademicYearId,
		Guid CategoryId,
		Guid SectionId,
		Guid ShelfId,
		Guid ChargeCategoryId,
		Guid ActiveStudentLibraryMemberId,
		Guid ActiveStudentId);
}
