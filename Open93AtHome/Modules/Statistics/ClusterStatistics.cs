using System;

namespace Open93AtHome.Modules.Statistics;

public class ClusterStatistics
{
    private readonly object _lock = new object();
    private readonly long[,] bytes = new long[24, 31]; // 24 hours, 31 days
    private readonly long[,] hits = new long[24, 31];  // 24 hours, 31 days

    public ClusterStatistics()
    {
        // No need to initialize, as long[,] is automatically initialized to 0
    }

    public void Add(long traffic)
    {
        lock (_lock)
        {
            var instance = DateTime.Now;
            int day = instance.Day - 1; // Adjust for zero-based index
            int hour = instance.Hour;

            bytes[hour, day] += traffic;
            hits[hour, day] += 1;
        }
    }

    public void Add(long hitsCount, long traffic)
    {
        lock (_lock)
        {
            var instance = DateTime.Now;
            int day = instance.Day - 1; // Adjust for zero-based index
            int hour = instance.Hour;

            bytes[hour, day] += traffic;
            hits[hour, day] += hitsCount;
        }
    }

    public long GetBytes(int dayOfMonth, int hour)
    {
        return bytes[hour, dayOfMonth];
    }

    public long GetHits(int dayOfMonth, int hour)
    {
        return hits[hour, dayOfMonth];
    }

    public void SetTraffic(int dayOfMonth, int hour, long value)
    {
        bytes[hour, dayOfMonth] = value;
    }

    public void SetHits(int dayOfMonth, int hour, long value)
    {
        hits[hour, dayOfMonth] = value;
    }

    public long[,] GetRawTraffic()
    {
        return bytes;
    }

    public long[,] GetRawHits()
    {
        return hits;
    }
}
