using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Util;
using Nethereum.Web3;

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

    public class PriceService
    {
        private readonly Web3 _web3;
        private readonly Contract _routerContract;
        private readonly Contract _factoryContract;
        private readonly List<string> _path;
        private readonly UnitConversion _uconv = new UnitConversion();
        private readonly Action<string> _logger; // Add logger field

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

        public PriceService(string rpcUrl, string routerAddress, List<string> path, Action<string> logger = null)
        {
            _web3 = new Web3(rpcUrl);
            _routerContract = _web3.Eth.GetContract(RouterAbi, routerAddress);
            _path = path;
            _logger = logger ?? (msg => Console.WriteLine(msg)); // Default to Console.WriteLine if no logger provided

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
                var tokenInfo = await GetTokenInfoAsync(tokenAddress);
                _logger($"Calculating USD price for {tokenInfo.Symbol} ({tokenAddress})");

                // Check if token is USDC.pol itself
                if (tokenAddress.Equals(USDC_POL_ADDRESS, StringComparison.OrdinalIgnoreCase))
                {
                    _logger($"{tokenInfo.Symbol} is a stablecoin, returning 1.0 USD");
                    return 1.0m; // USDC.pol is pegged to USD
                }

                // First try to find a direct pair with USDC.pol
                var usdcPrice = await GetTokenPairPriceAsync(tokenAddress, USDC_POL_ADDRESS);
                if (usdcPrice.HasValue)
                {
                    _logger($"Found direct USDC.pol pair. Price of {tokenInfo.Symbol}: ${usdcPrice.Value}");
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

                if (isPathOrderSameAsPair)
                {
                    var r0 = _uconv.FromWei(reservesOutput.Reserve0, token0Info.Decimals);
                    var r1 = _uconv.FromWei(reservesOutput.Reserve1, token1Info.Decimals);
                    _logger($"Reserves for {token0Info.Symbol}/{token1Info.Symbol}: {r0} {token0Info.Symbol}, {r1} {token1Info.Symbol}");
                    return (r0, r1);
                }
                else
                {
                    // Path order is different from pair order, so we need to swap
                    var r0 = _uconv.FromWei(reservesOutput.Reserve1, token0Info.Decimals);
                    var r1 = _uconv.FromWei(reservesOutput.Reserve0, token1Info.Decimals);
                    _logger($"Reserves for {token0Info.Symbol}/{token1Info.Symbol} (swapped): {r0} {token0Info.Symbol}, {r1} {token1Info.Symbol}");
                    return (r0, r1);
                }
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
            var tokenContract = _web3.Eth.GetContract(Erc20Abi, tokenAddress);

            var symbolFn = tokenContract.GetFunction("symbol");
            var nameFn = tokenContract.GetFunction("name");
            var decimalsFn = tokenContract.GetFunction("decimals");
            var totalSupplyFn = tokenContract.GetFunction("totalSupply");

            var symbol = await symbolFn.CallAsync<string>().ConfigureAwait(false);
            var name = await nameFn.CallAsync<string>().ConfigureAwait(false);
            var decimals = await decimalsFn.CallAsync<int>().ConfigureAwait(false);
            var totalSupplyWei = await totalSupplyFn.CallAsync<BigInteger>().ConfigureAwait(false);

            return new TokenInfo
            {
                Address = tokenAddress,
                Symbol = symbol,
                Name = name,
                Decimals = decimals,
                TotalSupply = _uconv.FromWei(totalSupplyWei, decimals)
            };
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
