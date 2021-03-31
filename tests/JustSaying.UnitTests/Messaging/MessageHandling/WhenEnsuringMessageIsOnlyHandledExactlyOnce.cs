using System;
using System.Threading;
using System.Threading.Tasks;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Messaging.Middleware;
using JustSaying.TestingFramework;
using JustSaying.UnitTests.Messaging.Channels.Fakes;
using JustSaying.UnitTests.Messaging.Channels.TestHelpers;
using MartinCostello.Logging.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace JustSaying.UnitTests.Messaging.MessageHandling
{
    public class WhenEnsuringMessageIsOnlyHandledExactlyOnce
    {
        private readonly ITestOutputHelper _outputHelper;

        public WhenEnsuringMessageIsOnlyHandledExactlyOnce(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task WhenMessageIsLockedByAnotherHandler_MessageWillBeLeftInTheQueue()
        {
            var messageLock = new FakeMessageLock(false);

            var testResolver = new FakeServiceResolver(sc => sc
                .AddLogging(l =>
                    l.AddXUnit(_outputHelper))
                .AddSingleton<IMessageLockAsync>(messageLock));

            var handler = new InspectableHandler<OrderAccepted>();

            var middlewareBuilder = new HandlerMiddlewareBuilder(testResolver, testResolver);
                middlewareBuilder.UseExactlyOnce<OrderAccepted>(nameof(InspectableHandler<OrderAccepted>),
                    TimeSpan.FromSeconds(1))
                .UseHandler(ctx => handler);

            var middleware = middlewareBuilder.Build();

            var context = new HandleMessageContext(new OrderAccepted(), typeof(OrderAccepted),
                "test-queue");
            var result = await middleware.RunAsync(context, null, CancellationToken.None);

            handler.ReceivedMessages.ShouldBeEmpty();
            result.ShouldBeFalse();
        }
    }
}
