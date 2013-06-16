﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace CodersBlock.SuperFeed.Modules
{
    public class TwitterFeedModule : FeedModuleJson
    {
        private class Model
        {
            public List<ModelRoot> root { get; set; }
        }

        private class ModelRoot
        {
            public string created_at { get; set; }
            public string source { get; set; }
            public string text { get; set; }
            public long id { get; set; }
        }

        private string _consumerKey;
        private string _consumerSecret;
        private string _accessToken;
        private string _accessTokenSecret;
        private string _username;

        public override string SourceName
        {
            get { return "Twitter"; }
        }

        public override string SourceUri
        {
            get { return "https://api.twitter.com/1.1/statuses/user_timeline.json"; }
        }

        public TwitterFeedModule(int totalLimit, string consumerKey, string consumerSecret, string accessToken, string accessTokenSecret, string username) : base(totalLimit)
        {
            _consumerKey = consumerKey;
            _consumerSecret = consumerSecret;
            _accessToken = accessToken;
            _accessTokenSecret = accessTokenSecret;
            _username = username;
        }

        protected override HttpWebRequest GetRequest()
        {
            var oauthVersion = "1.0";
            var oauthSignatureMethod = "HMAC-SHA1";
            var oauthNonce = Convert.ToBase64String(new ASCIIEncoding().GetBytes(DateTime.Now.Ticks.ToString()));
            var timeSpan = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            var oauthTimestamp = Convert.ToInt64(timeSpan.TotalSeconds).ToString();

            var baseString = "GET&" + Uri.EscapeDataString(SourceUri) + "&" +
                Uri.EscapeDataString(
                    string.Format(
                        // must be alphabetical
                        // non-OAuth stuff must also be added to requestQuery
                        "count={0}&oauth_consumer_key={1}&oauth_nonce={2}&oauth_signature_method={3}&oauth_timestamp={4}&oauth_token={5}&oauth_version={6}&screen_name={7}&trim_user=true",
                        _totalLimit, _consumerKey, oauthNonce, oauthSignatureMethod, oauthTimestamp, _accessToken, oauthVersion, Uri.EscapeDataString(_username)
                    )
                );

            var compositeKey = _consumerSecret + "&" + _accessTokenSecret;
            string oauth_signature;
            using (HMACSHA1 hasher = new HMACSHA1(ASCIIEncoding.ASCII.GetBytes(compositeKey)))
            {
                oauth_signature = Convert.ToBase64String(hasher.ComputeHash(ASCIIEncoding.ASCII.GetBytes(baseString)));
            }

            var authHeader = string.Format(
                "OAuth oauth_nonce=\"{0}\", oauth_signature_method=\"{1}\", oauth_timestamp=\"{2}\", oauth_consumer_key=\"{3}\", oauth_token=\"{4}\", oauth_signature=\"{5}\", oauth_version=\"{6}\"",
                oauthNonce, oauthSignatureMethod, oauthTimestamp, _consumerKey, _accessToken, Uri.EscapeDataString(oauth_signature), oauthVersion
            );

            var requestQuery = string.Format("count={0}&screen_name={1}&trim_user=true",
                _totalLimit, Uri.EscapeDataString(_username)
            );

            var request = (HttpWebRequest)WebRequest.Create(SourceUri + "?" + requestQuery);
            request.Headers.Add("Authorization", authHeader);
            request.Method = "GET";
            request.ContentType = "application/x-www-form-urlencoded";

            return request;
        }

        protected override List<FeedItem> ParseDocument(Dictionary<string, object> doc)
        {
            var feedItems = new List<FeedItem>(_totalLimit);
            if (doc != null)
            {
                var serializer = new JavaScriptSerializer();
                var model = serializer.ConvertToType<Model>(doc);
                for (var i = 0; i < Math.Min(_totalLimit, model.root.Count); i++)
                {
                    var modelItem = model.root[i];
                    feedItems.Add(new FeedItem()
                    {
                        SourceName = SourceName,
                        Published = GetPublished(modelItem.created_at),
                        Title = GetTitle(modelItem.source),
                        Snippet = modelItem.text,
                        ViewUri = GetViewUri(modelItem.id)
                    });
                }
            }
            
            return feedItems;
        }

        protected override string GetSnippet(string title)
        {
            // disregard all that stuff in base class's GetSnippet()
            title = Regex.Replace(title, @"^" + _username + ": ", "");
            title = Regex.Replace(title, @"https?://\w+\.\w+/\w+", "<a href=\"$0\" target=\"_blank\">$0</a>");
            title = Regex.Replace(title, @"@(\w+)", "<a href=\"http://twitter.com/$1\" target=\"_blank\">$0</a>");
            title = Regex.Replace(title, @"#(\w+)", "<a href=\"http://twitter.com/search?q=%23$1\" target=\"_blank\">$0</a>");
            return title;
        }

        private DateTime GetPublished(string pubDate)
        {
            return DateTime.ParseExact(pubDate, "ddd MMM dd HH:mm:ss zzzz yyyy", CultureInfo.InvariantCulture);
        }

        private string GetTitle(string source)
        {
            if (source == "web")
            {
                source = "Web";
            }
            source = "Via " + Regex.Replace(source, "<.+?>", "");
            return source;
        }

        private string GetViewUri(long id)
        {
            return string.Format("https://twitter.com/{0}/status/{1}", _username, id);
        }
    }
}