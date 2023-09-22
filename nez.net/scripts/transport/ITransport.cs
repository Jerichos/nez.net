using nez.net.transport.socket;

namespace nez.net.transport;

public interface ITransport
{
    void StartServer(int port);
    void ConnectClient(string ipAddress, int port);
    void ServerSend(NetworkMessage message, uint clientID);
    void ClientSend(NetworkMessage message);
    void StopServer();
    void StopClient();
}
