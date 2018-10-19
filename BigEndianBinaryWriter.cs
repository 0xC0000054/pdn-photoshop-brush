/////////////////////////////////////////////////////////////////////////////////
//
// ABR FileType Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012-2018 Nicholas Hayes
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

using System.Drawing;
using System.IO;
using System.Text;

namespace AbrFileTypePlugin
{
    internal sealed class BigEndianBinaryWriter : BinaryWriter
    {
        private readonly bool leaveOpen;

        public BigEndianBinaryWriter(Stream stream, bool leaveOpen) : base(stream)
        {
            this.leaveOpen = leaveOpen;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.leaveOpen)
            {
                base.Dispose(disposing);
            }
        }

        public override void Write(char value)
        {
            unsafe
            {
                SwapBytes((byte*)&value, 2);
            }
            base.Write(value);
        }

        public override void Write(short value)
        {
            unsafe
            {
                SwapBytes((byte*)&value, 2);
            }
            base.Write(value);
        }

        public override void Write(int value)
        {
            unsafe
            {
                SwapBytes((byte*)&value, 4);
            }
            base.Write(value);
        }

        public override void Write(long value)
        {
            unsafe
            {
                SwapBytes((byte*)&value, 8);
            }
            base.Write(value);
        }

        public override void Write(ushort value)
        {
            unsafe
            {
                SwapBytes((byte*)&value, 2);
            }
            base.Write(value);
        }

        public override void Write(uint value)
        {
            unsafe
            {
                SwapBytes((byte*)&value, 4);
            }
            base.Write(value);
        }

        public override void Write(ulong value)
        {
            unsafe
            {
                SwapBytes((byte*)&value, 8);
            }
            base.Write(value);
        }

        public override void Write(double value)
        {
            unsafe
            {
                SwapBytes((byte*)&value, 8);
            }
            base.Write(value);
        }

        //////////////////////////////////////////////////////////////////

        public void WriteInt16Rectangle(Rectangle rect)
        {
            Write((short)rect.Top);
            Write((short)rect.Left);
            Write((short)rect.Bottom);
            Write((short)rect.Right);
        }

        public void WriteInt32Rectangle(Rectangle rect)
        {
            Write(rect.Top);
            Write(rect.Left);
            Write(rect.Bottom);
            Write(rect.Right);
        }

        public void WriteUnicodeString(string value)
        {
            Write(checked(value.Length + 1));
            Write(Encoding.BigEndianUnicode.GetBytes(value));
            // The string is always null-terminated.
            Write((ushort)0);
        }

        //////////////////////////////////////////////////////////////////

        private static unsafe void SwapBytes(byte* ptr, int nLength)
        {
            for (long i = 0; i < nLength / 2; ++i)
            {
                byte t = *(ptr + i);
                *(ptr + i) = *(ptr + nLength - i - 1);
                *(ptr + nLength - i - 1) = t;
            }
        }

    }
}
