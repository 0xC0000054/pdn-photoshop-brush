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

using System;
using System.Reflection;
using PaintDotNet;

namespace AbrFileTypePlugin
{
    public sealed class PluginSupportInfo : IPluginSupportInfo
    {
        public string Author => "null54";

        public string Copyright => ((AssemblyCopyrightAttribute)typeof(AbrFileType).Assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)[0]).Copyright;

        public string DisplayName => AbrFileType.StaticName;

        public Version Version => typeof(AbrFileType).Assembly.GetName().Version;

        public Uri WebsiteUri => new Uri("https://forums.getpaint.net/index.php?/topic/25792-photoshop-brush-filetype/");
    }
}
