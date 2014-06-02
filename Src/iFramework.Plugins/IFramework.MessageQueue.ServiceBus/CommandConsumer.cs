﻿using IFramework.Command;
using IFramework.Infrastructure;
using IFramework.Infrastructure.Unity.LifetimeManagers;
using IFramework.Message;
using IFramework.MessageQueue.MessageFormat;
using IFramework.SysExceptions;
using IFramework.UnitOfWork;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IFramework.MessageQueue.ServiceBus
{
    public class CommandConsumer : MessageProcessor, IMessageConsumer
    {
        protected IHandlerProvider _handlerProvider;
        protected Dictionary<string, TopicClient> _replyProducers;
        protected string _commandQueueName;
        protected QueueClient _commandQueueClient;
        protected Task _commandConsumerTask;

        public CommandConsumer(IHandlerProvider handlerProvider, 
                               string serviceBusConnectionString,
                               string commandQueueName)
            : base(serviceBusConnectionString)
        {
            _handlerProvider = handlerProvider;
            _commandQueueName = commandQueueName;
            _replyProducers = new Dictionary<string, TopicClient>();
        }


        protected TopicClient GetReplyProducer(string replyTopicName)
        {
            TopicClient replyProducer = null;
            if (!string.IsNullOrWhiteSpace(replyTopicName))
            {
                if (!_replyProducers.TryGetValue(replyTopicName, out replyProducer))
                {
                    try
                    {
                        replyProducer = CreateTopicClient(replyTopicName);
                        _replyProducers[replyTopicName] = replyProducer;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex.GetBaseException().Message, ex);
                    }
                }
            }
            return replyProducer;
        }

        void OnMessageHandled(IMessageContext messageContext, MessageReply reply)
        {
            if (!string.IsNullOrWhiteSpace(messageContext.ReplyToEndPoint))
            {
                var replyProducer = GetReplyProducer(messageContext.ReplyToEndPoint);
                if (replyProducer != null)
                {
                    replyProducer.Send(reply.BrokeredMessage);
                    _logger.InfoFormat("send reply, commandID:{0}", reply.MessageID);
                }
            }
        }

        public void Start()
        {
            try
            {
                _commandQueueClient = CreateQueueClient(_commandQueueName);
                _commandConsumerTask = Task.Factory.StartNew(ConsumeMessages, TaskCreationOptions.LongRunning);

            }
            catch (Exception ex)
            {
                _logger.Error(ex.GetBaseException().Message, ex);
            }
        }

        protected virtual void ConsumeMessages()
        {
            while (!_exit)
            {
                BrokeredMessage brokeredMessage = null;
                try
                {
                    brokeredMessage = _commandQueueClient.Receive();
                    ConsumeMessage(brokeredMessage);
                    brokeredMessage.Complete();
                    MessageCount++;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex.GetBaseException().Message, ex);
                }
            }
        }

        protected void ConsumeMessage(BrokeredMessage brokeredMessage)
        {
            var messageContext = new MessageContext(brokeredMessage);
            MessageReply messageReply = null;
            if (messageContext == null || messageContext.Message as ICommand == null)
            {
                return;
            }
            var message = messageContext.Message as ICommand;
            var needRetry = message.NeedRetry;
            do
            {
                try
                {
                    PerMessageContextLifetimeManager.CurrentMessageContext = messageContext;
                    var messageHandler = _handlerProvider.GetHandler(message.GetType());
                    _logger.InfoFormat("Handle command, commandID:{0}", messageContext.MessageID);

                    if (messageHandler == null)
                    {
                        messageReply = new MessageReply(messageContext.MessageID, new NoHandlerExists());
                    }
                    else
                    {
                        var unitOfWork = IoCFactory.Resolve<IUnitOfWork>();
                        ((dynamic)messageHandler).Handle((dynamic)message);
                        unitOfWork.Commit();
                        messageReply = new MessageReply(messageContext.MessageID, messageContext.Reply);
                    }
                    needRetry = false;
                }
                catch (Exception e)
                {
                    if (!(e is OptimisticConcurrencyException) || !needRetry)
                    {
                        messageReply = new MessageReply(messageContext.MessageID, e.GetBaseException());
                        if (e is DomainException)
                        {
                            _logger.Warn(message.ToJson(), e);
                        }
                        else
                        {
                            _logger.Error(message.ToJson(), e);
                        }
                        needRetry = false;
                    }
                }
                finally
                {
                    PerMessageContextLifetimeManager.CurrentMessageContext = null;
                }
            } while (needRetry);
            OnMessageHandled(messageContext, messageReply);
        }


        public void Stop()
        {
            _exit = true;
            if (_commandConsumerTask != null)
            {
                _commandQueueClient.Close();
                if (!_commandConsumerTask.Wait(1000))
                {
                    _logger.ErrorFormat("receiver can't be stopped!");
                }
            }
        }

        public string GetStatus()
        {
            return this.ToString();
        }

        public decimal MessageCount { get; set; }
    }
}
