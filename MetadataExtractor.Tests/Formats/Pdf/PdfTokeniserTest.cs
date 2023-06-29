// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using MetadataExtractor.Formats.Pdf;

namespace MetadataExtractor.Tests.Formats.Pdf
{
    /// <summary>Unit tests for <see cref="PdfTokeniser"/>.</summary>
    /// <author>Drew Noakes https://drewnoakes.com</author>
    public sealed class PdfTokeniserTest
    {
        private static PdfTokeniser GetTokeniserForInput(string input)
        {
            StringByteProviderSource byteSource = new StringByteProviderSource(input, 0, ExtractionDirection.Forward);

            BufferedItemProvider<byte> byteProvider = new BufferedItemProvider<byte>(byteSource, 16);

            PdfTokeniser tokeniser = new PdfTokeniser(byteProvider);

            return tokeniser;
        }

        private class TokenEqualityComparer : IEqualityComparer<Token?>
        {
            public bool Equals(Token? token1, Token? token2)
            {
                if (ReferenceEquals(token1, token2))
                {
                    return true;
                }

                if (token1 is null || token2 is null)
                {
                    return false;
                }

                if (token1 is HeaderCommentToken commentToken1 && token2 is HeaderCommentToken commentToken2)
                {
                    return commentToken1.Type == commentToken2.Type
                        && commentToken1.Version == commentToken2.Version
                        && commentToken1.DecimalVersion == commentToken2.DecimalVersion
                        && commentToken1.StartIndex == commentToken2.StartIndex
                        && commentToken1.Value.EqualTo(commentToken2.Value);
                }
                else if (token1 is StreamBeginToken streamToken1 && token2 is StreamBeginToken streamToken2)
                {
                    return streamToken1.Type == streamToken2.Type
                        && streamToken1.StreamStartIndex == streamToken2.StreamStartIndex
                        && streamToken1.StartIndex == streamToken2.StartIndex
                        && streamToken1.Value.EqualTo(streamToken2.Value);
                }
                else if (token1.Type == token2.Type)
                {
                    return token1.StartIndex == token2.StartIndex
                       && token1.Value.EqualTo(token2.Value);
                }
                else
                {
                    return false;
                }
            }

            public int GetHashCode(Token token) => token.Value.GetHashCode();
        }

        [Theory]
        [InlineData("(This is a string)", "This is a string", 0)]
        [InlineData("(Whitespace   is preserved)", "Whitespace   is preserved", 0)]
        [InlineData("(Strings may contain newlines\r\nand such.)", "Strings may contain newlines\nand such.", 0)]
        [InlineData("(Strings may contain newlines\rand such.)", "Strings may contain newlines\nand such.", 0)]
        [InlineData("(Strings may contain newlines\nand such.)", "Strings may contain newlines\nand such.", 0)]
        [InlineData("(Strings may contain newlines\r\n\r\nand such.)", "Strings may contain newlines\n\nand such.", 0)]
        [InlineData("(Strings may contain newlines\r\rand such.)", "Strings may contain newlines\n\nand such.", 0)]
        [InlineData("(Strings may contain newlines\n\nand such.)", "Strings may contain newlines\n\nand such.", 0)]
        [InlineData(
            "(Strings may contain balanced parentheses ( ) and special characters (*!&}^% and so on).)",
            "Strings may contain balanced parentheses ( ) and special characters (*!&}^% and so on).",
            0
        )]
        [InlineData("()", "", 0)]
        [InlineData("(It has zero (0) length.)", "It has zero (0) length.", 0)]
        [InlineData(@"( \n \r \t \b \f \( \) \\ \123 \x )", " \u000A \u000D \u0009 \u0008 \u000C \u0028 \u0029 \u005C \u0053 x ", 0)]
        [InlineData("(These \\\r\ntwo strings \\\rare the same\\\n.)", "These two strings are the same.", 0)]
        [InlineData("(This string has an end-of-line at the end of it.\n)", "This string has an end-of-line at the end of it.\n", 0)]
        [InlineData(@"(This string contains \245two octal characters\307.)", "This string contains \u00A5two octal characters\u00C7.", 0)]
        [InlineData(@"(High-order overflow (\765) is ignored.)", "High-order overflow (\u00F5) is ignored.", 0)]
        [InlineData(
            @"(The literal (\0053) denotes a string containing two characters, \005 (Control-E) followed by the digit 3)",
            "The literal (\u00053) denotes a string containing two characters, \u0005 (Control-E) followed by the digit 3",
            0
        )]
        [InlineData(
            @"(Both (\053) and (\53) denote strings containing the single character \053, a plus sign (+).)",
            "Both (\u002B) and (\u002B) denote strings containing the single character \u002B, a plus sign (+).",
            0
        )]
        [InlineData(" (string)", "string", 1)]
        [InlineData(" \n(string)", "string", 2)]
        [InlineData(" \r\n(string)", "string", 3)]
        [InlineData(" \r\n (string)", "string", 4)]
        public void TestLiteralStrings(string actual, string expected, int startIndex)
        {
            Token[] tokens = GetTokeniserForInput(actual).Tokenise().ToArray();

            Assert.Single(tokens);

            Token actualToken = tokens.First();
            Token expectedToken = CreateStringToken(expected, startIndex);

            Assert.Equal(expectedToken, actualToken, new TokenEqualityComparer());
        }

