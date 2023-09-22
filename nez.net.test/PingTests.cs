using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using nez.net.transport.socket;

namespace nez.net.test
{
    [TestFixture]
    public class PingTests
    {
        private SocketTransport _serverTransport;
        private SocketTransport _clientTransport;
        private TaskCompletionSource<bool> _serverTaskCompletionSource;
        private TaskCompletionSource<bool> _clientTaskCompletionSource;
        private Stopwatch _totalTimer;
        private Stopwatch _pingTimer;
        
        NetworkMessage _pingMessage = new NetworkMessage { Payload = Encoding.ASCII.GetBytes("ping") };
        NetworkMessage _pongMessage = new NetworkMessage { Payload = Encoding.ASCII.GetBytes("pong") };

        [SetUp]
        public void Setup()
        {
            _serverTransport = new SocketTransport();
            _clientTransport = new SocketTransport();
        }
        
        [TearDown]
        public void TearDown()
        {
            _serverTransport.StopServer();
            _clientTransport.StopClient();
        }

        [Test]
        public async Task TestPingPong()
        {
            int port = 8888;
            string serverAddress = "127.0.0.1";

            // Initialize
            SetupServer(port);
            SetupClient(serverAddress, port);

            // Create a stopwatch to measure time
            _totalTimer = new Stopwatch();
            _pingTimer = new Stopwatch();

            // Create TaskCompletionSources
            _serverTaskCompletionSource = new TaskCompletionSource<bool>();
            _clientTaskCompletionSource = new TaskCompletionSource<bool>();

            // Wait for the server to start
            await Task.Delay(2000);

            // Start timer
            _totalTimer.Start();

            for (int i = 0; i < 5; i++)
            {
                _serverTaskCompletionSource = new TaskCompletionSource<bool>();
                _clientTaskCompletionSource = new TaskCompletionSource<bool>();
                
                _pingTimer.Restart();
                _clientTransport.ClientSend(_pingMessage);
                await Task.WhenAll(_serverTaskCompletionSource.Task, _clientTaskCompletionSource.Task);
                Console.WriteLine($"ping {i} time: {_pingTimer.ElapsedMilliseconds} ms");
            }

            Console.WriteLine($"Elapsed time: {_totalTimer.ElapsedMilliseconds} ms");
            _totalTimer.Stop();
        }
        
        private void SetupServer(int port)
        {
            _serverTransport.EServerReceive += ServerReceive;
            _serverTransport.StartServer(port);
        }

        private void ServerReceive(NetworkMessage message)
        {
            if (Encoding.ASCII.GetString(message.Payload) == "ping")
            {
                _serverTransport.ServerSend(_pongMessage);
                _serverTaskCompletionSource.SetResult(true);
            }
        }

        private void SetupClient(string serverAddress, int port)
        {
            _clientTransport.EClientReceive += ClientReceive;
            _clientTransport.ConnectClient(serverAddress, port);
        }

        private void ClientReceive(NetworkMessage message)
        {
            Assert.AreEqual("pong", Encoding.ASCII.GetString(message.Payload));
            _clientTaskCompletionSource.SetResult(true);
        }
    }
}
