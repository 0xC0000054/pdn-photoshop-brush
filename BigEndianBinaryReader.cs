/////////////////////////////////////////////////////////////////////////////////
//
// ABR FileType Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012-2020 Nicholas Hayes
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
using System.Drawing;
using System.IO;
using System.Text;

namespace AbrFileTypePlugin
{
    // Adapted from 'Problem and Solution: The Terrible Inefficiency of FileStream and BinaryReader'
    // https://jacksondunstan.com/articles/3568

    internal sealed class BigEndianBinaryReader : IDisposable
    {
        private Stream stream;
        private readonly byte[] buffer;
        private int readOffset;
        private int readLength;

        private readonly int bufferSize;

        private const int MaxBufferSize = 4096;

        /// <summary>
        /// Initializes a new instance of the <see cref="BigEndianBinaryReader"/> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        public BigEndianBinaryReader(Stream stream)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            this.bufferSize = (int)Math.Min(stream.Length, MaxBufferSize);
            this.buffer = new byte[this.bufferSize];
            this.readOffset = 0;
            this.readLength = 0;
        }

        /// <summary>
        /// Gets the length of the stream.
        /// </summary>
        /// <value>
        /// The length of the stream.
        /// </value>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public long Length
        {
            get
            {
                VerifyNotDisposed();

                return this.stream.Length;
            }
        }

        /// <summary>
        /// Gets or sets the position in the stream.
        /// </summary>
        /// <value>
        /// The position in the stream.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">value is negative.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public long Position
        {
            get
            {
                VerifyNotDisposed();

                return this.stream.Position - this.readLength + this.readOffset;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException("value");
                }

                VerifyNotDisposed();

                long current = this.Position;

                if (value != current)
                {
                    long bufferStartOffset = current - this.readOffset;
                    long bufferEndOffset = bufferStartOffset + this.readLength;

                    // Avoid reading from the stream if the offset is within the current buffer.
                    if (value >= bufferStartOffset && value <= bufferEndOffset)
                    {
                        this.readOffset = (int)(value - bufferStartOffset);
                    }
                    else
                    {
                        // Invalidate the existing buffer.
                        this.readOffset = 0;
                        this.readLength = 0;
                        this.stream.Seek(value, SeekOrigin.Begin);
                    }
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.stream != null)
            {
                this.stream.Dispose();
                this.stream = null;
            }
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream, starting from a specified point in the byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="offset">The starting offset in the array.</param>
        /// <param name="count">The count.</param>
        /// <returns>The number of bytes read from the stream.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public int Read(byte[] bytes, int offset, int count)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            VerifyNotDisposed();

            if (count == 0)
            {
                return 0;
            }

            if ((this.readOffset + count) <= this.readLength)
            {
                Buffer.BlockCopy(this.buffer, this.readOffset, bytes, offset, count);
                this.readOffset += count;

                return count;
            }
            else
            {
                // Ensure that any bytes at the end of the current buffer are included.
                int bytesUnread = this.readLength - this.readOffset;

                if (bytesUnread > 0)
                {
                    Buffer.BlockCopy(this.buffer, this.readOffset, bytes, offset, bytesUnread);
                }

                // Invalidate the existing buffer.
                this.readOffset = 0;
                this.readLength = 0;

                int totalBytesRead = bytesUnread;

                totalBytesRead += this.stream.Read(bytes, offset + bytesUnread, count - bytesUnread);

                return totalBytesRead;
            }
        }

        /// <summary>
        /// Reads the next byte from the current stream.
        /// </summary>
        /// <returns>The next byte read from the current stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public byte ReadByte()
        {
            VerifyNotDisposed();

            EnsureBuffer(sizeof(byte));

            byte val = this.buffer[this.readOffset];
            this.readOffset += sizeof(byte);

            return val;
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream.
        /// </summary>
        /// <param name="count">The number of bytes to read..</param>
        /// <returns>An array containing the specified bytes.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public byte[] ReadBytes(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            VerifyNotDisposed();

            if (count == 0)
            {
                return EmptyArray<byte>.Value;
            }

            byte[] bytes = new byte[count];

            if ((this.readOffset + count) <= this.readLength)
            {
                Buffer.BlockCopy(this.buffer, this.readOffset, bytes, 0, count);
                this.readOffset += count;
            }
            else
            {
                // Ensure that any bytes at the end of the current buffer are included.
                int bytesUnread = this.readLength - this.readOffset;

                if (bytesUnread > 0)
                {
                    Buffer.BlockCopy(this.buffer, this.readOffset, bytes, 0, bytesUnread);
                }

                int numBytesToRead = count - bytesUnread;
                int numBytesRead = bytesUnread;
                do
                {
                    int n = this.stream.Read(bytes, numBytesRead, numBytesToRead);

                    if (n == 0)
                    {
                        throw new EndOfStreamException();
                    }

                    numBytesRead += n;
                    numBytesToRead -= n;

                } while (numBytesToRead > 0);

                // Invalidate the existing buffer.
                this.readOffset = 0;
                this.readLength = 0;
            }

            return bytes;
        }

        /// <summary>
        /// Reads a 8-byte floating point value in big endian byte order.
        /// </summary>
        /// <returns>The 8-byte floating point value.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public unsafe double ReadDouble()
        {
            ulong temp = ReadUInt64();

            return *(double*)&temp;
        }

        /// <summary>
        /// Reads a 2-byte signed integer in big endian byte order.
        /// </summary>
        /// <returns>The 2-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public short ReadInt16()
        {
            return (short)ReadUInt16();
        }

