// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using MetadataExtractor.Formats.Pdf;

namespace MetadataExtractor.Tests.Formats.Pdf
{
    /// <summary>Unit tests for <see cref="StringByteProvider"/>.</summary>
    /// <author>Drew Noakes https://drewnoakes.com</author>
    public sealed class StringByteProviderBackwardTest
    {
        private static ByteStreamBufferedProvider GetBackwardProvider(int bufferLength, int startIndex)
        {
            string input = @"987654321";

            return new StringByteProvider(input, startIndex, bufferLength, ExtractionDirection.Backward);
        }

        [Fact]
        public void TestGetNextItemBackwardStartIndexZero()
        {
            ByteStreamBufferedProvider byteProvider = GetBackwardProvider(bufferLength: 2, startIndex: 0);

            for (char c = '1'; c <= '9'; c++)
            {
                Assert.Equal(c, (char)byteProvider.GetNextItem());
            }
        }

        [Fact]
        public void TestGetNextItemBackwardStartIndexNotZero()
        {
            ByteStreamBufferedProvider byteProvider = GetBackwardProvider(bufferLength: 2, startIndex: 2);

            for (char c = '3'; c <= '9'; c++)
            {
                Assert.Equal(c, (char)byteProvider.GetNextItem());
            }

            Assert.Equal(0, (char)byteProvider.GetNextItem());
            Assert.Equal(0, (char)byteProvider.GetNextItem());
        }


        [Fact]
        public void TestGetNextItemsBackwardStartIndexZero()
        {
            ByteStreamBufferedProvider byteProvider = GetBackwardProvider(bufferLength: 2, startIndex: 0);

            Assert.Equal(new byte[] { (byte)'1', (byte)'2', (byte)'3' }, byteProvider.GetNextItems(3));

            byteProvider.GetNextItem(); // 4
            byteProvider.GetNextItem(); // 5

            Assert.Equal(new byte[] { (byte)'6', (byte)'7', (byte)'8' }, byteProvider.GetNextItems(3));
        }

        [Fact]
        public void TestGetNextItemsBackwardStartIndexNotZero()
        {
            ByteStreamBufferedProvider byteProvider = GetBackwardProvider(bufferLength: 2, startIndex: 2);

            Assert.Equal(new byte[] { (byte)'3', (byte)'4', (byte)'5' }, byteProvider.GetNextItems(3));

            byteProvider.GetNextItem(); // 6
            byteProvider.GetNextItem(); // 7

            Assert.Equal(new byte[] { (byte)'8', (byte)'9', 0 }, byteProvider.GetNextItems(3));
        }


        [Fact]
        public void TestPeekNextItemBackwardStartIndexZero()
        {
            ByteStreamBufferedProvider byteProvider = GetBackwardProvider(bufferLength: 3, startIndex: 0);

            Assert.Equal('1', (char)byteProvider.PeekNextItem(0));
            Assert.Equal('2', (char)byteProvider.PeekNextItem(1));
            Assert.Equal('3', (char)byteProvider.PeekNextItem(2));

            byteProvider.GetNextItem(); // 1
            byteProvider.GetNextItem(); // 2

            Assert.Equal('3', (char)byteProvider.PeekNextItem(0));
            Assert.Equal('4', (char)byteProvider.PeekNextItem(1));
            Assert.Equal('5', (char)byteProvider.PeekNextItem(2));

            byteProvider.GetNextItem(); // 3
            byteProvider.GetNextItem(); // 4
            byteProvider.GetNextItem(); // 5
            byteProvider.GetNextItem(); // 6
            byteProvider.GetNextItem(); // 7
            byteProvider.GetNextItem(); // 8

            Assert.True(byteProvider.HasNextItem);

            Assert.Equal('9', (char)byteProvider.PeekNextItem(0));

            Assert.Equal(0, (char)byteProvider.PeekNextItem(1));
            Assert.Equal(0, (char)byteProvider.PeekNextItem(2));

            Assert.True(byteProvider.HasNextItem);

            byteProvider.GetNextItem(); // 9

            Assert.False(byteProvider.HasNextItem);

            Assert.Equal(0, (char)byteProvider.PeekNextItem(0));
        }

