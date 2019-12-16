using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;

namespace JustSaying.AwsTools.MessageHandling
{
    public interface IMessageBuffer : IDisposable
    {
        string QueueName { get; }
        Task<Message> GetMessageAsync(CancellationToken cancellationToken);
    }
}
