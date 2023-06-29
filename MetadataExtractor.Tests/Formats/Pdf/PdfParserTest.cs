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

        private class PdfObjectEqualityComparer : IEqualityComparer<PdfObject?>
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

                if (object1 is PdfNull && object2 is PdfNull)
                {
                    return true;
                }
                else if (object1 is PdfBoolean boolean1 && object2 is PdfBoolean boolean2)
                {
                    return boolean1.Value == boolean2.Value;
                }
                else if (object1 is PdfNumericInteger numericInteger1 && object2 is PdfNumericInteger numericInteger2)
                {
                    return numericInteger1.Value == numericInteger2.Value;
                }
                else if (object1 is PdfNumericReal numericReal1 && object2 is PdfNumericReal numericReal2)
                {
                    return numericReal1.Value == numericReal2.Value;
                }
                else if (object1 is PdfString string1 && object2 is PdfString string2) // PdfName is subtype of PdfString
                {
                    return string1.Value.Bytes.EqualTo(string2.Value.Bytes);
                }
                else if (object1 is PdfIndirectReference indirectRef1 && object2 is PdfIndirectReference indirectRef2)
                {
                    return indirectRef1.Value.ObjectNumber == indirectRef2.Value.ObjectNumber
                        && indirectRef1.Value.GenerationNumber == indirectRef2.Value.GenerationNumber;
                }
                else if (object1 is PdfArray array1 && object2 is PdfArray array2)
                {
                    return array1.Value.SequenceEqual(array2.Value, this);
                }
                else if (object1 is PdfDictionary dictionary1 && object2 is PdfDictionary dictionary2)
                {
                    return dictionary1.Value.Keys.Count == dictionary2.Value.Keys.Count
                        && dictionary1.Value.All(item => dictionary2.Value.ContainsKey(item.Key) && Equals(item.Value, dictionary2.Value[item.Key]));
                }
                else if (object1 is PdfIndirectObject indirect1 && object2 is PdfIndirectObject indirect2)
                {
                    return indirect1.Identifier.ObjectNumber == indirect2.Identifier.ObjectNumber
                        && indirect1.Identifier.GenerationNumber == indirect2.Identifier.GenerationNumber
                        && Equals(indirect1.Value, indirect2.Value);
                }
                else if (object1 is PdfStream stream1 && object2 is PdfStream stream2)
                {
                    return stream1.Identifier.ObjectNumber == stream2.Identifier.ObjectNumber
                        && stream1.Identifier.GenerationNumber == stream2.Identifier.GenerationNumber
                        && stream1.StreamStartIndex == stream2.StreamStartIndex
                        && Equals(stream1.StreamDictionary, stream2.StreamDictionary);
                }
                else
                {
                    return false;
                }
            }

            public int GetHashCode(PdfObject obj)
            {
                return obj.GetHashCode();
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

            PdfObject expected = new PdfBoolean(true);

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

            PdfObject expected = new PdfBoolean(false);

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

            PdfObject expected = new PdfNull();

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

            PdfObject expected = new PdfBoolean(true);

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

            PdfObject expected = new PdfNumericInteger(123);

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

            PdfObject expected = new PdfNumericReal(3.14m);

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

            PdfObject expected = CreatePdfString("aBcD");

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

            PdfObject expected = CreatePdfName("aBcD");

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

            PdfObject expected = new PdfIndirectReference(123, 456);

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
                new PdfIndirectReference(123, 456),
                CreatePdfString("aBcD"),
                new PdfBoolean(true),
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
                { "Key1", new PdfNumericInteger(123) },
                { "Key2", new PdfBoolean(true) },
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
                { "Key1", new PdfNumericInteger(123) },
                { "Key2", new PdfBoolean(true) },
                { "Key3", new PdfArray(new List<PdfObject>
                {
                    new PdfIndirectReference(123, 456),
                    CreatePdfString("aBcD"),
                    new PdfBoolean(true),
                }) }
            });

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }

        [Fact]
        public void TestIndirectObjects()
        {
            List<Token> tokens = new List<Token>
            {
                new NumericIntegerToken(123, new byte[] { (byte)'1', (byte)'2', (byte)'3' }, 1),
                new NumericIntegerToken(456, new byte[] { (byte)'4', (byte)'5', (byte)'6' }, 2),
                new IndirectObjectBeginToken(3),

                new DictionaryBeginToken(10),
                new NameToken(new byte[] { (byte)'K', (byte)'e', (byte)'y', (byte)'1' }, 11),
                new NumericIntegerToken(789, new byte[] { (byte)'7', (byte)'8', (byte)'9' }, 12),
                new NameToken(new byte[] { (byte)'K', (byte)'e', (byte)'y', (byte)'2' }, 13),
                new BooleanToken(true, 14),
                new DictionaryEndToken(15),

                new IndirectObjectEndToken(20),
            };

            PdfObject actual = ParseTokens(tokens);

            PdfIndirectObject expected = new PdfIndirectObject(123, 456, new PdfDictionary(new Dictionary<string, PdfObject>
            {
                { "Key1", new PdfNumericInteger(789) },
                { "Key2", new PdfBoolean(true) },
            }));

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }

        [Fact]
        public void TestStreams()
        {
            List<Token> tokens = new List<Token>
            {
                new NumericIntegerToken(123, new byte[] { (byte)'1', (byte)'2', (byte)'3' }, 1),
                new NumericIntegerToken(456, new byte[] { (byte)'4', (byte)'5', (byte)'6' }, 2),
                new IndirectObjectBeginToken(3),

                new DictionaryBeginToken(10),
                new NameToken(new byte[] { (byte)'L', (byte)'e', (byte)'n', (byte)'g', (byte)'t', (byte)'h' }, 11),
                new NumericIntegerToken(7, new byte[] { (byte)'7' }, 12),
                new NameToken(new byte[] { (byte)'D', (byte)'L' }, 13),
                new NumericIntegerToken(17, new byte[] { (byte)'1', (byte)'7' }, 14),
                new DictionaryEndToken(15),

                new StreamBeginToken(startIndex: 20, streamStartIndex: 22),

                new IndirectObjectEndToken(30),
            };

            PdfObject actual = ParseTokens(tokens);

            PdfIndirectObject expected = new PdfIndirectObject(123, 456);

            PdfDictionary streamDictionary = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                { "Length",new PdfNumericInteger(7) },
                { "DL", new PdfNumericInteger(17) },
            });

            PdfStream pdfStream = new PdfStream(expected.Identifier, streamDictionary, streamStartIndex: 22);

            expected.Add(pdfStream);

            Assert.Equal(expected, actual, new PdfObjectEqualityComparer());
        }

        [Fact]
        public void TestMissingStreamDictionary()
        {
            List<Token> tokens = new List<Token>
            {
                new NumericIntegerToken(123, new byte[] { (byte)'1', (byte)'2', (byte)'3' }, 1),
                new NumericIntegerToken(456, new byte[] { (byte)'4', (byte)'5', (byte)'6' }, 2),
                new IndirectObjectBeginToken(3),

                new StreamBeginToken(startIndex: 20, streamStartIndex: 22),

                new IndirectObjectEndToken(30),
            };

            Assert.ThrowsAny<Exception>(() =>
            {
                _ = ParseTokens(tokens);
            });
        }

        [Fact]
        public void TestArrayInsteadOfStreamDictionary()
        {
            // Length key is missing from stream dictionary

            List<Token> tokens = new List<Token>
            {
                new NumericIntegerToken(123, new byte[] { (byte)'1', (byte)'2', (byte)'3' }, 1),
                new NumericIntegerToken(456, new byte[] { (byte)'4', (byte)'5', (byte)'6' }, 2),
                new IndirectObjectBeginToken(3),

                new ArrayBeginToken(10),
                new NameToken(new byte[] { (byte)'D', (byte)'L' }, 11),
                new NumericIntegerToken(17, new byte[] { (byte)'1', (byte)'7' }, 12),
                new ArrayEndToken(13),

                new StreamBeginToken(startIndex: 20, streamStartIndex: 22),

                new IndirectObjectEndToken(30),
            };

            Assert.ThrowsAny<Exception>(() =>
            {
                _ = ParseTokens(tokens);
            });
        }

        [Fact]
        public void TestEquality()
        {
            PdfObjectEqualityComparer comparer = new PdfObjectEqualityComparer();

            PdfBoolean boolean = new PdfBoolean(true);

            Assert.Equal(boolean, boolean, comparer);

#pragma warning disable xUnit2003 // Do not use equality check to test for null value
#pragma warning disable xUnit2000 // Constants and literals should be the expected argument
            Assert.NotEqual(null, boolean, comparer);
            Assert.NotEqual(boolean, null, comparer);
#pragma warning restore xUnit2000 // Constants and literals should be the expected argument
#pragma warning restore xUnit2003 // Do not use equality check to test for null value

            // PdfNull
            Assert.Equal(new PdfNull(), new PdfNull(), comparer);
            Assert.NotEqual(new PdfNull(), new PdfBoolean(true), comparer);

            // PdfBoolean
            Assert.Equal(new PdfBoolean(true), new PdfBoolean(true), comparer);
            Assert.NotEqual(new PdfBoolean(true), new PdfBoolean(false), comparer);

            // PdfNumericInteger
            Assert.Equal(new PdfNumericInteger(123), new PdfNumericInteger(123), comparer);
            Assert.NotEqual(new PdfNumericInteger(123), new PdfNumericInteger(234), comparer);

            // PdfNumericReal
            Assert.Equal(new PdfNumericReal(3.14m), new PdfNumericReal(3.14m), comparer);
            Assert.NotEqual(new PdfNumericReal(3.14m), new PdfNumericReal(3.1m), comparer);

            // PdfString
            Assert.Equal(CreatePdfString("abc"), CreatePdfString("abc"), comparer);
            Assert.NotEqual(CreatePdfString("abc"), CreatePdfString("aBc"), comparer);

            // PdfIndirectReference
            Assert.Equal(new PdfIndirectReference(123, 456), new PdfIndirectReference(123, 456), comparer);
            Assert.NotEqual(new PdfIndirectReference(123, 456), new PdfIndirectReference(122, 456), comparer);
            Assert.NotEqual(new PdfIndirectReference(123, 456), new PdfIndirectReference(123, 455), comparer);

            // PdfArray
            Assert.Equal(
                new PdfArray(new List<PdfObject> { new PdfNull(), new PdfBoolean(true), new PdfBoolean(false) }),
                new PdfArray(new List<PdfObject> { new PdfNull(), new PdfBoolean(true), new PdfBoolean(false) }),
                comparer
            );
            Assert.NotEqual(
               new PdfArray(new List<PdfObject> { new PdfNull(), new PdfBoolean(true), new PdfBoolean(false) }),
               new PdfArray(new List<PdfObject> { new PdfNull(), new PdfBoolean(true), new PdfBoolean(false), new PdfNull() }),
               comparer
            );
            Assert.NotEqual(
               new PdfArray(new List<PdfObject> { new PdfNull(), new PdfBoolean(true), new PdfBoolean(false) }),
               new PdfArray(new List<PdfObject> { new PdfNull(), new PdfBoolean(true), new PdfBoolean(true) }),
               comparer
            );

            // PdfDictionary
            Assert.Equal(
                new PdfDictionary(new Dictionary<string, PdfObject> { { "a", new PdfNull() }, { "b", new PdfBoolean(true) }, { "c", new PdfBoolean(false) } }),
                new PdfDictionary(new Dictionary<string, PdfObject> { { "a", new PdfNull() }, { "b", new PdfBoolean(true) }, { "c", new PdfBoolean(false) } }),
                comparer
            );
            Assert.NotEqual(
                new PdfDictionary(new Dictionary<string, PdfObject> { { "a", new PdfNull() }, { "b", new PdfBoolean(true) }, { "c", new PdfBoolean(false) } }),
                new PdfDictionary(new Dictionary<string, PdfObject> { { "a", new PdfNull() }, { "b", new PdfBoolean(true) } }),
                comparer
            );
            Assert.NotEqual(
                new PdfDictionary(new Dictionary<string, PdfObject> { { "a", new PdfNull() }, { "b", new PdfBoolean(true) }, { "c", new PdfBoolean(false) } }),
                new PdfDictionary(new Dictionary<string, PdfObject> { { "a", new PdfNull() }, { "b", new PdfBoolean(true) }, { "d", new PdfBoolean(false) } }),
                comparer
            );
            Assert.NotEqual(
                new PdfDictionary(new Dictionary<string, PdfObject> { { "a", new PdfNull() }, { "b", new PdfBoolean(true) }, { "c", new PdfBoolean(false) } }),
                new PdfDictionary(new Dictionary<string, PdfObject> { { "a", new PdfNull() }, { "b", new PdfBoolean(true) }, { "c", new PdfBoolean(true) } }),
                comparer
            );

            // PdfIndirectObject
            Assert.Equal(new PdfIndirectObject(123, 456, new PdfBoolean(true)), new PdfIndirectObject(123, 456, new PdfBoolean(true)), comparer);
            Assert.NotEqual(new PdfIndirectObject(123, 456, new PdfBoolean(true)), new PdfIndirectObject(122, 456, new PdfBoolean(true)), comparer);
            Assert.NotEqual(new PdfIndirectObject(123, 456, new PdfBoolean(true)), new PdfIndirectObject(123, 455, new PdfBoolean(true)), comparer);
            Assert.NotEqual(new PdfIndirectObject(123, 456, new PdfBoolean(true)), new PdfIndirectObject(123, 456, new PdfBoolean(false)), comparer);

            // PdfStream
            PdfDictionary dictionary1 = new PdfDictionary(new Dictionary<string, PdfObject> { { "a", new PdfNull() }, { "b", new PdfBoolean(true) }, { "c", new PdfBoolean(false) } });
            PdfStream stream1 = new PdfStream(123, 456, dictionary1, 789);

            PdfDictionary dictionary2 = new PdfDictionary(new Dictionary<string, PdfObject> { { "a", new PdfNull() }, { "b", new PdfBoolean(true) }, { "c", new PdfBoolean(false) } });
            PdfDictionary dictionary3 = new PdfDictionary(new Dictionary<string, PdfObject> { { "a", new PdfNull() }, { "b", new PdfBoolean(true) }, { "d", new PdfBoolean(false) } });

            Assert.Equal(stream1, new PdfStream(123, 456, dictionary2, 789), comparer);
            Assert.NotEqual(stream1, new PdfStream(122, 456, dictionary2, 789), comparer);
            Assert.NotEqual(stream1, new PdfStream(123, 455, dictionary2, 789), comparer);
            Assert.NotEqual(stream1, new PdfStream(123, 456, dictionary2, 788), comparer);
            Assert.NotEqual(stream1, new PdfStream(123, 456, dictionary3, 789), comparer);
        }

        private static PdfString CreatePdfString(string value)
        {
            // NOTE: value should only contain 1-byte characters
            return new PdfString(new StringValue(value.ToCharArray().Select(c => (byte)c).ToArray()));
        }

        private static PdfName CreatePdfName(string value)
        {
            // NOTE: value should only contain 1-byte characters
            return new PdfName(new StringValue(value.ToCharArray().Select(c => (byte)c).ToArray()));
        }
    }
}
