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

using CommunityToolkit.HighPerformance.Buffers;
using PaintDotNet;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace AbrFileTypePlugin
{
    internal static class AbrSave
    {
        /// <summary>
        /// The default brush spacing, 25%.
        /// </summary>
        private const int DefaultSpacingPercent = 25;

        public static unsafe void Save(Document input, Stream output, PropertyBasedSaveConfigToken token, ProgressEventHandler progressCallback)
        {
            if (input.Layers.Count > short.MaxValue)
            {
                throw new FormatException($"The document must not have more than 32767 layers.");
            }

            if (input.Width > short.MaxValue || input.Height > short.MaxValue)
            {
                throw new FormatException($"The document dimensions must be 32767x32767 or less.");
            }

            bool rle = token.GetProperty<PaintDotNet.PropertySystem.BooleanProperty>(PropertyNames.RLE).Value;
            AbrFileVersion fileVersion = (AbrFileVersion)token.GetProperty(PropertyNames.FileVersion).Value;

            List<(int index, Rectangle saveBounds)> nonEmptyLayers = GetNonEmptyLayers(input);

            double progressPercentage = 0.0;
            double progressDelta = (1.0 / nonEmptyLayers.Count) * 100.0;

            using (BigEndianBinaryWriter writer = new(output, true))
            {
                writer.Write((short)fileVersion);
                writer.Write((short)nonEmptyLayers.Count);

                LayerList layers = input.Layers;

                foreach ((int index, Rectangle saveBounds) in nonEmptyLayers)
                {
                    BitmapLayer layer = (BitmapLayer)layers[index];

                    SaveLayer(writer, layer, saveBounds, fileVersion, rle);

                    progressPercentage += progressDelta;

                    progressCallback(null, new ProgressEventArgs(progressPercentage));
                }
            }
        }

        private static unsafe Rectangle GetImageRectangle(Surface surface)
        {
            int top = surface.Height;
            int left = surface.Width;
            int right = 0;
            int bottom = 0;

            for (int y = 0; y < surface.Height; y++)
            {
                ColorBgra* p = surface.GetRowPointerUnchecked(y);

                for (int x = 0; x < surface.Width; x++)
                {
                    // Get the smallest rectangle containing image data.
                    if (p->A > 0)
                    {
                        if (y < top)
                        {
                            top = y;
                        }
                        if (x < left)
                        {
                            left = x;
                        }
                        if (y > bottom)
                        {
                            bottom = y;
                        }
                        if (x > right)
                        {
                            right = x;
                        }
                    }
                    p++;
                }
            }

            if (top < surface.Height && left < surface.Width)
            {
                return new Rectangle(left, top, (right - left) + 1, (bottom - top) + 1);
            }
            else
            {
                return Rectangle.Empty;
            }
        }

        private static List<(int index, Rectangle saveBounds)> GetNonEmptyLayers(Document input)
        {
            LayerList layers = input.Layers;

            // Assume that the document does not contain any empty layers.
            List<(int, Rectangle)> nonEmptyLayers = new(layers.Count);

            for (int i = 0; i < layers.Count; i++)
            {
                BitmapLayer layer = (BitmapLayer)layers[i];

                Rectangle saveBounds = GetImageRectangle(layer.Surface);

                if (!saveBounds.IsEmpty)
                {
                    nonEmptyLayers.Add((i, saveBounds));
                }
            }

            return nonEmptyLayers;
        }

        private static unsafe void GetBrushAlphaData(Surface surface, Rectangle imageBounds, Span<byte> destination)
        {
            // The 'fixed' statement will throw an exception if the array length is zero.
            if (destination.Length > 0)
            {
                fixed (byte* ptr = destination)
                {
                    long windowOffset = ((long)imageBounds.Top * surface.Stride) + ((long)imageBounds.Left * ColorBgra.SizeOf);

                    RegionPtr<ColorBgra32> src = new(surface,
                                                     (ColorBgra32*)((byte*)surface.Scan0.VoidStar + windowOffset),
                                                     imageBounds.Width,
                                                     imageBounds.Height,
                                                     surface.Stride);
                    RegionPtr<byte> dst = new(ptr, imageBounds.Width, imageBounds.Height, imageBounds.Width);

                    PixelKernels.ExtractChannel(dst, src, 3);
                }
            }
        }

        private static void SaveLayer(BigEndianBinaryWriter writer,
                                      BitmapLayer layer,
                                      Rectangle imageBounds,
                                      AbrFileVersion fileVersion,
                                      bool rle)
        {
            writer.Write((short)AbrBrushType.Sampled);

            using (new LengthWriter(writer))
            {
                // Write the miscellaneous data, unused.
                writer.Write(0);

                // Write the spacing.
                string spacingMetaData = layer.Metadata.GetUserValue(AbrMetadataNames.BrushSpacing);

                if (spacingMetaData != null &&
                    short.TryParse(spacingMetaData, NumberStyles.Number, CultureInfo.InvariantCulture, out short spacing))
                {
                    writer.Write(spacing);
                }
                else
                {
                    writer.Write((short)DefaultSpacingPercent);
                }

                // Write the brush name, if applicable.
                if (fileVersion == AbrFileVersion.Version2)
                {
                    writer.WriteUnicodeString(layer.Name);
                }

                // Write the anti-aliasing.
                if (imageBounds.Width < 32 && imageBounds.Height < 32)
                {
                    // Only brushes less than 32x32 pixels are anti-aliased by Photoshop.
                    writer.Write((byte)1);
                }
                else
                {
                    writer.Write((byte)0);
                }

                // Write the Int16 bounds.
                writer.WriteInt16Rectangle(imageBounds);
                // Write the Int32 bounds.
                writer.WriteInt32Rectangle(imageBounds);
                // Write the depth.
                writer.Write((short)8);

                using (SpanOwner<byte> alphaOwner = SpanOwner<byte>.Allocate(imageBounds.Width * imageBounds.Height))
                {
                    Span<byte> alpha = alphaOwner.Span;

                    GetBrushAlphaData(layer.Surface, imageBounds, alpha);

                    int rowsRemaining = imageBounds.Height;
                    int rowsRead = 0;
                    do
                    {
                        // Brushes taller than 16384 pixels are split into 16384 line chunks.
                        int chunkHeight = Math.Min(rowsRemaining, 16384);

                        if (rle)
                        {
                            // Write the RLE compressed header.
                            writer.Write((byte)AbrImageCompression.RLE);

                            long rowCountOffset = writer.Position;

                            for (int i = 0; i < chunkHeight; i++)
                            {
                                // Placeholder for the row byte count.
                                writer.Write(short.MaxValue);
                            }

                            using (SpanOwner<short> rowByteCountOwner = SpanOwner<short>.Allocate(chunkHeight))
                            {
                                Span<short> rowByteCount = rowByteCountOwner.Span;

                                for (int y = 0; y < chunkHeight; y++)
                                {
                                    int currentRow = rowsRead + y;
                                    Span<byte> row = alpha.Slice(currentRow * imageBounds.Width, imageBounds.Width);

                                    rowByteCount[y] = checked((short)RLEHelper.EncodedRow(writer, row));
                                }

                                long current = writer.Position;

                                writer.Position = rowCountOffset;

                                for (int i = 0; i < chunkHeight; i++)
                                {
                                    writer.Write(rowByteCount[i]);
                                }

                                writer.Position = current;
                            }
                        }
                        else
                        {
                            // Write the uncompressed header.
                            writer.Write((byte)AbrImageCompression.Raw);

                            for (int y = 0; y < chunkHeight; y++)
                            {
                                int row = rowsRead + y;
                                writer.Write(alpha.Slice(row * imageBounds.Width,imageBounds.Width));
                            }
                        }

                        rowsRemaining -= 16384;
                        rowsRead += 16384;

                    } while (rowsRemaining > 0);
                }
            }
        }
    }
}
