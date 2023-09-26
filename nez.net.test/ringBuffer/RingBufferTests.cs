using nez.net.transport;

namespace nez.net.test.ringBuffer;

[TestFixture]
public class RingBufferTests
{
    [Test]
    public void TestWriteAndReadPacket()
    {
        // Arrange
        RingBuffer ringBuffer = new RingBuffer(1024);
        byte[] payload = { 1, 2, 3, 4, 5 };
        bool isChunked = false;
        ushort messageID = 42;

        // Act
        ringBuffer.AddPacketToRingBuffer(payload, isChunked, messageID);
        var (success, payloadLength, readIsChunked, readMessageID) = ringBuffer.ReadPacketHeader();
        var readPayload = ringBuffer.Read(payloadLength);

        // Assert
        Assert.IsTrue(success);
        Assert.AreEqual(payload.Length, payloadLength);
        Assert.AreEqual(isChunked, readIsChunked);
        Assert.AreEqual(messageID, readMessageID);
        CollectionAssert.AreEqual(payload, readPayload);
    }
    
    [Test]
    public void TestInsufficientSpace()
    {
        // Arrange
        RingBuffer ringBuffer = new RingBuffer(10);  // Intentionally small buffer
        byte[] payload = { 1, 2, 3, 4, 5, 6, 7, 8 };
        bool isChunked = false;
        ushort messageID = 42;

        // Act & Assert
        // This should print "Not enough space in the RingBuffer" to the console
        ringBuffer.AddPacketToRingBuffer(payload, isChunked, messageID);
    }
    
    [Test]
    public void TestIncompletePacket()
    {
        // Arrange
        RingBuffer ringBuffer = new RingBuffer(1024);
        byte[] payload = { 1, 2, 3, 4, 5 };
        bool isChunked = false;
        ushort messageID = 42;

        // Act
        ringBuffer.AddPacketToRingBuffer(payload, isChunked, messageID);

        // Intentionally read only part of the packet header to simulate an incomplete packet
        byte[] incompleteHeader = ringBuffer.Read(2);
        var (success, _, _, _) = ringBuffer.ReadPacketHeader();

        // Assert
        Assert.IsFalse(success);
    }
    
    [Test]
    public void TestIncompleteMessageGetsCompleted()
    {
        // Arrange
        RingBuffer ringBuffer = new RingBuffer(1024);
        byte[] payload = new byte[] { 1, 2, 3, 4, 5 };
        bool isChunked = false;
        ushort messageID = 42;

        // Act - Write only part of the packet header and payload
        ringBuffer.WriteBlock(BitConverter.GetBytes((ushort)payload.Length));
        ringBuffer.WriteBlock(new byte[] { 0 });  // flags
        byte[] incompleteMessageID = BitConverter.GetBytes(messageID);
        ringBuffer.WriteBlock(new byte[] { incompleteMessageID[0] });  // Write only the first byte of messageID

        // Try to read - should fail because the packet is incomplete
        var (success1, _, _, _) = ringBuffer.ReadPacketHeader();
        Assert.IsFalse(success1);

        // Act - Complete the message
        ringBuffer.WriteBlock(new byte[] { incompleteMessageID[1] });  // Write the second byte of messageID
        ringBuffer.WriteBlock(payload);  // Write the payload

        // Try to read again - should succeed
        var (success2, payloadLength, readIsChunked, readMessageID) = ringBuffer.ReadPacketHeader();
        var readPayload = ringBuffer.Read(payloadLength);

        // Assert
        Assert.IsTrue(success2);
        Assert.AreEqual(payload.Length, payloadLength);
        Assert.AreEqual(isChunked, readIsChunked);
        Assert.AreEqual(messageID, readMessageID);
        CollectionAssert.AreEqual(payload, readPayload);
    }
}