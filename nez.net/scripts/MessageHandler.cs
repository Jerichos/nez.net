using System;
using System.Collections.Generic;
using nez.net.transport.socket;
using ZeroFormatter;

namespace nez.net;

public class MessageHandler
{
    private readonly Dictionary<int, List<byte>> _incompleteMessages = new();
    private readonly Dictionary<ushort, HashSet<int>> _receivedChunks = new();

    public NetworkMessage HandleReceivedData(byte[] data, ushort messageID, bool isChunked, int chunkIndex, int totalChunks)
    {
        if (!isChunked)
        {
            return DeserializeMessage(data);
        }
        else
        {
            return HandleChunkedMessage(data, messageID, chunkIndex, totalChunks);
        }
    }

    private NetworkMessage DeserializeMessage(byte[] data)
    {
        return ZeroFormatterSerializer.Deserialize<NetworkMessage>(data);
    }

    private NetworkMessage HandleChunkedMessage(byte[] chunk, ushort messageID, int chunkIndex, int totalChunks)
    {
        if (!_incompleteMessages.ContainsKey(messageID))
        {
            _incompleteMessages[messageID] = new List<byte>();
        }

        _incompleteMessages[messageID].AddRange(chunk);

        if (AllChunksReceived(messageID, totalChunks, chunkIndex))
        {
            byte[] completeMessage = _incompleteMessages[messageID].ToArray();
            _incompleteMessages.Remove(messageID);
            return DeserializeMessage(completeMessage);
        }

        return null;
    }

    private bool AllChunksReceived(ushort messageID, int totalChunks, int chunkIndex)
    {
        if (!_receivedChunks.ContainsKey(messageID))
        {
            _receivedChunks[messageID] = new HashSet<int>();
        }

        _receivedChunks[messageID].Add(chunkIndex);

        bool allChunksReceived = _receivedChunks[messageID].Count == totalChunks;

        if (allChunksReceived)
        {
            _receivedChunks.Remove(messageID);
        }

        return allChunksReceived;
    }
    
    // TODO: should we trim the end of the array before deserialize?
    private static byte[] TrimEnd(byte[] array)
    {
        int lastIndex = Array.FindLastIndex(array, b => b != 0);

        if (lastIndex == -1)
            return Array.Empty<byte>();

        byte[] trimmedArray = new byte[lastIndex + 1];
        Array.Copy(array, trimmedArray, lastIndex + 1);
        return trimmedArray;
    }
}