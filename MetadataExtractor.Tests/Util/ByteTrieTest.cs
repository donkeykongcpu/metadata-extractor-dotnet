// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace MetadataExtractor.Tests.Util
{
    /// <summary>Unit tests for <see cref="ByteTrie{T}"/>.</summary>
    /// <author>Drew Noakes https://drewnoakes.com</author>
    public sealed class ByteTrieTest
    {
        [Fact]
        public void Basics()
        {
            var trie = new ByteTrie<string>("DEFAULT");

            var strings = new[] { "HELLO", "HELLO WORLD", "HERBERT" };

            foreach (var s in strings)
                trie.Add(s, Encoding.UTF8.GetBytes(s));

            foreach (var s1 in strings)
                Assert.Same(s1, trie.Find(Encoding.UTF8.GetBytes(s1)));

            Assert.Equal("DEFAULT", trie.Find(Encoding.UTF8.GetBytes("Not Included")));
            Assert.Equal("DEFAULT", trie.Find(Encoding.UTF8.GetBytes("HELL")));
            Assert.Equal("DEFAULT", trie.Find(Encoding.UTF8.GetBytes("HEL")));
            Assert.Equal("DEFAULT", trie.Find(Encoding.UTF8.GetBytes("HE")));
            Assert.Equal("DEFAULT", trie.Find(Encoding.UTF8.GetBytes("Also Not Included")));

            Assert.Equal("HELLO", trie.Find(Encoding.UTF8.GetBytes("HELLO MUM")));
            Assert.Equal("HELLO WORLD".Length, trie.MaxDepth);
        }
    }
}
