// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace MetadataExtractor.Formats.Pdf
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public sealed class PdfFileTrailerDirectory : Directory
    {
        public const int TagSize = 1; // the total number of entries in the file's Cross-Reference Table
        public const int TagPrev = 2;
        public const int TagRoot = 3;
        public const int TagEncrypt = 4;
        public const int TagInfo = 5;
        public const int TagID = 6;

        public override string Name => "PDF File Trailer";

        private static readonly Dictionary<int, string> _tagNameMap = new()
        {
            { TagSize,           "Size" },
            { TagPrev,           "Prev" },
            { TagRoot,           "Root" },
            { TagEncrypt,        "Encrypt" },
            { TagInfo,           "Info" },
            { TagID,             "ID" },
        };

        internal static readonly Dictionary<string, int> TagIntegerMap = new()
        {
            { "/Size", TagSize },
            { "/Prev", TagPrev },
            { "/Root", TagRoot },
            { "/Encrypt", TagEncrypt },
            { "/Info", TagInfo },
            { "/ID", TagID },
        };

        public PdfFileTrailerDirectory() : base(_tagNameMap)
        {
            SetDescriptor(new TagDescriptor<PdfFileTrailerDirectory>(this));
        }
    }
}
