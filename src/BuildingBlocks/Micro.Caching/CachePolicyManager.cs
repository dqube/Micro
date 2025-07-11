using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Micro.Caching;

// Policy/CachePolicyManager.cs
public class CachePolicyManager : ICachePolicyManager
{
    private readonly ConcurrentDictionary<string, CachePolicy> _policies = new();
    private readonly ILogger<CachePolicyManager> _logger;

    public CachePolicyManager(ILogger<CachePolicyManager> logger)
    {
        _logger = logger;
    }

    public void SetPolicy(string keyPattern, CachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(keyPattern))
            throw new ArgumentException("Key pattern cannot be null or whitespace", nameof(keyPattern));

        try
        {
            _policies.AddOrUpdate(
                keyPattern,
                policy,
                (k, old) => policy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set policy for pattern {Pattern}", keyPattern);
            throw;
        }
    }

    public CachePolicy GetPolicy(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return CachePolicy.Default;

        try
        {
            var matchingPolicy = _policies
                .Where(kvp => MatchesPattern(key, kvp.Key))
                .OrderByDescending(kvp => kvp.Value.Priority)
                .FirstOrDefault();

            return matchingPolicy.Value ?? CachePolicy.Default;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get policy for key {Key}", key);
            return CachePolicy.Default;
        }
    }

    public void RemovePolicy(string keyPattern)
    {
        if (string.IsNullOrWhiteSpace(keyPattern))
            return;

        _policies.TryRemove(keyPattern, out _);
    }

    private bool MatchesPattern(string key, string pattern)
    {
        if (pattern == "*") return true;

        try
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".")
                + "$";

            return Regex.IsMatch(key, regexPattern);
        }
        catch
        {
            return false;
        }
    }
}