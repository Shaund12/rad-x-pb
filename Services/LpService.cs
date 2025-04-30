using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using System.Linq;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.JsonRpc.Client;
using Nethereum.Util;
using Nethereum.Web3;

namespace RadXPriceBot.Services
{
    // Define constants at namespace level
    public static class TokenAddresses
    {
        public const string USDC_POL_ADDRESS = "0xbCfB3FCa16b12C7756CD6C24f1cC0AC0E38569CF";
        public const string VTRO_ADDRESS = "0xDECAF2f187Cb837a42D26FA364349Abc3e80Aa5D";
        public const string WVTRU_ADDRESS = "0x3ccc3F22462cAe34766820894D04a40381201ef9";
    }

    // Define a struct for getReserves output - Keep only one definition
    [FunctionOutput]
    public class ReservesOutput
    {
        [Parameter("uint112", "_reserve0", 1)]
        public BigInteger Reserve0 { get; set; }

        [Parameter("uint112", "_reserve1", 2)]
        public BigInteger Reserve1 { get; set; }

        [Parameter("uint32", "_blockTimestampLast", 3)]
        public uint BlockTimestampLast { get; set; }
    }

    public class PairInfo
    {
        public string Address { get; set; }
        public TokenInfo Token0 { get; set; }
        public TokenInfo Token1 { get; set; }
        public decimal Reserve0 { get; set; }
        public decimal Reserve1 { get; set; }
        public decimal Liquidity { get; set; }
        public decimal Price { get; set; }
        public int HolderCount { get; set; } // Added holder count
        public string Name => $"{Token0?.Symbol}/{Token1?.Symbol}";

        // Helper property to determine if this pair contains USDC.pol
        public bool ContainsUsdcPol =>
            Token0?.Address.Equals(TokenAddresses.USDC_POL_ADDRESS, StringComparison.OrdinalIgnoreCase) == true ||
            Token1?.Address.Equals(TokenAddresses.USDC_POL_ADDRESS, StringComparison.OrdinalIgnoreCase) == true;

        // Helper property to identify WVTRU/USDC.pol pairs
        public bool IsWvtruUsdcPair =>
            (Token0?.Address.Equals(TokenAddresses.WVTRU_ADDRESS, StringComparison.OrdinalIgnoreCase) == true &&
             Token1?.Address.Equals(TokenAddresses.USDC_POL_ADDRESS, StringComparison.OrdinalIgnoreCase) == true) ||
            (Token1?.Address.Equals(TokenAddresses.WVTRU_ADDRESS, StringComparison.OrdinalIgnoreCase) == true &&
             Token0?.Address.Equals(TokenAddresses.USDC_POL_ADDRESS, StringComparison.OrdinalIgnoreCase) == true);

        // Helper property to identify VTRO/USDC.pol pairs
        public bool IsVtroUsdcPair =>
            (Token0?.Address.Equals(TokenAddresses.VTRO_ADDRESS, StringComparison.OrdinalIgnoreCase) == true &&
             Token1?.Address.Equals(TokenAddresses.USDC_POL_ADDRESS, StringComparison.OrdinalIgnoreCase) == true) ||
            (Token1?.Address.Equals(TokenAddresses.VTRO_ADDRESS, StringComparison.OrdinalIgnoreCase) == true &&
             Token0?.Address.Equals(TokenAddresses.USDC_POL_ADDRESS, StringComparison.OrdinalIgnoreCase) == true);
    }

    public class LpService
    {
        private readonly Web3 _web3;
        private readonly Contract _factory;
        private readonly string _rpcUrl;
        private readonly Action<string> _logger;
        private readonly int _requestTimeoutSeconds = 30;

        // Use the shared constants
        private const string USDC_POL_ADDRESS = TokenAddresses.USDC_POL_ADDRESS;
        private const string VTRO_ADDRESS = TokenAddresses.VTRO_ADDRESS;
        private const string WVTRU_ADDRESS = TokenAddresses.WVTRU_ADDRESS;

