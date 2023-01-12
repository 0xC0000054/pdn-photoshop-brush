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

namespace AbrFileTypePlugin
{
    internal sealed class SampledBrush
    {
        public string Name { get; }

        public string Tag { get; }

        public int Diameter { get; }

        public int Spacing { get; }

        public SampledBrush(string name, string tag, int diameter, int spacing)
        {
            this.Name = name;
            this.Tag = tag;
            this.Diameter = diameter;
            this.Spacing = spacing;
        }
    }
}
