using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Setting.BorrowingRules;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using ShikshaMitra.Library.Setting.Sections;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
    // ───────────────────────────────────────────────
    // Library — General Settings
    // ───────────────────────────────────────────────
    public DbSet<LibraryTiming> LibraryTimings { get; set; }
    public DbSet<BookCategory> BookCategories { get; set; }

    // ───────────────────────────────────────────────
    // Library — Charge Category
    // ───────────────────────────────────────────────
    public DbSet<LibraryChargeCategory> LibraryChargeCategories { get; set; }

    // ───────────────────────────────────────────────
    // Library — Borrowing Rules
    // ───────────────────────────────────────────────
    public DbSet<BorrowingRule> BorrowingRules { get; set; }

    // ───────────────────────────────────────────────
    // Library — Sections
    // ───────────────────────────────────────────────
    public DbSet<LibrarySection> LibrarySections { get; set; }
    public DbSet<LibraryShelf> LibraryShelves { get; set; }

    // ───────────────────────────────────────────────
    // Library — Books
    // ───────────────────────────────────────────────
    public DbSet<ShikshaMitra.Library.Books.Book> Books { get; set; }
    public DbSet<ShikshaMitra.Library.Books.BookCopy> BookCopies { get; set; }
    public DbSet<ShikshaMitra.Library.Books.BookReservation> BookReservations { get; set; }
    public DbSet<ShikshaMitra.Library.Books.BookIssue> BookIssues { get; set; }
    public DbSet<ShikshaMitra.Library.Books.LibraryCharge> LibraryCharges { get; set; }

    // ───────────────────────────────────────────────
    // Library — Members
    // ───────────────────────────────────────────────
    public DbSet<LibraryMember> LibraryMembers { get; set; }
}
