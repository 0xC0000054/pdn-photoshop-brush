/////////////////////////////////////////////////////////////////////////////////
//
// ABR FileType Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012-2020, 2022, 2023 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

// Portions of this file has been adapted from:
/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop PSD FileType Plugin for Paint.NET
// http://psdplugin.codeplex.com/
//
// This software is provided under the MIT License:
//   Copyright (c) 2006-2007 Frank Blumenberg
//   Copyright (c) 2010-2011 Tao Yue
//
// Portions of this file are provided under the BSD 3-clause License:
//   Copyright (c) 2006, Jonas Beckeman
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;

namespace AbrFileTypePlugin
{
    internal static class RLEHelper
    {
        ////////////////////////////////////////////////////////////////////////

        private class RlePacketStateMachine
        {
            private bool rlePacket = false;
            private byte lastValue;
            private int idxPacketData;
            private int packetLength;
            private readonly Stream stream;

            private const int maxPacketLength = 128;

            internal RlePacketStateMachine(Stream stream)
            {
                this.stream = stream;
            }

            internal void PushRow(ReadOnlySpan<byte> row)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    byte color = row[i];

                    if (this.packetLength == 0)
                    {
                        // Starting a fresh packet.
                        this.rlePacket = false;
                        this.lastValue = color;
                        this.idxPacketData = i;
                        this.packetLength = 1;
                    }
                    else if (this.packetLength == 1)
                    {
                        // 2nd byte of this packet... decide RLE or non-RLE.
                        this.rlePacket = (color == this.lastValue);
                        this.lastValue = color;
                        this.packetLength = 2;
                    }
                    else if (this.packetLength == maxPacketLength)
                    {
                        // Packet is full. Start a new one.
                        Flush(row);
                        this.rlePacket = false;
                        this.lastValue = color;
                        this.idxPacketData = i;
                        this.packetLength = 1;
                    }
                    else if (this.packetLength >= 2 && this.rlePacket && color != this.lastValue)
                    {
                        // We were filling in an RLE packet, and we got a non-repeated color.
                        // Emit the current packet and start a new one.
                        Flush(row);
                        this.rlePacket = false;
                        this.lastValue = color;
                        this.idxPacketData = i;
                        this.packetLength = 1;
                    }
                    else if (this.packetLength >= 2 && this.rlePacket && color == this.lastValue)
                    {
                        // We are filling in an RLE packet, and we got another repeated color.
                        // Add the new color to the current packet.
                        this.packetLength++;
                    }
                    else if (this.packetLength >= 2 && !this.rlePacket && color != this.lastValue)
                    {
                        // We are filling in a raw packet, and we got another random color.
                        // Add the new color to the current packet.
                        this.lastValue = color;
                        this.packetLength++;
                    }
                    else if (this.packetLength >= 2 && !this.rlePacket && color == this.lastValue)
                    {
                        // We were filling in a raw packet, but we got a repeated color.
                        // Emit the current packet without its last color, and start a
                        // new RLE packet that starts with a length of 2.
                        this.packetLength--;
                        Flush(row);
                        this.rlePacket = true;
                        this.packetLength = 2;
                        this.lastValue = color;
                    }
                }

                Flush(row);
            }

            private void Flush(ReadOnlySpan<byte> data)
            {
                byte header;
                if (this.rlePacket)
                {
                    header = (byte)(-(this.packetLength - 1));
                    this.stream.WriteByte(header);
                    this.stream.WriteByte(this.lastValue);
                }
                else
                {
                    header = (byte)(this.packetLength - 1);
                    this.stream.WriteByte(header);
                    this.stream.Write(data.Slice(this.idxPacketData, this.packetLength));
                }

                this.packetLength = 0;
            }
        }

        ////////////////////////////////////////////////////////////////////////

        public static long EncodedRow(Stream stream, ReadOnlySpan<byte> row)
        {
            long startPosition = stream.Position;

            RlePacketStateMachine machine = new(stream);
            machine.PushRow(row);

            return stream.Position - startPosition;
        }

        ////////////////////////////////////////////////////////////////////////

        public static void DecodedRow(BigEndianBinaryReader reader, Span<byte> destination)
        {
            int count = 0;
            while (count < destination.Length)
            {
                byte byteValue = reader.ReadByte();

                int len = byteValue;
                if (len < 128)
                {
                    len++;

                    reader.ProperRead(destination.Slice(count, len));

                    count += len;
                }
                else if (len > 128)
                {
                    // Next -len+1 bytes in the dest are replicated from next source byte.
                    // (Interpret len as a negative 8-bit int.)
                    len ^= 0x0FF;
                    len += 2;

                    byteValue = reader.ReadByte();

                    destination.Slice(count, len).Fill(byteValue);

                    count += len;
                }
            }
        }
    }
}
