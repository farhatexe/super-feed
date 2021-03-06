﻿using System.Collections.Generic;
using System.Threading;
using System.Xml.Linq;

namespace CodersBlock.SuperFeed
{
    public abstract class FeedModuleXml : FeedModule
    {
        public FeedModuleXml(int totalLimit) : base(totalLimit) { }

        public override List<FeedItem> GetItems()
        {
            XDocument doc = null;
            int attempts = 0;
            do
            {
                try
                {
                    doc = XDocument.Load(SourceUri);
                }
                catch
                {
                    Thread.Sleep(FeedCoordinator.RetryDelay);
                }
                attempts++;
            }
            while (attempts < FeedCoordinator.RetryAttempts && doc == null);

            return ParseDocument(doc);
        }

        protected abstract List<FeedItem> ParseDocument(XDocument doc);
    }
}