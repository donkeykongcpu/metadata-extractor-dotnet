// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using MetadataExtractor.Formats.Pdf;

namespace MetadataExtractor.Tests.Formats.Pdf
{
    /// <summary>Unit tests for <see cref="PdfParser"/>.</summary>
    /// <author>Drew Noakes https://drewnoakes.com</author>
    public sealed class PdfParserTest
    {
        private static PdfObject ParseTokens(IEnumerable<Token> tokens)
        {
            EnumeratedItemProviderSource<Token> tokenSource = new EnumeratedItemProviderSource<Token>(tokens, new DummyToken());

            ItemProvider<Token> tokenProvider = new BufferedItemProvider<Token>(tokenSource, 5);

            return PdfParser.ParseObject(tokenProvider);
        }

        private class PdfObjectEqualityComparer : IEqualityComparer<PdfObject>
        {
            public bool Equals(PdfObject? object1, PdfObject? object2)
            {
                if (ReferenceEquals(object1, object2))
                {
                    return true;
                }

                if (object1 is null || object2 is null)
                {
                    return false;
                }

                if (object1.Type != object2.Type)
                {
                    return false;
                }

                object? value1 = object1.GetValue();

                object? value2 = object2.GetValue();

                if (value1 is null && value2 is null && object1.Type == "null" && object2.Type == "null")
                {
                    return true;
                }
                else if (value1 is null || value2 is null)
                {
                    return false;
                }

                switch (object1.Type)
                {
                    case "boolean":
                        return Convert.ToBoolean(value1) == Convert.ToBoolean(value2);
                    case "numeric-integer":
                        return Convert.ToInt32(value1) == Convert.ToInt32(value2);
                    case "numeric-real":
                        return Convert.ToDecimal(value1) == Convert.ToDecimal(value2);
                    case "string":
                    case "name":
                        return ((StringValue)value1)!.Bytes.EqualTo(((StringValue)value2)!.Bytes);
                    case "indirect-reference":
                        return ((IndirectReference)value1).ObjectNumber == ((IndirectReference)value2).ObjectNumber
                            && ((IndirectReference)value1).Generation == ((IndirectReference)value2).Generation;
                    case "array":
                        return ((List<PdfObject>)value1).SequenceEqual((List<PdfObject>)value2, this);
                    case "dictionary":
                        Dictionary<string, PdfObject> dictionary1 = (Dictionary<string, PdfObject>)value1;
                        Dictionary<string, PdfObject> dictionary2 = (Dictionary<string, PdfObject>)value2;
                        return dictionary1.Keys.Count == dictionary2.Keys.Count
                            && dictionary1.All(item => dictionary2.ContainsKey(item.Key) && Equals(item.Value, dictionary2[item.Key]));

                    //case "XXX": return XXX;
                    //case "XXX": return XXX;
                    default: throw new Exception($"Unknown object type: {object1.Type}");
                }
            }

            public int GetHashCode(PdfObject obj)
            {
                object? value = obj.GetValue();

                return value is null ? 0 : value.GetHashCode();
            }
        }

        [Fact]
        public void TestBooleanTrue()
        {
            List<Token> tokens = new List<Token>
            {
                new BooleanToken(true, 1),
                new NullToken(2),
            };

            PdfObject actual = ParseTokens(tokens);

            PdfObject expected = PdfScalarValue.FromToken(new BooleanToken(true, 1));

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }

        [Fact]
        public void TestBooleanFalse()
        {
            List<Token> tokens = new List<Token>
            {
                new BooleanToken(false, 1),
                new NullToken(2),
            };

            PdfObject actual = ParseTokens(tokens);

            PdfObject expected = PdfScalarValue.FromToken(new BooleanToken(false, 1));

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }

        [Fact]
        public void TestNull()
        {
            List<Token> tokens = new List<Token>
            {
                new NullToken(1),
                new BooleanToken(true, 2),
            };

            PdfObject actual = ParseTokens(tokens);

            PdfObject expected = PdfScalarValue.FromToken(new NullToken(1));

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }

        [Fact]
        public void TestCommentsIgnored()
        {
            List<Token> tokens = new List<Token>
            {
                new CommentToken(new byte[] { 1, 2, 3 }, 1),
                new CommentToken(new byte[] { 1, 2, 3 }, 2),
                new CommentToken(new byte[] { 1, 2, 3 }, 3),
                new CommentToken(new byte[] { 1, 2, 3 }, 4),
                new BooleanToken(true, 5),
                new CommentToken(new byte[] { 1, 2, 3 }, 6),
            };

            PdfObject actual = ParseTokens(tokens);

            PdfObject expected = PdfScalarValue.FromToken(new BooleanToken(true, 5));

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }

        [Fact]
        public void TestNumericIntegers()
        {
            List<Token> tokens = new List<Token>
            {
                new NumericIntegerToken(123, new byte[] { (byte)'1', (byte)'2', (byte)'3' }, 1),
                new BooleanToken(true, 2),
            };

            PdfObject actual = ParseTokens(tokens);

            PdfObject expected = PdfScalarValue.FromToken(new NumericIntegerToken(123, new byte[] { (byte)'1', (byte)'2', (byte)'3' }, 1));

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }

        [Fact]
        public void TestNumericReals()
        {
            List<Token> tokens = new List<Token>
            {
                new NumericRealToken(3.14m, new byte[] { (byte)'3', (byte)'.', (byte)'1', (byte)'4' }, 1),
                new BooleanToken(true, 2),
            };

            PdfObject actual = ParseTokens(tokens);

            PdfObject expected = PdfScalarValue.FromToken(new NumericRealToken(3.14m, new byte[] { (byte)'3', (byte)'.', (byte)'1', (byte)'4' }, 1));

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }

        [Fact]
        public void TestStrings()
        {
            List<Token> tokens = new List<Token>
            {
                new StringToken(new byte[] { (byte)'a', (byte)'B', (byte)'c', (byte)'D' }, 1),
                new BooleanToken(true, 2),
            };

            PdfObject actual = ParseTokens(tokens);

            PdfObject expected = PdfScalarValue.FromToken(new StringToken(new byte[] { (byte)'a', (byte)'B', (byte)'c', (byte)'D' }, 1));

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }

        [Fact]
        public void TestNames()
        {
            List<Token> tokens = new List<Token>
            {
                new NameToken(new byte[] { (byte)'a', (byte)'B', (byte)'c', (byte)'D' }, 1),
                new BooleanToken(true, 2),
            };

            PdfObject actual = ParseTokens(tokens);

            PdfObject expected = PdfScalarValue.FromToken(new NameToken(new byte[] { (byte)'a', (byte)'B', (byte)'c', (byte)'D' }, 1));

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }

        [Fact]
        public void TestIndirectReference()
        {
            List<Token> tokens = new List<Token>
            {
                new NumericIntegerToken(123, new byte[] { (byte)'1', (byte)'2', (byte)'3' }, 1),
                new NumericIntegerToken(456, new byte[] { (byte)'4', (byte)'5', (byte)'6' }, 2),
                new IndirectReferenceMarkerToken(3),
                new BooleanToken(true, 4),
            };

            PdfObject actual = ParseTokens(tokens);

            PdfObject expected = PdfScalarValue.FromIndirectReference(123, 456);

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }

        [Fact]
        public void TestArrays()
        {
            List<Token> tokens = new List<Token>
            {
                new ArrayBeginToken(1),
                new NumericIntegerToken(123, new byte[] { (byte)'1', (byte)'2', (byte)'3' }, 2),
                new NumericIntegerToken(456, new byte[] { (byte)'4', (byte)'5', (byte)'6' }, 3),
                new IndirectReferenceMarkerToken(4),
                new StringToken(new byte[] { (byte)'a', (byte)'B', (byte)'c', (byte)'D' }, 5),
                new BooleanToken(true, 6),
                new ArrayEndToken(7),
            };

            PdfObject actual = ParseTokens(tokens);

            PdfObject expected = new PdfArray(new List<PdfObject>
            {
                PdfScalarValue.FromIndirectReference(123, 456),
                PdfScalarValue.FromToken(new StringToken(new byte[] { (byte)'a', (byte)'B', (byte)'c', (byte)'D' }, 5)),
                PdfScalarValue.FromToken(new BooleanToken(true, 6))
            });

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }

        [Fact]
        public void TestDictionaries()
        {
            List<Token> tokens = new List<Token>
            {
                new DictionaryBeginToken(1),
                new NameToken(new byte[] { (byte)'K', (byte)'e', (byte)'y', (byte)'1' }, 2),
                new NumericIntegerToken(123, new byte[] { (byte)'1', (byte)'2', (byte)'3' }, 3),
                new NameToken(new byte[] { (byte)'K', (byte)'e', (byte)'y', (byte)'2' }, 4),
                new BooleanToken(true, 5),
                new DictionaryEndToken(6),
            };

            PdfObject actual = ParseTokens(tokens);

            PdfObject expected = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                { "Key1", PdfScalarValue.FromToken(new NumericIntegerToken(123, new byte[] { (byte)'1', (byte)'2', (byte)'3' }, 3)) },
                { "Key2", PdfScalarValue.FromToken(new BooleanToken(true, 5)) },
            });

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }

        [Fact]
        public void TestNested()
        {
            List<Token> tokens = new List<Token>
            {
                new DictionaryBeginToken(1),
                new NameToken(new byte[] { (byte)'K', (byte)'e', (byte)'y', (byte)'1' }, 2),
                new NumericIntegerToken(123, new byte[] { (byte)'1', (byte)'2', (byte)'3' }, 3),
                new NameToken(new byte[] { (byte)'K', (byte)'e', (byte)'y', (byte)'2' }, 4),
                new BooleanToken(true, 5),
                new NameToken(new byte[] { (byte)'K', (byte)'e', (byte)'y', (byte)'3' }, 6),

                new ArrayBeginToken(10),
                new NumericIntegerToken(123, new byte[] { (byte)'1', (byte)'2', (byte)'3' }, 11),
                new NumericIntegerToken(456, new byte[] { (byte)'4', (byte)'5', (byte)'6' }, 12),
                new IndirectReferenceMarkerToken(13),
                new StringToken(new byte[] { (byte)'a', (byte)'B', (byte)'c', (byte)'D' }, 14),
                new BooleanToken(true, 15),
                new ArrayEndToken(16),

                new DictionaryEndToken(20),
            };

            PdfObject actual = ParseTokens(tokens);

            PdfObject expected = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                { "Key1", PdfScalarValue.FromToken(new NumericIntegerToken(123, new byte[] { (byte)'1', (byte)'2', (byte)'3' }, 3)) },
                { "Key2", PdfScalarValue.FromToken(new BooleanToken(true, 5)) },
                { "Key3", new PdfArray(new List<PdfObject>
                {
                    PdfScalarValue.FromIndirectReference(123, 456),
                    PdfScalarValue.FromToken(new StringToken(new byte[] { (byte)'a', (byte)'B', (byte)'c', (byte)'D' }, 5)),
                    PdfScalarValue.FromToken(new BooleanToken(true, 6)),
                }) }
            });

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }







    }
}