        private const string FactoryAbi = @"[{
            ""constant"":true,""inputs"":[],""name"":""allPairsLength"",
            ""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],
            ""stateMutability"":""view"",""type"":""function""
        },{
            ""constant"":true,""inputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],
            ""name"":""allPairs"",
            ""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],
            ""stateMutability"":""view"",""type"":""function""
        }]";

        private const string PairAbi = @"[{
            ""constant"":true,""inputs"":[],""name"":""token0"",
            ""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],
            ""stateMutability"":""view"",""type"":""function""
        },{
            ""constant"":true,""inputs"":[],""name"":""token1"",
            ""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],
            ""stateMutability"":""view"",""type"":""function""
        },{
            ""constant"":true,
            ""inputs"":[],
            ""name"":""getReserves"",
            ""outputs"":[
              {""internalType"":""uint112"",""name"":""_reserve0"",""type"":""uint112""},
              {""internalType"":""uint112"",""name"":""_reserve1"",""type"":""uint112""},
              {""internalType"":""uint32"",""name"":""_blockTimestampLast"",""type"":""uint32""}
            ],
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

        // Added Holder Mapping ABI
        private const string TokenHoldersAbi = @"[{
            ""constant"":true,""inputs"":[],""name"":""holderCount"",
            ""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],
            ""stateMutability"":""view"",""type"":""function""
        }]";

        public LpService(string rpcUrl, string factoryAddress)
        {
            _rpcUrl = rpcUrl;
            _web3 = new Web3(rpcUrl);
            _factory = _web3.Eth.GetContract(FactoryAbi, factoryAddress);
            _logger = msg => Console.WriteLine(msg);
        }

        public LpService(string rpcUrl, string factoryAddress, Action<string> logger = null)
        {
            _rpcUrl = rpcUrl;

            // Create a custom HTTP client with timeout settings
            var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(_requestTimeoutSeconds);

            // Create RPC client with custom HTTP client
            var client = new Nethereum.JsonRpc.Client.RpcClient(new Uri(rpcUrl), httpClient);
            _web3 = new Web3(client);

            _factory = _web3.Eth.GetContract(FactoryAbi, factoryAddress);
            _logger = logger ?? (msg => Console.WriteLine(msg));
        }

        public async Task<List<PairInfo>> GetAllPairsAsync()
        {
            try
            {
                _logger($"Starting pair loading from {_rpcUrl}");
                _logger($"Factory address: {_factory.Address}");

                var lengthFn = _factory.GetFunction("allPairsLength");
                var allPairsFn = _factory.GetFunction("allPairs");

                _logger("Calling allPairsLength...");
                var count = await lengthFn.CallAsync<BigInteger>().ConfigureAwait(false);
                _logger($"Found {count} total pairs");

                var uconv = new UnitConversion();

                // Configurable batch size
                var limit = new BigInteger(160);
                var maxCount = count > limit ? limit : count;
                _logger($"Will process up to {maxCount} pairs");

                // Get all pair addresses in parallel batches
                _logger("Fetching pair addresses in parallel...");
                var tasks = new List<Task<string>>();
                for (BigInteger i = 0; i < maxCount; i++)
                {
                    tasks.Add(allPairsFn.CallAsync<string>(i));
                }

                var pairAddresses = await Task.WhenAll(tasks).ConfigureAwait(false);
                _logger($"Successfully fetched {pairAddresses.Length} pair addresses");

                // Use a semaphore to limit concurrent requests
                using var semaphore = new SemaphoreSlim(5);

                // Process pairs in parallel but with throttling
                var pairTasks = pairAddresses.Select(async (pairAddr, index) =>
                {
                    try
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            _logger($"Processing pair {index}: {pairAddr}");
                            var pairCtr = _web3.Eth.GetContract(PairAbi, pairAddr);

                            // Get token addresses in parallel
                            _logger($"Fetching token addresses for pair {index}...");
                            var token0Task = pairCtr.GetFunction("token0").CallAsync<string>();
                            var token1Task = pairCtr.GetFunction("token1").CallAsync<string>();

                            await Task.WhenAll(token0Task, token1Task).ConfigureAwait(false);
                            var token0Addr = token0Task.Result;
                            var token1Addr = token1Task.Result;
                            _logger($"Token addresses: {token0Addr}, {token1Addr}");

                            var t0Ctr = _web3.Eth.GetContract(Erc20Abi, token0Addr);
                            var t1Ctr = _web3.Eth.GetContract(Erc20Abi, token1Addr);

                            // Get token0 details with error handling
                            _logger($"Fetching token0 details for pair {index}...");
                            string symbol0, name0;
                            int decimals0;
                            BigInteger totalSupply0;
                            int holderCount0 = 0; // Default to 0 which will display as N/A

                            try
                            {
                                var symbol0Task = t0Ctr.GetFunction("symbol").CallAsync<string>();
                                var name0Task = t0Ctr.GetFunction("name").CallAsync<string>();
                                var decimals0Task = t0Ctr.GetFunction("decimals").CallAsync<int>();
                                var totalSupply0Task = t0Ctr.GetFunction("totalSupply").CallAsync<BigInteger>();

                                // Try to get holder count if available from the contract directly
                                Task<int> holderCount0Task = null;
                                try
                                {
                                    var holdersContract = _web3.Eth.GetContract(TokenHoldersAbi, token0Addr);
                                    if (holdersContract.ContractBuilder.ContractABI.Functions.Any(f => f.Name == "holderCount"))
                                    {
                                        holderCount0Task = holdersContract.GetFunction("holderCount").CallAsync<int>();
                                    }
                                    // No else branch - we don't want any estimation
                                }
                                catch { /* Holder count function not available - keep as 0 */ }

                                await Task.WhenAll(
                                    symbol0Task,
                                    name0Task,
                                    decimals0Task,
                                    totalSupply0Task
                                );

                                symbol0 = symbol0Task.Result;
                                name0 = name0Task.Result;
                                decimals0 = decimals0Task.Result;
                                totalSupply0 = totalSupply0Task.Result;

                                // Get holder count result if task was created and completed successfully
                                if (holderCount0Task != null && holderCount0Task.Status == TaskStatus.RanToCompletion)
                                {
                                    holderCount0 = holderCount0Task.Result;
                                }
                                // No else branch - no estimation
                            }
                            catch (Exception ex)
                            {
                                _logger($"Error fetching token0 details: {ex.Message}. Using fallback values.");
                                symbol0 = $"T0_{token0Addr.Substring(0, 6)}";
                                name0 = "Unknown Token";
                                decimals0 = 18;
                                totalSupply0 = BigInteger.Parse("1000000000000000000000000");
                                holderCount0 = 0; // Keep as 0 for N/A
                            }

                            // Get token1 details with error handling
                            _logger($"Fetching token1 details for pair {index}...");
                            string symbol1, name1;
                            int decimals1;
                            BigInteger totalSupply1;
                            int holderCount1 = 0; // Default to 0 which will display as N/A

                            try
                            {
                                var symbol1Task = t1Ctr.GetFunction("symbol").CallAsync<string>();
                                var name1Task = t1Ctr.GetFunction("name").CallAsync<string>();
                                var decimals1Task = t1Ctr.GetFunction("decimals").CallAsync<int>();
                                var totalSupply1Task = t1Ctr.GetFunction("totalSupply").CallAsync<BigInteger>();

                                // Try to get holder count if available from the contract directly
                                Task<int> holderCount1Task = null;
                                try
                                {
                                    var holdersContract = _web3.Eth.GetContract(TokenHoldersAbi, token1Addr);
                                    if (holdersContract.ContractBuilder.ContractABI.Functions.Any(f => f.Name == "holderCount"))
                                    {
                                        holderCount1Task = holdersContract.GetFunction("holderCount").CallAsync<int>();
                                    }
                                    // No else branch - we don't want any estimation
                                }
                                catch { /* Holder count function not available - keep as 0 */ }

                                await Task.WhenAll(
                                    symbol1Task,
                                    name1Task,
                                    decimals1Task,
                                    totalSupply1Task
                                );

                                symbol1 = symbol1Task.Result;
                                name1 = name1Task.Result;
                                decimals1 = decimals1Task.Result;
                                totalSupply1 = totalSupply1Task.Result;

                                // Get holder count result if task was created and completed successfully
                                if (holderCount1Task != null && holderCount1Task.Status == TaskStatus.RanToCompletion)
                                {
                                    holderCount1 = holderCount1Task.Result;
                                }
                                // No else branch - no estimation
                            }
                            catch (Exception ex)
                            {
                                _logger($"Error fetching token1 details: {ex.Message}. Using fallback values.");
                                symbol1 = $"T1_{token1Addr.Substring(0, 6)}";
                                name1 = "Unknown Token";
                                decimals1 = 18;
                                totalSupply1 = BigInteger.Parse("1000000000000000000000000");
                                holderCount1 = 0; // Keep as 0 for N/A
                            }

                            // Create token objects
                            var token0 = new TokenInfo
                            {
                                Address = token0Addr,
                                Symbol = symbol0,
                                Name = name0,
                                Decimals = decimals0,
                                TotalSupply = uconv.FromWei(totalSupply0, decimals0)
                            };

                            var token1 = new TokenInfo
                            {
                                Address = token1Addr,
                                Symbol = symbol1,
                                Name = name1,
                                Decimals = decimals1,
                                TotalSupply = uconv.FromWei(totalSupply1, decimals1)
                            };

                            // Get reserves with retry logic
                            _logger($"Fetching reserves for pair {index}...");
                            ReservesOutput reserves = null;
                            decimal reserve0 = 0, reserve1 = 0;
                            decimal price = 0;

                            try
                            {
                                var reservesFn = pairCtr.GetFunction("getReserves");
                                reserves = await reservesFn.CallDeserializingToObjectAsync<ReservesOutput>().ConfigureAwait(false);

                                reserve0 = uconv.FromWei(reserves.Reserve0, token0.Decimals);
                                reserve1 = uconv.FromWei(reserves.Reserve1, token1.Decimals);

                                // Calculate actual price based on reserves - price of token0 in terms of token1
                                if (reserve0 > 0)
                                {
                                    price = reserve1 / reserve0;
                                    _logger($"Calculated price for {symbol0}/{symbol1}: {price} based on reserves");
                                }
                                else
                                {
                                    price = 0;
                                    _logger($"Zero reserves for {symbol0}, setting price to 0");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger($"Error fetching reserves: {ex.Message}. Using zero values.");
                            }

                            // Calculate liquidity using the actual reserves
                            var liquidity = reserve1; // Base liquidity in token1
                            if (price > 0)
                            {
                                liquidity += reserve0 * price; // Add token0 converted to token1 value
                            }

                            // Create pair info
                            _logger($"Successfully processed pair {index}: {token0.Symbol}/{token1.Symbol} - Price: {price}");
                            return new PairInfo
                            {
                                Address = pairAddr,
                                Token0 = token0,
                                Token1 = token1,
                                Reserve0 = reserve0,
                                Reserve1 = reserve1,
                                Price = price,
                                Liquidity = liquidity,
                                HolderCount = Math.Max(holderCount0, holderCount1) // Use the higher holder count
                            };
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger($"Error processing pair {index} ({pairAddr}): {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            _logger($"Inner exception: {ex.InnerException.Message}");
                        }
                        return null;
                    }
                }).ToArray();

                _logger("Waiting for all pair processing to complete...");
                var pairResults = await Task.WhenAll(pairTasks).ConfigureAwait(false);
                var validResults = pairResults.Where(p => p != null).ToList();
                // Fix string interpolation by explicitly using the Count property
                _logger($"Finished processing pairs. Retrieved {validResults.Count} valid pairs out of {pairAddresses.Length} total");

                // Special sorting: WVTRU/USDC.pol pairs first, then VTRO/USDC.pol pairs, 
                // then other USDC.pol pairs, then sort the rest by liquidity
                var sortedPairs = validResults
                    .OrderByDescending(p => p.IsWvtruUsdcPair) // WVTRU/USDC.pol pairs first
                    .ThenByDescending(p => p.IsVtroUsdcPair)   // VTRO/USDC.pol pairs second
                    .ThenByDescending(p => p.ContainsUsdcPol)  // Other USDC.pol pairs third
                    .ThenByDescending(p => p.Liquidity)        // Then sort by liquidity
                    .ToList();

                _logger("Sorting completed - prioritized WVTRU/USDC.pol and VTRO/USDC.pol pairs");
                return sortedPairs;
            }
            catch (Exception ex)
            {
                _logger($"FATAL ERROR in GetAllPairsAsync: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger($"Inner exception: {ex.InnerException.Message}");
                }
                _logger($"Stack trace: {ex.StackTrace}");
                return new List<PairInfo>();
            }
        }

        // Helper method to estimate holder count - always returns 0 for N/A
        private int EstimateHolderCount(decimal totalSupply)
        {
            // No estimation, always return 0 to indicate unavailable data
            return 0;
        }


   
        public async Task<PairInfo> GetPairInfoAsync(string pairAddress)
        {
            try
            {
                _logger($"Getting info for pair {pairAddress}");
                var uconv = new UnitConversion();

                var pairCtr = _web3.Eth.GetContract(PairAbi, pairAddress);

                // Get token addresses
                var token0Addr = await pairCtr.GetFunction("token0").CallAsync<string>();
                var token1Addr = await pairCtr.GetFunction("token1").CallAsync<string>();

                var t0Ctr = _web3.Eth.GetContract(Erc20Abi, token0Addr);
                var t1Ctr = _web3.Eth.GetContract(Erc20Abi, token1Addr);

                // Get token details
                var token0 = new TokenInfo
                {
                    Address = token0Addr,
                    Symbol = await t0Ctr.GetFunction("symbol").CallAsync<string>(),
                    Name = await t0Ctr.GetFunction("name").CallAsync<string>(),
                    Decimals = await t0Ctr.GetFunction("decimals").CallAsync<int>()
                };

                var totalSupply0 = await t0Ctr.GetFunction("totalSupply").CallAsync<BigInteger>();
                token0.TotalSupply = uconv.FromWei(totalSupply0, token0.Decimals);

                // Try to get holder count for token0
                int holderCount0 = 0;
                try
                {
                    var holdersContract = _web3.Eth.GetContract(TokenHoldersAbi, token0Addr);
                    if (holdersContract.ContractBuilder.ContractABI.Functions.Any(f => f.Name == "holderCount"))
                    {
                        holderCount0 = await holdersContract.GetFunction("holderCount").CallAsync<int>();
                    }
                    else
                    {
                        holderCount0 = EstimateHolderCount(token0.TotalSupply);
                    }
                }
                catch
                {
                    holderCount0 = EstimateHolderCount(token0.TotalSupply);
                }

                var token1 = new TokenInfo
                {
                    Address = token1Addr,
                    Symbol = await t1Ctr.GetFunction("symbol").CallAsync<string>(),
                    Name = await t1Ctr.GetFunction("name").CallAsync<string>(),
                    Decimals = await t1Ctr.GetFunction("decimals").CallAsync<int>()
                };

                var totalSupply1 = await t1Ctr.GetFunction("totalSupply").CallAsync<BigInteger>();
                token1.TotalSupply = uconv.FromWei(totalSupply1, token1.Decimals);

                // Try to get holder count for token1
                int holderCount1 = 0;
                try
                {
                    var holdersContract = _web3.Eth.GetContract(TokenHoldersAbi, token1Addr);
                    if (holdersContract.ContractBuilder.ContractABI.Functions.Any(f => f.Name == "holderCount"))
                    {
                        holderCount1 = await holdersContract.GetFunction("holderCount").CallAsync<int>();
                    }
                    else
                    {
                        holderCount1 = EstimateHolderCount(token1.TotalSupply);
                    }
                }
                catch
                {
                    holderCount1 = EstimateHolderCount(token1.TotalSupply);
                }

                // Get reserves and calculate price directly from them
                var reserves = await pairCtr.GetFunction("getReserves").CallDeserializingToObjectAsync<ReservesOutput>();
                var reserve0 = uconv.FromWei(reserves.Reserve0, token0.Decimals);
                var reserve1 = uconv.FromWei(reserves.Reserve1, token1.Decimals);

                // Calculate actual price based on reserves
                decimal price = 0;
                if (reserve0 > 0)
                {
                    price = reserve1 / reserve0;
                    _logger($"Calculated price for {token0.Symbol}/{token1.Symbol}: {price} based on reserves");
                }
                else
                {
                    _logger($"Zero reserves for {token0.Symbol}, setting price to 0");
                }

                // Calculate liquidity using actual reserves
                var liquidity = reserve1; // Base liquidity in token1
                if (price > 0)
                {
                    liquidity += reserve0 * price; // Add token0 converted to token1 value
                }

                return new PairInfo
                {
                    Address = pairAddress,
                    Token0 = token0,
                    Token1 = token1,
                    Reserve0 = reserve0,
                    Reserve1 = reserve1,
                    Price = price,
                    Liquidity = liquidity,
                    HolderCount = Math.Max(holderCount0, holderCount1) // Use the higher holder count
                };
            }
            catch (Exception ex)
            {
                _logger($"Error getting pair info: {ex.Message}");
                return null;
            }
        }

        private async Task<decimal> GetPriceEstimateAsync(Web3 web3, string token0Addr, string token1Addr, UnitConversion uconv)
        {
            // Improved price estimation with fallback
            try
            {
                // Try to find a direct pair for price reference
                var factoryAddr = _factory.Address;
                var factoryContract = web3.Eth.GetContract(FactoryAbi, factoryAddr);

                var getPairFn = factoryContract.GetFunction("getPair");
                var pairAddr = await getPairFn.CallAsync<string>(token0Addr, token1Addr);

                if (pairAddr != "0x0000000000000000000000000000000000000000")
                {
                    var pairContract = web3.Eth.GetContract(PairAbi, pairAddr);
                    var reservesFn = pairContract.GetFunction("getReserves");

                    var reserves = await reservesFn.CallDeserializingToObjectAsync<ReservesOutput>();

                    // Get token decimals
                    var t0Contract = web3.Eth.GetContract(Erc20Abi, token0Addr);
                    var t1Contract = web3.Eth.GetContract(Erc20Abi, token1Addr);

                    int decimals0 = 18, decimals1 = 18;
                    try
                    {
                        decimals0 = await t0Contract.GetFunction("decimals").CallAsync<int>();
                        decimals1 = await t1Contract.GetFunction("decimals").CallAsync<int>();
                    }
                    catch { /* Use default decimals */ }

                    if (reserves.Reserve0 > 0)
                    {
                        var reserve0 = uconv.FromWei(reserves.Reserve0, decimals0);
                        var reserve1 = uconv.FromWei(reserves.Reserve1, decimals1);

                        if (reserve0 > 0)
                        {
                            return reserve1 / reserve0;
                        }
                    }
                }

                // If we can't get a direct price, return 1:1
                return 1.0m;
            }
            catch
            {
                return 1.0m; // Default price ratio of 1:1 if anything fails
            }
        }
    }
}
