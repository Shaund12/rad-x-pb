using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using RadXPriceBot.Data;

namespace RadXPriceBot.Services
{
    public class TokenInfo
    {
        public string Address { get; set; }
        public string Symbol { get; set; }
        public string Name { get; set; }
        public int Decimals { get; set; }
        public decimal TotalSupply { get; set; }
        public decimal UsdPrice { get; set; } // Add USD price information
    }

    public class SwapTransaction
    {
        public string TransactionHash { get; set; }
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
        public long BlockNumber { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        // Token information
        public string Token0Address { get; set; }
        public string Token1Address { get; set; }
        public string Token0Symbol { get; set; }
        public string Token1Symbol { get; set; }
        public int Token0Decimals { get; set; } = 18;
        public int Token1Decimals { get; set; } = 18;

        // Transaction amounts
        public decimal TokenAmount { get; set; }
        public decimal ValueAmount { get; set; }
        public decimal UsdValue { get; set; }

        // Swap metrics
        public decimal PriceImpact { get; set; }
        public bool IsBuyTransaction { get; set; }
    }

    [Event("Swap")]
    public class SwapEventDTO : IEventDTO
    {
        [Parameter("address", "sender", 1, true)] public string Sender { get; set; }
        [Parameter("uint256", "amount0In", 2, false)] public BigInteger Amount0In { get; set; }
        [Parameter("uint256", "amount1In", 3, false)] public BigInteger Amount1In { get; set; }
        [Parameter("uint256", "amount0Out", 4, false)] public BigInteger Amount0Out { get; set; }
        [Parameter("uint256", "amount1Out", 5, false)] public BigInteger Amount1Out { get; set; }
        [Parameter("address", "to", 6, true)] public string To { get; set; }
    }

    public class PriceService
    {
        private readonly Web3 _web3;
        private readonly Contract _routerContract;
        private readonly Contract _factoryContract;
        private readonly List<string> _path;
        private readonly UnitConversion _uconv = new UnitConversion();
        private readonly Action<string> _logger; // Add logger field
        private readonly DatabaseService _dbService; // Add database service
        private readonly bool _useDbCache; // Flag to use database cache

        private const string USDC_POL_ADDRESS = "0xbCfB3FCa16b12C7756CD6C24f1cC0AC0E38569CF";
        private const string VTRO_ADDRESS = "0xDECAF2f187Cb837a42D26FA364349Abc3e80Aa5D";
        private const string WVTRU_ADDRESS = "0x3ccc3F22462cAe34766820894D04a40381201ef9";

        private const string RouterAbi = @"[{
            ""constant"":true,""inputs"":[
              {""internalType"":""uint256"",""name"":""amountIn"",""type"":""uint256""},
              {""internalType"":""address[]"",""name"":""path"",""type"":""address[]""}
            ],
            ""name"":""getAmountsOut"",
            ""outputs"":[{""internalType"":""uint256[]"",""name"":"""",""type"":""uint256[]""}],
            ""stateMutability"":""view"",""type"":""function""
        },{
            ""constant"":true,""inputs"":[],""name"":""factory"",""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],
            ""stateMutability"":""view"",""type"":""function""
        }]";

        private const string FactoryAbi = @"[{
            ""constant"":true,
            ""inputs"":[
              {""internalType"":""address"",""name"":""tokenA"",""type"":""address""},
              {""internalType"":""address"",""name"":""tokenB"",""type"":""address""}
            ],
            ""name"":""getPair"",
            ""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],
            ""stateMutability"":""view"",""type"":""function""
        }]";

        // Update this in PriceService.cs
        private const string PairAbi = @"[{
    ""constant"":true,
    ""inputs"":[],
    ""name"":""getReserves"",
    ""outputs"":[
      {""internalType"":""uint112"",""name"":""_reserve0"",""type"":""uint112""},
      {""internalType"":""uint112"",""name"":""_reserve1"",""type"":""uint112""},
      {""internalType"":""uint32"",""name"":""_blockTimestampLast"",""type"":""uint32""}
    ],
    ""stateMutability"":""view"",""type"":""function""
},{
    ""constant"":true,
    ""inputs"":[],
    ""name"":""token0"",
    ""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],
    ""stateMutability"":""view"",""type"":""function""
},{
    ""constant"":true,
    ""inputs"":[],
    ""name"":""token1"",
    ""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],
    ""stateMutability"":""view"",""type"":""function""
}]";


        private const string Erc20Abi = @"[{
            ""constant"":true,""inputs"":[],""name"":""symbol"",
            ""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],
            ""stateMutability"":""view"",""type"":""function""
        },{
            ""constant"":true,""inputs"":[],""name"":""name"",
            ""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],
            ""stateMutability"":""view"",""type"":""function""
        },{
            ""constant"":true,""inputs"":[],""name"":""decimals"",
            ""outputs"":[{""internalType"":""uint8"",""name"":"""",""type"":""uint8""}],
            ""stateMutability"":""view"",""type"":""function""
        },{
            ""constant"":true,""inputs"":[],""name"":""totalSupply"",
            ""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],
            ""stateMutability"":""view"",""type"":""function""
        }]";

