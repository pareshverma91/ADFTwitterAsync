using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Tweetinvi;
using Tweetinvi.Core.Enum;
using Tweetinvi.Core.Interfaces;
using Tweetinvi.Core.Interfaces.Credentials;
using Tweetinvi.Core.Interfaces.Models.Parameters;

namespace ADFSample.TweetReader
{
    public interface ITwitterLocation
    {
        int? Limit { get; set; }
        string Keywords { get; set; }
        string SearchResultType { get; set; }
        string Since { get; set; }
        string Until { get; set; }
    }

    public interface ITwitterAccountInfo
    {
        string UserAccessToken { get; set; }
        string UserAccessTokenSecret { get; set; }
        string ConsumerKey { get; set; }
        string ConsumerSecret { get; set; }
    }

    public interface IStorageAccountInfo
    {
        string ConnectionString { get; set; }
        string FolderPath { get; set; }
        string FileName { get; set; }
    }

    public interface IExecutionInput
    {
        ITwitterAccountInfo TwitterAccountInfo { get; set; }
        ITwitterLocation TwitterLocation { get; set; }
        IStorageAccountInfo StorageAccountInfo { get; set; }
    }

    public class TweetReaderActivity
    {
        public IDictionary<string, string> Execute(JObject inputObject)
        {
            if (inputObject == null)
            {
                throw new Exception("No input defined.");
            }

            IExecutionInput input = inputObject.ToObject<IExecutionInput>();
            if (input == null)
            {
                throw new Exception(string.Format(CultureInfo.InvariantCulture, "Input is not in expected format. {0}", inputObject.ToString()));
            }

            IEnumerable<ITweet> tweets = GetTweets(input.TwitterAccountInfo, input.TwitterLocation);
            UploadTweets(input.StorageAccountInfo, tweets);

            return null;
        }

        private IEnumerable<ITweet> GetTweets(ITwitterAccountInfo twitterAccountInfo, ITwitterLocation twitterLocation)
        {
            string userAccessToken = twitterAccountInfo.UserAccessToken;
            string userAccessTokenSecret = twitterAccountInfo.UserAccessTokenSecret;
            string consumerKey = twitterAccountInfo.ConsumerKey;
            string consumerSecret = twitterAccountInfo.ConsumerSecret;

            TwitterCredentials.ApplicationCredentials = TwitterCredentials.CreateCredentials(userAccessToken, userAccessTokenSecret, consumerKey, consumerSecret);

            ITokenRateLimit rateLimit = RateLimit.GetCurrentCredentialsRateLimits().SearchTweetsLimit;
            //logger.Write(TraceEventType.Information, "SearchTweetsLimit: Limit: {0}, Remaining: {1}, Reset: {2}", rateLimit.Limit, rateLimit.Remaining, rateLimit.ResetDateTime);

            int limit = rateLimit.Remaining;
            int? limitValue = twitterLocation.Limit;
            if (limitValue != null)
            {
                int userLimit = limitValue.Value;
                //logger.Write(TraceEventType.Information, "User Limit value: {0}", limitValue);
                limit = Math.Min(rateLimit.Remaining, userLimit);
            }
            //logger.Write(TraceEventType.Information, "Limit: {0}", limit);

            string keywordsValue = twitterLocation.Keywords;
            //logger.Write(TraceEventType.Information, "Keywords: {0}", keywordsValue);

            SearchResultType searchResultType = SearchResultType.Mixed;
            string searchResultTypeValue = twitterLocation.SearchResultType;
            if (!String.IsNullOrEmpty(searchResultTypeValue) &&
                !Enum.TryParse<SearchResultType>(searchResultTypeValue, out searchResultType))
            {
                //logger.Write(TraceEventType.Warning, "Unable to parse SearchResultType property. User SearchResultType value: {0}. Enum values: {1}", searchResultTypeValue, string.Join(",", Enum.GetNames(typeof(SearchResultType))));
            }
            //logger.Write(TraceEventType.Information, "SearchResultType: {0}", searchResultType);

            string sinceValue = twitterLocation.Since;
            DateTime since = default(DateTime);
            if (!String.IsNullOrEmpty(sinceValue) &&
                !DateTime.TryParse(sinceValue, out since))
            {
                //logger.Write(TraceEventType.Warning, "Unable to parse Since property. User value: {0}", sinceValue);
            }

            string untilValue = twitterLocation.Until;
            DateTime until = default(DateTime);
            if (!String.IsNullOrEmpty(untilValue) &&
                !DateTime.TryParse(untilValue, out until))
            {
                //logger.Write(TraceEventType.Warning, "Unable to parse Until property. User value: {0}", untilValue);
            }

            string[] keywords = keywordsValue.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            int limitPerKeyword = limit / keywords.Length;

            //logger.Write(TraceEventType.Information, "Limit per keyword: {0}", limitPerKeyword);

            foreach (string keyword in keywords)
            {
                for (int i = 0; i < limitPerKeyword; i++)
                {
                    ITweetSearchParameters searchParameter = Search.GenerateSearchTweetParameter(keyword);
                    searchParameter.SearchType = searchResultType;
                    searchParameter.MaximumNumberOfResults = 100;

                    if (since != default(DateTime))
                    {
                        searchParameter.Since = since;
                    }
                    if (until != default(DateTime))
                    {
                        searchParameter.Until = until;
                    }

                    IEnumerable<ITweet> tweets = Search.SearchTweets(searchParameter);

                    foreach (ITweet tweet in tweets)
                    {
                        yield return tweet;
                    }
                }
            }
        }

        private void UploadTweets(IStorageAccountInfo storageAccountInfo, IEnumerable<ITweet> tweets)
        {
            string tempFileName = Path.GetTempFileName();
            //logger.Write(TraceEventType.Information, "Writing tweets to file: {0}", tempFileName);

            try
            {
                using (StreamWriter streamWriter = new StreamWriter(tempFileName))
                {
                    streamWriter.WriteLine("Id,Text,CreatorId,CreatorName,CreatorScreenName,CreatorLocation,CreatedAt,IsRetweet,RetweetCount,Retweeted");

                    foreach (ITweet tweet in tweets)
                    {
                        streamWriter.WriteLine(
                            "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                            tweet.IdStr,
                            Regex.Replace(tweet.Text, @"\r\n?|\n|,", " "),
                            tweet.Creator.IdStr,
                            tweet.Creator.Name,
                            tweet.Creator.ScreenName,
                            tweet.Creator.Location,
                            tweet.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                            tweet.IsRetweet,
                            tweet.RetweetCount,
                            tweet.Retweeted);
                    }
                }

                string blobPath = storageAccountInfo.FolderPath.TrimEnd('/');

                if (String.IsNullOrEmpty(storageAccountInfo.FileName))
                {
                    blobPath = String.Concat(blobPath, '/', Guid.NewGuid().ToString(), ".txt");
                }
                else
                {
                    blobPath = String.Concat(blobPath, '/', storageAccountInfo.FileName);
                }

                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageAccountInfo.ConnectionString);
                CloudBlockBlob blob = new CloudBlockBlob(new Uri(storageAccount.BlobEndpoint, blobPath), storageAccount.Credentials);

                //logger.Write(TraceEventType.Information, "Uploading tweets to: {0}", blob.Uri);
                blob.UploadFromFile(tempFileName, FileMode.Open);
            }
            finally
            {
                if (File.Exists(tempFileName))
                {
                    File.Delete(tempFileName);
                }
            }
        }
    }
}
