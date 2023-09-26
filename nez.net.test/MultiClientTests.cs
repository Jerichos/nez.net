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
        
        Dictionary<string, string> receivedMessages = new();
        
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

        private readonly object _lock = new object();
    }
}
