using System;

namespace nez.net.transport;

public class RingBuffer
{
    private readonly byte[] _buffer;
    private readonly int _capacity;
    private int _start;
    private int _end;

    private int FreeSpace => CalculateFreeSpace();

    public RingBuffer(int capacity)
    {
        _buffer = new byte[capacity];
        _capacity = capacity;
        _start = 0;
        _end = 0;
    }
    
    public void Write(byte[] data)
    {
        // Using Buffer.BlockCopy for better performance
        Buffer.BlockCopy(data, 0, _buffer, _end, data.Length);
        _end = (_end + data.Length) % _capacity;
    }
    
    private int CalculateFreeSpace()
    {
        return _end >= _start ? _capacity - (_end - _start) : _start - _end - 1;
    }
    
    public void WriteBlock(byte[] data)
    {
        if (HasEnoughSpace(data.Length))
        {
            int dataLength = data.Length;

            // Check if the data wraps around the end of the buffer
            if (_end + dataLength > _capacity)
            {
                // Calculate the size of the two slices
                int slice1Size = _capacity - _end;
                int slice2Size = dataLength - slice1Size;

                // Copy the first slice
                Buffer.BlockCopy(data, 0, _buffer, _end, slice1Size);

                // Copy the second slice
                Buffer.BlockCopy(data, slice1Size, _buffer, 0, slice2Size);

                // Update the _end index
                _end = slice2Size;
            }
            else
            {
                // Copy the data in one go
                Buffer.BlockCopy(data, 0, _buffer, _end, dataLength);

                // Update the _end index
                _end = (_end + dataLength) % _capacity;
            }
        }
        else
        {
            throw new InvalidOperationException("Not enough space in the RingBuffer");
        }
    }
    
    private bool HasEnoughSpace(int length)
    {
        return FreeSpace >= length;
    }

    public byte[] Read(int length)
    {
        int availableData = AvailableData();
        if (availableData < length)
        {
            // Return null or special value instead of throwing exception
            return null;
        }

        byte[] result = new byte[length];

        // Inline calculations and copy bytes before and after the wrap-around
        Buffer.BlockCopy(_buffer, _start, result, 0, Math.Min(length, _capacity - _start));
        if (length > _capacity - _start)
        {
            Buffer.BlockCopy(_buffer, 0, result, _capacity - _start, length - (_capacity - _start));
        }

        _start = (_start + length) % _capacity;

        return result;
    }
    
    public void AddPacketToRingBuffer(byte[] payload, bool isChunked, ushort messageID)
    {
        // Calculate the length of the packet header (5 bytes) and the payload
        int totalLength = 5 + payload.Length;

        // Check if the RingBuffer has enough space
        if (FreeSpace >= totalLength)
        {
            // Write the length directly into the RingBuffer
            WriteBlock(BitConverter.GetBytes((ushort)payload.Length));

            // Write the flags directly into the RingBuffer
            byte flags = 0;
            if (isChunked)
            {
                flags |= 1;  // Set the first bit to 1 if the message is chunked
            }
            WriteBlock(new[] { flags });

            // Write the messageID directly into the RingBuffer
            WriteBlock(BitConverter.GetBytes(messageID));

            // Write the payload to the RingBuffer
            WriteBlock(payload);
        }
        else
        {
            Console.WriteLine("Not enough space in the RingBuffer");
            // Handle the case where there's not enough space
            // You could either wait or drop the packet
        }
    }
    
    public void AddPacketToRingBuffer(byte[] payload, bool isChunked, ushort messageID, byte chunkIndex, byte chunkCount, int maxBufferSize)
    {
        // Calculate the length of the packet header (5 bytes) and the payload
        int headerLength = 5;
        int chunkHeaderLength = isChunked ? 2 : 0; // 1 byte for chunkIndex and 1 byte for chunkCount
        int maxPayloadSize = maxBufferSize - (headerLength + chunkHeaderLength);

        // Calculate the size and offset of the chunk
        int chunkSize = payload.Length / chunkCount;
        int lastChunkSize = payload.Length - (chunkSize * (chunkCount - 1));
        int chunkOffset = chunkSize * chunkIndex;

        // If this is the last chunk, adjust the chunk size
        if (chunkIndex == chunkCount - 1)
        {
            chunkSize = lastChunkSize;
        }

        // Check if the chunk size exceeds maxPayloadSize
        if (chunkSize > maxPayloadSize)
        {
            Console.WriteLine("Chunk size exceeds maxBufferSize");
            // Handle this case, perhaps by further splitting the chunk or dropping it
            return;
        }

        // Check if the RingBuffer has enough space
        int totalLength = headerLength + chunkHeaderLength + chunkSize;
        if (FreeSpace >= totalLength)
        {
            // Write the length directly into the RingBuffer
            WriteBlock(BitConverter.GetBytes((ushort)chunkSize));

            // Write the flags directly into the RingBuffer
            byte flags = 0;
            if (isChunked)
            {
                flags |= 1;  // Set the first bit to 1 if the message is chunked
            }
            WriteBlock(new[] { flags });

            // Write the messageID directly into the RingBuffer
            WriteBlock(BitConverter.GetBytes(messageID));

            if (isChunked)
            {
                // Write the chunkIndex and chunkCount directly into the RingBuffer
                WriteBlock(new[] { chunkIndex, chunkCount });
            }

            // Get a chunk of chunkIndex/chunkCount from the payload
            byte[] chunkData = new byte[chunkSize];
            Array.Copy(payload, chunkOffset, chunkData, 0, chunkSize);

            // Write the chunk to the RingBuffer
            WriteBlock(chunkData);
        }
        else
        {
            Console.WriteLine("Not enough space in the RingBuffer");
            // Handle the case where there's not enough space
            // You could either wait or drop the packet
        }
    }
    
