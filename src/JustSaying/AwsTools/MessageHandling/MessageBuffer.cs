using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;

namespace JustSaying.AwsTools.MessageHandling
{
    public class MessageBuffer : IMessageBuffer
    {
        private readonly SqsQueueBase _queue;
        private readonly ILogger _log;
        private readonly int _maxBufferAddSize;
        private readonly int _bufferPauseInMilliseconds;
        private readonly ConcurrentQueue<Message> _messages = new ConcurrentQueue<Message>();
        private readonly List<string> _requestMessageAttributeNames = new List<string>();

        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public string QueueName => _queue.QueueName;

        public MessageBuffer(SqsQueueBase queue, ILogger log, int maxBufferAddSize = 10, int bufferPauseInMilliseconds = -1)
        {
            _queue = queue;
            _log = log;
            _maxBufferAddSize = maxBufferAddSize;
            _bufferPauseInMilliseconds = bufferPauseInMilliseconds;
        }


        public async Task<Message> GetMessageAsync(CancellationToken cancellationToken)
        {
            if (_messages.Count <= 0)
            {
                try
                {
                    //assume that if we can't get the semaphore fast, someone else has it
                    if (await _semaphore.WaitAsync(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false))
                    {
                        if (_messages.Count <= 0)
                        {
                            foreach (var message in await ReadMessagesFromSqsAsync(cancellationToken).ConfigureAwait(false))
                            {
                                _messages.Enqueue(message);
                            }

                            if (_bufferPauseInMilliseconds > 0)
                            {
                                await Task.Delay(_bufferPauseInMilliseconds, cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            //TODO: A null object message would be better than null propagation
            return _messages.TryDequeue(out Message msg) ? msg : null;
        }

        private async Task<List<Message>> ReadMessagesFromSqsAsync(CancellationToken cancellationToken)
        {
            var queueName = _queue.QueueName;
            var regionName = _queue.Region.SystemName;
            ReceiveMessageResponse sqsMessageResponse = null;
            try
            {
                sqsMessageResponse = await PollSqsAsync(cancellationToken).ConfigureAwait(false);

                var messageCount = sqsMessageResponse?.Messages?.Count ?? 0;

                _log.LogTrace(
                    "Polled for messages on queue '{QueueName}' in region '{Region}', and received {MessageCount} messages.",
                    queueName,
                    regionName,
                    messageCount);
            }
            catch (InvalidOperationException ex)
            {
                _log.LogTrace(
                    ex,
                    "Could not determine number of messages to read from queue '{QueueName}' in '{Region}'.",
                    queueName,
                    regionName);
            }
            catch (OperationCanceledException ex)
            {
                _log.LogTrace(
                    ex,
                    "Suspected no message on queue '{QueueName}' in region '{Region}'.",
                    queueName,
                    regionName);
            }
#pragma warning disable CA1031
                catch (Exception ex)
#pragma warning restore CA1031
            {
                _log.LogError(
                    ex,
                    "Error receiving messages on queue '{QueueName}' in region '{Region}'.",
                    queueName,
                    regionName);
            }

            return sqsMessageResponse != null ? sqsMessageResponse.Messages : new List<Message>();
        }

        private async Task<ReceiveMessageResponse> PollSqsAsync(CancellationToken ct)
        {
            var request = new ReceiveMessageRequest
            {
                QueueUrl = _queue.Uri.AbsoluteUri,
                MaxNumberOfMessages = _maxBufferAddSize,
                WaitTimeSeconds = 20,
                AttributeNames = _requestMessageAttributeNames
            };

            using var receiveTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(300));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, receiveTimeout.Token);

            return await _queue.Client.ReceiveMessageAsync(request, linkedCts.Token).ConfigureAwait(false);
        }

        ~MessageBuffer()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _semaphore.Dispose();
            }
        }
    }
}