        [Fact]
        public void TestUnterminatedLiteralStrings()
        {
            Assert.ThrowsAny<Exception>(() =>
            {
                _ = GetTokeniserForInput(" (abcd").Tokenise().ToArray();
            });
        }

        [Theory]
        [InlineData("<4E6F762073686D6F7A>", "\u004E\u006F\u0076\u0020\u0073\u0068\u006D\u006F\u007A", 0)]
        [InlineData("<57 68 69 74 65 73 70 61 63 65 20 69 73 20 69 67 6E 6F 72 65 64>", "Whitespace is ignored", 0)]
        [InlineData("<901FA3>", "\u0090\u001F\u00A3", 0)]
        [InlineData("<901FA>", "\u0090\u001F\u00A0", 0)] // odd number of hex chars appends zero
        [InlineData(" <0000>", "\0\0", 1)]
        [InlineData(" \n<0000>", "\0\0", 2)]
        [InlineData(" \r\n<0000>", "\0\0", 3)]
        [InlineData(" \r\n <0000>", "\0\0", 4)]
        public void TestHexadecimalStrings(string actual, string expected, int startIndex)
        {
            Token[] tokens = GetTokeniserForInput(actual).Tokenise().ToArray();

            Assert.Single(tokens);

            Token actualToken = tokens.First();
            Token expectedToken = CreateStringToken(expected, startIndex);

            Assert.Equal(expectedToken, actualToken, new TokenEqualityComparer());
        }

        [Fact]
        public void TestUnterminatedHexadecimalStrings()
        {
            Assert.ThrowsAny<Exception>(() =>
            {
                _ = GetTokeniserForInput(" <abcd").Tokenise().ToArray();
            });
        }

        [Theory]
        [InlineData("/Name1", "Name1", 0)]
        [InlineData("/ASomewhatLongerName", "ASomewhatLongerName", 0)]
        [InlineData("/A;Name_With-Various***Characters?", "A;Name_With-Various***Characters?", 0)]
        [InlineData("/1.2", "1.2", 0)]
        [InlineData("/$$", "$$", 0)]
        [InlineData("/@pattern", "@pattern", 0)]
        [InlineData("/.notdef", ".notdef", 0)]
        [InlineData("/Lime#20Green", "Lime Green", 0)]
        [InlineData("/paired#28#29parentheses", "paired()parentheses", 0)]
        [InlineData("/The_Key_of_F#23_Minor", "The_Key_of_F#_Minor", 0)]
        [InlineData("/A#42", "AB", 0)]
        [InlineData("/", "", 0)]
        [InlineData(" /Name", "Name", 1)]
        [InlineData(" \n/Name", "Name", 2)]
        [InlineData(" \r\n/Name", "Name", 3)]
        [InlineData(" \r\n /Name", "Name", 4)]
        public void TestNames(string actual, string expected, int startIndex)
        {
            Token[] tokens = GetTokeniserForInput(actual).Tokenise().ToArray();

            Assert.Single(tokens);

            Token actualToken = tokens.First();
            Token expectedToken = CreateNameToken(expected, startIndex);

            Assert.Equal(expectedToken, actualToken, new TokenEqualityComparer());
        }