        [Fact]
        public void TestPeekNextItemBackwardStartIndexNotZero()
        {
            ByteStreamBufferedProvider byteProvider = GetBackwardProvider(bufferLength: 3, startIndex: 2);

            Assert.Equal('3', (char)byteProvider.PeekNextItem(0));
            Assert.Equal('4', (char)byteProvider.PeekNextItem(1));
            Assert.Equal('5', (char)byteProvider.PeekNextItem(2));

            byteProvider.GetNextItem(); // 3
            byteProvider.GetNextItem(); // 4

            Assert.Equal('5', (char)byteProvider.PeekNextItem(0));
            Assert.Equal('6', (char)byteProvider.PeekNextItem(1));
            Assert.Equal('7', (char)byteProvider.PeekNextItem(2));

            byteProvider.GetNextItem(); // 5
            byteProvider.GetNextItem(); // 6
            byteProvider.GetNextItem(); // 7
            byteProvider.GetNextItem(); // 8

            Assert.True(byteProvider.HasNextItem);

            Assert.Equal('9', (char)byteProvider.PeekNextItem(0));

            Assert.Equal(0, (char)byteProvider.PeekNextItem(1));
            Assert.Equal(0, (char)byteProvider.PeekNextItem(2));

            Assert.True(byteProvider.HasNextItem);

            byteProvider.GetNextItem(); // 9

            Assert.False(byteProvider.HasNextItem);

            Assert.Equal(0, (char)byteProvider.PeekNextItem(0));
        }


        [Fact]
        public void TestConsumeBackwardStartIndexZero()
        {
            ByteStreamBufferedProvider byteProvider = GetBackwardProvider(bufferLength: 2, startIndex: 0);

            Assert.Equal('1', (char)byteProvider.PeekNextItem(0));

            byteProvider.Consume(1); // 1

            Assert.Equal('2', (char)byteProvider.PeekNextItem(0));

            byteProvider.Consume(4); // 2 3 4 5

            Assert.Equal('6', (char)byteProvider.PeekNextItem(0));
            Assert.Equal('7', (char)byteProvider.PeekNextItem(1));

            Assert.True(byteProvider.HasNextItem);

            byteProvider.Consume(4); // 6 7 8 9

            Assert.False(byteProvider.HasNextItem);

            Assert.Equal(0, (char)byteProvider.PeekNextItem(0));

            byteProvider.Consume(1); // --

            Assert.False(byteProvider.HasNextItem);

            Assert.Equal(0, (char)byteProvider.PeekNextItem(0));
        }

        [Fact]
        public void TestConsumeBackwardStartIndexNotZero()
        {
            ByteStreamBufferedProvider byteProvider = GetBackwardProvider(bufferLength: 2, startIndex: 2);

            Assert.Equal('3', (char)byteProvider.PeekNextItem(0));

            byteProvider.Consume(1); // 3

            Assert.Equal('4', (char)byteProvider.PeekNextItem(0));

            byteProvider.Consume(4); // 4 5 6 7

            Assert.Equal('8', (char)byteProvider.GetNextItem()); // 8

            Assert.True(byteProvider.HasNextItem);

            Assert.Equal('9', (char)byteProvider.PeekNextItem(0));

            Assert.True(byteProvider.HasNextItem);

            byteProvider.Consume(1); // 9

            Assert.False(byteProvider.HasNextItem);

            Assert.Equal(0, (char)byteProvider.PeekNextItem(0));

            byteProvider.Consume(1); // --

            Assert.False(byteProvider.HasNextItem);

            Assert.Equal(0, (char)byteProvider.PeekNextItem(0));
        }


