using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class ProbeReadBudgetTests
{
    [Fact]
    public void Budget_CapsAttemptsAtConfiguredMaximum()
    {
        var budget = new ProbeReadBudget(maxAttempts: 300, noSignalStopAttempts: 120, minimumScoreForDeepPhase: 40);

        budget.RegisterAttempt(attemptCount: 350, success: false);

        Assert.Equal(300, budget.AttemptsUsed);
        Assert.Equal(0, budget.RemainingAttempts);
        Assert.True(budget.IsExhausted);
    }

    [Fact]
    public void Budget_StopsForNoSignalAfterThreshold()
    {
        var budget = new ProbeReadBudget(maxAttempts: 300, noSignalStopAttempts: 120, minimumScoreForDeepPhase: 40);

        budget.RegisterAttempt(attemptCount: 120, success: false);

        Assert.True(budget.ShouldStopForNoSignal);
    }

    [Fact]
    public void Budget_PhaseTransitionRules_AreApplied()
    {
        var budget = new ProbeReadBudget(maxAttempts: 300, noSignalStopAttempts: 120, minimumScoreForDeepPhase: 40);

        Assert.False(budget.CanEnterExpandPhase);
        Assert.False(budget.CanEnterDeepPhase);

        budget.RegisterScore(25);
        Assert.True(budget.CanEnterExpandPhase);
        Assert.False(budget.CanEnterDeepPhase);

        budget.RegisterAttempt(attemptCount: 1, success: true);
        Assert.True(budget.CanEnterExpandPhase);
        Assert.False(budget.CanEnterDeepPhase);

        budget.RegisterScore(45);
        Assert.True(budget.CanEnterDeepPhase);
    }
}
