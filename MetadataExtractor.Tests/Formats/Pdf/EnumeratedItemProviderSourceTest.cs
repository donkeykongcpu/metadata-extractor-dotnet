// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using MetadataExtractor.Formats.Pdf;

namespace MetadataExtractor.Tests.Formats.Pdf
{
    /// <summary>Unit tests for <see cref="EnumeratedItemProviderSource"/>.</summary>
    /// <author>Drew Noakes https://drewnoakes.com</author>
    public sealed class EnumeratedItemProviderSourceTest
    {
        private static EnumeratedItemProviderSource<int> GetSource()
        {
            List<int> items = new List<int>();

            for (int i = 1; i <= 9; i++)
            {
                items.Add(i);
            }

            return new EnumeratedItemProviderSource<int>(items, 0);
        }

  
        [Fact]
        public void TestSequence()
        {
            EnumeratedItemProviderSource<int> intSource = GetSource();

            Assert.Equal(0, intSource.GetCurrentIndex(0));

            Assert.Equal(3, intSource.GetCurrentIndex(3));

            Assert.Equal(100, intSource.GetCurrentIndex(100));

            int[] items = intSource.GetNextItems(4);

            Assert.Equal(4, items.Length);

            Assert.Equal(1, items[0]);
            Assert.Equal(2, items[1]);
            Assert.Equal(3, items[2]);
            Assert.Equal(4, items[3]);

            items = intSource.GetNextItems(4);

            Assert.Equal(4, items.Length);

            Assert.Equal(5, items[0]);
            Assert.Equal(6, items[1]);
            Assert.Equal(7, items[2]);
            Assert.Equal(8, items[3]);

            items = intSource.GetNextItems(4);

            Assert.Single(items);

            Assert.Equal(9, items[0]);
        }
    }
}
