using System.Diagnostics;
using Nez;
using nez.net.components;
using nez.net.extensions;
using nez.net.test.components;
using nez.net.transport.socket;

namespace nez.net.test;

[TestFixture]
public class NetworkStateTest
{
    private SocketTransport _serverSocket;
    private SocketTransport _clientSocket;
    
    private NetworkState _serverNetworkState;
    private NetworkState _clientNetworkState;

    private Scene _serverScene;
    private Scene _clientScene;
    
    private Core _serverCore;

    private readonly string _ip = "127.0.0.1";
    private readonly int _port = 7777;

    [SetUp]
    public void Setup()
    {
        _serverSocket = new SocketTransport();
        _clientSocket = new SocketTransport();
        
        _serverNetworkState = new NetworkState();
        _clientNetworkState = new NetworkState();

        _serverCore = new Core();

        _serverScene = new Scene();
        _clientScene = new Scene();
        
        _serverSocket.Server.NetworkState = _serverNetworkState;
        _clientSocket.Client.NetworkState = _clientNetworkState;
        
        _serverSocket.Server.Start(_port);
        Thread.SpinWait(10);
    }

    [TearDown]
    public void TearDown()
    {
        _serverSocket.Stop();
        _clientSocket.Stop();
    }

    [Test, Timeout(100)]
    public async Task TestNetworkStateSynchronization()
    {
        var entity = CreateServerEntity();
        
        Stopwatch sw = Stopwatch.StartNew();

        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        _clientSocket.Client.OnReceive += message =>
        {
            if (message is NetworkStateMessage networkStateMessage)
            {
                if (networkStateMessage.NetworkComponents.Count == 1 && networkStateMessage.NetworkEntities.Count == 1)
                {
                    _clientNetworkState.UpdateScene(_clientScene);
                    _clientScene.Entities.UpdateLists();
                    tcs.SetResult(true);
                }
            }
        };
        
        _clientSocket.Client.Start(_ip, _port);
        
        await tcs.Task;
        Console.WriteLine("Elapsed: " + sw.ElapsedMilliseconds + "ms");
        
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
        Assert.IsTrue(_serverSocket.IsServerRunning);
        Assert.IsTrue(_clientSocket.IsClientRunning);
    }
    
    // test multiple entities synchronization between server and client
    [Test, Timeout(1000)]
    public async Task TestMessageChunking()
    {
        // create n entities to server scene
        int entityCount = 6;
        List<Entity> serverEntities = new List<Entity>();
        for (int i = 0; i < entityCount; i++)
        {
            serverEntities.Add(CreateServerEntity());
        }
        
        Stopwatch sw = Stopwatch.StartNew();

        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        _clientSocket.Client.OnReceive += message =>
        {
            if (message is NetworkStateMessage networkStateMessage)
            {
                if (networkStateMessage.NetworkComponents.Count == entityCount && networkStateMessage.NetworkEntities.Count == entityCount)
                {
                    _clientNetworkState.UpdateScene(_clientScene);
                    _clientScene.Entities.UpdateLists();
                    tcs.SetResult(true);
                }
            }
        };

        _clientSocket.Client.Start(_ip, _port);
        
        await tcs.Task;
        Console.WriteLine("Elapsed: " + sw.ElapsedMilliseconds + "ms");
        
        // compare entities if data matches
        Assert.That(_clientScene.Entities.Count, Is.EqualTo(_serverScene.Entities.Count));
        
        // iterate thru client entities, match with server entities, if match compare components
        for (int i = 0; i < _clientScene.Entities.Count; i++)
        {
            var clientEntity = _clientScene.Entities[i];
            
            if(!clientEntity.HasComponent<NetworkIdentity>())
                continue;
            
            for (int j = 0; j < _serverScene.Entities.Count; j++)
            {
                var serverEntity = _serverScene.Entities[j];
                
                if(!serverEntity.HasComponent<NetworkIdentity>())
                    continue;

                if (clientEntity.GetComponent<NetworkIdentity>().NetworkID ==
                    serverEntity.GetComponent<NetworkIdentity>().NetworkID)
                {
                    // if match, compare components
                    Assert.That(clientEntity.GetComponent<TestNetworkComponent>().ComponentID == 
                                    serverEntity.GetComponent<TestNetworkComponent>().ComponentID, Is.EqualTo(true));
                }
            }
        }
    }

    // helper method to test entity to server scene, with network components
    private Entity CreateServerEntity()
    {
        Entity entity = _serverScene.CreateEntity("TestEntity");
        entity.AddComponentUnique(new NetworkIdentity(_serverSocket.Server));
        entity.AddNetworkComponent(new TestNetworkComponent(_serverSocket.Server));
        _serverScene.Entities.UpdateLists();

        for (int i = 0; i < _serverScene.Entities.Count; i++)
        {
            _serverScene.Entities[i].Update();
        }

        return entity;
    }
}