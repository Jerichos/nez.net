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

        private PingMessage _pingMessage = new PingMessage();
        private PongMessage _pongMessage = new PongMessage();

        [SetUp]
        public void Setup()
        {
            _serverTransport = new SocketTransport();
            _clientTransport = new SocketTransport();
        }
        
        [TearDown]
        public void TearDown()
        {
            _serverTransport.Server.Stop();
            _clientTransport.Client.Stop();
        }

        [Test, Timeout(1000)]
        public async Task TestPingPong()
        {
            int port = 8888;
            string serverAddress = "127.0.0.1";

            // Initialize
            _serverTransport.Server.OnReceive += ServerReceive;
            _serverTransport.Server.Start(port);

            // Wait for the server to start
            await Task.Delay(10);
            
            _clientTransport.Client.OnReceive += ClientReceive;
            _clientTransport.Client.Start(serverAddress, port);

            // Create a stopwatch to measure time
            _totalTimer = new Stopwatch();
            _pingTimer = new Stopwatch();

            // Create TaskCompletionSources
            _serverTaskCompletionSource = new TaskCompletionSource<bool>();
            _clientTaskCompletionSource = new TaskCompletionSource<bool>();

            // Start timer
            _totalTimer.Start();

            for (int i = 0; i < 5; i++)
            {
                _serverTaskCompletionSource = new TaskCompletionSource<bool>();
                _clientTaskCompletionSource = new TaskCompletionSource<bool>();
                
                _pingTimer.Restart();
                _clientTransport.Client.Send(_pingMessage);
                await Task.WhenAll(_serverTaskCompletionSource.Task, _clientTaskCompletionSource.Task);
                Console.WriteLine($"ping {i} time: {_pingTimer.ElapsedMilliseconds} ms");
            }

            Console.WriteLine($"Elapsed time: {_totalTimer.ElapsedMilliseconds} ms");
            _totalTimer.Stop();
        }
        
        private void ServerReceive(NetworkMessage message)
        {
            if (message is PingMessage)
            {
                _serverTransport.Server.Send(_pongMessage);
                _serverTaskCompletionSource.SetResult(true);
            }
        }

        private void ClientReceive(NetworkMessage message)
        {
            if(message is PongMessage)
                _clientTaskCompletionSource.SetResult(true);
        }
    }
}