        /// <summary>
        /// Reads a 2-byte unsigned integer in big endian byte order.
        /// </summary>
        /// <returns>The 2-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public ushort ReadUInt16()
        {
            VerifyNotDisposed();

            EnsureBuffer(sizeof(ushort));

            ushort val = (ushort)((this.buffer[this.readOffset] << 8) | this.buffer[this.readOffset + 1]);
            this.readOffset += sizeof(ushort);

            return val;
        }

        /// <summary>
        /// Reads a 4-byte signed integer in big endian byte order.
        /// </summary>
        /// <returns>The 4-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public int ReadInt32()
        {
            return (int)ReadUInt32();
        }

        /// <summary>
        /// Reads a 4-byte unsigned integer in big endian byte order.
        /// </summary>
        /// <returns>The 4-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public uint ReadUInt32()
        {
            VerifyNotDisposed();

            EnsureBuffer(sizeof(uint));

            uint val = (uint)((this.buffer[this.readOffset] << 24) | (this.buffer[this.readOffset + 1] << 16) | (this.buffer[this.readOffset + 2] << 8) | this.buffer[this.readOffset + 3]);
            this.readOffset += sizeof(uint);

            return val;
        }

        /// <summary>
        /// Reads a 4-byte floating point value in big endian byte order.
        /// </summary>
        /// <returns>The 4-byte floating point value.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public unsafe float ReadSingle()
        {
            uint temp = ReadUInt32();

            return *(float*)&temp;
        }

        /// <summary>
        /// Reads a 8-byte signed integer in big endian byte order.
        /// </summary>
        /// <returns>The 8-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public long ReadInt64()
        {
            return (long)ReadUInt64();
        }

        /// <summary>
        /// Reads a 8-byte unsigned integer in big endian byte order.
        /// </summary>
        /// <returns>The 8-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public ulong ReadUInt64()
        {
            VerifyNotDisposed();

            EnsureBuffer(sizeof(ulong));

            uint hi = (uint)((this.buffer[this.readOffset] << 24) | (this.buffer[this.readOffset + 1] << 16) | (this.buffer[this.readOffset + 2] << 8) | this.buffer[this.readOffset + 3]);
            uint lo = (uint)((this.buffer[this.readOffset + 4] << 24) | (this.buffer[this.readOffset + 5] << 16) | (this.buffer[this.readOffset + 6] << 8) | this.buffer[this.readOffset + 7]);
            this.readOffset += sizeof(ulong);

            return (((ulong)hi) << 32) | lo;
        }

        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the pascal string.
        /// </summary>
        /// <returns>A string containing the characters of the Pascal string.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public string ReadPascalString()
        {
            VerifyNotDisposed();

            byte stringLength = ReadByte();

            if (stringLength == 0)
            {
                return string.Empty;
            }

            byte[] bytes = ReadBytes(stringLength);

            return Encoding.ASCII.GetString(bytes);
        }

        /// <summary>
        /// Reads a rectangle comprised of 4-byte signed integers.
        /// </summary>
        /// <returns>A rectangle comprised of 4-byte signed integers.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public Rectangle ReadInt32Rectangle()
        {
            VerifyNotDisposed();

#pragma warning disable IDE0017 // Simplify object initialization
            Rectangle rect = new Rectangle();
#pragma warning restore IDE0017 // Simplify object initialization

            rect.Y = ReadInt32();
            rect.X = ReadInt32();
            rect.Height = ReadInt32() - rect.Y;
            rect.Width = ReadInt32() - rect.X;

            return rect;
        }

        /// <summary>
        /// Reads a length-prefixed UTF-16 string.
        /// </summary>
        /// <returns>A string containing the characters of the UTF-16 string..</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public string ReadUnicodeString()
        {
            VerifyNotDisposed();

            int lengthInChars = ReadInt32();

            if (lengthInChars == 0)
            {
                return string.Empty;
            }

            byte[] bytes = ReadBytes(lengthInChars * 2);

            return Encoding.BigEndianUnicode.GetString(bytes).TrimEnd('\0');
        }

        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Fills the buffer with at least the number of bytes requested.
        /// </summary>
        /// <param name="minBytes">The minimum number of bytes to place in the buffer.</param>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        private void FillBuffer(int minBytes)
        {
            int bytesUnread = this.readLength - this.readOffset;

            if (bytesUnread > 0)
            {
                Buffer.BlockCopy(this.buffer, this.readOffset, this.buffer, 0, bytesUnread);
            }

            int numBytesToRead = this.bufferSize - bytesUnread;
            int numBytesRead = bytesUnread;
            do
            {
                int n = this.stream.Read(this.buffer, numBytesRead, numBytesToRead);

                if (n == 0)
                {
                    throw new EndOfStreamException();
                }

                numBytesRead += n;
                numBytesToRead -= n;

            } while (numBytesRead < minBytes);

            this.readOffset = 0;
            this.readLength = numBytesRead;
        }

        /// <summary>
        /// Ensures that the buffer contains at least the number of bytes requested.
        /// </summary>
        /// <param name="count">The minimum number of bytes the buffer should contain.</param>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        private void EnsureBuffer(int count)
        {
            if ((this.readOffset + count) > this.readLength)
            {
                FillBuffer(count);
            }
        }

        private void VerifyNotDisposed()
        {
            if (this.stream == null)
            {
                throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
            }
        }

        private static class EmptyArray<T>
        {
            public static readonly T[] Value = new T[0];
        }
    }
}
