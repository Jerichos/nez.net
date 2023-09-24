using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Text;
using nez.net.transport.socket;

namespace nez.net.test
{
    [TestFixture]
    public class MultiClientTests
    {
        private SocketTransport _serverTransport;
        private List<SocketTransport> _clientTransports;
        private List<TaskCompletionSource<bool>> _clientTaskCompletionSources;
        
        Dictionary<string, string> receivedMessages = new Dictionary<string, string>();
        
        [SetUp]
        public void Setup()
        {
            _serverTransport = new SocketTransport();
            _clientTransports = new List<SocketTransport>();
            _clientTaskCompletionSources = new List<TaskCompletionSource<bool>>();
        }

        [TearDown]
        public void TearDown()
        {
            _serverTransport.Server.Stop();
            foreach (var client in _clientTransports)
            {
                client.Client.Stop();
            }
        }

        [Test, Timeout(1000)]
        public async Task TestMultiClient()
        {
            int port = 8888;
            string serverAddress = "127.0.0.1";
            int numClients = 10; // Change this to the number of clients you want to simulate
            string[] clientNames = new string[numClients];

            // Initialize
            _serverTransport.Server.Start(port);

            // Wait for the server to start
            await Task.Delay(100);

            for (int i = 0; i < numClients; i++)
            {
                SocketTransport clientTransport = new SocketTransport();
                _clientTransports.Add(clientTransport);

                TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
                _clientTaskCompletionSources.Add(tcs);
                

                int index = i;
                
                clientNames[index] = $"Client-{i}";
                
                clientTransport.Client.OnMessageReceived += (msg) => ClientReceive(msg, tcs, clientNames[index], receivedMessages);
                
                clientTransport.Client.Start(serverAddress, port);

                // Create unique message for each client
                MirrorMessage uniqueMessage = new MirrorMessage { Message = clientNames[index]};

                // Send message
                clientTransport.Client.Send(uniqueMessage);
            }

            // Wait for all client tasks to complete
            await Task.WhenAll(_clientTaskCompletionSources.Select(tcs => tcs.Task));
            
            foreach (var pair in receivedMessages)
            {
                Assert.AreEqual(pair.Key, pair.Value);
            }
        }

        private readonly object _lock = new object();

        private void ClientReceive(NetworkMessage message, TaskCompletionSource<bool> tcs, string expectedStr, Dictionary<string, string> receivedMessages)
        {
            lock (_lock)
            {
                if (tcs.Task.IsCompleted)
                {
                    return;
                }

                if (message is MirrorMessage mirrorMessage)
                {
                    Assert.AreEqual(expectedStr, mirrorMessage.Message);
                    receivedMessages[expectedStr] = mirrorMessage.Message;
                    tcs.SetResult(true);
                }
            }
        }
    }
}
