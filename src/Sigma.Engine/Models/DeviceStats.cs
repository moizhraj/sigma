namespace Sigma.Engine.Models;

public class DeviceStats
{
    private long _totalReads;
    private long _totalWrites;
    private long _totalErrors;

    public long TotalReads => _totalReads;
    public long TotalWrites => _totalWrites;
    public long TotalErrors => _totalErrors;

    public void RecordRead() => Interlocked.Increment(ref _totalReads);
    public void RecordWrite() => Interlocked.Increment(ref _totalWrites);
    public void RecordError() => Interlocked.Increment(ref _totalErrors);
}
