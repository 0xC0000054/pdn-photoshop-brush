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

using PaintDotNet;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using System.Collections.Generic;
using System.IO;

namespace AbrFileTypePlugin
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    public sealed class AbrFileType : PropertyBasedFileType
    {
        public static string StaticName => "Photoshop Brush";

        public AbrFileType() : base(
            StaticName,
            new FileTypeOptions
            {
                LoadExtensions = new string[] { ".abr" },
                SaveExtensions = new string[] { ".abr" },
                SupportsLayers = true,
            })
        {
        }

        protected override Document OnLoad(Stream input)
        {
            return AbrLoad.Load(input);
        }

        public override PropertyCollection OnCreateSavePropertyCollection()
        {
            List<Property> props = new()
            {
                StaticListChoiceProperty.CreateForEnum(PropertyNames.FileVersion, AbrFileVersion.Version2, false),
                new BooleanProperty(PropertyNames.RLE, true)
            };

            return new PropertyCollection(props);
        }

        public override ControlInfo OnCreateSaveConfigUI(PropertyCollection props)
        {
            ControlInfo info = CreateDefaultSaveConfigUI(props);

            info.SetPropertyControlValue(PropertyNames.FileVersion, ControlInfoPropertyNames.DisplayName, Properties.Resources.FileVersionHeader);
            info.FindControlForPropertyName(PropertyNames.FileVersion).SetValueDisplayName(AbrFileVersion.Version1, Properties.Resources.FileVersion1);
            info.FindControlForPropertyName(PropertyNames.FileVersion).SetValueDisplayName(AbrFileVersion.Version2, Properties.Resources.FileVersion2);
            info.SetPropertyControlType(PropertyNames.FileVersion, PropertyControlType.RadioButton);

            info.SetPropertyControlValue(PropertyNames.RLE, ControlInfoPropertyNames.DisplayName, string.Empty);
            info.SetPropertyControlValue(PropertyNames.RLE, ControlInfoPropertyNames.Description, Properties.Resources.RLECompression);

            return info;
        }

        protected override void OnSaveT(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
        {
            AbrSave.Save(input, output, token, progressCallback);
        }
    }
}
