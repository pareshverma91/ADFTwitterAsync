using ADFSample.TweetReader;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Threading;
using System.Web.Http;

namespace ADFSample
{
    [RoutePrefix("api/Twitter")]
    public class TwitterController : ApiController
    {
        private static MemoryCache Cache = new MemoryCache("TwitterJobCache",
            new System.Collections.Specialized.NameValueCollection { { "cacheMemoryLimitMegabytes", "10" } });

        private const string Null = "null";

        public HttpResponseMessage ReadTwitter(JObject input)
        {
            Guid id = Guid.NewGuid();
            Cache.Set(id.ToString(), Null, GetExpiryTime());
            new Thread(() => DoWork(id, input)).Start();

            return this.CreateAcceptedMessage(id);
        }

        [HttpGet]
        [Route("CheckStatus/{id}")]
        public HttpResponseMessage CheckStatus([FromUri] Guid id)
        {
            if (Cache.Contains(id.ToString()))
            {
                var message = Cache.GetCacheItem(id.ToString()).Value;
                if (message.Equals(Null))
                {
                    return this.CreateAcceptedMessage(id);
                }
                return message as HttpResponseMessage;
            }
            else
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        private HttpResponseMessage CreateAcceptedMessage(Guid id)
        {
            HttpResponseMessage responseMessage = new HttpResponseMessage(HttpStatusCode.Accepted);
            responseMessage.Headers.Location = new Uri(String.Format(CultureInfo.InvariantCulture, "{0}://{1}/api/Twitter/CheckStatus/{2}", Request.RequestUri.Scheme, Request.RequestUri.Host, id));
            responseMessage.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(10));
            return responseMessage;
        }

        private void DoWork(Guid id, JObject input)
        {
            try
            {
                TweetReaderActivity reader = new TweetReaderActivity();
                reader.Execute(input);

                HttpResponseMessage message = new HttpResponseMessage(HttpStatusCode.OK);
                Cache.Set(id.ToString(), message, GetExpiryTime());
            }
            catch (Exception ex)
            {
                // The sample application is not differentiating between bad input and service errors.
                HttpResponseMessage message = new HttpResponseMessage(HttpStatusCode.BadRequest);
                message.Content = new StringContent(ex.ToString());

                Cache.Set(id.ToString(), message, GetExpiryTime());
            }
        }

        private DateTimeOffset GetExpiryTime()
        {
            return DateTime.UtcNow.Add(TimeSpan.FromMinutes(5));
        }
    }
}