    private void RollbackReadPointer(int bytes)
    {
        _start = (_start - bytes + _capacity) % _capacity;
    }
    
    public (bool success, ushort payloadLength, bool isChunked, ushort messageID) ReadPacketHeader()
    {
        // Check if there's enough data to read the packet header (5 bytes)
        if (AvailableData() < 5)
        {
            return (false, 0, false, 0);
        }

        try
        {
            // Read the packet header
            byte[] lengthBytes = Read(2);
            byte[] flagBytes = Read(1);
            byte[] messageIDBytes = Read(2);

            ushort payloadLength = BitConverter.ToUInt16(lengthBytes, 0);
            byte flags = flagBytes[0];
            bool isChunked = (flags & 1) != 0;  // Check the first bit
            ushort messageID = BitConverter.ToUInt16(messageIDBytes, 0);

            // Check if there's enough data to read the payload
            if (AvailableData() < payloadLength)
            {
                // Rollback the read pointer to before the packet header, so we can try again later
                _start = (_start - 5 + _capacity) % _capacity;
                return (false, 0, false, 0);
            }

            // Read the payload
            // byte[] payload = Read(payloadLength);

            return (true, payloadLength, isChunked, messageID);
        }
        catch (Exception e)
        {
            RollbackReadPointer(5);
            throw new InvalidOperationException("Failed to read packet header", e);
        }
    }

    public byte[] ReadSendPacket()
{
    // Check if there's enough data to read the packet header (5 bytes)
    if (AvailableData() < 5)
    {
        return null;
    }

    try
    {
        // Read the entire 5-byte packet header in one go
        byte[] headerBytes = Read(5);
        ushort payloadLength = BitConverter.ToUInt16(headerBytes, 0);

        Console.WriteLine($"Payload Length: {payloadLength}, Available Data: {AvailableData()}");

        // Read chunked flag
        bool isChunked = (headerBytes[2] & 1) != 0;  // Check the first bit

        // If chunked, include chunk header and payload
        byte[] chunkHeaderBytes = null;
        if (isChunked)
        {
            // Check if there's enough data to read the chunk header (2 bytes)
            if (AvailableData() < 2)
            {
                RollbackReadPointer(5);
                return null;
            }

            // Read the 2-byte chunk header
            chunkHeaderBytes = Read(2);
        }

        // Check if there's enough data to read the payload
        var availableData = AvailableData();
        if (availableData < payloadLength)
        {
            RollbackReadPointer(5);
            return null;
        }

        // Boundary check
        if (payloadLength > _buffer.Length)
        {
            throw new InvalidOperationException("Payload length exceeds buffer size");
        }

        // Read the payload
        byte[] payload = Read(payloadLength);

        // Calculate the total packet length
        int totalLength = 5 + (isChunked ? 2 : 0) + payloadLength;

        // Combine header, optional chunk header, and payload into a single byte array
        byte[] packet = new byte[totalLength];
        Buffer.BlockCopy(headerBytes, 0, packet, 0, 5);
        if (isChunked)
        {
            Buffer.BlockCopy(chunkHeaderBytes, 0, packet, 5, 2);
        }
        Buffer.BlockCopy(payload, 0, packet, 5 + (isChunked ? 2 : 0), payloadLength);

        return packet;
    }
    catch (Exception e)
    {
        RollbackReadPointer(5);
        throw new InvalidOperationException("Failed to read send packet", e);
    }
}

    public int AvailableData()
    {
        return _end >= _start ? _end - _start : _capacity - _start + _end;
    }

    public (byte chunkIndex, byte totalChunks, byte[] payload) ReadChunkHeader(ushort payloadLength)
    {
        // Check if there's enough data to read the packet header (5 bytes)
        if (AvailableData() < 5)
        {
            return (0, 0, null);
        }

        // Read the packet header
        byte chunkIndex = Read(1)[0];
        byte totalChunks = Read(1)[0];

        // Check if there's enough data to read the payload
        if (AvailableData() < payloadLength)
        {
            // Rollback the read pointer to before the packet header, so we can try again later
            _start = (_start - 5 + _capacity) % _capacity;
            return (0, 0, null);
        }

        // Read the payload
        byte[] payload = Read(payloadLength);

        return (chunkIndex, totalChunks, payload);
    }
}