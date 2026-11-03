using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

// Exercise the production predicates through SQLite without the full HTTP host/seeder.
public class NoneCategoryRecognitionSql_Tests
{
    [Theory]
    [InlineData(TransactionType.Income)]
    [InlineData(TransactionType.Expense)]
    public async Task Sql_register_query_excludes_None_and_preserves_the_other_category_amount(TransactionType type)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new RecognitionContext(new DbContextOptionsBuilder<RecognitionContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var year = Guid.NewGuid();
        var none = new TransactionCategory(Guid.NewGuid(), null, year, type, "None", null);
        var regular = new TransactionCategory(Guid.NewGuid(), null, year, type, "School", null);
        context.AddRange(none, regular);
        context.AddRange(Entry(none.Id, null, 25m, type), Entry(regular.Id, " none ", 10m, type),
            Entry(regular.Id, "School", 75m, type));
        await context.SaveChangesAsync();

        var rows = await AccountsRecognitionQuery.ExcludeNone(context.Set<Transaction>(), context.Set<TransactionCategory>()).ToListAsync();
        rows.ShouldHaveSingleItem().Amount.ShouldBe(75m);
        (await context.Set<Transaction>().ToListAsync()).Sum(x => x.Amount).ShouldBe(110m);
    }

    [Fact]
    public async Task Sql_refund_fallback_excludes_None_but_accepts_null_legacy_routing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new RecognitionContext(new DbContextOptionsBuilder<RecognitionContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var none = new TransactionCategory(Guid.NewGuid(), null, Guid.NewGuid(), TransactionType.Expense, "None", null);
        context.Add(none);
        var legacy = Refund(null, null);
        context.AddRange(Refund(none.Id, null), Refund(null, "None"), legacy);
        await context.SaveChangesAsync();

        var rows = await AccountsRecognitionQuery.ExcludeNone(context.Set<FeeRefundRequest>(), context.Set<TransactionCategory>()).ToListAsync();
        rows.ShouldHaveSingleItem().Id.ShouldBe(legacy.Id);
    }

    private static Transaction Entry(Guid category, string? name, decimal amount, TransactionType type) => Transaction.Automatic(
        Guid.NewGuid(), null, Guid.NewGuid(), category, name!, null, null, "Receipt", amount, 3,
        DateTime.UtcNow, type, Guid.NewGuid().ToString(), Guid.NewGuid(), null);

    private static FeeRefundRequest Refund(Guid? category, string? name)
    {
        var refund = (FeeRefundRequest)Activator.CreateInstance(typeof(FeeRefundRequest), nonPublic: true)!;
        typeof(FeeRefundRequest).GetProperty("Id")!.SetValue(refund, Guid.NewGuid());
        typeof(FeeRefundRequest).GetProperty("ExpenseCategoryId")!.SetValue(refund, category);
        typeof(FeeRefundRequest).GetProperty("ExpenseCategoryName")!.SetValue(refund, name);
        return refund;
    }

    private sealed class RecognitionContext(DbContextOptions<RecognitionContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder model)
        {
            Scalars<Transaction>(model, "Id", "CategoryId", "CategoryNameSnapshot", "Amount");
            Scalars<TransactionCategory>(model, "Id", "Name");
            Scalars<FeeRefundRequest>(model, "Id", "ExpenseCategoryId", "ExpenseCategoryName");
        }

        private static void Scalars<T>(ModelBuilder model, params string[] names) where T : class
        {
            var entity = model.Entity<T>();
            foreach (var property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public))
                if (!names.Contains(property.Name)) entity.Ignore(property.Name);
            entity.HasKey("Id");
        }
    }
}
