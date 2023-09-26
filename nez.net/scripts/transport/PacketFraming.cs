using System;

namespace nez.net.transport;

public static class PacketFraming
{
    // create method which will create packet header
    // 2 bytes for length, 1 byte for flags, 2 bytes for messageID
    // flags: 1 bit for isChunked
    public static byte[] CreatePacketHeader(bool isChunked, ushort messageID, ushort length = 0)
    {
        // Pre-allocate the header array. 2 bytes for length, 1 byte for flags, 2 bytes for messageID
        byte[] header = new byte[5];

        // set length, 2 bytes
        BitConverter.GetBytes(length).CopyTo(header, 0);
        
        // Set the flags
        byte flags = 0;
        if (isChunked)
        {
            flags |= 1;  // Set the first bit to 1 if the message is chunked
        }
        header[2] = flags;

        // Set the messageID
        BitConverter.GetBytes(messageID).CopyTo(header, 3);

        return header;
    }
    
    // create a packet header with chunk header
    // size should not exceed maxBufferSize
    // 2 bytes for length, 1 byte for flags, 2 bytes for messageID
    // flags: 1 bit for isChunked
    // 1 byte for chunkIndex, 1 byte for totalChunks
    public static byte[] AppendChunkHeader(ushort chunkIndex, ushort totalChunks, byte[] packetHeader)
    {
        // Validate the length of the packetHeader
        if (packetHeader.Length != 5)
        {
            Console.WriteLine("Invalid packet header length");
            return null;
        }

        // Pre-allocate the header array. 5 bytes for existing packet header
        // Plus 2 bytes for chunkIndex and 2 bytes for totalChunks
        byte[] header = new byte[9];

        // Copy existing packet header
        Buffer.BlockCopy(packetHeader, 0, header, 0, 5);

        // Set the chunkIndex
        BitConverter.GetBytes(chunkIndex).CopyTo(header, 5);

        // Set the totalChunks
        BitConverter.GetBytes(totalChunks).CopyTo(header, 7);

        return header;
    }

    // helper method to read packet header
    public static (ushort payloadLength, bool isChunked, ushort messageID) ReadPacketHeader(this byte[] framedPacket)
    {
        ushort payloadLength = BitConverter.ToUInt16(framedPacket, 0);
        byte flags = framedPacket[2];
        bool isChunked = (flags & 1) != 0;  // Check the first bit
        ushort messageID = BitConverter.ToUInt16(framedPacket, 3);

        return (payloadLength, isChunked, messageID);
    }
    
    // helper method to read chunk header, which assume that the packet already have a packet header
    public static (ushort chunkIndex, ushort totalChunks) ReadChunkHeader(this byte[] framedPacket)
    {
        ushort chunkIndex = BitConverter.ToUInt16(framedPacket, 5);
        ushort totalChunks = BitConverter.ToUInt16(framedPacket, 7);

        return (chunkIndex, totalChunks);
    }
    
    public static void SetLengthToPacketHeader(this byte[] framedPacket, ushort length)
    {
        BitConverter.GetBytes(length).CopyTo(framedPacket, 0);
    }
    
    public static byte[] PrependPacketHeaderToPayload(byte[] packetHeader, byte[] payload)
    {
        byte[] newPacket = new byte[packetHeader.Length + payload.Length];

        // Copy the packet header into the new array
        Buffer.BlockCopy(packetHeader, 0, newPacket, 0, packetHeader.Length);

        // Copy the payload into the new array, right after the header
        Buffer.BlockCopy(payload, 0, newPacket, packetHeader.Length, payload.Length);

        return newPacket;
    }
}