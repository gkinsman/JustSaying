using JustSaying.AwsTools;
using JustSaying.AwsTools.MessageHandling;
using JustSaying.AwsTools.Publishing;
using JustSaying.Messaging;
using JustSaying.Models;
using Microsoft.Extensions.Logging;

namespace JustSaying.Fluent
{
    /// <summary>
    /// Defines a builder for a publication.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the messages to publish.
    /// </typeparam>
    internal interface IPublicationBuilder<out T>
        where T : Message
    {
        /// <summary>
        /// Configures the publication for the <see cref="JustSayingBus"/>.
        /// </summary>
        /// <param name="bus">The <see cref="JustSayingBus"/> to configure subscriptions for.</param>
        /// <param name="proxy">The <see cref="IAwsClientFactoryProxy"/> to use to create SQS/SNS clients with.</param>
        /// <param name="publisherFactory">The <see cref="IMessagePublisherFactory"/> to use to create <see cref="IMessagePublisher"/>s with.</param>
        /// <param name="queueTopicCreatorFactory">The <see cref="IQueueTopicCreatorFactory"/> to use to create <see cref="ITopicCreator"/>s with.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> logger factory to use.</param>
        void Configure(JustSayingBus bus, IAwsClientFactoryProxy proxy,
            IMessagePublisherFactory publisherFactory,
            IQueueTopicCreatorFactory queueTopicCreatorFactory,
            ILoggerFactory loggerFactory);
    }
}
