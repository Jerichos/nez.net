using System.Diagnostics;
using Microsoft.Xna.Framework;
using Nez;
using Nez.IEnumerableExtensions;
using nez.net.components;
using nez.net.extensions;
using nez.net.test.components;
using nez.net.transport.socket;

namespace nez.net.test;

[TestFixture]
public class NetworkStateTest
{
    private SocketTransport _serverTransport;
    private SocketTransport _clientTransport;
    
    private NetworkState _serverNetworkState;
    private NetworkState _clientNetworkState;

    private Scene _serverScene;
    private Scene _clientScene;
    
    private readonly string _ip = "127.0.0.1";
    private readonly int _port = 7777;

    private Core _core;

    [SetUp]
    public void Setup()
    {
        Stopwatch sw = Stopwatch.StartNew();
        _serverTransport = new SocketTransport();
        _clientTransport = new SocketTransport();
        
        _serverNetworkState = new NetworkState();
        _clientNetworkState = new NetworkState();

        int bufferSize = 1024;
        _serverTransport.SetBufferSize(bufferSize);
        _clientTransport.SetBufferSize(bufferSize);

        _core = new Core();
        
        _serverScene = new Scene();
        _clientScene = new Scene();
        
        _serverTransport.Server.NetworkState = _serverNetworkState;
        _clientTransport.Client.NetworkState = _clientNetworkState;
        
        _serverTransport.Server.Start(_port);
        Console.WriteLine("Setup finished in: " + sw.ElapsedMilliseconds + "ms");
        Thread.SpinWait(10);
    }

    [TearDown]
    public void TearDown()
    {
        _serverTransport.Stop();
        _clientTransport.Stop();
        ((Game)_core).Exit();
    }

    [Test, Timeout(500)]
    public async Task TestNetworkStateSynchronization()
    {
        var entity = CreateServerEntity();
        
        Stopwatch sw = Stopwatch.StartNew();

        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

        var timer = Stopwatch.StartNew();
        _clientTransport.Client.OnMessageReceived += (connection, message) =>
        {
            if (message is NetworkStateMessage networkStateMessage)
            {
                Console.WriteLine($"Time until first NetworkStateMessage received: {timer.ElapsedMilliseconds}ms");
                if (networkStateMessage.NetworkComponents.Count == 1 && networkStateMessage.NetworkEntities.Count == 1)
                {
                    _clientNetworkState.UpdateScene(_clientScene);
                    _clientScene.Entities.UpdateLists();
                    tcs.SetResult(true);
                }
            }
        };
        
        _clientTransport.Client.Start(_ip, _port);
        
        await tcs.Task;
        
        // check if client scene has the same entity as the server scene
        Assert.That(_clientScene.Entities.Count, Is.EqualTo(_serverScene.Entities.Count));
        // check if component NetworkIdentity is the same
        var targetNetworkIdentity = _clientScene.Entities.FindComponentOfType<NetworkIdentity>();
        Assert.IsNotNull(targetNetworkIdentity);
        Assert.That(targetNetworkIdentity.NetworkID, Is.EqualTo(entity.GetComponent<NetworkIdentity>().NetworkID));
        // check if component TestNetworkComponent is the same
        var targetNetworkComponent = _clientScene.Entities.FindComponentOfType<TestNetworkComponent>();
        Assert.IsNotNull(targetNetworkComponent);
        Assert.That(targetNetworkComponent.ComponentID, Is.EqualTo(entity.GetComponent<TestNetworkComponent>().ComponentID));
        
        Assert.IsTrue(tcs.Task.Result);
        Assert.IsTrue(_serverTransport.IsServerRunning);
        Assert.IsTrue(_clientTransport.IsClientRunning);
        
        Console.WriteLine("Test finished in: " + sw.ElapsedMilliseconds + "ms");
    }
    
