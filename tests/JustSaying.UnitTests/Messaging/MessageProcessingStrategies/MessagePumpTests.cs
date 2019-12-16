using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using JustSaying.AwsTools.MessageHandling;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace JustSaying.UnitTests.Messaging.MessageProcessingStrategies
{
    public class MessagePumpTests
    {
        [Fact]
        public async Task Will_Eat_Messages_From_The_Buffer()
        {
            //arrange
            const int SIZE = 10;
            var messages = new Message[SIZE];
            for (int i = 0; i < SIZE; i++)
            {
                messages[i] = new Message() {Body = $"I am message # {i}"};
            }

            var fakeBuffer = new FakeBuffer(messages);
            var fakeDispatcher = new FakeDispatcher();
            var messagePump = new MessagePump(fakeBuffer, fakeDispatcher, Substitute.For<ILogger>());

            //act
            var cts = new CancellationTokenSource();

            //we don't do much, so this should be adequate to eat the queue
            cts.CancelAfter(3000);

            await messagePump.Start(cts.Token).ConfigureAwait(false);

            //assert
            Assert.Empty(fakeBuffer.Messages);
            Assert.Equal(fakeDispatcher.Dispatched, messages);
        }

        [Fact]
        public async Task Will_Eat_Messages_From_A_Shared_Buffer()
        {
            //arrange
            const int SIZE = 40;
            var messages = new Message[SIZE];
            for (int i = 0; i < SIZE; i++)
            {
                messages[i] = new Message() {Body = $"I am message # {i}"};
            }

            var fakeBuffer = new FakeBuffer(messages);
            var fakeDispatcherOne = new FakeDispatcher();
            var fakeDispatcherTwo = new FakeDispatcher();
            var messagePumpOne = new MessagePump(fakeBuffer, fakeDispatcherOne, Substitute.For<ILogger>());
            var messagePumpTwo = new MessagePump(fakeBuffer, fakeDispatcherTwo, Substitute.For<ILogger>());

            //act
            var cts = new CancellationTokenSource();

            //we don't do much, so this should be adequate to eat the queue
            cts.CancelAfter(3000);

            var pumpTasksQuery = from pump in new MessagePump[] {messagePumpOne, messagePumpTwo} select pump.Start(cts.Token);

            var pumpTasks = pumpTasksQuery.ToList();

            while (pumpTasks.Count > 0)
            {
                var closedPump = await Task.WhenAny(pumpTasks).ConfigureAwait(false);
                pumpTasks.Remove(closedPump);
            }

            //assert
            Assert.Empty(fakeBuffer.Messages);
            Assert.Equal(SIZE, fakeDispatcherOne.Dispatched.Count + fakeDispatcherTwo.Dispatched.Count);
        }

        private class FakeBuffer : IMessageBuffer
        {
            public ConcurrentQueue<Message> Messages { get; } = new ConcurrentQueue<Message>();
            public string QueueName { get; } = "Fake Queue";

            public FakeBuffer(IEnumerable<Message> messages)
            {
                foreach (var message in messages)
                {
                    Messages.Enqueue(message);
                }
            }

            public Task<Message> GetMessageAsync(CancellationToken cancellationToken)
            {
                var tcs = new TaskCompletionSource<Message>();

                var msg = Messages.TryDequeue(out Message message) ? message : null;

                tcs.SetResult(msg);

                return tcs.Task;
            }

            public void Dispose()
            {
            }
        }

        private class FakeDispatcher : IMessageDispatcher
        {
            public List<Message> Dispatched { get; } = new List<Message>();

            public Task DispatchMessage(Message message, CancellationToken cancellationToken)
            {
                //skip nulls returned when the loop idles.
                if (message != null)
                {
                    Dispatched.Add(message);
                }

                return Task.CompletedTask;
            }
        }
    }
}
