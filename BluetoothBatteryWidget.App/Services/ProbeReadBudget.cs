namespace BluetoothBatteryWidget.App.Services;

internal enum ReadPhase
{
    Quick = 0,
    Expand = 1,
    Deep = 2
}

internal sealed class ProbeReadBudget
{
    private readonly int _maxAttempts;
    private readonly int _noSignalStopAttempts;
    private readonly int _minimumScoreForDeepPhase;
    private int _attemptsUsed;
    private int _consecutiveFailures;

    public ProbeReadBudget(int maxAttempts, int noSignalStopAttempts, int minimumScoreForDeepPhase)
    {
        _maxAttempts = Math.Max(1, maxAttempts);
        _noSignalStopAttempts = Math.Max(1, noSignalStopAttempts);
        _minimumScoreForDeepPhase = Math.Max(0, minimumScoreForDeepPhase);
    }

    public int AttemptsUsed => _attemptsUsed;

    public int RemainingAttempts => Math.Max(0, _maxAttempts - _attemptsUsed);

    public bool HasSuccessfulRead { get; private set; }

    public int BestObservedScore { get; private set; }

    public bool IsExhausted => _attemptsUsed >= _maxAttempts;

    public bool ShouldStopForNoSignal =>
        !HasSuccessfulRead &&
        _attemptsUsed >= _noSignalStopAttempts &&
        _consecutiveFailures >= _noSignalStopAttempts;

    public bool CanEnterExpandPhase => !IsExhausted && (HasSuccessfulRead || BestObservedScore > 0);

    public bool CanEnterDeepPhase => !IsExhausted && HasSuccessfulRead && BestObservedScore >= _minimumScoreForDeepPhase;

    public void RegisterAttempt(int attemptCount, bool success)
    {
        var normalizedAttempts = Math.Max(1, attemptCount);
        _attemptsUsed = Math.Min(_maxAttempts, _attemptsUsed + normalizedAttempts);

        if (success)
        {
            HasSuccessfulRead = true;
            _consecutiveFailures = 0;
            return;
        }

        _consecutiveFailures = Math.Min(_maxAttempts, _consecutiveFailures + normalizedAttempts);
    }

    public void RegisterScore(int score)
    {
        if (score > BestObservedScore)
        {
            BestObservedScore = score;
        }
    }
}
