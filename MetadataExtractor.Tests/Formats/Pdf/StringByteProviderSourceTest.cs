// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using MetadataExtractor.Formats.Pdf;

namespace MetadataExtractor.Tests.Formats.Pdf
{
    /// <summary>Unit tests for <see cref="StringByteProviderSource"/>.</summary>
    /// <author>Drew Noakes https://drewnoakes.com</author>
    public sealed class StringByteProviderSourceTest
    {
        private static StringByteProviderSource GetForwardSource(int startIndex)
        {
            string input = @"123456789";

            return new StringByteProviderSource(input, startIndex, ExtractionDirection.Forward);
        }

        private static StringByteProviderSource GetBackwardSource(int startIndex)
        {
            string input = @"987654321";

            return new StringByteProviderSource(input, startIndex, ExtractionDirection.Backward);
        }

  
        [Fact]
        public void TestForwardStartIndexZero()
        {
            StringByteProviderSource byteSource = GetForwardSource(startIndex: 0);

            Assert.Equal(0, byteSource.GetCurrentIndex(0));

            Assert.Equal(3, byteSource.GetCurrentIndex(3));

            Assert.Equal(100, byteSource.GetCurrentIndex(100));

            byte[] items = byteSource.GetNextItems(4);

            Assert.Equal(4, items.Length);

            Assert.Equal((byte)'1', items[0]);
            Assert.Equal((byte)'2', items[1]);
            Assert.Equal((byte)'3', items[2]);
            Assert.Equal((byte)'4', items[3]);

            items = byteSource.GetNextItems(4);

            Assert.Equal(4, items.Length);

            Assert.Equal((byte)'5', items[0]);
            Assert.Equal((byte)'6', items[1]);
            Assert.Equal((byte)'7', items[2]);
            Assert.Equal((byte)'8', items[3]);

            items = byteSource.GetNextItems(4);

            Assert.Single(items);

            Assert.Equal((byte)'9', items[0]);
        }

        [Fact]
        public void TestForwardStartIndexNonZero()
        {
            StringByteProviderSource byteSource = GetForwardSource(startIndex: 2);

            Assert.Equal(2, byteSource.GetCurrentIndex(0));

            Assert.Equal(5, byteSource.GetCurrentIndex(3));

            Assert.Equal(102, byteSource.GetCurrentIndex(100));

            byte[] items = byteSource.GetNextItems(4);

            Assert.Equal(4, items.Length);

            Assert.Equal((byte)'3', items[0]);
            Assert.Equal((byte)'4', items[1]);
            Assert.Equal((byte)'5', items[2]);
            Assert.Equal((byte)'6', items[3]);

            items = byteSource.GetNextItems(4);

            Assert.Equal(3, items.Length);

            Assert.Equal((byte)'7', items[0]);
            Assert.Equal((byte)'8', items[1]);
            Assert.Equal((byte)'9', items[2]);
        }


        [Fact]
        public void TestBackwardStartIndexZero()
        {
            StringByteProviderSource byteSource = GetBackwardSource(startIndex: 0);

            Assert.Equal(8, byteSource.GetCurrentIndex(0));

            Assert.Equal(5, byteSource.GetCurrentIndex(3));

            Assert.Equal(8 - 100, byteSource.GetCurrentIndex(100));

            byte[] items = byteSource.GetNextItems(4);

            Assert.Equal(4, items.Length);

            Assert.Equal((byte)'1', items[0]);
            Assert.Equal((byte)'2', items[1]);
            Assert.Equal((byte)'3', items[2]);
            Assert.Equal((byte)'4', items[3]);

            items = byteSource.GetNextItems(4);

            Assert.Equal(4, items.Length);

            Assert.Equal((byte)'5', items[0]);
            Assert.Equal((byte)'6', items[1]);
            Assert.Equal((byte)'7', items[2]);
            Assert.Equal((byte)'8', items[3]);

            items = byteSource.GetNextItems(4);

            Assert.Single(items);

            Assert.Equal((byte)'9', items[0]);
        }

        [Fact]
        public void TestBackwardStartIndexNonZero()
        {
            StringByteProviderSource byteSource = GetBackwardSource(startIndex: 2);

            Assert.Equal(6, byteSource.GetCurrentIndex(0));

            Assert.Equal(3, byteSource.GetCurrentIndex(3));

            Assert.Equal(8 - 100 - 2, byteSource.GetCurrentIndex(100));

            byte[] items = byteSource.GetNextItems(4);

            Assert.Equal(4, items.Length);

            Assert.Equal((byte)'3', items[0]);
            Assert.Equal((byte)'4', items[1]);
            Assert.Equal((byte)'5', items[2]);
            Assert.Equal((byte)'6', items[3]);

            items = byteSource.GetNextItems(4);

            Assert.Equal(3, items.Length);

            Assert.Equal((byte)'7', items[0]);
            Assert.Equal((byte)'8', items[1]);
            Assert.Equal((byte)'9', items[2]);
        }
    }
}