        [Fact]
        public void TestHasNextItemBackwardStartIndexZero()
        {
            ByteStreamBufferedProvider byteProvider = GetBackwardProvider(bufferLength: 2, startIndex: 0);

            Assert.True(byteProvider.HasNextItem);

            byteProvider.Consume(8); // 1 2 3 4 5 6 7 8

            Assert.True(byteProvider.HasNextItem);

            byteProvider.Consume(1); // 9

            Assert.False(byteProvider.HasNextItem);

            byteProvider.Consume(1); // --

            Assert.False(byteProvider.HasNextItem);
        }

        [Fact]
        public void TestHasNextItemBackwardStartIndexNotZero()
        {
            ByteStreamBufferedProvider byteProvider = GetBackwardProvider(bufferLength: 2, startIndex: 2);

            Assert.True(byteProvider.HasNextItem);

            byteProvider.Consume(6); // 3 4 5 6 7 8

            Assert.True(byteProvider.HasNextItem);

            byteProvider.Consume(1); // 9

            Assert.False(byteProvider.HasNextItem);

            byteProvider.Consume(1); // --

            Assert.False(byteProvider.HasNextItem);
        }


        [Fact]
        public void TestCurrentIndexBackwardStartIndexZero()
        {
            ByteStreamBufferedProvider byteProvider = GetBackwardProvider(bufferLength: 3, startIndex: 0);

            Assert.Equal(8 - 0, byteProvider.CurrentIndex);

            Assert.Equal('1', (char)byteProvider.PeekNextItem(0));

            Assert.Equal(8 - 0, byteProvider.CurrentIndex);

            Assert.Equal('2', (char)byteProvider.PeekNextItem(1));
            Assert.Equal('3', (char)byteProvider.PeekNextItem(2));

            Assert.Equal(8 - 0, byteProvider.CurrentIndex);

            byteProvider.Consume(1); // 1

            Assert.Equal(8 - 1, byteProvider.CurrentIndex);

            Assert.Equal('2', (char)byteProvider.PeekNextItem(0));

            Assert.Equal(8 - 1, byteProvider.CurrentIndex);

            Assert.Equal('2', (char)byteProvider.GetNextItem()); // 2

            Assert.Equal(8 - 2, byteProvider.CurrentIndex);

            byteProvider.Consume(3); // 3 4 5

            Assert.Equal(8 - 5, byteProvider.CurrentIndex);

            byteProvider.Consume(3); // 6 7 8

            Assert.Equal(8 - 8, byteProvider.CurrentIndex); // NOTE CurrentIndex is undefined when !HasNextItem, so don't go further
        }

        [Fact]
        public void TestCurrentIndexBackwardStartIndexNotZero()
        {
            ByteStreamBufferedProvider byteProvider = GetBackwardProvider(bufferLength: 3, startIndex: 2);

            Assert.Equal(8 - 2, byteProvider.CurrentIndex);

            Assert.Equal('3', (char)byteProvider.PeekNextItem(0));

            Assert.Equal(8 - 2, byteProvider.CurrentIndex);

            Assert.Equal('4', (char)byteProvider.PeekNextItem(1));
            Assert.Equal('5', (char)byteProvider.PeekNextItem(2));

            Assert.Equal(8 - 2, byteProvider.CurrentIndex);

            byteProvider.Consume(1); // 3

            Assert.Equal(8 - 3, byteProvider.CurrentIndex);

            Assert.Equal('4', (char)byteProvider.PeekNextItem(0));

            Assert.Equal(8 - 3, byteProvider.CurrentIndex);

            Assert.Equal('4', (char)byteProvider.GetNextItem()); // 4

            Assert.Equal(8 - 4, byteProvider.CurrentIndex);

            byteProvider.Consume(3); // 5 6 7

            Assert.Equal(8 - 7, byteProvider.CurrentIndex);

            byteProvider.Consume(1); // 8

            Assert.Equal(8 - 8, byteProvider.CurrentIndex); // NOTE CurrentIndex is undefined when !HasNextItem, so don't go further
        }
    }
}
