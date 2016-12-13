using ADFSample.TweetReader;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Web.Http;

namespace ADFSample
{
    [RoutePrefix("api/Twitter")]
    public class TwitterController : ApiController
    {
        private static Dictionary<Guid, HttpResponseMessage> runningTasks = new Dictionary<Guid, HttpResponseMessage>();

        public HttpResponseMessage ReadTwitter(JObject input)
        {
            Guid id = Guid.NewGuid();
            runningTasks[id] = null;
            new Thread(() => DoWork(id, input)).Start();

            return this.CreateAcceptedMessage(id);
        }

        [HttpGet]
        [Route("CheckStatus/{id}")]
        public HttpResponseMessage CheckStatus([FromUri] Guid id)
        {
            if (runningTasks.ContainsKey(id))
            {
                HttpResponseMessage message = runningTasks[id];
                if (message == null)
                {
                    return this.CreateAcceptedMessage(id);
                }
                runningTasks.Remove(id);
                return message;
            }
            else
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
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
                runningTasks[id] = message;
            }
            catch (Exception ex)
            {
                // The sample application is not differentiating between bad input and service errors.
                HttpResponseMessage message = new HttpResponseMessage(HttpStatusCode.BadRequest);
                message.Content = new StringContent(ex.ToString());

                runningTasks[id] = message;
            }
        }
    }
}