        [Theory]
        [InlineData("%comment", "comment", 0)]
        [InlineData("% comment", " comment", 0)]
        [InlineData(" %comment\r\n", "comment", 1)]
        [InlineData(" %comment\r", "comment", 1)]
        [InlineData(" %comment\n", "comment", 1)]
        [InlineData(" \n%comment\n", "comment", 2)]
        [InlineData(" \r\n%comment\n", "comment", 3)]
        [InlineData(" \r\n %comment\n", "comment", 4)]
        public void TestComments(string actual, string expected, int startIndex)
        {
            Token[] tokens = GetTokeniserForInput(actual).Tokenise().ToArray();

            Assert.Single(tokens);

            Token actualToken = tokens.First();
            Token expectedToken = CreateCommentToken(expected, startIndex);

            Assert.Equal(expectedToken, actualToken, new TokenEqualityComparer());
        }

        [Theory]
        [InlineData("%PDF-1.0", "PDF-1.0", "1.0", 0)]
        [InlineData("%PDF-1.1 ", "PDF-1.1 ", "1.1", 0)]
        [InlineData("%PDF-1.2\r\n", "PDF-1.2", "1.2", 0)]
        [InlineData("%PDF-1.3\r", "PDF-1.3", "1.3", 0)]
        [InlineData("%PDF-1.4\n", "PDF-1.4", "1.4", 0)]
        [InlineData("%PDF-1.5 \n", "PDF-1.5 ", "1.5", 0)]
        [InlineData("%PDF-1.6", "PDF-1.6", "1.6", 0)]
        [InlineData("%PDF-1.7", "PDF-1.7", "1.7", 0)]
        [InlineData("%PDF-2.0", "PDF-2.0", "2.0", 0)]
        public void TestHeaderComments(string actual, string expectedValue, string expectedVersion, int startIndex)
        {
            Token[] tokens = GetTokeniserForInput(actual).Tokenise().ToArray();

            Assert.Single(tokens);

            Token actualToken = tokens.First();
            Token expectedToken = CreateHeaderCommentToken(expectedValue, expectedVersion, startIndex);

            Assert.Equal(expectedToken, actualToken, new TokenEqualityComparer());
        }

        [Fact]
        public void TestInterspersedComments()
        {
            Token[] actual = GetTokeniserForInput("(abc)% comment ( /%) blah blah blah\r\n123").Tokenise().ToArray();
            //                                     012345678901234567890123456789012345 6 789
            //                                     0         10        20        30

            Token[] expected = new Token[]
            {
                CreateStringToken("abc", 0),
                CreateCommentToken(" comment ( /%) blah blah blah", 5),
                CreateNumericIntegerToken(123, 37),
            };

            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.Equal(expected[i], actual[i], new TokenEqualityComparer());
            }
        }

        [Fact]
        public void TestBinaryIndicatorComment()
        {
            string input = " %\u00E2\u00E3\u00CF\u00D3\r\n";

            Token[] tokens = GetTokeniserForInput(input).Tokenise().ToArray();

            Assert.Single(tokens);

            Token expected = new BinaryIndicatorCommentToken(new byte[] { 0xE2, 0xE3, 0xCF, 0xD3 }, 1);

            Assert.Equal(expected, tokens.First(), new TokenEqualityComparer());
        }

        [Fact]
        public void TestNull()
        {
            string input = " null";

            Token[] tokens = GetTokeniserForInput(input).Tokenise().ToArray();

            Assert.Single(tokens);

            Token expected = new NullToken(1);

            Assert.Equal(expected, tokens.First(), new TokenEqualityComparer());
        }

        [Fact]
        public void TestBooleanTrue()
        {
            string input = "  true";

            Token[] tokens = GetTokeniserForInput(input).Tokenise().ToArray();

            Assert.Single(tokens);

            Token expected = new BooleanToken(true, 2);

            Assert.Equal(expected, tokens.First(), new TokenEqualityComparer());
        }

