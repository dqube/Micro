namespace Micro.CQRS.Core;

// Dummy IPerformanceTracker interface for demonstration
public interface IPerformanceTracker
{
    void StartTracking(string name);
    void StopTracking(string name);
}
