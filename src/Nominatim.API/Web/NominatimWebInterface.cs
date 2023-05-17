using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Nominatim.API.Contracts;
using Nominatim.API.Interfaces;
using Nominatim.API.Models;

namespace Nominatim.API.Web {
    /// <summary>
    ///     Provides a means of sending HTTP requests to a Nominatim server
    /// </summary>
    public class NominatimWebInterface : INominatimWebInterface {
        private static readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new PrivateContractResolver()
        };

        private static readonly string _version = Assembly.GetExecutingAssembly().GetName().Version.ToString();


        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _httpClientName;
        private readonly string _productName;

        private readonly ProductInfoHeaderValue _productInfoHeaderValue;

        private readonly IMemoryCache _successCache = null;
        private readonly IMemoryCache _errorsCache = null;
        private readonly MemoryCacheEntryOptions _successCacheEntryOptions = null;
        private readonly MemoryCacheEntryOptions _errorsCacheEntryOptions = null;

        public NominatimWebInterface(
            IHttpClientFactory httpClientFactory,
            string httpClientName = "DefaultNominatimWebInterfaceHttpClient",
            string productName = "f1ana.Nominatim.API",
            NominatimCacheConfig cacheConfig = null) {
            _httpClientFactory = httpClientFactory;
            _httpClientName = httpClientName;
            _productName = productName;

            _productInfoHeaderValue = new ProductInfoHeaderValue(_productName, _version);

            if (cacheConfig != null)
            {
                _successCache = new MemoryCache(new MemoryCacheOptions
                {
                    SizeLimit = cacheConfig.SuccessCacheSize,
                });

                _successCacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = cacheConfig.SuccessCacheEntityLifespan,
                    Size = 1,
                };

                _errorsCache = new MemoryCache(new MemoryCacheOptions
                {
                    SizeLimit = cacheConfig.ErrorsCacheSize,
                });

                _errorsCacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = cacheConfig.ErrorsCacheEntityLifespan,
                    Size = 1,
                };
            }
        }
        
        /// <summary>
        ///     Send a request to the Nominatim server
        /// </summary>
        /// <typeparam name="T">Type of object to deserialize response onto</typeparam>
        /// <param name="url">URL of Nominatim server method</param>
        /// <param name="parameters">Query string parameters</param>
        /// <returns>Deserialized instance of T</returns>
        public async Task<T> GetRequest<T>(string url, Dictionary<string, string> parameters) {
            var req = addQueryStringToUrl(url, parameters);

            if (_successCache != null && _successCache.TryGetValue<T>(req, out var cachedResult)){
                return cachedResult;
            }

            if (_errorsCache != null && _errorsCache.TryGetValue(req, out _)){
                throw new ArgumentException(paramName: nameof(req), message: $"Failed to send request '{req}' to Nominatim server. Cached error.");
            }

            using (var httpClient = _httpClientFactory.CreateClient(_httpClientName))
            {
                try{
                    AddUserAgent(httpClient);
                    var response = await httpClient.GetStringAsync(req).ConfigureAwait(false);

                    var result = JsonConvert.DeserializeObject<T>(response, _jsonSerializerSettings);

                    if (_successCache != null){
                        _successCache.Set(req, result, _successCacheEntryOptions);
                    }

                    return result;
                } catch (Exception ex){
                    if (_errorsCache != null){
                        _errorsCache.Set(req, ex.Message, _errorsCacheEntryOptions);
                    }

                    throw new ArgumentException(paramName: nameof(req), message: $"Failed to send request '{req}' to Nominatim server.", innerException: ex);
                }
            }
        }

        private static string addQueryStringToUrl(string url, IDictionary<string, string> parameters) {
            if ((parameters?.Keys.Count ?? 0) == 0) {
                return url;
            }

            var op = url.IndexOf('?') != -1 ? '&' : '?';
            var sb = new StringBuilder();
            sb.Append(url);
            foreach (var kvp in parameters) {
                sb.Append(op);
                sb.Append($"{UrlEncoder.Default.Encode(kvp.Key)}={UrlEncoder.Default.Encode(kvp.Value)}");
                op = '&';
            }

            return sb.ToString();
        }

        private void AddUserAgent(HttpClient httpClient) {
            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.Add(_productInfoHeaderValue);
        }
    }
}