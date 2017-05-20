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

namespace AbrFileTypePlugin
{
    internal sealed class SampledBrush
    {
        private readonly string name;
        private readonly string tag;
        private readonly int diameter;

        public string Name
        {
            get
            {
                return this.name;
            }
        }

        public string Tag
        {
            get
            {
                return this.tag;
            }
        }

        public int Diameter
        {
            get
            {
                return this.diameter;
            }
        }

        public SampledBrush(string name, string tag, int diameter)
        {
            this.name = name;
            this.tag = tag;
            this.diameter = diameter;
        }
    }
}
