using System;
using System.Threading.Tasks;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Messaging.Middleware;
using JustSaying.Models;

namespace JustSaying.Fluent.Definitions
{
    public interface IDefinition
    { }

    public interface ISubscriptionDefinition : IDefinition
    {

    }

    public interface ISubscriptionDefinition<THandler, TMessage> : ISubscriptionDefinition
        where THandler : IHandlerAsync<TMessage>
        where TMessage : Message
    {
        Type HandlerType { get; }
        ISubscriptionGroupDefinition SubscriptionGroup { get; }
        void ConfigureMiddleware(IHandlerMiddlewareBuilder middlewareBuilder);
    }

    public class SubscriptionDefinition<THandler, TMessage> : ISubscriptionDefinition<THandler, TMessage>
        where THandler : IHandlerAsync<TMessage>
        where TMessage : Message
    {
        public SubscriptionDefinition()
        {
            SubscriptionGroup = new DefaultSubscriptionGroupDefinition<TMessage>();
        }

        public Type HandlerType => typeof(THandler);
        public Type MessageType => typeof(TMessage);

        public ISubscriptionGroupDefinition SubscriptionGroup { get; protected set; }

        public virtual void ConfigureMiddleware(IHandlerMiddlewareBuilder middlewareBuilder)
        { }
    }

    public interface ISubscriptionGroupDefinition : IDefinition
    {
        public string Name { get; }
    }

    public class DefaultSubscriptionGroupDefinition<TMessage> : ISubscriptionGroupDefinition
        where TMessage : Message
    {
        public string Name => typeof(TMessage).Name;
    }


}
