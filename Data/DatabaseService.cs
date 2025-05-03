// Data/DatabaseService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RadXPriceBot.Data.Models;
using RadXPriceBot.Services;

namespace RadXPriceBot.Data
{
    public class DatabaseService
    {
        private readonly Action<string> _logger;

        public DatabaseService(Action<string> logger = null)
        {
            _logger = logger ?? (msg => Console.WriteLine(msg));
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            try
            {
                _logger("Initializing database...");
                using (var context = new AppDbContext())
                {
                    // Create database if it doesn't exist
                    context.Database.EnsureCreated();

                    _logger("Database initialized successfully");
                }
            }
            catch (Exception ex)
            {
                _logger($"Error initializing database: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        #region Token Methods

        public async Task<TokenEntity> GetOrCreateTokenAsync(TokenInfo tokenInfo)
        {
            if (tokenInfo == null) return null;

            try
            {
                using (var context = new AppDbContext())
                {
                    // Convert address to uppercase to ensure case-insensitive comparison
                    var normalizedAddress = tokenInfo.Address.ToUpperInvariant();

                    // Try to find existing token using normalized comparison
                    var token = await context.Tokens
                        .FirstOrDefaultAsync(t => t.Address.ToUpper() == normalizedAddress);

                    if (token == null)
                    {
                        // Create new token
                        token = new TokenEntity
                        {
                            Address = tokenInfo.Address,
                            Symbol = tokenInfo.Symbol,
                            Name = tokenInfo.Name,
                            Decimals = tokenInfo.Decimals,
                            TotalSupply = tokenInfo.TotalSupply,
                            UsdPrice = tokenInfo.UsdPrice,
                            LastUpdated = DateTime.UtcNow
                        };

                        context.Tokens.Add(token);
                        await context.SaveChangesAsync();
                        _logger($"Created new token in database: {token.Symbol} ({token.Address})");
                    }
                    else
                    {
                        // Update token data
                        bool changes = false;

                        if (token.Symbol != tokenInfo.Symbol)
                        {
                            token.Symbol = tokenInfo.Symbol;
                            changes = true;
                        }

                        if (token.Name != tokenInfo.Name)
                        {
                            token.Name = tokenInfo.Name;
                            changes = true;
                        }

                        if (token.Decimals != tokenInfo.Decimals)
                        {
                            token.Decimals = tokenInfo.Decimals;
                            changes = true;
                        }

                        if (token.TotalSupply != tokenInfo.TotalSupply)
                        {
                            token.TotalSupply = tokenInfo.TotalSupply;
                            changes = true;
                        }

                        if (tokenInfo.UsdPrice > 0 && token.UsdPrice != tokenInfo.UsdPrice)
                        {
                            token.UsdPrice = tokenInfo.UsdPrice;
                            changes = true;
                        }

                        if (changes)
                        {
                            token.LastUpdated = DateTime.UtcNow;
                            await context.SaveChangesAsync();
                            _logger($"Updated token in database: {token.Symbol} ({token.Address})");
                        }
                    }

                    return token;
                }
            }
            catch (Exception ex)
            {
                _logger($"Error in GetOrCreateTokenAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<TokenEntity> GetTokenByAddressAsync(string address)
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    // Convert address to uppercase for case-insensitive comparison
                    var normalizedAddress = address.ToUpperInvariant();

                    return await context.Tokens
                        .FirstOrDefaultAsync(t => t.Address.ToUpper() == normalizedAddress);
                }
            }
            catch (Exception ex)
            {
                _logger($"Error in GetTokenByAddressAsync: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Pair Methods

        public async Task<PairEntity> GetOrCreatePairAsync(PairInfo pairInfo, string factoryAddress = null)
        {
            if (pairInfo == null) return null;

            try
            {
                using (var context = new AppDbContext())
                {
                    // Convert address to uppercase for case-insensitive comparison
                    var normalizedAddress = pairInfo.Address.ToUpperInvariant();

                    // Try to find existing pair using normalized comparison
                    var pair = await context.Pairs
                        .Include(p => p.Token0)
                        .Include(p => p.Token1)
                        .FirstOrDefaultAsync(p => p.Address.ToUpper() == normalizedAddress);

                    if (pair == null)
                    {
                        // First ensure tokens exist
                        var token0 = await GetOrCreateTokenAsync(pairInfo.Token0);
                        var token1 = await GetOrCreateTokenAsync(pairInfo.Token1);

                        if (token0 == null || token1 == null)
                        {
                            _logger($"Failed to create tokens for pair {pairInfo.Address}");
                            return null;
                        }

                        // Create new pair
                        pair = new PairEntity
                        {
                            Address = pairInfo.Address,
                            Token0Id = token0.Id,
                            Token1Id = token1.Id,
                            LastPrice = pairInfo.Price,
                            Reserve0 = pairInfo.Reserve0,
                            Reserve1 = pairInfo.Reserve1,
                            Liquidity = pairInfo.Liquidity,
                            FactoryAddress = factoryAddress,
                            LastUpdated = DateTime.UtcNow
                        };

                        context.Pairs.Add(pair);
                        await context.SaveChangesAsync();

                        // Now reload with tokens included
                        pair = await context.Pairs
                            .Include(p => p.Token0)
                            .Include(p => p.Token1)
                            .FirstAsync(p => p.Id == pair.Id);

                        _logger($"Created new pair in database: {pair.Name} ({pair.Address})");

                        // Add initial price history
                        await AddPriceHistoryAsync(pair.Id, pairInfo.Price, pairInfo.Token0.UsdPrice);

                        // Add initial reserve history
                        await AddReserveHistoryAsync(pair.Id, pairInfo.Reserve0, pairInfo.Reserve1, pairInfo.Liquidity);
                    }
                    else
                    {
                        // Update pair data
                        bool changes = false;

                        if (pair.LastPrice != pairInfo.Price)
                        {
                            pair.LastPrice = pairInfo.Price;
                            changes = true;

                            // Add to price history
                            await AddPriceHistoryAsync(pair.Id, pairInfo.Price, pairInfo.Token0.UsdPrice);
                        }

                        if (pair.Reserve0 != pairInfo.Reserve0 || pair.Reserve1 != pairInfo.Reserve1)
                        {
                            pair.Reserve0 = pairInfo.Reserve0;
                            pair.Reserve1 = pairInfo.Reserve1;
                            changes = true;

                            // Only add to reserve history if reserves changed significantly
                            decimal changeThreshold = 0.005m; // 0.5% change
                            if (IsSignificantChange(pair.Reserve0, pairInfo.Reserve0, changeThreshold) ||
                                IsSignificantChange(pair.Reserve1, pairInfo.Reserve1, changeThreshold))
                            {
                                await AddReserveHistoryAsync(pair.Id, pairInfo.Reserve0, pairInfo.Reserve1, pairInfo.Liquidity);
                            }
                        }

                        if (pair.Liquidity != pairInfo.Liquidity)
                        {
                            pair.Liquidity = pairInfo.Liquidity;
                            changes = true;
                        }

                        if (factoryAddress != null && pair.FactoryAddress != factoryAddress)
                        {
                            pair.FactoryAddress = factoryAddress;
                            changes = true;
                        }

                        if (changes)
                        {
                            pair.LastUpdated = DateTime.UtcNow;
                            await context.SaveChangesAsync();
                            _logger($"Updated pair in database: {pair.Name} ({pair.Address})");
                        }

                        // Update tokens if needed
                        await GetOrCreateTokenAsync(pairInfo.Token0);
                        await GetOrCreateTokenAsync(pairInfo.Token1);
                    }

                    return pair;
                }
            }
            catch (Exception ex)
            {
                _logger($"Error in GetOrCreatePairAsync: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger($"Inner exception: {ex.InnerException.Message}");
                }
                return null;
            }
        }

        public async Task<PairEntity> GetPairByAddressAsync(string address)
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    // Convert address to uppercase for case-insensitive comparison
                    var normalizedAddress = address.ToUpperInvariant();

                    return await context.Pairs
                        .Include(p => p.Token0)
                        .Include(p => p.Token1)
                        .FirstOrDefaultAsync(p => p.Address.ToUpper() == normalizedAddress);
                }
            }
            catch (Exception ex)
            {
                _logger($"Error in GetPairByAddressAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<List<PairEntity>> GetAllPairsAsync(string factoryAddress = null)
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    var query = context.Pairs
                        .Include(p => p.Token0)
                        .Include(p => p.Token1)
                        .AsQueryable();

                    if (!string.IsNullOrEmpty(factoryAddress))
                    {
                        query = query.Where(p => p.FactoryAddress == factoryAddress);
                    }

                    // Get all pairs without ordering in the database
                    var pairs = await query.ToListAsync();

                    // Perform ordering in memory (LINQ to Objects) instead of in the database
                    return pairs
                        .OrderByDescending(p => p.Token0.Symbol == "WVTRU" && p.Token1.Symbol == "USDC.POL" ||
                                           p.Token0.Symbol == "USDC.POL" && p.Token1.Symbol == "WVTRU")
                        .ThenByDescending(p => p.Token0.Symbol == "VTRO" && p.Token1.Symbol == "USDC.POL" ||
                                            p.Token0.Symbol == "USDC.POL" && p.Token1.Symbol == "VTRO")
                        .ThenByDescending(p => p.Token0.Symbol == "USDC.POL" || p.Token1.Symbol == "USDC.POL")
                        .ThenByDescending(p => p.Liquidity) // Now this ordering happens in memory
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger($"Error in GetAllPairsAsync: {ex.Message}");
                return new List<PairEntity>();
            }
        }


        #endregion

        #region History Methods

        public async Task AddPriceHistoryAsync(int pairId, decimal price, decimal? usdPrice = null, decimal? marketCap = null,
                                             decimal? volume24h = null, string blockNumber = null, string txHash = null)
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    var priceHistory = new PriceHistoryEntity
                    {
                        PairId = pairId,
                        Timestamp = DateTime.UtcNow,
                        Price = price,
                        UsdPrice = usdPrice,
                        MarketCap = marketCap,
                        Volume24h = volume24h,
                        BlockNumber = blockNumber,
                        TransactionHash = txHash
                    };

                    context.PriceHistory.Add(priceHistory);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger($"Error in AddPriceHistoryAsync: {ex.Message}");
            }
        }

        public async Task AddReserveHistoryAsync(int pairId, decimal reserve0, decimal reserve1, decimal liquidity,
                                               string blockNumber = null, string txHash = null)
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    var reserveHistory = new ReserveHistoryEntity
                    {
                        PairId = pairId,
                        Timestamp = DateTime.UtcNow,
                        Reserve0 = reserve0,
                        Reserve1 = reserve1,
                        Liquidity = liquidity,
                        BlockNumber = blockNumber,
                        TransactionHash = txHash
                    };

                    context.ReserveHistory.Add(reserveHistory);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger($"Error in AddReserveHistoryAsync: {ex.Message}");
            }
        }

