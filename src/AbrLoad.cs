/////////////////////////////////////////////////////////////////////////////////
//
// ABR FileType Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012-2020, 2022 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

// Portions of this file has been adapted from:
/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See src/Resources/Files/License.txt for full licensing and attribution      //
// details.                                                                    //
// .                                                                           //
/////////////////////////////////////////////////////////////////////////////////

using AbrFileTypePlugin.Properties;
using CommunityToolkit.HighPerformance.Buffers;
using PaintDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AbrFileTypePlugin
{
    internal static class AbrLoad
    {
        public static Document Load(Stream stream)
        {
            using (BigEndianBinaryReader reader = new(stream))
            {
                List<Brush> brushes;
                short version = reader.ReadInt16();

                switch (version)
                {
                    case 1:
                    case 2:
                        brushes = DecodeVersion1(reader, version);
                        break;
                    case 6:
                    case 7: // Used by Photoshop CS and later for brushes containing 16-bit data.
                    case 10: // Used by Photoshop CS6 and/or CC?
                        brushes = DecodeVersion6(reader, version);
                        break;
                    default:
                        throw new FormatException(string.Format(CultureInfo.CurrentCulture, Resources.UnsupportedABRVersion, version));
                }

                if (brushes.Count == 0)
                {
                    throw new FormatException(Resources.NoSampledBrushes);
                }

                int maxWidth = 0;
                int maxHeight = 0;
                foreach (Brush item in brushes)
                {
                    if (item.Surface.Width > maxWidth)
                    {
                        maxWidth = item.Surface.Width;
                    }

                    if (item.Surface.Height > maxHeight)
                    {
                        maxHeight = item.Surface.Height;
                    }
                }

                Document doc = null;
                Document tempDoc = null;

                try
                {
                    tempDoc = new Document(maxWidth, maxHeight);

                    for (int i = 0; i < brushes.Count; i++)
                    {
                        Brush abr = brushes[i];

                        BitmapLayer layer = null;
                        BitmapLayer tempLayer = null;
                        try
                        {
                            tempLayer = new BitmapLayer(maxWidth, maxHeight);
                            tempLayer.IsBackground = i == 0;
                            tempLayer.Name = !string.IsNullOrEmpty(abr.Name) ? abr.Name : string.Format(CultureInfo.CurrentCulture, Resources.BrushNameFormat, i);
                            tempLayer.Metadata.SetUserValue(AbrMetadataNames.BrushSpacing, abr.Spacing.ToString(CultureInfo.InvariantCulture));

                            tempLayer.Surface.CopySurface(abr.Surface);
                            layer = tempLayer;
                            tempLayer = null;
                        }
                        finally
                        {
                            if (tempLayer != null)
                            {
                                tempLayer.Dispose();
                                tempLayer = null;
                            }
                            abr.Dispose();
                        }

                        tempDoc.Layers.Add(layer);
                    }
                    doc = tempDoc;
                    tempDoc = null;
                }
                finally
                {
                    tempDoc?.Dispose();
                }

                return doc;
            }
        }

        private static List<Brush> DecodeVersion1(BigEndianBinaryReader reader, short version)
        {
            short count = reader.ReadInt16();

            List<Brush> brushes = new(count);

            for (int i = 0; i < count; i++)
            {
                AbrBrushType type = (AbrBrushType)reader.ReadInt16();
                int size = reader.ReadInt32();

                long endOffset = reader.Position + size;

#if DEBUG
                System.Diagnostics.Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Brush: {0}, type: {1}, size: {2} bytes", i, type, size));
#endif
                if (type == AbrBrushType.Computed)
                {
#if DEBUG
                    // Skip the obsolete 'miscellaneous' field
                    reader.Position += 4L;
                    short spacing = reader.ReadInt16();

                    string name = string.Empty;
                    if (version == 2)
                    {
                        name = reader.ReadUnicodeString();
                    }

                    short diameter = reader.ReadInt16();
                    short roundness = reader.ReadInt16();
                    short angle = reader.ReadInt16();
                    short hardness = reader.ReadInt16();
#else
                    reader.Position += size;
#endif
                }
                else if (type == AbrBrushType.Sampled)
                {
                    // Skip the obsolete 'miscellaneous' field
                    reader.Position += 4L;
                    short spacing = reader.ReadInt16();

                    string name = string.Empty;
                    if (version == 2)
                    {
                        name = reader.ReadUnicodeString();
                    }

                    bool antiAlias = reader.ReadByte() != 0;

                    // Skip the Int16 bounds.
                    reader.Position += 8L;

                    Rectangle bounds = reader.ReadInt32Rectangle();
                    if (bounds.Width <= 0 || bounds.Height <= 0)
                    {
                        // Skip any brushes that have invalid dimensions.
                        reader.Position += (endOffset - reader.Position);
                        continue;
                    }

                    short depth = reader.ReadInt16();

                    if (depth != 8)
                    {
                        // The format specs state that brushes must be 8-bit, skip any that are not.
                        reader.Position += (endOffset - reader.Position);
                        continue;
                    }
                    int height = bounds.Height;
                    int width = bounds.Width;

                    int rowsRemaining = height;
                    int rowsRead = 0;

                    int alphaDataSize = checked(width * height);

                    using (SpanOwner<byte> alphaDataOwner = SpanOwner<byte>.Allocate(alphaDataSize))
                    {
                        Span<byte> alphaData = alphaDataOwner.Span;

                        do
                        {
                            // Sampled brush data is broken into repeating chunks for brushes taller that 16384 pixels.
                            int chunkHeight = Math.Min(rowsRemaining, 16384);
                            // The format specs state that compression is stored as a 2-byte field, but it is written as a 1-byte field in actual files.
                            AbrImageCompression compression = (AbrImageCompression)reader.ReadByte();

                            if (compression == AbrImageCompression.RLE)
                            {
                                // Skip the compressed row lengths
                                reader.Position += (long)chunkHeight * sizeof(short);

                                for (int y = 0; y < chunkHeight; y++)
                                {
                                    int row = rowsRead + y;
                                    RLEHelper.DecodedRow(reader, alphaData.Slice(row * width, width));
                                }
                            }
                            else
                            {
                                int numBytesToRead = chunkHeight * width;
                                int numBytesRead = rowsRead * width;

                                reader.ProperRead(alphaData.Slice(numBytesRead, numBytesToRead));
                            }

                            rowsRemaining -= 16384;
                            rowsRead += 16384;

                        } while (rowsRemaining > 0);

                        brushes.Add(CreateSampledBrush(width, height, depth, alphaData, name, spacing));
                    }
                }
                else
                {
                    // Skip any unknown brush types.
                    reader.Position += size;
                }
            }

            return brushes;
        }

        private static List<Brush> DecodeVersion6(BigEndianBinaryReader reader, short majorVersion)
        {
            short minorVersion = reader.ReadInt16();
            long unusedDataLength;

            switch (minorVersion)
            {
                case 1:
                    // Skip the Int16 bounds rectangle and the unknown Int16.
                    unusedDataLength = 10L;
                    break;
                case 2:
                    // Skip the unknown bytes.
                    unusedDataLength = 264L;
                    break;
                default:
                    throw new FormatException(string.Format(CultureInfo.CurrentCulture, Resources.UnsupportedABRSubVersion, majorVersion, minorVersion));
            }

            BrushSectionParser parser = new(reader);

            List<Brush> brushes = new(parser.SampledBrushes.Count);

            long sampleSectionOffset = parser.SampleSectionOffset;

            if (parser.SampledBrushes.Count > 0 && sampleSectionOffset >= 0)
            {
                reader.Position = sampleSectionOffset;

                uint sectionLength = reader.ReadUInt32();

                long sectionEnd = reader.Position + sectionLength;

                while (reader.Position < sectionEnd)
                {
                    uint brushLength = reader.ReadUInt32();

                    // The brush data is padded to 4 byte alignment.
                    long paddedBrushLength = ((long)brushLength + 3) & ~3;

                    long endOffset = reader.Position + paddedBrushLength;

                    string tag = reader.ReadPascalString();

                    // Skip the unneeded data that comes before the Int32 bounds rectangle.
                    reader.Position += unusedDataLength;

                    Rectangle bounds = reader.ReadInt32Rectangle();
                    if (bounds.Width <= 0 || bounds.Height <= 0)
                    {
                        // Skip any brushes that have invalid dimensions.
                        reader.Position += (endOffset - reader.Position);
                        continue;
                    }

                    short depth = reader.ReadInt16();
                    if (depth != 8 && depth != 16)
                    {
                        // Skip any brushes with an unknown bit depth.
                        reader.Position += (endOffset - reader.Position);
                        continue;
                    }

                    SampledBrush sampledBrush = parser.SampledBrushes.FindLargestBrush(tag);
                    if (sampledBrush != null)
                    {
                        AbrImageCompression compression = (AbrImageCompression)reader.ReadByte();

                        int height = bounds.Height;
                        int width = bounds.Width;
                        Brush brush = null;

                        int alphaDataSize = depth == 16 ? checked(width * height * 2) : checked(width * height);

                        using (SpanOwner<byte> alphaDataOwner = SpanOwner<byte>.Allocate(alphaDataSize))
                        {
                            Span<byte> alphaData = alphaDataOwner.Span;

                            if (compression == AbrImageCompression.RLE)
                            {
                                // Skip the compressed row lengths
                                reader.Position += (long)height * sizeof(short);

                                int bytesPerRow = depth == 16 ? checked(width * 2) : width;

                                for (int y = 0; y < height; y++)
                                {
                                    RLEHelper.DecodedRow(reader, alphaData.Slice(y * width, bytesPerRow));
                                }
                            }
                            else
                            {
                                reader.ProperRead(alphaData);
                            }

                            brush = CreateSampledBrush(width, height, depth, alphaData, sampledBrush.Name, sampledBrush.Spacing);
                        }

                        brushes.Add(brush);

                        // Some brushes only store the largest item and scale it down.
                        IEnumerable<SampledBrush> scaledBrushes = parser.SampledBrushes.Where(i => i.Tag.Equals(tag, StringComparison.Ordinal) && i.Diameter < sampledBrush.Diameter);
                        if (scaledBrushes.Any())
                        {
                            int originalWidth = brush.Surface.Width;
                            int originalHeight = brush.Surface.Height;

                            foreach (SampledBrush item in scaledBrushes.OrderByDescending(p => p.Diameter))
                            {
                                Size size = ComputeBrushSize(originalWidth, originalHeight, item.Diameter);

                                Brush scaledBrush = new(size.Width, size.Height, item.Name, item.Spacing);
                                scaledBrush.Surface.FitSurface(ResamplingAlgorithm.AdaptiveBestQuality, brush.Surface);

                                brushes.Add(scaledBrush);
                            }
                        }
                    }

                    long remaining = endOffset - reader.Position;
                    // Skip any remaining bytes until the next sampled brush.
                    if (remaining > 0)
                    {
                        reader.Position += remaining;
                    }
                }
            }

            return brushes;
        }

        private static unsafe Brush CreateSampledBrush(int width,
                                                       int height,
                                                       int depth,
                                                       ReadOnlySpan<byte> alphaData,
                                                       string name,
                                                       int spacing)
        {
            Brush brush = null;
            Brush tempBrush = null;

            try
            {
                tempBrush = new Brush(width, height, name, spacing);
                Surface surface = tempBrush.Surface;

                fixed (byte* ptr = alphaData)
                {
                    if (depth == 16)
                    {
                        int srcStride = width * 2;
                        for (int y = 0; y < height; y++)
                        {
                            byte* src = ptr + (y * srcStride);
                            ColorBgra* dst = surface.GetRowPointerUnchecked(y);

                            for (int x = 0; x < width; x++)
                            {
                                ushort val = (ushort)((src[0] << 8) | src[1]);

                                dst->B = dst->G = dst->R = 0;
                                // The 16-bit brush data is stored in the range of [0, 32768].
                                dst->A = (byte)((val * 10) / 1285);

                                src += 2;
                                dst++;
                            }
                        }
                    }
                    else
                    {
                        for (int y = 0; y < height; y++)
                        {
                            byte* src = ptr + (y * width);
                            ColorBgra* dst = surface.GetRowPointerUnchecked(y);
                            for (int x = 0; x < width; x++)
                            {
                                dst->B = dst->G = dst->R = 0;
                                dst->A = *src;

                                src++;
                                dst++;
                            }
                        }
                    }
                }

                brush = tempBrush;
                tempBrush = null;
            }
            finally
            {
                if (tempBrush != null)
                {
                    tempBrush.Dispose();
                    tempBrush = null;
                }
            }

            return brush;
        }

        private static Size ComputeBrushSize(int originalWidth, int originalHeight, int maxEdgeLength)
        {
            Size thumbSize = Size.Empty;

            if (originalWidth <= 0 || originalHeight <= 0)
            {
                thumbSize.Width = 1;
                thumbSize.Height = 1;
            }
            else if (originalWidth > originalHeight)
            {
                int longSide = Math.Min(originalWidth, maxEdgeLength);
                thumbSize.Width = longSide;
                thumbSize.Height = Math.Max(1, (originalHeight * longSide) / originalWidth);
            }
            else if (originalHeight > originalWidth)
            {
                int longSide = Math.Min(originalHeight, maxEdgeLength);
                thumbSize.Width = Math.Max(1, (originalWidth * longSide) / originalHeight);
                thumbSize.Height = longSide;
            }
            else
            {
                int longSide = Math.Min(originalWidth, maxEdgeLength);
                thumbSize.Width = longSide;
                thumbSize.Height = longSide;
            }

            return thumbSize;
        }

        private sealed class Brush : IDisposable
        {
            private Surface surface;
            private readonly string name;
            private readonly int spacing;

            public Brush(int width, int height, string name, int spacing)
            {
                this.surface = new Surface(width, height);
                this.name = name;
                this.spacing = spacing;
            }

            public Surface Surface => this.surface;

            public string Name => this.name;

            public int Spacing => this.spacing;

            public void Dispose()
            {
                if (this.surface != null)
                {
                    this.surface.Dispose();
                    this.surface = null;
                }
            }
        }
    }
}
