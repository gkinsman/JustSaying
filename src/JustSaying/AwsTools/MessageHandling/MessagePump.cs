using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace JustSaying.AwsTools.MessageHandling
{
    public class MessagePump
    {
        private readonly IMessageBuffer _messageBuffer;
        private readonly ILogger _log;
        private readonly int _noWaitingMessagesPauseInMilliseconds;
        private IMessageDispatcher _messageDispatcher;
        public int Id { get; set; }
        public string QueueName => _messageBuffer.QueueName;

        public MessagePump(
            IMessageBuffer messageBuffer,
            IMessageDispatcher messageDispatcher,
            ILogger log,
            int noWaitingMessagesPauseInMilliseconds = 100)
        {
            _messageBuffer = messageBuffer;
            _messageDispatcher = messageDispatcher;
            _log = log;
            _noWaitingMessagesPauseInMilliseconds = noWaitingMessagesPauseInMilliseconds;
        }

        public Task Start(CancellationToken cancellationToken)
        {
            return Task.Run(async () => { await EventLoop(cancellationToken).ConfigureAwait(false); });
        }

        private async Task EventLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var message = await _messageBuffer.GetMessageAsync(cancellationToken).ConfigureAwait(false);
                    if (message == null)
                    {
                        //yield control to other threads by pausing this one
                        await Task.Delay(_noWaitingMessagesPauseInMilliseconds, cancellationToken).ConfigureAwait(false);
                    }
                    //translate message and dispatch message
                    await _messageDispatcher.DispatchMessage(message, cancellationToken).ConfigureAwait(false);
                }
#pragma warning disable CA1031
                catch (Exception e)
#pragma warning restore CA1031
                {
                    //Need to catch the exceptions we care about here
                    _log.LogError(e, "Error reading messages from {QueueName}", QueueName);
                }
            }
        }
  }
}
