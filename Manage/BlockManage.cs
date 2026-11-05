using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Numerics;
using Solana.Unity.Rpc.Core.Http;
using Nethereum.Web3;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace jmsTools.Manage
{

    public class RpcClientManager
    {
        private List<string> rpcUrls;
        private Random random = new Random();




   
        public RpcClientManager(string filePath)
        {
            rpcUrls = File.ReadAllLines(filePath).ToList();
        }

        public string GetRandomRpcUrl()
        {
            return rpcUrls[random.Next(rpcUrls.Count)];
        }
    }

    public sealed class BalanceQueryResult
    {
        public bool Success { get; set; }
        public decimal Balance { get; set; }
        public string Error { get; set; }
        public string NodeDiagnostics { get; set; }
        public static BalanceQueryResult Failed(string error) { return new BalanceQueryResult { Error = error }; }
    }

    public static class BlockManage
    {

        /// <summary>
        /// 根据主网查询交易次数,SOL链不直接提供交易次数接口，所以直接查余额,用于碰撞
        /// </summary>
        /// <param name="address"></param>
        /// <param name="selectedNetwork"></param>
        /// <returns></returns>
        public static async Task<decimal?> GetBalanceInfoByPz(string address, string selectedNetwork)
        {
            var result = await GetBalanceResultAsync(address, selectedNetwork);
            return result.Success ? (decimal?)result.Balance : null;
        }

        private static readonly HttpClient BalanceClient = CreateBalanceClient();
        private const int MaxConcurrency = 500;
        private const double MaxRequestsPerSecond = 500;
        private const double DefaultRequestsPerSecond = 100;
        private static int maxConcurrentRequests = LoadConcurrency();
        private static System.Threading.SemaphoreSlim BalanceGate = new System.Threading.SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
        private static DateTime nextBalanceRequest = DateTime.MinValue;
        private static DateTime lastTokenRefillUtc = DateTime.UtcNow;
        private static int roundRobinCounter;
        private static int rateLimitStrikes;
        private static readonly object NodeCacheLock = new object();
        private static readonly Dictionary<string, BalanceNodeCacheEntry> NodeCache =
            new Dictionary<string, BalanceNodeCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly object RateScheduleLock = new object();
        private static readonly TimeSpan MinRequestInterval = TimeSpan.FromSeconds(1 / MaxRequestsPerSecond);
        private static readonly TimeSpan MaxRequestInterval = TimeSpan.FromSeconds(60);
        private static double requestsPerSecond = LoadRequestsPerSecond();
        private static double requestTokens = BurstCapacity(requestsPerSecond);

        /// <summary>
        /// 兼容旧接口：由目标请求速率换算出的平均间隔。
        /// </summary>
        public static TimeSpan RequestInterval
        {
            get
            {
                lock (RateScheduleLock)
                    return TimeSpan.FromSeconds(1d / requestsPerSecond);
            }
            set { RequestsPerSecond = 1d / ClampRequestInterval(value).TotalSeconds; }
        }

        /// <summary>
        /// 全局目标请求速率。令牌桶允许小批量突发，再按该速率补充令牌。
        /// </summary>
        public static double RequestsPerSecond
        {
            get
            {
                lock (RateScheduleLock) return requestsPerSecond;
            }
            set
            {
                var clamped = ClampRequestsPerSecond(value);
                lock (RateScheduleLock)
                {
                    requestsPerSecond = clamped;
                    requestTokens = BurstCapacity(clamped);
                    lastTokenRefillUtc = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// 全局并发请求数，默认 5。修改后对后续请求生效。
        /// </summary>
        public static int MaxConcurrentRequests
        {
            get { return maxConcurrentRequests; }
            set
            {
                var clamped = value < 1 ? 1 : value > MaxConcurrency ? MaxConcurrency : value;
                if (clamped == maxConcurrentRequests) return;
                maxConcurrentRequests = clamped;
                BalanceGate = new System.Threading.SemaphoreSlim(clamped, clamped);
            }
        }

        private static double LoadRequestsPerSecond()
        {
            try
            {
                var path = Path.Combine(AppPaths.DataDirectory, "RequestRate.txt");
                if (File.Exists(path))
                {
                    double rate;
                    if (double.TryParse(File.ReadAllText(path).Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out rate))
                        return ClampRequestsPerSecond(rate);
                }
            }
            catch { }

            try
            {
                var path = Path.Combine(AppPaths.DataDirectory, "RateLimit.txt");
                if (File.Exists(path))
                {
                    double seconds;
                    if (double.TryParse(File.ReadAllText(path).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                    {
                        // 旧版最小值 0.05 秒代表“最快”，迁移到新的高速默认值。
                        if (seconds <= 0.050001)
                            return DefaultRequestsPerSecond;
                        return ClampRequestsPerSecond(1d / seconds);
                    }
                }
            }
            catch { }
            return DefaultRequestsPerSecond;
        }

        public static void SaveRequestInterval()
        {
            SaveRequestsPerSecond();
        }

        public static void SaveRequestsPerSecond()
        {
            try
            {
                double rate;
                lock (RateScheduleLock) rate = requestsPerSecond;
                File.WriteAllText(
                    Path.Combine(AppPaths.DataDirectory, "RequestRate.txt"),
                    rate.ToString("0.##", CultureInfo.InvariantCulture));
                File.WriteAllText(
                    Path.Combine(AppPaths.DataDirectory, "RateLimit.txt"),
                    (1d / rate).ToString("0.000000", CultureInfo.InvariantCulture));
            }
            catch { }
        }

        private static int LoadConcurrency()
        {
            try
            {
                var path = Path.Combine(AppPaths.DataDirectory, "Concurrency.txt");
                if (File.Exists(path))
                {
                    int count;
                    if (int.TryParse(File.ReadAllText(path).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                        return count < 1 ? 1 : count > MaxConcurrency ? MaxConcurrency : count;
                }
            }
            catch { }
            return 50;
        }

        public static void SaveConcurrency()
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(AppPaths.DataDirectory, "Concurrency.txt"),
                    maxConcurrentRequests.ToString(CultureInfo.InvariantCulture));
            }
            catch { }
        }

        private static TimeSpan ClampRequestInterval(TimeSpan value)
        {
            return value < MinRequestInterval ? MinRequestInterval : value > MaxRequestInterval ? MaxRequestInterval : value;
        }

        private static double ClampRequestsPerSecond(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                return DefaultRequestsPerSecond;
            return Math.Max(1d, Math.Min(MaxRequestsPerSecond, value));
        }

        private static double BurstCapacity(double rate)
        {
            return Math.Max(1d, Math.Min(250d, Math.Ceiling(rate / 2d)));
        }

        private static async Task ReserveRequestSlotAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TimeSpan delay;
                lock (RateScheduleLock)
                {
                    var now = DateTime.UtcNow;
                    if (nextBalanceRequest > now)
                    {
                        delay = nextBalanceRequest - now;
                    }
                    else
                    {
                        double elapsed = Math.Max(0d, (now - lastTokenRefillUtc).TotalSeconds);
                        lastTokenRefillUtc = now;
                        requestTokens = Math.Min(BurstCapacity(requestsPerSecond),
                            requestTokens + elapsed * requestsPerSecond);
                        if (requestTokens >= 1d)
                        {
                            requestTokens -= 1d;
                            return;
                        }
                        delay = TimeSpan.FromSeconds((1d - requestTokens) / requestsPerSecond);
                    }
                }
                if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
            }
        }

        private static int RegisterRateLimit(HttpResponseMessage response)
        {
            int strikes = System.Threading.Interlocked.Increment(ref rateLimitStrikes);
            int seconds = Math.Min(30, 3 << Math.Min(3, Math.Max(0, strikes - 1)));
            var retryAfter = response == null ? null : response.Headers.RetryAfter;
            if (retryAfter != null)
            {
                if (retryAfter.Delta.HasValue)
                    seconds = (int)Math.Ceiling(retryAfter.Delta.Value.TotalSeconds);
                else if (retryAfter.Date.HasValue)
                    seconds = (int)Math.Ceiling((retryAfter.Date.Value - DateTimeOffset.UtcNow).TotalSeconds);
            }
            seconds = Math.Max(1, Math.Min(30, seconds));
            lock (RateScheduleLock)
            {
                var pauseUntil = DateTime.UtcNow.AddSeconds(seconds);
                if (pauseUntil > nextBalanceRequest) nextBalanceRequest = pauseUntil;
                requestTokens = 0;
                lastTokenRefillUtc = nextBalanceRequest;
            }
            return seconds;
        }

        private static void RegisterSuccessfulRequest()
        {
            System.Threading.Interlocked.Exchange(ref rateLimitStrikes, 0);
        }

        private static HttpClient CreateBalanceClient()
        {
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("JmsTools/1.0");
            return client;
        }

        public static string NodeFile(string network)
        {
            string file;
            switch (network.ToUpperInvariant()) {
                case "ETH": file = "Ethrpc_nodes.txt"; break;
                case "BSC": file = "Bscrpc_nodes.txt"; break;
                case "SOL": file = "Solrpc_nodes.txt"; break;
                default: throw new ArgumentException("不支持的主网");
            }
            var path = Path.Combine(AppPaths.DataDirectory, file);
            if (!File.Exists(path))
            {
                string node = network.Equals("ETH", StringComparison.OrdinalIgnoreCase)
                    ? "https://ethereum-rpc.publicnode.com" : network.Equals("BSC", StringComparison.OrdinalIgnoreCase)
                    ? "https://bsc-dataseed.binance.org" : "https://api.mainnet-beta.solana.com";
                try
                {
                    using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.WriteLine(node);
                }
                catch (IOException) when (File.Exists(path)) { }
            }
            return path;
        }

        // 独立配置只供手动公开地址查询使用，不影响自动任务的节点文件。
        public static string EnsurePublicNodeFile(string network)
        {
            var original = NodeFile(network);
            network = network.ToUpperInvariant();
            var directory = Path.Combine(AppPaths.DataDirectory, "PublicRpcNodes");
            var file = Path.Combine(directory, network + ".txt");
            if (File.Exists(file)) return file;
            var nodes = File.ReadAllLines(original).Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#")).Distinct().ToList();
            if (network == "BSC" && !nodes.Contains("https://public.1rpc.io/bnb"))
                nodes.Insert(Math.Min(2, nodes.Count), "https://public.1rpc.io/bnb");
            var lines = new List<string> {
                "# 仅用于单地址公开查询。保存后下次查询生效，不会发送助记词或私钥。",
                "# 每行一个完整 HTTPS 主网 RPC 地址；# 开头为注释。仅使用前 3 个不同的非注释地址。",
                "# 自定义服务请从服务商控制台复制含本人 API Key 的完整地址，替换或移到前三行。",
                "# 连接/服务端故障时尝试备用；额度、权限或限流错误会停止，不切换绕过限制。",
                "# Alchemy（ETH/BSC/SOL）：https://dashboard.alchemy.com/",
                "# Ankr（ETH/BSC/SOL）：https://www.ankr.com/rpc/",
                "# dRPC（ETH/BSC/SOL）：https://drpc.org/",
                "# Helius（仅 SOL）：https://dashboard.helius.dev/",
                "# 上述服务需自行申请账户/凭证；没有预填任何 API Key 或占位地址。",
                "# 2026-11-01 本机检查：1RPC BSC 可用；1RPC ETH 和 dRPC ETH/BSC 连接失败，未启用。",
                ""
            };
            lines.AddRange(nodes);
            Directory.CreateDirectory(directory);
            FileStream stream;
            try { stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write); }
            catch (IOException) when (File.Exists(file)) { return file; }
            using (stream)
            using (var writer = new StreamWriter(stream, new UTF8Encoding(true)))
                foreach (var line in lines) writer.WriteLine(line);
            return file;
        }

        public static string[] ReadPublicNodeUrls(string network)
        {
            return File.ReadAllLines(EnsurePublicNodeFile(network)).Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#")).Distinct().Take(3).ToArray();
        }

        public static void SavePublicNodeUrls(string network, string[] values)
        {
            if (values == null) throw new ArgumentException("请填写节点地址。");
            var nodes = values.Select(value => (value ?? "").Trim()).Where(value => value.Length > 0).ToArray();
            if (nodes.Length == 0 || nodes.Length > 3) throw new ArgumentException("每个网络请填写 1 至 3 个节点地址。");
            foreach (var node in nodes)
            {
                Uri uri;
                if (node.Any(char.IsWhiteSpace) || node.Contains("<") || node.Contains(">") ||
                    !Uri.TryCreate(node, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps ||
                    string.IsNullOrWhiteSpace(uri.Host) || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0)
                    throw new ArgumentException("请填写完整的 HTTPS RPC 地址，不要包含空格、换行或占位符。");
            }
            if (nodes.Distinct(StringComparer.Ordinal).Count() != nodes.Length)
                throw new ArgumentException("节点地址重复，请删除重复项。");
            var file = EnsurePublicNodeFile(network);
            var comments = File.ReadAllLines(file).Where(line => line.TrimStart().StartsWith("#"));
            var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllLines(temporary, comments.Concat(new[] { "" }).Concat(nodes), new UTF8Encoding(true));
                File.Replace(temporary, file, null);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public static async Task<BalanceQueryResult> GetBalanceResultAsync(string address, string network)
        {
            var gate = BalanceGate;
            await gate.WaitAsync();
            try
            {
                await ReserveRequestSlotAsync();
                Uri[] nodes;
                try { nodes = GetBalanceNodes(network); }
                catch (IOException) { return BalanceQueryResult.Failed("无法读取节点配置文件"); }
                if (nodes.Length == 0)
                    return BalanceQueryResult.Failed("节点配置无效，请填写 HTTPS RPC 地址");

                var start = (int)((uint)System.Threading.Interlocked.Increment(ref roundRobinCounter) % (uint)nodes.Length);
                var attempts = new List<string>();
                for (int i = 0; i < nodes.Length; i++)
                {
                    var uri = nodes[(start + i) % nodes.Length];
                    try
                    {
                        var result = await QueryBalanceNodeAsync(uri, address, network);
                        attempts.Add(uri.Host + "：查询成功");
                        result.NodeDiagnostics = string.Join("；", attempts);
                        return result;
                    }
                    catch (PublicNodeException ex)
                    {
                        attempts.Add(uri.Host + "：" + ex.Message);
                        if (!ex.CanTryNext)
                        {
                            var stopped = BalanceQueryResult.Failed(ex.Message);
                            stopped.NodeDiagnostics = string.Join("；", attempts);
                            return stopped;
                        }
                    }
                    catch (TaskCanceledException) { attempts.Add(uri.Host + "：连接超时"); }
                    catch (HttpRequestException) { attempts.Add(uri.Host + "：连接失败"); }
                    catch (Newtonsoft.Json.JsonException) { attempts.Add(uri.Host + "：响应格式无效"); }
                    catch (Exception) { attempts.Add(uri.Host + "：响应或配置无效"); }
                }
                var failed = BalanceQueryResult.Failed("所有配置线路均未能完成余额查询");
                failed.NodeDiagnostics = string.Join("；", attempts);
                return failed;
            }
            finally { gate.Release(); }
        }

        private static Uri[] GetBalanceNodes(string network)
        {
            string path = NodeFile(network);
            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
            lock (NodeCacheLock)
            {
                BalanceNodeCacheEntry cached;
                if (NodeCache.TryGetValue(path, out cached) && cached.LastWriteUtc == lastWriteUtc)
                    return cached.Nodes;
            }

            Uri[] nodes = File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#"))
                .Select(line => {
                    Uri uri;
                    return Uri.TryCreate(line, UriKind.Absolute, out uri) && uri.Scheme == "https" ? uri : null;
                })
                .Where(uri => uri != null)
                .Distinct()
                .ToArray();
            lock (NodeCacheLock)
                NodeCache[path] = new BalanceNodeCacheEntry {
                    LastWriteUtc = lastWriteUtc,
                    Nodes = nodes
                };
            return nodes;
        }

        private sealed class BalanceNodeCacheEntry
        {
            public DateTime LastWriteUtc;
            public Uri[] Nodes;
        }

        private static async Task<BalanceQueryResult> QueryBalanceNodeAsync(Uri uri, string address, string network)
        {
            var payload = new {
                jsonrpc = "2.0", id = 1,
                method = network.Equals("SOL", StringComparison.OrdinalIgnoreCase) ? "getBalance" : "eth_getBalance",
                @params = GetParamsByMainNet(network, address)
            };
            using (var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"))
            using (var response = await BalanceClient.PostAsync(uri, content))
            {
                if (!response.IsSuccessStatusCode)
                {
                    var code = (int)response.StatusCode;
                    int cooldown = code == 429 ? RegisterRateLimit(response) : 0;
                    string reason = code == 402 ? "节点额度耗尽" : code == 401 ? "节点凭证无效" :
                        code == 403 ? "节点拒绝访问" : code == 429 ? "节点限流，暂停" + cooldown + "秒后再试" : "节点请求失败";
                    throw new PublicNodeException(reason + "（HTTP " + code + "，" + uri.Host + "）", code >= 500 && code <= 599);
                }
                RegisterSuccessfulRequest();
                var result = ParseBalanceResponse(await response.Content.ReadAsStringAsync(), network);
                if (!result.Success) throw new PublicNodeException(result.Error, false);
                return result;
            }
        }

        // 仅用于手动输入公开地址的查询页；原有自动任务不调用此入口。
        public static async Task<BalanceQueryResult> GetPublicAddressBalanceResultAsync(string address, string network, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
        {
            var gate = BalanceGate;
            await gate.WaitAsync(cancellationToken);
            try
            {
                var nodes = ReadPublicNodeUrls(network);
                return await QueryPublicNodesAsync(address, network, nodes, BalanceClient, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (IOException) { return BalanceQueryResult.Failed("无法读取节点配置文件"); }
            catch (Exception) { return BalanceQueryResult.Failed("节点响应或配置无效"); }
            finally { gate.Release(); }
        }

        private sealed class PublicNodeException : Exception
        {
            public bool CanTryNext { get; }
            public PublicNodeException(string message, bool canTryNext) : base(message) { CanTryNext = canTryNext; }
        }

        private static async Task<Newtonsoft.Json.Linq.JObject> SendPublicRpcAsync(HttpClient client, Uri uri, string method, object[] parameters, System.Threading.CancellationToken cancellationToken)
        {
            await ReserveRequestSlotAsync(cancellationToken);
            var payload = new { jsonrpc = "2.0", id = 1, method, @params = parameters };
            using (var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"))
            using (var response = await client.PostAsync(uri, content, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    int code = (int)response.StatusCode;
                    int cooldown = code == 429 ? RegisterRateLimit(response) : 0;
                    var reason = code == 429 ? "节点限流，暂停" + cooldown + "秒后再试" : code == 402 ? "节点额度耗尽" :
                        code == 401 || code == 403 ? "节点凭证或访问权限错误" : "节点请求失败";
                    throw new PublicNodeException(reason + "（HTTP " + code + "）", code >= 500 && code <= 599);
                }
                RegisterSuccessfulRequest();
                var json = Newtonsoft.Json.Linq.JObject.Parse(await response.Content.ReadAsStringAsync());
                if (json["error"] != null)
                    throw new PublicNodeException("RPC错误 " + (string)json["error"]["code"] + "，请检查节点额度、权限或请求参数", false);
                return json;
            }
        }

        private static async Task<BalanceQueryResult> QueryPublicNodesAsync(string address, string network, string[] nodes, HttpClient client, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
        {
            string expected;
            switch (network)
            {
                case "ETH": expected = "0x1"; break;
                case "BSC": expected = "0x38"; break;
                case "SOL": expected = "5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d"; break;
                default: return BalanceQueryResult.Failed("不支持的主网");
            }
            var attempts = new List<string>();
            foreach (var node in nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Uri uri;
                if (!Uri.TryCreate(node, UriKind.Absolute, out uri) || uri.Scheme != "https")
                {
                    attempts.Add("跳过无效HTTPS节点配置");
                    continue;
                }
                try
                {
                    var identity = await SendPublicRpcAsync(client, uri, network == "SOL" ? "getGenesisHash" : "eth_chainId", new object[0], cancellationToken);
                    var actual = (string)identity["result"];
                    bool matches = network == "SOL" ? actual == expected : string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
                    if (!matches) throw new PublicNodeException("主网标识不匹配，已拒绝使用", true);
                    var response = await SendPublicRpcAsync(client, uri, network == "SOL" ? "getBalance" : "eth_getBalance", GetParamsByMainNet(network, address), cancellationToken);
                    var result = ParseBalanceResponse(response.ToString(), network);
                    if (!result.Success) throw new PublicNodeException(result.Error, true);
                    attempts.Add(uri.Host + "：主网校验通过、查询成功");
                    result.NodeDiagnostics = string.Join("；", attempts);
                    return result;
                }
                catch (PublicNodeException ex)
                {
                    attempts.Add(uri.Host + "：" + ex.Message);
                    if (!ex.CanTryNext)
                    {
                        var stopped = BalanceQueryResult.Failed(ex.Message);
                        stopped.NodeDiagnostics = string.Join("；", attempts);
                        return stopped;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (TaskCanceledException) { attempts.Add(uri.Host + "：连接超时"); }
                catch (HttpRequestException) { attempts.Add(uri.Host + "：连接失败"); }
                catch (Newtonsoft.Json.JsonException) { attempts.Add(uri.Host + "：响应格式无效"); }
                catch (InvalidCastException) { attempts.Add(uri.Host + "：主网标识格式无效"); }
                catch (ArgumentException) { attempts.Add(uri.Host + "：响应字段格式无效"); }
            }
            var failed = BalanceQueryResult.Failed(nodes.Length == 0 ? "未配置可用节点" : "所有配置线路均未能完成主网余额查询");
            failed.NodeDiagnostics = string.Join("；", attempts);
            return failed;
        }

        internal static BalanceQueryResult ParseBalanceResponse(string json, string network)
        {
            try {
                var root = Newtonsoft.Json.Linq.JObject.Parse(json);
                if (root["error"] != null) {
                    var code = (string)root["error"]["code"];
                    return BalanceQueryResult.Failed("RPC错误 " + code + "，请检查节点权限、额度及地址格式");
                }
                var result = root["result"];
                if (result == null || result.Type == Newtonsoft.Json.Linq.JTokenType.Null)
                    return BalanceQueryResult.Failed("节点未返回余额");
                if (network.Equals("SOL", StringComparison.OrdinalIgnoreCase)) {
                    if (result["value"] == null) return BalanceQueryResult.Failed("SOL余额响应格式无效");
                    return new BalanceQueryResult { Success = true, Balance = ((ulong)result["value"]) / 1000000000m };
                }
                var hex = (string)result;
                BigInteger value;
                if (hex == null || !hex.StartsWith("0x") || hex.Length <= 2 ||
                    !BigInteger.TryParse("0" + hex.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value))
                    return BalanceQueryResult.Failed("余额格式无效");
                return new BalanceQueryResult { Success = true, Balance = Web3.Convert.FromWei(value) };
            }
            catch (Exception) { return BalanceQueryResult.Failed("节点响应解析失败"); }
        }

        /// <summary>
        /// 根据主网查询交易次数,SOL链不直接提供交易次数接口，所以直接查余额, 其他链还继续查交易记录等信息，用于单地址查询
        /// </summary>
        /// <param name="address"></param>
        /// <param name="selectedNetwork"></param>
        /// <returns></returns>
        public static async Task<(decimal? Balance, List<TransactionRecord> Transactions)> GetBalanceInfo(string address, string selectedNetwork)
        {

            decimal? balance = null;
            int transactionCount = 0;
            var transactions = new List<TransactionRecord>();

            switch (selectedNetwork.ToUpper())
            {
                case "ETH":
                case "BSC":
                    // 对于以太坊和币安智能链，保持原有逻辑不变
                    string[] rpcNodes;
                    if (selectedNetwork.ToUpper() == "ETH")
                    {
                        rpcNodes = File.ReadAllLines("Ethrpc_nodes.txt");
                    }
                    else // BSC
                    {
                        rpcNodes = File.ReadAllLines("Bscrpc_nodes.txt");
                    }

                    using (var client = new HttpClient())
                    {
                        var url = rpcNodes[new Random().Next(rpcNodes.Length)];


                        Web3 _web3 = new Web3(url);

                     

                        var payload = new
                        {
                            jsonrpc = "2.0",
                            method = GetMethodByMainNet(selectedNetwork),
                            @params = GetParamsByMainNet(selectedNetwork, address),
                            id = 1
                        };

                        var jsonString = System.Text.Json.JsonSerializer.Serialize(payload);
                        var stringContent = new StringContent(jsonString, System.Text.Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(url, stringContent);

                        if (response.IsSuccessStatusCode)
                        {
                            var jsonResponse = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
                            // 结果是以wei为单位的十六进制字符串形式返回
                            var balanceInWeiHex = jsonResponse.GetProperty("result").GetString();

                            // 将十六进制字符串转换为BigInteger
                            bool success = BigInteger.TryParse("0" + balanceInWeiHex.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out BigInteger balanceInWei);
                            if (!success)
                            {

                                return (balance, transactions);
                            }

                            balance = Web3.Convert.FromWei(balanceInWei); // 使用通用转换方法
                        }
                        // 获取交易记录
                        transactions = await GetTransactionRecordsForEthOrBsc(_web3, address);
                        transactionCount = transactions.Count;
                    }
                    break;

                case "SOL":

                    var rpcManager = new RpcClientManager("Solrpc_nodes.txt");
                    var rpcUrl = rpcManager.GetRandomRpcUrl();//随机使用rpc，防止单一rpc请求限制

                    // 对于Solana，使用Solana.Unity.Rpc查询余额
                    var rpcClient = ClientFactory.GetClient(rpcUrl); // 或者Cluster.Testnet, Cluster.Devnet等



                    try
                    {
                        var balanceResponse = await rpcClient.GetBalanceAsync(address);


                        if (balanceResponse.WasSuccessful)
                        {
                            // 确保balanceResponse.Result是可以进行除法运算的数值类型
                            if (balanceResponse.Result is ResponseValue<ulong> lamports)
                            {
                                decimal solBalance = lamports.Value / (decimal)1e9; // 转换为SOL
                                //return solBalance > 0 ? solBalance : 0;

                                return (solBalance, transactions);

                            }
                            else
                            {
                                return (balance, transactions);
                            }
                        }
                        else
                        {
                            //return null; // 查询失败时返回null
                        }
                    }
                    catch (Exception ex)
                    {
                        //return null; // 错误发生时返回null
                        return (balance, transactions);
                    }
                    break;

                default:
                    throw new NotSupportedException("不支持的网络类型");
            }

            return (balance, transactions);
        }



        /// <summary>
        /// 查询ETH及BSC链的交易记录逻辑，过滤合约交互及代币转移，仅保留链上转账交易
        /// </summary>
        /// <param name="web3"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        private static async Task<List<TransactionRecord>> GetTransactionRecordsForEthOrBsc(Web3 web3, string address)
        {
            var transactions = new List<TransactionRecord>();
            var blockNumberHex = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            // 使用Value属性获取ulong类型的最新区块号
            var latestBlockNumber = (ulong)blockNumberHex.Value;

            // 定义起始块和结束块（这里我们查询最新块之前的500个块作为示例）
            var startBlock = Math.Max(0, latestBlockNumber - 200);
            var endBlock = latestBlockNumber;


            // 使用列表存储所有要执行的任务
            var tasks = new List<Task>();
            for (var block = startBlock; block <= endBlock; block++)
            {
                tasks.Add(Task.Run(async () => {

                    try
                    {
                        var blockWithTransactions = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new Nethereum.Hex.HexTypes.HexBigInteger(block));

                        if (blockWithTransactions!=null)
                        {
                            foreach (var transaction in blockWithTransactions.Transactions)
                            {

                                // 过滤掉合约交互，只保留普通转账
                                if ((transaction.From.Equals(address, StringComparison.OrdinalIgnoreCase) ||
                                     (!string.IsNullOrEmpty(transaction.To) && transaction.To.Equals(address, StringComparison.OrdinalIgnoreCase))) &&
                                    (transaction.Input == "0x"))
                                {

                                    // 获取区块时间戳并转换为DateTime
                                    var timestampInSeconds = blockWithTransactions.Timestamp.Value;
                                    var blockTimestamp = DateTimeOffset.FromUnixTimeSeconds((long)timestampInSeconds).UtcDateTime;

                                    var txRecord = new TransactionRecord
                                    {
                                        TxHash = transaction.BlockHash,
                                        Timestamp = blockTimestamp,
                                        Value = Web3.Convert.FromWei(transaction.Value),
                                        From = transaction.From,
                                        To = transaction.To
                                    };

                                    lock (transactions) // 确保线程安全
                                    {
                                        transactions.Add(txRecord);
                                    }
                                }

                            }
                        }
                       
                    }
                    catch (Exception ex)
                    {
                        // 错误处理：可以记录日志或者进行其他处理
                        Console.WriteLine($"Error fetching block {block}: {ex.Message}");
                    }

                }));


              
            }

            // 并发执行所有任务
            await Task.WhenAll(tasks);

            return transactions;
        }


        // 根据主网获取对应的RPC方法名
        public static string GetMethodByMainNet(string mainNet)
        {
            switch (mainNet.ToLower())
            {
                case "eth":
                case "bsc":
                    return "eth_getBalance";
                case "sol":
                    return "getBalance";
                default:
                    return "eth_getBalance";
            }
        }

        // 根据主网获取对应的RPC请求参数
        public static object[] GetParamsByMainNet(string mainNet, string address)
        {
            switch (mainNet.ToLower())
            {
                case "eth":
                case "bsc":
                    return new object[] { address, "latest" };
                case "sol":
                    return new object[] { address }; // Solana的具体参数格式需要根据实际情况调整
                default:
                    return new object[] { address, "latest" };
            }
        }



        /// <summary>
        /// 多链查询
        /// </summary>
        /// <param name="address"></param>
        /// <param name="network"></param>
        /// <returns></returns>
        public static async Task<BlockchainData> FullAddressQuery(string address, string network)
        {
            var data = new BlockchainData();

            // 并发执行基础查询 
            var balanceTask =await GetBalanceInfo(address, network);

            data.Balance = (decimal)balanceTask.Balance;
            data.Transactions = balanceTask.Transactions;
            data.TransactionCount = balanceTask.Transactions.Count;
            return data;
        }
    }
}
