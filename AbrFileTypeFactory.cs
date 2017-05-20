/////////////////////////////////////////////////////////////////////////////////
//
// ABR FileType Plugin for Paint.NET
// 
// This software is provided under the MIT License:
//   Copyright (c) 2012-2017 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;

namespace AbrFileTypePlugin
{
    public sealed class AbrFileTypeFactory : IFileTypeFactory
    {
        public FileType[] GetFileTypeInstances()
        {
            return new FileType[] { new AbrFileType() };
        }
    }
}
