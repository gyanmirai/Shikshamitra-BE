using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Finance;

public class FeeRefundLineTracking_Tests
{
    private const string Connection = "Host=127.0.0.1;Port=1;Database=tracking_only;Username=tracking_only";

    [Fact]
    public void Application_assigned_line_keys_preserve_the_PostgreSql_table_definition()
    {
        using var corrected = new RefundTrackingContext(new DbContextOptionsBuilder<RefundTrackingContext>()
            .UseNpgsql(Connection).Options);
        using var previous = new GeneratedLineKeyContext(new DbContextOptionsBuilder<GeneratedLineKeyContext>()
            .UseNpgsql(Connection).Options);

        corrected.Database.GenerateCreateScript().ShouldBe(previous.Database.GenerateCreateScript());
    }

    [Theory]
    [InlineData("fee", 3750)]
    [InlineData("fee", 2500)]
    [InlineData("charge", 3750)]
    [InlineData("charge", 2500)]
    [InlineData("lateFee", 3750)]
    [InlineData("lateFee", 2500)]
    public void Replacing_lines_on_an_existing_request_inserts_new_rows_instead_of_updating_missing_rows(string source, int amount)
    {
        using var context = new RefundTrackingContext(new DbContextOptionsBuilder<RefundTrackingContext>()
            .UseNpgsql(Connection).Options);
        var request = new FeeRefundRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "REF-EDIT-TRACKING", "Cash", null, "Original reason");
        var receiptId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        FeeRefundLine Line(decimal amount) => new(Guid.NewGuid(), request.Id,
            source == "fee" ? sourceId : null, allocationId, receiptId, null,
            "Paid source", "July", 3750m, amount, amount,
            chargeId: source == "charge" ? sourceId : null,
            lateFeeDueFeeId: source == "lateFee" ? sourceId : null);
        var original = Line(3750m);
        request.AddLine(original);
        request.PinPolicy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), receiptId,
            "RCPT-TRACKING", "{}", "{}", new string('a', 64));
        request.UseSimpleWorkflow(Guid.NewGuid(), "edit-tracking");
        context.Attach(request);

        var replacement = Line(amount);
        request.ReplaceLines([replacement]);
        request.UpdateReason("Updated reason");
        context.Update(request);
        context.ChangeTracker.DetectChanges();

        context.Entry(original).State.ShouldBe(EntityState.Deleted);
        context.Entry(replacement).State.ShouldBe(EntityState.Added);
        context.Entry(request).State.ShouldBe(EntityState.Modified);
        context.Entry(request).Property(x => x.ConcurrencyStamp).Metadata.IsConcurrencyToken.ShouldBeTrue();
        request.TotalAmount.ShouldBe((decimal)amount);
        request.Status.ShouldBe(FeeRefundStatus.Submitted);
    }

    private class RefundTrackingContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new FeeRefundRequestConfiguration());
            // Keep the production refund mapping while excluding unrelated routing masters.
            var refundTypes = new[]
            {
                typeof(FeeRefundRequest), typeof(FeeRefundLine),
                typeof(FeeRefundReservation), typeof(FeeRefundSettlement)
            };
            foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                         .Where(entity => !refundTypes.Contains(entity.ClrType))
                         .Select(entity => entity.ClrType).Distinct().ToArray())
                modelBuilder.Ignore(entityType);
        }
    }

    private sealed class GeneratedLineKeyContext(DbContextOptions<GeneratedLineKeyContext> options)
        : RefundTrackingContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<FeeRefundRequest>().OwnsMany(request => request.Lines)
                .Property(line => line.Id).ValueGeneratedOnAdd();
        }
    }
}
