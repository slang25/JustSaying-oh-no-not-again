﻿using System.Collections.Generic;
using System.Net;
using Amazon.SQS;
using Amazon.SQS.Model;
using JustSaying.AwsTools.QueueCreation;
using NLog;

namespace JustSaying.AwsTools
{
    public abstract class SqsQueueBase
    {
        public string Arn { get; protected set; }
        public string Url { get; protected set; }
        public IAmazonSQS Client { get; private set; }
        public string QueueName { get; protected set; }
        public ErrorQueue ErrorQueue { get; protected set; }
        internal int MessageRetentionPeriod { get; set; }
        internal int VisibilityTimeout { get; set; }
        internal RedrivePolicy RedrivePolicy { get; set; }
        
        private static readonly Logger Log = LogManager.GetLogger("JustSaying");

        protected SqsQueueBase(IAmazonSQS client)
        {
            Client = client;
        }

        public abstract bool Exists();

        public virtual void Delete()
        {
            Arn = null;
            Url = null;

            if (!Exists())
                return;

            var result = Client.DeleteQueue(new DeleteQueueRequest { QueueUrl = Url });
            //return result.IsSetResponseMetadata();
            Arn = null;
            Url = null;
        }

        protected void SetQueueProperties()
        {
            var attributes = GetAttrs(new[]
            {
                JustSayingConstants.ATTRIBUTE_ARN, 
                JustSayingConstants.ATTRIBUTE_REDRIVE_POLICY,
                JustSayingConstants.ATTRIBUTE_POLICY,
                JustSayingConstants.ATTRIBUTE_RETENTION_PERIOD,
                JustSayingConstants.ATTRIBUTE_VISIBILITY_TIMEOUT
            });
            Arn = attributes.QueueARN;
            MessageRetentionPeriod = attributes.MessageRetentionPeriod;
            VisibilityTimeout = attributes.VisibilityTimeout;
            RedrivePolicy = ExtractRedrivePolicyFromQueueAttributes(attributes.Attributes);
        }

        protected GetQueueAttributesResult GetAttrs(IEnumerable<string> attrKeys)
        {
            var request = new GetQueueAttributesRequest { 
                QueueUrl = Url,
                AttributeNames = new List<string>(attrKeys)
            };
            
            var result = Client.GetQueueAttributes(request);

            return result;
        }

        protected internal void UpdateQueueAttribute(SqsBasicConfiguration queueConfig)
        {
            if (QueueNeedsUpdating(queueConfig))
            {
                var response = Client.SetQueueAttributes(
                    new SetQueueAttributesRequest
                    {
                        QueueUrl = Url,
                        Attributes = new Dictionary<string, string>
                        {
                            {JustSayingConstants.ATTRIBUTE_RETENTION_PERIOD, queueConfig.MessageRetentionSeconds.ToString()},
                            {JustSayingConstants.ATTRIBUTE_VISIBILITY_TIMEOUT, queueConfig.VisibilityTimeoutSeconds.ToString()},
                        }
                    });
                if (response.HttpStatusCode == HttpStatusCode.OK)
                {
                    MessageRetentionPeriod = queueConfig.MessageRetentionSeconds;
                    VisibilityTimeout = queueConfig.VisibilityTimeoutSeconds;
                }
            }
        }

        private bool QueueNeedsUpdating(SqsBasicConfiguration queueConfig)
        {
            return MessageRetentionPeriod != queueConfig.MessageRetentionSeconds
                   || VisibilityTimeout != queueConfig.VisibilityTimeoutSeconds;
        }

        private RedrivePolicy ExtractRedrivePolicyFromQueueAttributes(Dictionary<string, string> queueAttributes)
        {
            if (!queueAttributes.ContainsKey(JustSayingConstants.ATTRIBUTE_REDRIVE_POLICY))
                return null;
            return RedrivePolicy.ConvertFromString(queueAttributes[JustSayingConstants.ATTRIBUTE_REDRIVE_POLICY]);
        }
    }
}
