// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace MetadataExtractor.Formats.Pdf
{
    /// <summary>Provides human-readable string versions of the tags stored in a <see cref="PdfDirectory"/>.</summary>
    /// <author>Payton Garland</author>
    /// <author>Kevin Mott https://github.com/kwhopper</author>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public sealed class PdfDescriptor : TagDescriptor<PdfDirectory>
    {
        public PdfDescriptor(PdfDirectory directory)
            : base(directory)
        {
        }

        public override string? GetDescription(int tagType)
        {
            switch (tagType)
            {
                case PdfDirectory.TagImageWidth:
                case PdfDirectory.TagImageHeight:
                    return GetPixelDescription(tagType);
                case PdfDirectory.TagTiffPreviewSize:
                case PdfDirectory.TagTiffPreviewOffset:
                    return GetByteSizeDescription(tagType);
                case PdfDirectory.TagColorType:
                    return GetColorTypeDescription();
                default:
                    return base.GetDescription(tagType);
            }
        }

        public string GetPixelDescription(int tagType)
        {
            return Directory.GetString(tagType) + " pixels";
        }

        public string GetByteSizeDescription(int tagType)
        {
            return Directory.GetString(tagType) + " bytes";
        }

        public string? GetColorTypeDescription()
        {
            return GetIndexedDescription(PdfDirectory.TagColorType, 1,
                "Grayscale", "Lab", "RGB", "CMYK");
        }
    }
}