        [Fact]
        public void TestBooleanFalse()
        {
            string input = " \r\n false";

            Token[] tokens = GetTokeniserForInput(input).Tokenise().ToArray();

            Assert.Single(tokens);

            Token expected = new BooleanToken(false, 4);

            Assert.Equal(expected, tokens.First(), new TokenEqualityComparer());
        }

        [Fact]
        public void TestNumericIntegers()
        {
            string input = " 123 43445 +17 -98 0";
            //              01234567890123456789

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Token[] expected = new Token[]
            {
                new NumericIntegerToken(123, new byte[] { (byte)'1', (byte)'2', (byte)'3' }, 1),
                new NumericIntegerToken(43445, new byte[] { (byte)'4', (byte)'3', (byte)'4', (byte)'4', (byte)'5' }, 5),
                new NumericIntegerToken(17, new byte[] { (byte)'+', (byte)'1', (byte)'7' }, 11),
                new NumericIntegerToken(-98, new byte[] { (byte)'-', (byte)'9', (byte)'8' }, 15),
                new NumericIntegerToken(0, new byte[] { (byte)'0' }, 19),
            };

            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.Equal(expected[i], actual[i], new TokenEqualityComparer());
            }
        }

        [Fact]
        public void TestNumericReals()
        {
            string input = " 34.5 -3.62 +123.6 4. -.002 0.0";
            //              01234567890123456789012345678

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Token[] expected = new Token[]
             {
                new NumericRealToken(34.5m, new byte[] { (byte)'3', (byte)'4', (byte)'.', (byte)'5' }, 1),
                new NumericRealToken(-3.62m, new byte[] { (byte)'-', (byte)'3', (byte)'.', (byte)'6', (byte)'2' }, 6),
                new NumericRealToken(123.6m, new byte[] { (byte)'+', (byte)'1', (byte)'2', (byte)'3', (byte)'.', (byte)'6' }, 12),
                new NumericRealToken(4.0m, new byte[] { (byte)'4', (byte)'.' }, 19),
                new NumericRealToken(-0.002m, new byte[] { (byte)'-', (byte)'.', (byte)'0', (byte)'0', (byte)'2' }, 22),
                new NumericRealToken(0.0m, new byte[] { (byte)'0', (byte)'.', (byte)'0' }, 28),
             };

            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.Equal(expected[i], actual[i], new TokenEqualityComparer());
            }
        }

        [Fact]
        public void TestTokenDelimiters()
        {
            string input = " <</Key1 [1 2]/Key2 1 2 R/Key3 <abcd>>>";
            //              01234567890123456789012345678901234567
            //              0         10        20        30

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Token[] expected =
            {
                new DictionaryBeginToken(1),
                CreateNameToken("Key1", 3),
                new ArrayBeginToken(9),
                CreateNumericIntegerToken(1, 10),
                CreateNumericIntegerToken(2, 12),
                new ArrayEndToken(13),
                CreateNameToken("Key2", 14),
                CreateNumericIntegerToken(1, 20),
                CreateNumericIntegerToken(2, 22),
                new IndirectReferenceMarkerToken(24),
                CreateNameToken("Key3", 25),
                CreateStringToken("\u00AB\u00CD", 31),
                new DictionaryEndToken(37),
            };

            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.Equal(expected[i], actual[i], new TokenEqualityComparer());
            }
        }

        [Fact]
        public void TestBreakAtStreamMarkerWithCRLF()
        {
            // the "stream" keyword must be followed by CRLF or LF, but not by CR alone

            string input = " <</Length 42>>stream\r\n$$$$$";
            //              0123456789012345678901 2 3
            //              0         10        20

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Token[] expected =
            {
                new DictionaryBeginToken(1),
                CreateNameToken("Length", 3),
                CreateNumericIntegerToken(42, 11),
                new DictionaryEndToken(13),
                new StreamBeginToken(startIndex: 15, streamStartIndex: 23),
            };

            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.Equal(expected[i], actual[i], new TokenEqualityComparer());
            }
        }

        [Fact]
        public void TestBreakAtStreamMarkerWithLF()
        {
            // the "stream" keyword must be followed by CRLF or LF, but not by CR alone

            string input = " <</Length 42>>stream\n$$$$$";
            //              0123456789012345678901 2
            //              0         10        20

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Token[] expected =
            {
                new DictionaryBeginToken(1),
                CreateNameToken("Length", 3),
                CreateNumericIntegerToken(42, 11),
                new DictionaryEndToken(13),
                new StreamBeginToken(startIndex: 15, streamStartIndex: 22),
            };

            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.Equal(expected[i], actual[i], new TokenEqualityComparer());
            }
        }

        [Fact]
        public void TestBreakAtStreamMarkerWithCR()
        {
            // the "stream" keyword must be followed by CRLF or LF, but not by CR alone

            string input = " <</Length 42>>stream\r$$$$$";
            //              0123456789012345678901 2
            //              0         10        20

            Assert.ThrowsAny<Exception>(() =>
            {
                _ = GetTokeniserForInput(input).Tokenise().ToArray();
            });
        }

        [Fact]
        public void TestConsecutiveKeysNoSpacing()
        {
            string input = " <</Type/SomeName/SomeKey [1 2]/AnotherKey(some string)>>";
            //              01234567890123456789012345678901234567890123456789012345
            //              0         10        20        30        40        50

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Token[] expected =
            {
                new DictionaryBeginToken(1),
                CreateNameToken("Type", 3),
                CreateNameToken("SomeName", 8),
                CreateNameToken("SomeKey", 17),
                new ArrayBeginToken(26),
                CreateNumericIntegerToken(1, 27),
                CreateNumericIntegerToken(2, 29),
                new ArrayEndToken(30),
                CreateNameToken("AnotherKey", 31),
                CreateStringToken("some string", 42),
                new DictionaryEndToken(55),
            };

            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.Equal(expected[i], actual[i], new TokenEqualityComparer());
            }
        }

        [Fact]
        public void TestConsecutiveArrayValuesNoSpacing()
        {
            string input = " <</Names[(DC)37 0 R(DO)38 0 R]>>";
            //              01234567890123456789012345678901
            //              0         10        20        30

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Token[] expected =
            {
                new DictionaryBeginToken(1),
                CreateNameToken("Names", 3),
                new ArrayBeginToken(9),
                CreateStringToken("DC", 10),
                CreateNumericIntegerToken(37, 14),
                CreateNumericIntegerToken(0, 17),
                new IndirectReferenceMarkerToken(19),
                CreateStringToken("DO", 20),
                CreateNumericIntegerToken(38, 24),
                CreateNumericIntegerToken(0, 27),
                new IndirectReferenceMarkerToken(29),
                new ArrayEndToken(30),
                new DictionaryEndToken(31),
            };

            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.Equal(expected[i], actual[i], new TokenEqualityComparer());
            }
        }

        [Fact]
        public void TestDictionaryFromSampleFile()
        {
            string input = " 699 0 obj<</First 700 0 R/Count 13/Last 701 0 R>>endobj\r\n1129 0 obj\r\n<</Subtype/XML/Length 3649/Type/Metadata>>";
            //              012345678901234567890123456789012345678901234567890123456 7 89012345678 9 01234567890123456789012345678901234567890
            //              0         10        20        30        40        50          60          70        80        90        100       110

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Token[] expected =
            {
                CreateNumericIntegerToken(699, 1),
                CreateNumericIntegerToken(0, 5),
                new IndirectObjectBeginToken(7),
                new DictionaryBeginToken(10),
                CreateNameToken("First", 12),
                CreateNumericIntegerToken(700, 19),
                CreateNumericIntegerToken(0, 23),
                new IndirectReferenceMarkerToken(25),
                CreateNameToken("Count", 26),
                CreateNumericIntegerToken(13, 33),
                CreateNameToken("Last", 35),
                CreateNumericIntegerToken(701, 41),
                CreateNumericIntegerToken(0, 45),
                new IndirectReferenceMarkerToken(47),
                new DictionaryEndToken(48),
                new IndirectObjectEndToken(50),
                CreateNumericIntegerToken(1129, 58),
                CreateNumericIntegerToken(0, 63),
                new IndirectObjectBeginToken(65),
                new DictionaryBeginToken(70),
                CreateNameToken("Subtype", 72),
                CreateNameToken("XML", 80),
                CreateNameToken("Length", 84),
                CreateNumericIntegerToken(3649, 92),
                CreateNameToken("Type", 96),
                CreateNameToken("Metadata", 101),
                new DictionaryEndToken(110),
            };

            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.Equal(expected[i], actual[i], new TokenEqualityComparer());
            }
        }

        [Fact]
        public void TestDelimitersFromSampleFile()
        {
            string input = " <</Outlines 699 0 R/OCProperties<</D<</RBGroups[]/OFF[]/Order[[(35%_ATM_5-26-09.pdf)1134 0 R[1135 0 R]>>>>";
            //              0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345
            //              0         10        20        30        40        50        60        70        80        90        100

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Token[] expected =
            {
                new DictionaryBeginToken(1),
                CreateNameToken("Outlines", 3),
                CreateNumericIntegerToken(699, 13),
                CreateNumericIntegerToken(0, 17),
                new IndirectReferenceMarkerToken(19),
                CreateNameToken("OCProperties", 20),
                new DictionaryBeginToken(33),
                CreateNameToken("D", 35),
                new DictionaryBeginToken(37),
                CreateNameToken("RBGroups", 39),
                new ArrayBeginToken(48),
                new ArrayEndToken(49),
                CreateNameToken("OFF", 50),
                new ArrayBeginToken(54),
                new ArrayEndToken(55),
                CreateNameToken("Order", 56),
                new ArrayBeginToken(62),
                new ArrayBeginToken(63),
                CreateStringToken("35%_ATM_5-26-09.pdf", 64),
                CreateNumericIntegerToken(1134, 85),
                CreateNumericIntegerToken(0, 90),
                new IndirectReferenceMarkerToken(92),
                new ArrayBeginToken(93),
                CreateNumericIntegerToken(1135, 94),
                CreateNumericIntegerToken(0, 99),
                new IndirectReferenceMarkerToken(101),
                new ArrayEndToken(102),
                new DictionaryEndToken(103),
                new DictionaryEndToken(105),
            };

            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.Equal(expected[i], actual[i], new TokenEqualityComparer());
            }
        }

        [Fact]
        public void TestEquality()
        {
            TokenEqualityComparer comparer = new TokenEqualityComparer();

            Assert.NotEqual(CreateStringToken("abc", 0), CreateNameToken("abc", 0), comparer);

            // StringToken
            Assert.Equal(CreateStringToken("abc", 0), CreateStringToken("abc", 0), comparer);
            Assert.NotEqual(CreateStringToken("abc", 0), CreateStringToken("aBc", 0), comparer);
            Assert.NotEqual(CreateStringToken("abc", 0), CreateStringToken("abcd", 0), comparer);
            Assert.NotEqual(CreateStringToken("abc", 0), CreateStringToken("abc", 1), comparer);

            // HeaderCommentToken
            Assert.Equal(CreateHeaderCommentToken("abc", "1.0", 0), CreateHeaderCommentToken("abc", "1.0", 0), comparer);
            Assert.NotEqual(CreateHeaderCommentToken("abc", "1.0", 0), CreateHeaderCommentToken("aBc", "1.0", 0), comparer);
            Assert.NotEqual(CreateHeaderCommentToken("abc", "1.0", 0), CreateHeaderCommentToken("abcd", "1.0", 0), comparer);
            Assert.NotEqual(CreateHeaderCommentToken("abc", "1.0", 0), CreateHeaderCommentToken("abc", "1.1", 0), comparer);
            Assert.NotEqual(CreateHeaderCommentToken("abc", "1.0", 0), CreateHeaderCommentToken("abc", "1.0", 1), comparer);

            // StreamBeginToken
            Assert.Equal(new StreamBeginToken(1, 2), new StreamBeginToken(1, 2), comparer);
            Assert.NotEqual(new StreamBeginToken(1, 2), new StreamBeginToken(0, 2), comparer);
            Assert.NotEqual(new StreamBeginToken(1, 2), new StreamBeginToken(1, 0), comparer);
        }

        [Theory]
        [MemberData(nameof(TokensFollowedByWhitespace))]
        public void TestTokensFollowedByWhitespace(string input, Token expectedToken)
        {
            Token[] tokens = GetTokeniserForInput(input).Tokenise().ToArray();

            Assert.Single(tokens);

            Token actualToken = tokens.First();

            Assert.Equal(expectedToken, actualToken, new TokenEqualityComparer());
        }

        public static IEnumerable<object[]> TokensFollowedByWhitespace = new List<object[]>
        {
            new object[] { " R\t", new IndirectReferenceMarkerToken(1) },
            new object[] { " obj\t", new IndirectObjectBeginToken(1) },
            new object[] { " endobj\t", new IndirectObjectEndToken(1) },
            new object[] { " stream\n$", new StreamBeginToken(1, streamStartIndex: 8) },
            new object[] { " null\t", new NullToken(1) },
            new object[] { " true\t", new BooleanToken(true, 1) },
            new object[] { " 123\t", CreateNumericIntegerToken(123, 1) },
            new object[] { " 3.14\t", CreateNumericRealToken(3.14m, 1) },
            new object[] { " [\t", new ArrayBeginToken(1) },
            new object[] { " ]\t", new ArrayEndToken(1) },
            new object[] { " <<\t", new DictionaryBeginToken(1) },
            new object[] { " >>\t", new DictionaryEndToken(1) },
            new object[] { " (abc\t)\t", CreateStringToken("abc\t", 1) },
            new object[] { " /abc\t", CreateNameToken("abc", 1) },
            new object[] { " %abc\t\n", CreateCommentToken("abc\t", 1) },
            new object[] { " %PDF-2.0\n", CreateHeaderCommentToken("PDF-2.0", "2.0", 1) },
            new object[] { " %\u00C3\u00C4\u00C5\u00C6\n", new BinaryIndicatorCommentToken(new byte[] { 0xC3, 0xC4, 0xC5, 0xC6 }, 1) },
        };

        [Theory]
        [MemberData(nameof(TokensFollowedByDelimiter))]
        public void TestTokensFollowedByDelimiter(string input, Token expectedToken)
        {
            Token[] tokens = GetTokeniserForInput(input).Tokenise().ToArray();

            Token actualToken = tokens.First();

            Assert.Equal(expectedToken, actualToken, new TokenEqualityComparer());
        }

        public static IEnumerable<object[]> TokensFollowedByDelimiter = new List<object[]>
        {
            new object[] { " R]", new IndirectReferenceMarkerToken(1) },
            new object[] { " R>>", new IndirectReferenceMarkerToken(1) },
            new object[] { " R/a", new IndirectReferenceMarkerToken(1) },
            new object[] { " R%a", new IndirectReferenceMarkerToken(1) },

            new object[] { " obj<<", new IndirectObjectBeginToken(1) },
            new object[] { " obj[", new IndirectObjectBeginToken(1) },
            new object[] { " obj/a", new IndirectObjectBeginToken(1) },
            new object[] { " obj%a", new IndirectObjectBeginToken(1) },

            new object[] { " endobj]", new IndirectObjectEndToken(1) },
            new object[] { " endobj>>", new IndirectObjectEndToken(1) },
            new object[] { " endobj/a", new IndirectObjectEndToken(1) },
            new object[] { " endobj%a", new IndirectObjectEndToken(1) },

            new object[] { " null]", new NullToken(1) },
            new object[] { " null>>", new NullToken(1) },
            new object[] { " null/a", new NullToken(1) },
            new object[] { " null%a", new NullToken(1) },

            new object[] { " true]", new BooleanToken(true, 1) },
            new object[] { " true>>", new BooleanToken(true, 1) },
            new object[] { " true/a", new BooleanToken(true, 1) },
            new object[] { " true%a", new BooleanToken(true, 1) },

            new object[] { " 123]", CreateNumericIntegerToken(123, 1) },
            new object[] { " 123>>", CreateNumericIntegerToken(123, 1) },
            new object[] { " 123/a", CreateNumericIntegerToken(123, 1) },
            new object[] { " 123%a", CreateNumericIntegerToken(123, 1) },

            new object[] { " 3.14]", CreateNumericRealToken(3.14m, 1) },
            new object[] { " 3.14>>", CreateNumericRealToken(3.14m, 1) },
            new object[] { " 3.14/a", CreateNumericRealToken(3.14m, 1) },
            new object[] { " 3.14%a", CreateNumericRealToken(3.14m, 1) },

            new object[] { " [[", new ArrayBeginToken(1) },
            new object[] { " [<<", new ArrayBeginToken(1) },
            new object[] { " [/a", new ArrayBeginToken(1) },
            new object[] { " [%a", new ArrayBeginToken(1) },

            new object[] { " ]]", new ArrayEndToken(1) },
            new object[] { " ]>>", new ArrayEndToken(1) },
            new object[] { " ]/a", new ArrayEndToken(1) },
            new object[] { " ]%a", new ArrayEndToken(1) },

            new object[] { " <<<ab>", new DictionaryBeginToken(1) },
            new object[] { " <<<<", new DictionaryBeginToken(1) },
            new object[] { " <<[", new DictionaryBeginToken(1) },
            new object[] { " <</a", new DictionaryBeginToken(1) },
            new object[] { " <<%a", new DictionaryBeginToken(1) },

            new object[] { " >>>>", new DictionaryEndToken(1) },
            new object[] { " >>]", new DictionaryEndToken(1) },
            new object[] { " >>/a", new DictionaryEndToken(1) },
            new object[] { " >>%a", new DictionaryEndToken(1) },

            new object[] { " (abc])]", CreateStringToken("abc]", 1) },
            new object[] { " (abc>>)>>", CreateStringToken("abc>>", 1) },
            new object[] { " (abc/b)/a", CreateStringToken("abc/b", 1) },
            new object[] { " (abc%b)%a", CreateStringToken("abc%b", 1) },

            new object[] { " <abc>]", CreateStringToken("\u00AB\u00C0", 1) },
            new object[] { " <abc>>>", CreateStringToken("\u00AB\u00C0", 1) },
            new object[] { " <abc>/a", CreateStringToken("\u00AB\u00C0", 1) },
            new object[] { " <abc>%a", CreateStringToken("\u00AB\u00C0", 1) },

            new object[] { " /abc]", CreateNameToken("abc", 1) },
            new object[] { " /abc>>", CreateNameToken("abc", 1) },
            new object[] { " /abc/a", CreateNameToken("abc", 1) },
            new object[] { " /abc%a", CreateNameToken("abc", 1) },
        };

        private static StringToken CreateStringToken(string value, int startIndex)
        {
            // NOTE: value should only contain 1-byte characters
            return new StringToken(value.ToCharArray().Select(c => (byte)c).ToArray(), startIndex);
        }

        private static NameToken CreateNameToken(string value, int startIndex)
        {
            // NOTE: value should only contain 1-byte characters
            return new NameToken(value.ToCharArray().Select(c => (byte)c).ToArray(), startIndex);
        }

        private static CommentToken CreateCommentToken(string value, int startIndex)
        {
            // NOTE: value should only contain 1-byte characters
            return new CommentToken(value.ToCharArray().Select(c => (byte)c).ToArray(), startIndex);
        }

        private static HeaderCommentToken CreateHeaderCommentToken(string value, string version, int startIndex)
        {
            // NOTE: value should only contain 1-byte characters
            return new HeaderCommentToken(value.ToCharArray().Select(c => (byte)c).ToArray(), version, startIndex);
        }

        private static NumericIntegerToken CreateNumericIntegerToken(int value, int startIndex)
        {
            return new NumericIntegerToken(value, value.ToString().ToCharArray().Select(c => (byte)c).ToArray(), startIndex);
        }

        private static NumericRealToken CreateNumericRealToken(decimal value, int startIndex)
        {
            return new NumericRealToken(value, value.ToString().ToCharArray().Select(c => (byte)c).ToArray(), startIndex);
        }
    }
}
