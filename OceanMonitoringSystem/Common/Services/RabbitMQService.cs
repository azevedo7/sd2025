using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Models;

namespace OceanMonitoringSystem.Common.Services
{
    /**
     * @class RabbitMQService
     * @description Provides RabbitMQ connection and messaging functionality for the Ocean Monitoring System.
     * Handles connection management, queue declaration, message publishing, and consumption.
     */
    public class RabbitMQService : IDisposable
    {
        private readonly RabbitMQConfig _config;
        private IConnection? _connection;
        private IModel? _channel;
        private readonly string _exchangeName = "ocean_monitoring_exchange";
        private bool _disposed = false;

        public RabbitMQService(RabbitMQConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            InitializeConnection();
        }

        /**
         * @method InitializeConnection
         * @description Establishes connection to RabbitMQ server and creates communication channel
         */
        private void InitializeConnection()
        {
            try
            {
                var factory = new ConnectionFactory()
                {
                    HostName = _config.HostName,
                    Port = _config.Port,
                    UserName = _config.UserName,
                    Password = _config.Password,
                    VirtualHost = _config.VirtualHost,
                    AutomaticRecoveryEnabled = _config.AutomaticRecoveryEnabled,
                    NetworkRecoveryInterval = _config.NetworkRecoveryInterval,
                    RequestedHeartbeat = TimeSpan.FromSeconds(_config.RequestedHeartbeat)
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declare the exchange as topic type for pattern-based routing
                _channel.ExchangeDeclare(exchange: _exchangeName, type: ExchangeType.Topic, durable: true);

                Console.WriteLine($"Connected to RabbitMQ at {_config.HostName}:{_config.Port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to RabbitMQ: {ex.Message}");
                throw;
            }
        }

        /**
         * @method DeclareQueue
         * @description Declares a queue with the specified name and binds it to the exchange
         * @param queueName Name of the queue to declare
         * @param routingKey Routing key for message routing
         */
        public void DeclareQueue(string queueName, string routingKey)
        {
            if (_channel == null) throw new InvalidOperationException("RabbitMQ channel is not initialized");

            _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(queue: queueName, exchange: _exchangeName, routingKey: routingKey);

            Console.WriteLine($"Queue '{queueName}' declared and bound with routing key '{routingKey}'");
        }

        /**
         * @method PublishMessage
         * @description Publishes a message to the specified routing key
         * @param message The RabbitMQ message to publish
         * @param routingKey Routing key for message destination
         */
        public void PublishMessage(RabbitMQMessage message, string routingKey)
        {
            if (_channel == null) throw new InvalidOperationException("RabbitMQ channel is not initialized");

            try
            {
                var messageJson = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(messageJson);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.Priority = message.Priority;
                properties.MessageId = message.MessageId;
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                _channel.BasicPublish(exchange: _exchangeName, routingKey: routingKey, basicProperties: properties, body: body);

                Console.WriteLine($"Message published to routing key '{routingKey}': {message.MessageType} from {message.WavyId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to publish message: {ex.Message}");
                throw;
            }
        }

        /**
         * @method StartConsumer
         * @description Starts consuming messages from the specified queue
         * @param queueName Name of the queue to consume from
         * @param onMessageReceived Callback function to handle received messages
         */
        public void StartConsumer(string queueName, Func<RabbitMQMessage, bool> onMessageReceived)
        {
            if (_channel == null) throw new InvalidOperationException("RabbitMQ channel is not initialized");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var messageJson = Encoding.UTF8.GetString(body);
                    var message = JsonSerializer.Deserialize<RabbitMQMessage>(messageJson);

                    if (message != null)
                    {
                        Console.WriteLine($"Message received from queue '{queueName}': {message.MessageType} from {message.WavyId}");

                        // Process the message
                        bool processed = onMessageReceived(message);

                        if (processed)
                        {
                            // Acknowledge the message
                            _channel?.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                            Console.WriteLine($"Message acknowledged: {message.MessageId}");
                        }
                        else
                        {
                            // Reject and requeue the message
                            _channel?.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                            Console.WriteLine($"Message rejected and requeued: {message.MessageId}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to deserialize message, rejecting...");
                        _channel?.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing message: {ex.Message}");
                    // Reject the message without requeuing on processing errors
                    _channel?.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
            Console.WriteLine($"Started consuming messages from queue '{queueName}'");
        }

        /**
         * @method IsConnected
         * @description Checks if the RabbitMQ connection is active
         * @return Boolean indicating connection status
         */
        public bool IsConnected()
        {
            return _connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen;
        }

        /**
         * @method PublishToTopic
         * @description Publishes a message to a topic pattern
         * @param topic Topic pattern for routing (e.g., "sensor.temperature.wavy1")
         * @param message The RabbitMQ message to publish
         */
        public void PublishToTopic(string topic, RabbitMQMessage message)
        {
            if (_channel == null) throw new InvalidOperationException("RabbitMQ channel is not initialized");

            try
            {
                var messageJson = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(messageJson);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.Priority = message.Priority;
                properties.MessageId = message.MessageId;
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                _channel.BasicPublish(exchange: _exchangeName, routingKey: topic, basicProperties: properties, body: body);

                Console.WriteLine($"Message published to topic '{topic}': {message.MessageType} from {message.WavyId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to publish message to topic: {ex.Message}");
                throw;
            }
        }

        /**
         * @method SubscribeToTopics
         * @description Subscribes to multiple topic patterns using a single queue
         * @param queueName Name of the queue to create/use
         * @param topicPatterns Array of topic patterns to subscribe to (e.g., "sensor.temperature.*")
         * @param onMessageReceived Callback function to handle received messages
         */
        public void SubscribeToTopics(string queueName, string[] topicPatterns, Func<RabbitMQMessage, bool> onMessageReceived)
        {
            if (_channel == null) throw new InvalidOperationException("RabbitMQ channel is not initialized");

            // Declare queue
            _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

            // Bind queue to each topic pattern
            foreach (var pattern in topicPatterns)
            {
                _channel.QueueBind(queue: queueName, exchange: _exchangeName, routingKey: pattern);
                Console.WriteLine($"Bound queue '{queueName}' to topic pattern: {pattern}");
            }

            // Set up consumer
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var messageJson = Encoding.UTF8.GetString(body);
                    var message = JsonSerializer.Deserialize<RabbitMQMessage>(messageJson);

                    if (message != null)
                    {
                        Console.WriteLine($"Message received from topic '{ea.RoutingKey}': {message.MessageType} from {message.WavyId}");

                        // Process the message
                        bool processed = onMessageReceived(message);

                        if (processed)
                        {
                            _channel?.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                            Console.WriteLine($"Message acknowledged: {message.MessageId}");
                        }
                        else
                        {
                            _channel?.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                            Console.WriteLine($"Message rejected and requeued: {message.MessageId}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to deserialize message, rejecting...");
                        _channel?.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing message: {ex.Message}");
                    _channel?.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
            Console.WriteLine($"Started consuming messages from queue '{queueName}' with {topicPatterns.Length} topic patterns");
        }

        /**
         * @method GenerateTopic
         * @description Generates a topic string following the pattern: sensor.{type}.{wavyId}.{messageType}
         * @param sensorType Type of sensor (temperature, humidity, etc.)
         * @param wavyId ID of the Wavy device
         * @param messageType Type of message (data, maintenance, etc.)
         * @return Generated topic string
         */
        public static string GenerateTopic(string sensorType, string wavyId, string messageType = "data")
        {
            return $"sensor.{sensorType.ToLower()}.{wavyId.ToLower()}.{messageType.ToLower()}";
        }

        /**
         * @method Reconnect
         * @description Attempts to reconnect to RabbitMQ server
         */
        public void Reconnect()
        {
            Dispose();
            InitializeConnection();
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _channel?.Close();
                _channel?.Dispose();
                _connection?.Close();
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing RabbitMQ connection: {ex.Message}");
            }

            _disposed = true;
        }
    }
}