        public async Task<List<PriceHistoryEntity>> GetPriceHistoryAsync(int pairId, DateTime? startTime = null, DateTime? endTime = null, int maxPoints = 1000)
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    var query = context.PriceHistory
                        .Where(h => h.PairId == pairId);

                    if (startTime.HasValue)
                    {
                        query = query.Where(h => h.Timestamp >= startTime.Value);
                    }

                    if (endTime.HasValue)
                    {
                        query = query.Where(h => h.Timestamp <= endTime.Value);
                    }

                    // Get all data and then perform sampling in memory
                    var allPoints = await query.OrderBy(h => h.Timestamp).ToListAsync();
                    var totalPoints = allPoints.Count();

                    if (totalPoints <= maxPoints)
                    {
                        // Return all points if we have fewer than maxPoints
                        return allPoints;
                    }
                    else
                    {
                        // Sample points evenly in memory
                        var sampling = totalPoints / maxPoints;

                        // Get every nth point
                        return allPoints
                            .Where((h, index) => index % sampling == 0)
                            .Take(maxPoints)
                            .ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger($"Error in GetPriceHistoryAsync: {ex.Message}");
                return new List<PriceHistoryEntity>();
            }
        }

        public async Task<List<ReserveHistoryEntity>> GetReserveHistoryAsync(int pairId, DateTime? startTime = null, DateTime? endTime = null, int maxPoints = 1000)
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    var query = context.ReserveHistory
                        .Where(h => h.PairId == pairId);

