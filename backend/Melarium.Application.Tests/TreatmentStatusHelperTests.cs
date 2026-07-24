using Melarium.Domain.Common;
using Melarium.Domain.Enums;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>Locks the karenca (withdrawal) date and derived status rules (SPEC-08), including the
/// two edge cases: no end date → InProgress, and zero withdrawal → straight to Completed.</summary>
public class TreatmentStatusHelperTests
{
    private static readonly DateTime Today = new(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void KarencaUntil_UsesEndDateWhenPresent_ElseStartDate()
    {
        Assert.Equal(new DateTime(2026, 1, 6), TreatmentStatusHelper.KarencaUntil(new DateTime(2026, 1, 1), null, 5));
        Assert.Equal(new DateTime(2026, 2, 11), TreatmentStatusHelper.KarencaUntil(new DateTime(2026, 1, 1), new DateTime(2026, 2, 1), 10));
    }

    [Fact]
    public void Status_NoEndDate_IsInProgress()
    {
        var s = TreatmentStatusHelper.Status(Today.AddDays(-3), endDate: null, withdrawalDays: 10, Today);
        Assert.Equal(TreatmentStatus.InProgress, s);
    }

    [Fact]
    public void Status_EndedWithZeroWithdrawal_IsCompleted_NoKarencaPhase()
    {
        var s = TreatmentStatusHelper.Status(Today.AddDays(-2), endDate: Today, withdrawalDays: 0, Today);
        Assert.Equal(TreatmentStatus.Completed, s);
    }

    [Fact]
    public void Status_WithinWithdrawalWindow_IsKarenca()
    {
        var s = TreatmentStatusHelper.Status(Today.AddDays(-5), endDate: Today.AddDays(-1), withdrawalDays: 10, Today);
        Assert.Equal(TreatmentStatus.Karenca, s); // karencaUntil = today+9 → still in karenca
    }

    [Fact]
    public void Status_AfterWithdrawalWindow_IsCompleted()
    {
        var s = TreatmentStatusHelper.Status(Today.AddDays(-30), endDate: Today.AddDays(-20), withdrawalDays: 5, Today);
        Assert.Equal(TreatmentStatus.Completed, s); // karencaUntil = today-15 → passed
    }
}