    // test multiple entities synchronization between server and client
    [Test, Timeout(1000)]
    public async Task TestMessageChunking()
    {
        // create n entities to server scene, it should exceed the max buffer size 512 bytes
        int expectedEntityCount = 300;
        List<Entity> serverEntities = new List<Entity>();
        for (int i = 0; i < expectedEntityCount; i++)
        {
            serverEntities.Add(CreateServerEntity());
        }
        
        Stopwatch sw = Stopwatch.StartNew();

        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        _clientTransport.Client.OnMessageReceived += (connection, message) =>
        {
            if (message is NetworkStateMessage networkStateMessage)
            {
                if(networkStateMessage.NetworkEntities.Count == expectedEntityCount)
                    tcs.SetResult(true);
            }
        };

        _clientTransport.Client.Start(_ip, _port);
        
        await tcs.Task;
        Assert.IsTrue(tcs.Task.Result);
        Console.WriteLine("Elapsed: " + sw.ElapsedMilliseconds + "ms");
    }
    
    [Test, Timeout(1000)]
    public async Task TestConcurrentMessageChunking()
    {
        int entityCount = 6;
        int totalMessages = 1; // Number of concurrent messages

        for (int j = 0; j < entityCount; j++)
        {
            CreateServerEntity();
        }
        
        Stopwatch sw = Stopwatch.StartNew();

        int messageID = 0;
        int messagesReceived = 0;
        int expectedMessagesReceived = totalMessages + 1; // +1 because of the initial state message
        int entitiesReceived = 0;
        
        var tcs = new TaskCompletionSource<bool>();
        
        _clientTransport.Client.OnMessageReceived += (connection, message) =>
        {
            if (message is NetworkStateMessage networkStateMessage)
            {
                messagesReceived++;
                entitiesReceived = networkStateMessage.NetworkEntities.Count;
                
                if(messagesReceived == expectedMessagesReceived)
                    tcs.SetResult(true);
            }
        };

        _clientTransport.Client.Start(_ip, _port);
        sw.Restart();

        for (int i = 0; i < totalMessages; i++)
        {
            // Attach the message index to the entities or message to identify them in the client.
            // Send the state to the client
            _serverTransport.Server.GetNetworkState(out var networkEntities, out var networkComponents);
            
            // send message to all clients
            _serverTransport.Server.Send(new NetworkStateMessage
            {
                NetworkEntities = new Dictionary<Guid, NetworkIdentity>(networkEntities),
                NetworkComponents = new Dictionary<Guid, NetworkComponent>(networkComponents)
            });
        }

        // Wait for all messages to be received and processed by the client
        await tcs.Task;

        // Assert the number of messages received
        Assert.That(messagesReceived, Is.EqualTo(expectedMessagesReceived));
        // Assert the number of entities received
        Assert.That(entitiesReceived, Is.EqualTo(entityCount));

        // print bit rates
        Console.WriteLine();
        Console.WriteLine($"Server total bytes sent: {_serverTransport.Server.TotalBitsSent}");
        Console.WriteLine($"Server total bytes received: {_serverTransport.Server.TotalBitsReceived}");
        Console.WriteLine($"Client total bytes sent: {_clientTransport.Client.TotalBitsSent}");
        Console.WriteLine($"Client total bytes received: {_clientTransport.Client.TotalBitsReceived}");
        
        // calculate ratio difference between server and client of total bits send and receive
        double sendReceiveRatio = (double)_serverTransport.Server.TotalBitsSent / _clientTransport.Client.TotalBitsReceived;
        // print ratios
        Console.WriteLine();
        Console.WriteLine($"Server sent to received ratio: {sendReceiveRatio}");
        Console.WriteLine();
    }


    // helper method to test entity to server scene, with network components
    private Entity CreateServerEntity()
    {
        Entity entity = _serverScene.CreateEntity("TestEntity");
        entity.AddComponentUnique(new NetworkIdentity(_serverTransport.Server));
        entity.AddNetworkComponent(new TestNetworkComponent(_serverTransport.Server));
        _serverScene.Entities.UpdateLists();

        for (int i = 0; i < _serverScene.Entities.Count; i++)
        {
            _serverScene.Entities[i].Update();
        }

        return entity;
    }
}