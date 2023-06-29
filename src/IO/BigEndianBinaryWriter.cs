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
using System.Buffers.Binary;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace AbrFileTypePlugin
{
    internal sealed class BigEndianBinaryWriter : IDisposable
    {
        private Stream stream;

        private readonly bool leaveOpen;

        /// <summary>
        /// Initializes a new instance of the <see cref="BigEndianBinaryWriter"/> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="leaveOpen">If set to <c>true</c> leave the stream open when disposing..</param>
        /// <exception cref="ArgumentNullException">stream</exception>
        public BigEndianBinaryWriter(Stream stream, bool leaveOpen)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            this.leaveOpen = leaveOpen;
        }

        /// <summary>
        /// Gets the underlying stream of the <see cref="BigEndianBinaryWriter"/>.
        /// </summary>
        /// <value>
        /// The underlying stream of the <see cref="BigEndianBinaryWriter"/>.
        /// </value>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public Stream BaseStream
        {
            get
            {
                VerifyNotDisposed();

                // Force the stream to write any buffered data.
                this.stream.Flush();

                return this.stream;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!this.leaveOpen && this.stream != null)
            {
                this.stream.Dispose();
                this.stream = null;
            }
        }

        /// <summary>
        /// Writes a byte to the current stream.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void Write(byte value)
        {
            VerifyNotDisposed();

            this.stream.WriteByte(value);
        }

        /// <summary>
        /// Writes a 2-byte signed integer to the current stream in big endian byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        [SkipLocalsInit]
        public void Write(short value)
        {
            VerifyNotDisposed();

            Span<byte> buffer = stackalloc byte[sizeof(short)];
            BinaryPrimitives.WriteInt16BigEndian(buffer, value);
            this.stream.Write(buffer);
        }

        /// <summary>
        /// Writes a 4-byte signed integer to the current stream in big endian byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        [SkipLocalsInit]
        public void Write(int value)
        {
            VerifyNotDisposed();

            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            this.stream.Write(buffer);
        }

        /// <summary>
        /// Writes an 8-byte signed integer to the current stream in big endian byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        [SkipLocalsInit]
        public void Write(long value)
        {
            VerifyNotDisposed();

            Span<byte> buffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(buffer, value);
            this.stream.Write(buffer);
        }

        /// <summary>
        /// Writes a 2-byte unsigned integer to the current stream in big endian byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        [SkipLocalsInit]
        public void Write(ushort value)
        {
            VerifyNotDisposed();

            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
            this.stream.Write(buffer);
        }

        /// <summary>
        /// Writes a 4-byte unsigned integer to the current stream in big endian byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        [SkipLocalsInit]
        public void Write(uint value)
        {
            VerifyNotDisposed();

            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            this.stream.Write(buffer);
        }

        /// <summary>
        /// Writes an 8-byte unsigned integer to the current stream in big endian byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        [SkipLocalsInit]
        public void Write(ulong value)
        {
            VerifyNotDisposed();

            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
            this.stream.Write(buffer);
        }

        /// <summary>
        /// Writes a 4-byte floating point value to the current stream in big endian byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        [SkipLocalsInit]
        public void Write(float value)
        {
            VerifyNotDisposed();

            Span<byte> buffer = stackalloc byte[sizeof(float)];
            BinaryPrimitives.WriteSingleBigEndian(buffer, value);
            this.stream.Write(buffer);
        }

        /// <summary>
        /// Writes an 8-byte floating point value to the current stream in big endian byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        [SkipLocalsInit]
        public void Write(double value)
        {
            VerifyNotDisposed();

            Span<byte> buffer = stackalloc byte[sizeof(double)];
            BinaryPrimitives.WriteDoubleBigEndian(buffer, value);
            this.stream.Write(buffer);
        }

        /// <summary>
        /// Writes the specified byte array to the current stream.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void Write(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            VerifyNotDisposed();

            this.stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Writes the specified number of bytes to the current stream, starting from a specified point in the byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void Write(ReadOnlySpan<byte> bytes)
        {
            VerifyNotDisposed();

            this.stream.Write(bytes);
        }

        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes a rectangle comprised of 2-byte signed integers.
        /// </summary>
        /// <param name="rect">The rect.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void WriteInt16Rectangle(Rectangle rect)
        {
            VerifyNotDisposed();

            Write((short)rect.Top);
            Write((short)rect.Left);
            Write((short)rect.Bottom);
            Write((short)rect.Right);
        }

        /// <summary>
        /// Writes a rectangle comprised of 4-byte signed integers.
        /// </summary>
        /// <param name="rect">The rect.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void WriteInt32Rectangle(Rectangle rect)
        {
            VerifyNotDisposed();

            Write(rect.Top);
            Write(rect.Left);
            Write(rect.Bottom);
            Write(rect.Right);
        }

        /// <summary>
        /// Writes a length-prefixed UTF-16 string.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void WriteUnicodeString(string value)
        {
            VerifyNotDisposed();

            Write(checked(value.Length + 1));
            Write(Encoding.BigEndianUnicode.GetBytes(value));
            // The string is always null-terminated.
            Write((ushort)0);
        }

        private void VerifyNotDisposed()
        {
            if (this.stream == null)
            {
                throw new ObjectDisposedException(nameof(BigEndianBinaryWriter));
            }
        }
    }
}