                    if (startTime.HasValue)
                    {
                        query = query.Where(h => h.Timestamp >= startTime.Value);
                    }

                    if (endTime.HasValue)
                    {
                        query = query.Where(h => h.Timestamp <= endTime.Value);
                    }

                    // Get all data and then perform sampling in memory
                    var allPoints = await query.OrderBy(h => h.Timestamp).ToListAsync();
                    var totalPoints = allPoints.Count();

                    if (totalPoints <= maxPoints)
                    {
                        // Return all points if we have fewer than maxPoints
                        return allPoints;
                    }
                    else
                    {
                        // Sample points evenly in memory
                        var sampling = totalPoints / maxPoints;

                        // Get every nth point
                        return allPoints
                            .Where((h, index) => index % sampling == 0)
                            .Take(maxPoints)
                            .ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger($"Error in GetReserveHistoryAsync: {ex.Message}");
                return new List<ReserveHistoryEntity>();
            }
        }

        #endregion

        #region Helper Methods

        // Convert database entity to application model
        public PairInfo ConvertToPairInfo(PairEntity entity)
        {
            if (entity == null) return null;

            return new PairInfo
            {
                Address = entity.Address,
                Token0 = new TokenInfo
                {
                    Address = entity.Token0.Address,
                    Symbol = entity.Token0.Symbol,
                    Name = entity.Token0.Name,
                    Decimals = entity.Token0.Decimals,
                    TotalSupply = entity.Token0.TotalSupply,
                    UsdPrice = entity.Token0.UsdPrice ?? 0
                },
                Token1 = new TokenInfo
                {
                    Address = entity.Token1.Address,
                    Symbol = entity.Token1.Symbol,
                    Name = entity.Token1.Name,
                    Decimals = entity.Token1.Decimals,
                    TotalSupply = entity.Token1.TotalSupply,
                    UsdPrice = entity.Token1.UsdPrice ?? 0
                },
                Reserve0 = entity.Reserve0,
                Reserve1 = entity.Reserve1,
                Price = entity.LastPrice,
                Liquidity = entity.Liquidity,
                HolderCount = entity.Token0.HolderCount ?? 0
            };
        }

        // Convert list of entities to list of application models
        public List<PairInfo> ConvertToPairInfoList(List<PairEntity> entities)
        {
            return entities.Select(ConvertToPairInfo).ToList();
        }

        // Check if a change is significant enough to record
        private bool IsSignificantChange(decimal oldValue, decimal newValue, decimal threshold)
        {
            if (oldValue == 0) return true;
            var percentChange = Math.Abs((newValue - oldValue) / oldValue);
            return percentChange > threshold;
        }

        // Housekeeping method to clean up old data
        public async Task CleanupOldDataAsync(int daysToKeep = 30)
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);

                    // Delete old price history entries
                    var oldPriceHistory = await context.PriceHistory
                        .Where(p => p.Timestamp < cutoffDate)
                        .ToListAsync();

                    if (oldPriceHistory.Any())
                    {
                        context.PriceHistory.RemoveRange(oldPriceHistory);
                        _logger($"Removed {oldPriceHistory.Count()} old price history records");
                    }

                    // Delete old reserve history entries
                    var oldReserveHistory = await context.ReserveHistory
                        .Where(r => r.Timestamp < cutoffDate)
                        .ToListAsync();

                    if (oldReserveHistory.Any())
                    {
                        context.ReserveHistory.RemoveRange(oldReserveHistory);
                        _logger($"Removed {oldReserveHistory.Count()} old reserve history records");
                    }

                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger($"Error in CleanupOldDataAsync: {ex.Message}");
            }
        }

        #endregion
    }
}
