// Services/CacheService.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RadXPriceBot.Services
{
    /// <summary>
    /// Simple in-memory cache service for frequently accessed data.
    /// </summary>
    public class CacheService
    {
        private readonly ConcurrentDictionary<string, CacheItem> _cache = new();
        private readonly Action<string> _logger;

        public CacheService(Action<string> logger = null)
        {
            _logger = logger ?? (msg => Console.WriteLine($"[CacheService] {msg}"));
        }

        /// <summary>
        /// Gets a cached value or executes the factory function to get the value.
        /// </summary>
        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            var cacheExpiry = expiry ?? TimeSpan.FromMinutes(5); // Default 5 minutes

            // Check if we have a valid cached item
            if (_cache.TryGetValue(key, out var cachedItem) && 
                cachedItem.ExpiresAt > DateTime.UtcNow)
            {
                _logger($"Cache hit for key: {key}");
                return (T)cachedItem.Value;
            }

            try
            {
                _logger($"Cache miss for key: {key}, executing factory function");
                
                // Execute the factory function to get the value
                var value = await factory();
                
                // Cache the result
                var newItem = new CacheItem
                {
                    Value = value,
                    ExpiresAt = DateTime.UtcNow.Add(cacheExpiry),
                    CreatedAt = DateTime.UtcNow
                };

                _cache.AddOrUpdate(key, newItem, (k, old) => newItem);
                
                _logger($"Cached value for key: {key} (expires: {newItem.ExpiresAt:HH:mm:ss})");
                
                return value;
            }
            catch (Exception ex)
            {
                _logger($"Error executing factory function for key {key}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets a cached value synchronously or executes the factory function.
        /// </summary>
        public T GetOrSet<T>(string key, Func<T> factory, TimeSpan? expiry = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            var cacheExpiry = expiry ?? TimeSpan.FromMinutes(5);

            // Check if we have a valid cached item
            if (_cache.TryGetValue(key, out var cachedItem) && 
                cachedItem.ExpiresAt > DateTime.UtcNow)
            {
                _logger($"Cache hit for key: {key}");
                return (T)cachedItem.Value;
            }

            try
            {
                _logger($"Cache miss for key: {key}, executing factory function");
                
                var value = factory();
                
                var newItem = new CacheItem
                {
                    Value = value,
                    ExpiresAt = DateTime.UtcNow.Add(cacheExpiry),
                    CreatedAt = DateTime.UtcNow
                };

                _cache.AddOrUpdate(key, newItem, (k, old) => newItem);
                
                _logger($"Cached value for key: {key} (expires: {newItem.ExpiresAt:HH:mm:ss})");
                
                return value;
            }
            catch (Exception ex)
            {
                _logger($"Error executing factory function for key {key}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Invalidates a specific cache entry.
        /// </summary>
        public void Invalidate(string key)
        {
            if (_cache.TryRemove(key, out _))
            {
                _logger($"Invalidated cache entry for key: {key}");
            }
        }

        /// <summary>
        /// Clears all expired cache entries.
        /// </summary>
        public void ClearExpired()
        {
            var now = DateTime.UtcNow;
            var expiredKeys = new List<string>();

            foreach (var kvp in _cache)
            {
                if (kvp.Value.ExpiresAt <= now)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                _logger($"Cleared {expiredKeys.Count} expired cache entries");
            }
        }

        /// <summary>
        /// Clears all cache entries.
        /// </summary>
        public void ClearAll()
        {
            var count = _cache.Count;
            _cache.Clear();
            _logger($"Cleared all {count} cache entries");
        }

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        public CacheStats GetStats()
        {
            var now = DateTime.UtcNow;
            var totalEntries = _cache.Count;
            var expiredEntries = 0;

            foreach (var item in _cache.Values)
            {
                if (item.ExpiresAt <= now)
                    expiredEntries++;
            }

            return new CacheStats
            {
                TotalEntries = totalEntries,
                ExpiredEntries = expiredEntries,
                ActiveEntries = totalEntries - expiredEntries
            };
        }

        private class CacheItem
        {
            public object Value { get; set; }
            public DateTime ExpiresAt { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }

    /// <summary>
    /// Cache statistics information.
    /// </summary>
    public class CacheStats
    {
        public int TotalEntries { get; set; }
        public int ExpiredEntries { get; set; }
        public int ActiveEntries { get; set; }
    }
}