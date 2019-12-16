using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSaying.Messaging;
using JustSaying.Messaging.Interrogation;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Messaging.MessageProcessingStrategies;
using JustSaying.Messaging.MessageSerialization;
using JustSaying.Messaging.Monitoring;
using JustSaying.Models;
using Microsoft.Extensions.Logging;

namespace JustSaying.AwsTools.MessageHandling
{
    public class Dispatcher : INotificationSubscriber, IDisposable
    {
        private const int MAX_CONCURRENCY = 1;
        private readonly SqsQueueBase _queue;
        private readonly List<MessagePump> _performers = new List<MessagePump>();
        private readonly HandlerMap _handlerMap = new HandlerMap();
        private readonly MessageHandlerWrapper _messageHandlerWrapper;
        private ILogger _log;
        private MessageBuffer _messageBuffer;

        public string Queue => _queue.QueueName;

        public Dispatcher(
            SqsQueueBase queue,
            IMessageSerializationRegister serializationRegister,
            IMessageMonitor messagingMonitor,
            ILoggerFactory loggerFactory,
            IMessageContextAccessor messageContextAccessor,
            Action<Exception, Amazon.SQS.Model.Message> onError = null,
            IMessageLockAsync messageLock = null,
            IMessageBackoffStrategy messageBackoffStrategy = null)
        {
            _queue = queue;
            _log = loggerFactory.CreateLogger("JustSaying");

            _messageHandlerWrapper = new MessageHandlerWrapper(messageLock, messagingMonitor, loggerFactory);

            var messageDispatcher = new MessageDispatcher(
                _queue,
                serializationRegister,
                messagingMonitor,
                onError,
                _handlerMap,
                loggerFactory,
                messageBackoffStrategy,
                messageContextAccessor);

            _messageBuffer = new MessageBuffer(queue, _log);

            //MAX_CONCURRENCY probably needs to come from back off strategy
            for (int i = 0; i < MAX_CONCURRENCY; i++)
            {
                _performers.Add(new MessagePump(_messageBuffer, messageDispatcher, _log));
            }
        }

        public ICollection<ISubscriber> Subscribers { get; }
        public bool IsListening { get; }
        public void AddMessageHandler<T>(Func<IHandlerAsync<T>> futureHandler) where T : Message
        {
            if (_handlerMap.ContainsKey(typeof(T)))
            {
                throw new NotSupportedException(
                    $"The handler for '{typeof(T)}' messages on this queue has already been registered.");
            }

            Subscribers.Add(new Subscriber(typeof(T)));

            var handlerFunc = _messageHandlerWrapper.WrapMessageHandler(futureHandler);
            _handlerMap.Add(typeof(T), handlerFunc);
        }

        public void Listen(CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            foreach (var performer in _performers)
            {
                var task = performer.Start(cancellationToken);
                performer.Id = task.Id;
                tasks.Add(task);
                _log.LogInformation($"Started performer to consume messages from {performer.QueueName} with Id {performer.Id}");
            };

            while (tasks.Count > 0)
            {
                try
                {
                    var runningTasks = tasks.ToArray();
                    var index = Task.WaitAny(runningTasks);
                    var stoppingPump = runningTasks[index];

                    var pumpTask = _performers.SingleOrDefault(p => p.Id == stoppingPump.Id);
                    if (pumpTask  != null)
                    {
                        _performers.Remove(pumpTask);
                        _log.LogInformation($"Stopped performer consuming messages from {pumpTask.QueueName} with Id {pumpTask.Id}");
                    }

                    tasks.Remove(stoppingPump);
                }
                catch (AggregateException ae)
                {
                    ae.Handle(ex => true);
                }
            }

        }

        ~Dispatcher()
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
                _messageBuffer.Dispose();
            }
        }

     }
}
