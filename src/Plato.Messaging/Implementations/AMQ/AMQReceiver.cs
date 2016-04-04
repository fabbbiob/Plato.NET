﻿// Plato.NET
// Copyright (c) 2016 ReflectSoftware Inc.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using Apache.NMS;
using Apache.NMS.Util;
using Plato.Messaging.Implementations.AMQ.Interfaces;
using Plato.Messaging.Implementations.AMQ.Settings;
using Plato.Messaging.Interfaces;
using System;
using System.Threading;

namespace Plato.Messaging.Implementations.AMQ
{
    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="Plato.Messaging.AMQ.AMQReceiverSender" />
    /// <seealso cref="Plato.Messaging.Interfaces.IMessageReceiver" />
    public class AMQReceiver : AMQReceiverSender, IAMQReceiver
    {
        private readonly TimeoutException _timeoutException;
        private readonly AMQDestinationSettings _destination;
        private IMessageConsumer _consumer;

        /// <summary>
        /// Initializes a new instance of the <see cref="AMQReceiver"/> class.
        /// </summary>
        /// <param name="connectionFactory">The connection factory.</param>
        /// <param name="connectionName">Name of the connection.</param>
        /// <param name="destination">The destination.</param>
        public AMQReceiver(IAMQConnectionFactory connectionFactory, string connectionName, AMQDestinationSettings destination) : base(connectionFactory, connectionName)
        {
            _timeoutException = new TimeoutException();
            _destination = destination;
            _consumer = null;
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override void Close()
        {
            if (_consumer != null)
            {
                _consumer.Close();
                _consumer.Dispose();
                _consumer = null;
            }

            base.Close();
        }

        /// <summary>
        /// Gets the delivery mode.
        /// </summary>
        /// <value>
        /// The delivery mode.
        /// </value>
        protected override AcknowledgementMode AcknowledgementMode
        {
            get { return _destination.AckMode; }
        }

        /// <summary>
        /// Clears the cache buffer.
        /// </summary>
        public void ClearCacheBuffer()
        {
            if (_session != null)
            {
                try
                {
                    _session.Recover();
                }
                catch (Exception ex)
                {
                    Close();

                    var newException = AMQExceptionHandler.ExceptionHandler(_connection, ex);
                    if (newException != null)
                    {
                        throw newException;
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Reads the specified msec timeout.
        /// </summary>
        /// <param name="msecTimeout">The msec timeout.</param>
        /// <returns></returns>
        public IMessageReceiveResult<string> Receive(int msecTimeout = Timeout.Infinite)
        {
            try
            {
                Open();

                if (_consumer == null)
                {
                    var destination = SessionUtil.GetDestination(_session, _destination.Path);
                    if (_destination.Durable && destination.IsTopic)
                    {
                        _consumer = _session.CreateDurableConsumer((ITopic)destination, _destination.SubscriberId, _destination.Selector, false);
                    }
                    else
                    {
                        _consumer = _session.CreateConsumer(destination, _destination.Selector, false);
                    }
                }

                ITextMessage message;
                if (msecTimeout != Timeout.Infinite)
                {
                    message = (ITextMessage)_consumer.Receive(TimeSpan.FromMilliseconds(msecTimeout));
                }
                else
                {
                    message = (ITextMessage)_consumer.Receive();
                }

                if (message == null)
                {
                    throw _timeoutException;
                }

                return new AMQReceiverResult(message);
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Close();

                var newException = AMQExceptionHandler.ExceptionHandler(_connection, ex);
                if (newException != null)
                {
                    throw newException;
                }

                throw;
            }
        }
    }
}
