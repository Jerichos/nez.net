using nez.net.transport;
using nez.net.transport.socket;
using ZeroFormatter;

namespace nez.net.test.packetTests;

[TestFixture]
public class PacketTests
{
    [Test]
    public void CreatePacketHeaderTest()
    {
        // create packet header
        bool isChunkedSent = false;
        ushort messageIDSent = 1;
        ushort lengthSent = 256;
        var packetHeader = PacketFraming.CreatePacketHeader(isChunkedSent, messageIDSent);
        packetHeader.SetLengthToPacketHeader(lengthSent);
        
        var (payloadLength, isChunked, messageID) = packetHeader.ReadPacketHeader();
    
        // Assert
        Assert.AreEqual(lengthSent, payloadLength);
        Assert.AreEqual(isChunkedSent, isChunked);
        Assert.AreEqual(messageIDSent, messageID);
    }
    
    [Test]
    public void ChunkedPacketHeaderTest()
    {
        bool isChunkedSent = true;
        ushort messageIDSent = 1;
        ushort lengthSent = 256;
        ushort chunkIndexSent = 3;
        ushort totalChunksSent = 6;
        
        var packetHeader = PacketFraming.CreatePacketHeader(isChunkedSent, messageIDSent, lengthSent);
        packetHeader = PacketFraming.AppendChunkHeader(chunkIndexSent, totalChunksSent, packetHeader);
        
        var (payloadLength, isChunked, messageID) = packetHeader.ReadPacketHeader();
        var (chunkIndex, totalChunks) = packetHeader.ReadChunkHeader();
    
        // Assert
        Assert.AreEqual(lengthSent, payloadLength);
        Assert.AreEqual(isChunkedSent, isChunked);
        Assert.AreEqual(messageIDSent, messageID);
        
        Assert.AreEqual(chunkIndexSent, chunkIndex);
        Assert.AreEqual(totalChunksSent, totalChunks);
    }

    [Test]
    public void PacketHeaderMessageTest()
    {
        // create random packet payload
        byte[] payloadSent = new byte[256];
        new Random().NextBytes(payloadSent);
        
        // create packet header
        bool isChunkedSent = false;
        ushort messageIDSent = 1;
        ushort lengthSent = (ushort)payloadSent.Length;
        var packetHeader = PacketFraming.CreatePacketHeader(isChunkedSent, messageIDSent, lengthSent);
        
        // prepend packet header to payload
        var buffer = PacketFraming.PrependPacketHeaderToPayload(packetHeader, payloadSent);
        
        // read packet header
        var (payloadLength, isChunked, messageID) = buffer.ReadPacketHeader();
        // read payload
        var payloadReceived = new byte[payloadLength];
        Buffer.BlockCopy(buffer, 5, payloadReceived, 0, payloadLength);
        
        // Assert
        Assert.AreEqual(lengthSent, payloadLength);
        Assert.AreEqual(isChunkedSent, isChunked);
        Assert.AreEqual(messageIDSent, messageID);
        // Assert payloadSent and payloadReceived are equal
        Assert.IsTrue(payloadSent.SequenceEqual(payloadReceived));
    }
}