        public PriceService(string rpcUrl, string routerAddress, List<string> path, Action<string> logger = null, bool useDbCache = true)
        {
            _web3 = new Web3(rpcUrl);
            _routerContract = _web3.Eth.GetContract(RouterAbi, routerAddress);
            _path = path;
            _logger = logger ?? (msg => Console.WriteLine(msg)); // Default to Console.WriteLine if no logger provided
            _useDbCache = useDbCache;

            // Initialize database service
            _dbService = new DatabaseService(_logger);

            var factoryAddress = _routerContract
                .GetFunction("factory")
                .CallAsync<string>()
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            _factoryContract = _web3.Eth.GetContract(FactoryAbi, factoryAddress);
        }

        public async Task<decimal> GetPriceAsync(decimal inputAmount = 1.0m)
        {
            try
            {
                // Get reserves directly
                var (reserve0, reserve1) = await GetReservesAsync();

                // Calculate price from reserves - this is more accurate than router.getAmountsOut
                if (reserve0 > 0)
                {
                    var price = reserve1 / reserve0;
                    _logger($"Price calculated from reserves: {price}");

                    // Record price in database if pair exists
                    try
                    {
                        var pairAddress = await GetPairAddress(_path[0], _path[1]);
                        if (pairAddress != "0x0000000000000000000000000000000000000000")
                        {
                            // Get the pair from database
                            var dbPair = await _dbService.GetPairByAddressAsync(pairAddress);
                            if (dbPair != null)
                            {
                                // Get token USD price if available
                                decimal? usdPrice = null;
                                var token0 = await GetTokenInfoAsync(_path[0]);
                                if (token0.UsdPrice > 0)
                                {
                                    usdPrice = token0.UsdPrice;
                                }

                                // Add to price history
                                await _dbService.AddPriceHistoryAsync(dbPair.Id, price, usdPrice);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger($"Failed to record price history: {ex.Message}");
                    }

                    return price;
                }

                // Fallback to router if reserves approach fails
                var fn = _routerContract.GetFunction("getAmountsOut");

                // Get token info for proper decimal handling
                var tokenInfo = await GetTokenInfoAsync(_path[0]);
                var weiIn = _uconv.ToWei(inputAmount, tokenInfo.Decimals);

                var result = await fn
                    .CallAsync<List<BigInteger>>(weiIn, _path)
                    .ConfigureAwait(false);

                // Get output token decimals
                var outputTokenInfo = await GetTokenInfoAsync(_path[^1]);
                var weiOut = result[^1];

                return _uconv.FromWei(weiOut, outputTokenInfo.Decimals);
            }
            catch (Exception ex)
            {
                _logger($"Error calculating price: {ex.Message}");
                throw;
            }
        }

        public async Task<decimal> GetTokenUsdPriceAsync(string tokenAddress)
        {
            try
            {
                // Try to get token from database first if caching is enabled
                if (_useDbCache)
                {
                    var dbToken = await _dbService.GetTokenByAddressAsync(tokenAddress);
                    if (dbToken != null && dbToken.UsdPrice.HasValue && dbToken.UsdPrice.Value > 0)
                    {
                        _logger($"Using cached USD price for {dbToken.Symbol}: ${dbToken.UsdPrice.Value}");
                        return dbToken.UsdPrice.Value;
                    }
                }

                var tokenInfo = await GetTokenInfoAsync(tokenAddress);
                _logger($"Calculating USD price for {tokenInfo.Symbol} ({tokenAddress})");

                // Check if token is USDC.pol itself
                if (tokenAddress.Equals(USDC_POL_ADDRESS, StringComparison.OrdinalIgnoreCase))
                {
                    _logger($"{tokenInfo.Symbol} is a stablecoin, returning 1.0 USD");

                    // Update token in database with USD price
                    if (_useDbCache)
                    {
                        var dbToken = await _dbService.GetOrCreateTokenAsync(new TokenInfo
                        {
                            Address = tokenAddress,
                            Symbol = tokenInfo.Symbol,
                            Name = tokenInfo.Name,
                            Decimals = tokenInfo.Decimals,
                            TotalSupply = tokenInfo.TotalSupply,
                            UsdPrice = 1.0m
                        });
                    }

                    return 1.0m; // USDC.pol is pegged to USD
                }

                // First try to find a direct pair with USDC.pol
                var usdcPrice = await GetTokenPairPriceAsync(tokenAddress, USDC_POL_ADDRESS);
                if (usdcPrice.HasValue)
                {
                    _logger($"Found direct USDC.pol pair. Price of {tokenInfo.Symbol}: ${usdcPrice.Value}");

                    // Update token in database with USD price
                    if (_useDbCache)
                    {
                        tokenInfo.UsdPrice = usdcPrice.Value;
                        await _dbService.GetOrCreateTokenAsync(tokenInfo);
                    }

                    return usdcPrice.Value;
                }

                // Try through VTRO
                if (!tokenAddress.Equals(VTRO_ADDRESS, StringComparison.OrdinalIgnoreCase))
                {
                    var vtroInfo = await GetTokenInfoAsync(VTRO_ADDRESS);
                    _logger($"Trying to calculate via {vtroInfo.Symbol}");

                    var vtroTokenPrice = await GetTokenPairPriceAsync(tokenAddress, VTRO_ADDRESS);
                    if (vtroTokenPrice.HasValue)
                    {
                        var vtroUsdPrice = await GetTokenPairPriceAsync(VTRO_ADDRESS, USDC_POL_ADDRESS);
                        if (vtroUsdPrice.HasValue)
                        {
                            var usdPrice = vtroTokenPrice.Value * vtroUsdPrice.Value;
                            _logger($"Calculated via {vtroInfo.Symbol}: 1 {tokenInfo.Symbol} = {vtroTokenPrice.Value} {vtroInfo.Symbol} = ${usdPrice} USD");

                            // Update token in database with USD price
                            if (_useDbCache)
                            {
                                tokenInfo.UsdPrice = usdPrice;
                                await _dbService.GetOrCreateTokenAsync(tokenInfo);
                            }

                            return usdPrice;
                        }
                    }
                }

                // Try through wVTRU
                if (!tokenAddress.Equals(WVTRU_ADDRESS, StringComparison.OrdinalIgnoreCase))
                {
                    var wvtruInfo = await GetTokenInfoAsync(WVTRU_ADDRESS);
                    _logger($"Trying to calculate via {wvtruInfo.Symbol}");

                    var wvtruTokenPrice = await GetTokenPairPriceAsync(tokenAddress, WVTRU_ADDRESS);
                    if (wvtruTokenPrice.HasValue)
                    {
                        var wvtruUsdPrice = await GetTokenPairPriceAsync(WVTRU_ADDRESS, USDC_POL_ADDRESS);
                        if (wvtruUsdPrice.HasValue)
                        {
                            var usdPrice = wvtruTokenPrice.Value * wvtruUsdPrice.Value;
                            _logger($"Calculated via {wvtruInfo.Symbol}: 1 {tokenInfo.Symbol} = {wvtruTokenPrice.Value} {wvtruInfo.Symbol} = ${usdPrice} USD");

                            // Update token in database with USD price
                            if (_useDbCache)
                            {
                                tokenInfo.UsdPrice = usdPrice;
                                await _dbService.GetOrCreateTokenAsync(tokenInfo);
                            }

                            return usdPrice;
                        }
                    }
                }

                _logger($"Could not determine USD price for {tokenInfo.Symbol} through any reference tokens");
                return 0;
            }
            catch (Exception ex)
            {
                _logger($"Error calculating USD price for {tokenAddress}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger($"Inner exception: {ex.InnerException.Message}");
                }
                return 0;
            }
        }

        // Add this method to get USD prices for tokens in the path
        public async Task<Dictionary<string, decimal>> GetPathUsdPricesAsync()
        {
            var result = new Dictionary<string, decimal>();

            foreach (var tokenAddress in _path)
            {
                var tokenInfo = await GetTokenInfoAsync(tokenAddress);
                var usdPrice = await GetTokenUsdPriceAsync(tokenAddress);

                tokenInfo.UsdPrice = usdPrice; // Set the USD price on the token info
                result[tokenInfo.Symbol] = usdPrice;

                _logger($"USD price for {tokenInfo.Symbol}: ${usdPrice}");
            }

            return result;
        }

        // Add method to get price history from database
        public async Task<List<(DateTime timestamp, decimal price, decimal? usdPrice)>> GetPriceHistoryAsync(
            DateTime? startTime = null, DateTime? endTime = null, int maxPoints = 1000)
        {
            try
            {
                if (_path.Count < 2)
                {
                    _logger("Path must contain at least two tokens to get price history");
                    return new List<(DateTime, decimal, decimal?)>();
                }

                // Get pair address
                var pairAddress = await GetPairAddress(_path[0], _path[1]);
                if (pairAddress == "0x0000000000000000000000000000000000000000")
                {
                    _logger("No pair exists for the specified tokens");
                    return new List<(DateTime, decimal, decimal?)>();
                }

                // Get pair entity from database
                var pair = await _dbService.GetPairByAddressAsync(pairAddress);
                if (pair == null)
                {
                    _logger($"Pair not found in database: {pairAddress}");
                    return new List<(DateTime, decimal, decimal?)>();
                }

                // Get price history
                var history = await _dbService.GetPriceHistoryAsync(pair.Id, startTime, endTime, maxPoints);

                // Convert to simplified format
                return history.Select(h => (h.Timestamp, h.Price, h.UsdPrice)).ToList();
            }
            catch (Exception ex)
            {
                _logger($"Error getting price history: {ex.Message}");
                return new List<(DateTime, decimal, decimal?)>();
            }
        }


        // Add this method to PriceService.cs
        public async Task<List<SwapTransaction>> GetRecentSwapsAsync(int count = 10)
        {
            try
            {
                _logger?.Invoke($"Fetching recent swaps (max: {count})...");

                // 1. Get the pair address and token info
                var (pairAddress, token0, token1) = await GetPairDetailsAsync();

                // 2. Create a typed handler for the Swap event
                var eventHandler = _web3.Eth.GetEvent<SwapEventDTO>(pairAddress);

                // 3. Figure out our block range (last ~1000 blocks)
                var latestBlockNumber = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                var fromBlockNumber = latestBlockNumber.Value - 1000;
                if (fromBlockNumber < 0) fromBlockNumber = 0;

                // 4. Build the filter input in one shot
                var filter = eventHandler.CreateFilterInput(
                    new BlockParameter(new HexBigInteger(fromBlockNumber)),
                    BlockParameter.CreateLatest()
                );

                // 5. Fetch all matching logs
                var logs = await eventHandler.GetAllChangesAsync(filter);

                var transactions = new List<SwapTransaction>();

                // 6. Process up to [count] events
                foreach (var eventLog in logs.Take(count))
                {
                    var data = eventLog.Event;
                    var sender = data.Sender;
                    var to = data.To;
                    var amount0In = Web3.Convert.FromWei(data.Amount0In, token0.Decimals);
                    var amount1In = Web3.Convert.FromWei(data.Amount1In, token1.Decimals);
                    var amount0Out = Web3.Convert.FromWei(data.Amount0Out, token0.Decimals);
                    var amount1Out = Web3.Convert.FromWei(data.Amount1Out, token1.Decimals);

                    // Determine buy vs sell
                    var isBuy = amount1In > 0 && amount0Out > 0;

                    // Compute USD value
                    decimal usdValue = 0;
                    if (isBuy)
                    {
                        if (token1.Symbol is "USDC.POL" or "USDC" or "USDT")
                            usdValue = amount1In;
                        else
                        {
                            var price1 = await GetTokenUsdPriceAsync(token1.Address);
                            usdValue = amount1In * price1;
                        }
                    }
                    else
                    {
                        var price0 = await GetTokenUsdPriceAsync(token0.Address);
                        usdValue = amount0In * price0;
                    }

                    // Approximate price impact
                    var (reserve0, reserve1) = await GetReservesAsync();
                    decimal priceImpact = 0;
                    if (isBuy && reserve1 > 0) priceImpact = amount1In / reserve1;
                    else if (!isBuy && reserve0 > 0) priceImpact = amount0In / reserve0;

                    // Get timestamp from block
                    var receipt = await _web3.Eth.Transactions
                        .GetTransactionReceipt.SendRequestAsync(eventLog.Log.TransactionHash);
                    var block = await _web3.Eth.Blocks
                        .GetBlockWithTransactionsByNumber
                        .SendRequestAsync(new BlockParameter(receipt.BlockNumber));

                    transactions.Add(new SwapTransaction
                    {
                        TransactionHash = eventLog.Log.TransactionHash,
                        FromAddress = sender,
                        ToAddress = to,
                        BlockNumber = (long)eventLog.Log.BlockNumber.Value,
                        Timestamp = DateTimeOffset
                                             .FromUnixTimeSeconds((long)block.Timestamp.Value),
                        Token0Address = token0.Address,
                        Token1Address = token1.Address,
                        Token0Symbol = token0.Symbol,
                        Token1Symbol = token1.Symbol,
                        Token0Decimals = token0.Decimals,
                        Token1Decimals = token1.Decimals,
                        IsBuyTransaction = isBuy,
                        TokenAmount = isBuy ? amount0Out : amount0In,
                        ValueAmount = isBuy ? amount1In : amount1Out,
                        UsdValue = usdValue,
                        PriceImpact = priceImpact
                    });
                }

                // 7. Return newest first
                return transactions
                       .OrderByDescending(t => t.BlockNumber)
                       .ToList();
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error fetching recent swaps: {ex.Message}");
                return new List<SwapTransaction>();
            }
        }


        private async Task<decimal?> GetTokenPairPriceAsync(string tokenA, string tokenB)
        {
            try
            {
                _logger($"Calculating price between {tokenA} and {tokenB}");

                // Get token info for proper decimal handling
                var infoA = await GetTokenInfoAsync(tokenA);
                var infoB = await GetTokenInfoAsync(tokenB);
                _logger($"Token info: {infoA.Symbol}({infoA.Decimals} decimals), {infoB.Symbol}({infoB.Decimals} decimals)");

                var getPairFn = _factoryContract.GetFunction("getPair");

                // Try to find pair address
                var pairAddress = await getPairFn.CallAsync<string>(tokenA, tokenB);
                bool reversed = false;

                // If pair doesn't exist in this order, try reverse
                if (pairAddress == "0x0000000000000000000000000000000000000000")
                {
                    pairAddress = await getPairFn.CallAsync<string>(tokenB, tokenA);
                    reversed = true;

                    if (pairAddress == "0x0000000000000000000000000000000000000000")
                    {
                        _logger($"No liquidity pair exists for {infoA.Symbol}/{infoB.Symbol}");
                        return null; // No pair exists in either direction
                    }
                }

                _logger($"Found pair at {pairAddress}, reversed: {reversed}");

                // Get pair contract and reserves
                var pairContract = _web3.Eth.GetContract(PairAbi, pairAddress);
                var reservesFn = pairContract.GetFunction("getReserves");
                var reserves = await reservesFn.CallDeserializingToObjectAsync<ReservesOutput>();

                // Get token0 and token1 from the pair contract
                var token0 = await pairContract.GetFunction("token0").CallAsync<string>();
                var token1 = await pairContract.GetFunction("token1").CallAsync<string>();

                _logger($"Pair tokens: token0={token0}, token1={token1}");
                _logger($"Reserves: reserve0={reserves.Reserve0}, reserve1={reserves.Reserve1}");

                // The decimal places for each token must be considered
                decimal reserve0Adjusted, reserve1Adjusted;

                // Determine which decimals to use based on token positions
                if (token0.Equals(tokenA, StringComparison.OrdinalIgnoreCase))
                {
                    // token0 is tokenA, token1 is tokenB
                    reserve0Adjusted = _uconv.FromWei(reserves.Reserve0, infoA.Decimals);
                    reserve1Adjusted = _uconv.FromWei(reserves.Reserve1, infoB.Decimals);
                    _logger($"Adjusted reserves: {infoA.Symbol}={reserve0Adjusted}, {infoB.Symbol}={reserve1Adjusted}");

                    if (reserve0Adjusted > 0)
                    {
                        decimal price = reserve1Adjusted / reserve0Adjusted;
                        _logger($"Price: 1 {infoA.Symbol} = {price} {infoB.Symbol}");

                        // Store price in database
                        try
                        {
                            if (_useDbCache)
                            {
                                // Store or update the pair in database
                                var pairInfo = new PairInfo
                                {
                                    Address = pairAddress,
                                    Token0 = infoA,
                                    Token1 = infoB,
                                    Reserve0 = reserve0Adjusted,
                                    Reserve1 = reserve1Adjusted,
                                    Price = price,
                                    Liquidity = reserve1Adjusted + (reserve0Adjusted * price)
                                };

                                var dbPair = await _dbService.GetOrCreatePairAsync(pairInfo);

                                // Add price history entry
                                if (dbPair != null)
                                {
                                    decimal? usdPrice = null;
                                    if (infoB.Symbol == "USDC.POL" || infoB.Symbol == "USDC" || infoB.Symbol == "USDT")
                                    {
                                        usdPrice = price;
                                    }

                                    await _dbService.AddPriceHistoryAsync(dbPair.Id, price, usdPrice);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger($"Error storing pair data: {ex.Message}");
                        }

                        return reversed ? 1 / price : price;
                    }
                }
                else if (token0.Equals(tokenB, StringComparison.OrdinalIgnoreCase))
                {
                    // token0 is tokenB, token1 is tokenA
                    reserve0Adjusted = _uconv.FromWei(reserves.Reserve0, infoB.Decimals);
                    reserve1Adjusted = _uconv.FromWei(reserves.Reserve1, infoA.Decimals);
                    _logger($"Adjusted reserves: {infoB.Symbol}={reserve0Adjusted}, {infoA.Symbol}={reserve1Adjusted}");

                    if (reserve0Adjusted > 0)
                    {
                        decimal price = reserve1Adjusted / reserve0Adjusted;
                        _logger($"Price: 1 {infoB.Symbol} = {price} {infoA.Symbol}");

                        // Store price in database
                        try
                        {
                            if (_useDbCache)
                            {
                                // Note: In this case, the price is inverted for our storage since
                                // we're storing from token0->token1 perspective
                                decimal invertedPrice = price > 0 ? 1 / price : 0;

                                // Store or update the pair in database
                                var pairInfo = new PairInfo
                                {
                                    Address = pairAddress,
                                    Token0 = infoB,
                                    Token1 = infoA,
                                    Reserve0 = reserve0Adjusted,
                                    Reserve1 = reserve1Adjusted,
                                    Price = invertedPrice,
                                    Liquidity = reserve0Adjusted + (reserve1Adjusted * invertedPrice)
                                };

                                var dbPair = await _dbService.GetOrCreatePairAsync(pairInfo);

                                // Add price history entry
                                if (dbPair != null)
                                {
                                    decimal? usdPrice = null;
                                    if (infoA.Symbol == "USDC.POL" || infoA.Symbol == "USDC" || infoA.Symbol == "USDT")
                                    {
                                        usdPrice = invertedPrice;
                                    }

                                    await _dbService.AddPriceHistoryAsync(dbPair.Id, invertedPrice, usdPrice);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger($"Error storing pair data: {ex.Message}");
                        }

                        // We want price of tokenA in terms of tokenB
                        return reversed ? price : 1 / price;
                    }
                }

                _logger("Could not calculate price due to zero reserves");
                return null;
            }
            catch (Exception ex)
            {
                _logger($"Error calculating pair price {tokenA}/{tokenB}: {ex.Message}");
                return null;
            }
        }

        public async Task<(decimal reserve0, decimal reserve1)> GetReservesAsync()
        {
            if (_path.Count < 2)
                throw new InvalidOperationException("Need at least two tokens in path to fetch reserves.");

            try
            {
                // Get token decimals first
                var token0Info = await GetTokenInfoAsync(_path[0]);
                var token1Info = await GetTokenInfoAsync(_path[1]);

                var getPairFn = _factoryContract.GetFunction("getPair");
                var pairAddress = await getPairFn
                    .CallAsync<string>(_path[0], _path[1])
                    .ConfigureAwait(false);

                _logger($"Pair address for {token0Info.Symbol}/{token1Info.Symbol}: {pairAddress}");

                if (pairAddress == "0x0000000000000000000000000000000000000000")
                {
                    throw new InvalidOperationException($"No liquidity pair exists for {token0Info.Symbol}/{token1Info.Symbol}");
                }

                var pairContract = _web3.Eth.GetContract(PairAbi, pairAddress);

                // Get token order in the pair
                var token0 = await pairContract.GetFunction("token0").CallAsync<string>();
                var token1 = await pairContract.GetFunction("token1").CallAsync<string>();

                var reservesFn = pairContract.GetFunction("getReserves");
                var reservesOutput = await reservesFn.CallDeserializingToObjectAsync<ReservesOutput>().ConfigureAwait(false);

                // We need to check if our path tokens match the pair's order
                bool isPathOrderSameAsPair = _path[0].Equals(token0, StringComparison.InvariantCultureIgnoreCase);

                decimal reserve0, reserve1;

                if (isPathOrderSameAsPair)
                {
                    reserve0 = _uconv.FromWei(reservesOutput.Reserve0, token0Info.Decimals);
                    reserve1 = _uconv.FromWei(reservesOutput.Reserve1, token1Info.Decimals);
                    _logger($"Reserves for {token0Info.Symbol}/{token1Info.Symbol}: {reserve0} {token0Info.Symbol}, {reserve1} {token1Info.Symbol}");
                }
                else
                {
                    // Path order is different from pair order, so we need to swap
                    reserve0 = _uconv.FromWei(reservesOutput.Reserve1, token0Info.Decimals);
                    reserve1 = _uconv.FromWei(reservesOutput.Reserve0, token1Info.Decimals);
                    _logger($"Reserves for {token0Info.Symbol}/{token1Info.Symbol} (swapped): {reserve0} {token0Info.Symbol}, {reserve1} {token1Info.Symbol}");
                }

                // Store reserve information in database
                try
                {
                    if (_useDbCache)
                    {
                        // Get or create the pair in database
                        var pairInfo = new PairInfo
                        {
                            Address = pairAddress,
                            Token0 = token0Info,
                            Token1 = token1Info,
                            Reserve0 = reserve0,
                            Reserve1 = reserve1,
                            Price = reserve0 > 0 ? reserve1 / reserve0 : 0,
                            Liquidity = reserve1 + (reserve0 * (reserve0 > 0 ? reserve1 / reserve0 : 0))
                        };

                        var dbPair = await _dbService.GetOrCreatePairAsync(pairInfo);

                        // Add reserve history entry
                        if (dbPair != null)
                        {
                            // Calculate liquidity
                            decimal liquidity = reserve1;
                            if (reserve0 > 0)
                            {
                                liquidity += reserve0 * (reserve1 / reserve0);
                            }

                            await _dbService.AddReserveHistoryAsync(
                                dbPair.Id,
                                reserve0,
                                reserve1,
                                liquidity);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger($"Error storing reserve data: {ex.Message}");
                }

                return (reserve0, reserve1);
            }
            catch (Exception ex)
            {
                _logger($"Error getting reserves: {ex.Message}");
                throw;
            }
        }

        public async Task<decimal> GetLiquidityAsync()
        {
            var (reserve0, reserve1) = await GetReservesAsync();
            var price = await GetPriceAsync();
            // Calculate total liquidity in terms of token1's value
            return reserve0 * price + reserve1;
        }

        public async Task<TokenInfo> GetTokenInfoAsync(string tokenAddress)
        {
            // Try to get token from cache first
            if (_useDbCache)
            {
                var dbToken = await _dbService.GetTokenByAddressAsync(tokenAddress);
                if (dbToken != null)
                {
                    _logger($"Using cached token info for {dbToken.Symbol}");

                    // Convert database entity to TokenInfo
                    return new TokenInfo
                    {
                        Address = dbToken.Address,
                        Symbol = dbToken.Symbol,
                        Name = dbToken.Name,
                        Decimals = dbToken.Decimals,
                        TotalSupply = dbToken.TotalSupply,
                        UsdPrice = dbToken.UsdPrice ?? 0
                    };
                }
            }

            // If not in cache or cache disabled, fetch from blockchain
            var tokenContract = _web3.Eth.GetContract(Erc20Abi, tokenAddress);

            var symbolFn = tokenContract.GetFunction("symbol");
            var nameFn = tokenContract.GetFunction("name");
            var decimalsFn = tokenContract.GetFunction("decimals");
            var totalSupplyFn = tokenContract.GetFunction("totalSupply");

            var symbol = await symbolFn.CallAsync<string>().ConfigureAwait(false);
            var name = await nameFn.CallAsync<string>().ConfigureAwait(false);
            var decimals = await decimalsFn.CallAsync<int>().ConfigureAwait(false);
            var totalSupplyWei = await totalSupplyFn.CallAsync<BigInteger>().ConfigureAwait(false);

            var tokenInfo = new TokenInfo
            {
                Address = tokenAddress,
                Symbol = symbol,
                Name = name,
                Decimals = decimals,
                TotalSupply = _uconv.FromWei(totalSupplyWei, decimals)
            };

            // Store token in database for future use
            if (_useDbCache)
            {
                await _dbService.GetOrCreateTokenAsync(tokenInfo);
            }

            return tokenInfo;
        }

        public async Task<decimal> GetMarketCapAsync(string tokenAddress = null)
        {
            // If no specific token address is provided, use the first token in the path
            var address = tokenAddress ?? _path[0];
            var tokenInfo = await GetTokenInfoAsync(address);
            var price = await GetPriceAsync();

            // Market cap = Total Supply * Price
            return tokenInfo.TotalSupply * price;
        }

        public async Task<(string pairAddress, TokenInfo token0Info, TokenInfo token1Info)> GetPairDetailsAsync()
        {
            if (_path.Count < 2)
                throw new InvalidOperationException("Need at least two tokens in path to fetch pair details.");

            var getPairFn = _factoryContract.GetFunction("getPair");
            var pairAddress = await getPairFn.CallAsync<string>(_path[0], _path[1]).ConfigureAwait(false);

            var token0Info = await GetTokenInfoAsync(_path[0]);
            var token1Info = await GetTokenInfoAsync(_path[1]);

            return (pairAddress, token0Info, token1Info);
        }

        public async Task<Dictionary<string, decimal>> GetTokenMetricsAsync()
        {
            try
            {
                // Get token info and USD prices
                var token0Info = await GetTokenInfoAsync(_path[0]);
                var token1Info = await GetTokenInfoAsync(_path[1]);

                _logger($"Calculating metrics for {token0Info.Symbol}/{token1Info.Symbol}");

                // Get USD prices (with error handling)
                token0Info.UsdPrice = await GetTokenUsdPriceAsync(_path[0]);
                token1Info.UsdPrice = await GetTokenUsdPriceAsync(_path[1]);

                _logger($"USD Prices: {token0Info.Symbol}=${token0Info.UsdPrice}, {token1Info.Symbol}=${token1Info.UsdPrice}");

                // Get reserves and price
                var (reserve0, reserve1) = await GetReservesAsync();
                var price = await GetPriceAsync();

                // Calculate USD values
                var token0Value = reserve0 * token0Info.UsdPrice;
                var token1Value = reserve1 * token1Info.UsdPrice;
                var totalLiquidityUsd = token0Value + token1Value;
                var marketCapUsd = token0Info.TotalSupply * token0Info.UsdPrice;

                _logger($"Price: {price} ({token0Info.Symbol}/{token1Info.Symbol})");
                _logger($"Market Cap: ${marketCapUsd} USD");
                _logger($"Liquidity: ${totalLiquidityUsd} USD");

                // After calculating metrics, store the updated pair and price history in the database
                try
                {
                    if (_useDbCache)
                    {
                        // Get pair address
                        var pairAddress = await GetPairAddress(_path[0], _path[1]);
                        if (pairAddress != "0x0000000000000000000000000000000000000000")
                        {
                            // Create PairInfo object
                            var pairInfo = new PairInfo
                            {
                                Address = pairAddress,
                                Token0 = token0Info,
                                Token1 = token1Info,
                                Reserve0 = reserve0,
                                Reserve1 = reserve1,
                                Price = price,
                                Liquidity = totalLiquidityUsd
                            };

                            // Store in database
                            await _dbService.GetOrCreatePairAsync(pairInfo);
                        }
                    }
                }
                catch (Exception dbEx)
                {
                    // Log database error but don't fail the operation
                    _logger($"Error storing metrics in database: {dbEx.Message}");
                }

                return new Dictionary<string, decimal>
                {
                    ["Price"] = price,
                    ["PriceUsd"] = token0Info.UsdPrice,
                    ["Token1PriceUsd"] = token1Info.UsdPrice,
                    ["Reserve0"] = reserve0,
                    ["Reserve1"] = reserve1,
                    ["Reserve0Usd"] = token0Value,
                    ["Reserve1Usd"] = token1Value,
                    ["Liquidity"] = totalLiquidityUsd,
                    ["MarketCap"] = marketCapUsd,
                    ["TotalSupply"] = token0Info.TotalSupply
                };
            }
            catch (Exception ex)
            {
                _logger($"Error in GetTokenMetricsAsync: {ex.Message}");
                return new Dictionary<string, decimal>
                {
                    ["Price"] = 0,
                    ["PriceUsd"] = 0,
                    ["Token1PriceUsd"] = 0,
                    ["Reserve0"] = 0,
                    ["Reserve1"] = 0,
                    ["Reserve0Usd"] = 0,
                    ["Reserve1Usd"] = 0,
                    ["Liquidity"] = 0,
                    ["MarketCap"] = 0,
                    ["TotalSupply"] = 0
                };
            }
        }

        // Add this method to PriceService.cs
        public async Task<string> GetPairAddress(string token0Address, string token1Address)
        {
            try
            {
                _logger($"Getting pair address for tokens: {token0Address} and {token1Address}");

                // Sort token addresses to match how DEXes like Uniswap store them
                string[] sortedTokens = new[] { token0Address, token1Address };
                Array.Sort(sortedTokens, StringComparer.OrdinalIgnoreCase);

                // Use the factory to find the pair address
                var getPairFn = _factoryContract.GetFunction("getPair");
                var pairAddress = await getPairFn.CallAsync<string>(sortedTokens[0], sortedTokens[1]);

                if (pairAddress == "0x0000000000000000000000000000000000000000")
                {
                    _logger($"No liquidity pair exists between the tokens");
                }
                else
                {
                    _logger($"Found pair at address: {pairAddress}");
                }

                return pairAddress;
            }
            catch (Exception ex)
            {
                _logger($"Error getting pair address: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger($"Inner exception: {ex.InnerException.Message}");
                }
                return "0x0000000000000000000000000000000000000000";
            }
        }

        // Also need to add the ReservesOutput class here since it's referenced in the code
        [Nethereum.ABI.FunctionEncoding.Attributes.FunctionOutput]
        public class ReservesOutput
        {
            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint112", "_reserve0", 1)]
            public BigInteger Reserve0 { get; set; }

            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint112", "_reserve1", 2)]
            public BigInteger Reserve1 { get; set; }

            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint32", "_blockTimestampLast", 3)]
            public uint BlockTimestampLast { get; set; }
        }
    }
}
