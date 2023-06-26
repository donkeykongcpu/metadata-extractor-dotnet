// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using MetadataExtractor.Formats.Pdf;

namespace MetadataExtractor.Tests.Formats.Pdf
{
    /// <summary>Unit tests for <see cref="PdfTokeniser"/>.</summary>
    /// <author>Drew Noakes https://drewnoakes.com</author>
    public sealed class PdfTokeniserTest
    {
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

        private static Dictionary<string, StringToken[]> _testLiteralStrings = new Dictionary<string, StringToken[]>
        {
            { "(This is a string)", new StringToken[] { CreateStringToken("This is a string", 0) } },

            { "(Whitespace   is preserved)", new StringToken[] { CreateStringToken("Whitespace   is preserved", 0) } },

            { "(Strings may contain newlines\r\nand such.)", new StringToken[] { CreateStringToken("Strings may contain newlines\nand such.", 0) } },
            { "(Strings may contain newlines\rand such.)", new StringToken[] { CreateStringToken("Strings may contain newlines\nand such.", 0) } },
            { "(Strings may contain newlines\nand such.)", new StringToken[] { CreateStringToken("Strings may contain newlines\nand such.", 0) } },

            { "(Strings may contain newlines\r\n\r\nand such.)", new StringToken[] { CreateStringToken("Strings may contain newlines\n\nand such.", 0) } },
            { "(Strings may contain newlines\r\rand such.)", new StringToken[] { CreateStringToken("Strings may contain newlines\n\nand such.", 0) } },
            { "(Strings may contain newlines\n\nand such.)", new StringToken[] { CreateStringToken("Strings may contain newlines\n\nand such.", 0) } },

            { "(Strings may contain balanced parentheses ( ) and special characters (*!&}^% and so on).)",
                new StringToken[] { CreateStringToken("Strings may contain balanced parentheses ( ) and special characters (*!&}^% and so on).", 0) } },

            { "()", new StringToken[] { CreateStringToken("", 0) } },

            { "(It has zero (0) length.)", new StringToken[] { CreateStringToken("It has zero (0) length.", 0) } },

            { @"( \n \r \t \b \f \( \) \\ \123 \x )", new StringToken[] { CreateStringToken(" \u000A \u000D \u0009 \u0008 \u000C \u0028 \u0029 \u005C \u0053 x ", 0) } },

            { "(These \\\r\ntwo strings \\\rare the same\\\n.)", new StringToken[] { CreateStringToken("These two strings are the same.", 0) } },

            { "(This string has an end-of-line at the end of it.\n)", new StringToken[] { CreateStringToken("This string has an end-of-line at the end of it.\n", 0) } },
            { @"(So does this one.\n)", new StringToken[] { CreateStringToken("So does this one.\n", 0) } },

            { @"(This string contains \245two octal characters\307.)", new StringToken[] { CreateStringToken("This string contains \u00A5two octal characters\u00C7.", 0) } },
            { @"(High-order overflow (\765) is ignored.)", new StringToken[] { CreateStringToken("High-order overflow (\u00F5) is ignored.", 0) } },
            { @"(The literal (\0053) denotes a string containing two characters, \005 (Control-E) followed by the digit 3)",
                new StringToken[] { CreateStringToken("The literal (\u00053) denotes a string containing two characters, \u0005 (Control-E) followed by the digit 3", 0) } },
            { @"(Both (\053) and (\53) denote strings containing the single character \053, a plus sign (+).)",
                new StringToken[] { CreateStringToken("Both (\u002B) and (\u002B) denote strings containing the single character \u002B, a plus sign (+).", 0) } },

            { " (string)", new StringToken[] { CreateStringToken("string", 1) } },
            { " \n(string)", new StringToken[] { CreateStringToken("string", 2) } },
            { " \r\n(string)", new StringToken[] { CreateStringToken("string", 3) } },
            { " \r\n (string)", new StringToken[] { CreateStringToken("string", 4) } },
        };

        private static Dictionary<string, StringToken[]> _testHexadecimalStrings = new Dictionary<string, StringToken[]>
        {
            { "<4E6F762073686D6F7A>", new StringToken[] { CreateStringToken("\u004E\u006F\u0076\u0020\u0073\u0068\u006D\u006F\u007A", 0) } },

            { "<57 68 69 74 65 73 70 61 63 65 20 69 73 20 69 67 6E 6F 72 65 64>", new StringToken[] { CreateStringToken("Whitespace is ignored", 0) } },

            { "<901FA3>", new StringToken[] { CreateStringToken("\u0090\u001F\u00A3", 0) } },
            { "<901FA>", new StringToken[] { CreateStringToken("\u0090\u001F\u00A0", 0) } },

            { " <0000>", new StringToken[] { CreateStringToken("\0\0", 1) } },
            { " \n<0000>", new StringToken[] { CreateStringToken("\0\0", 2) } },
            { " \r\n<0000>", new StringToken[] { CreateStringToken("\0\0", 3) } },
            { " \r\n <0000>", new StringToken[] { CreateStringToken("\0\0", 4) } },
        };

        private static Dictionary<string, NameToken[]> _testNames = new Dictionary<string, NameToken[]>
        {
            { "/Name1", new NameToken[] { CreateNameToken("Name1", 0) } },
            { "/ASomewhatLongerName", new NameToken[] { CreateNameToken("ASomewhatLongerName", 0) } },
            { "/A;Name_With-Various***Characters?", new NameToken[] { CreateNameToken("A;Name_With-Various***Characters?", 0) } },
            { "/1.2", new NameToken[] { CreateNameToken("1.2", 0) } },
            { "/$$", new NameToken[] { CreateNameToken("$$", 0) } },
            { "/@pattern", new NameToken[] { CreateNameToken("@pattern", 0) } },
            { "/.notdef", new NameToken[] { CreateNameToken(".notdef", 0) } },
            { "/Lime#20Green", new NameToken[] { CreateNameToken("Lime Green", 0) } },
            { "/paired#28#29parentheses", new NameToken[] { CreateNameToken("paired()parentheses", 0) } },
            { "/The_Key_of_F#23_Minor", new NameToken[] { CreateNameToken("The_Key_of_F#_Minor", 0) } },
            { "/A#42", new NameToken[] { CreateNameToken("AB", 0) } },

            { "/", new NameToken[] { CreateNameToken("", 0) } },

            { " /Name", new NameToken[] { CreateNameToken("Name", 1) } },
            { " \n/Name", new NameToken[] { CreateNameToken("Name", 2) } },
            { " \r\n/Name", new NameToken[] { CreateNameToken("Name", 3) } },
            { " \r\n /Name", new NameToken[] { CreateNameToken("Name", 4) } },
        };

        private static Dictionary<string, CommentToken[]> _testComments = new Dictionary<string, CommentToken[]>
        {
            { "%comment", new CommentToken[] { CreateCommentToken("comment", 0) } },
            { "% comment", new CommentToken[] { CreateCommentToken(" comment", 0) } },

            { " %comment\r\n", new CommentToken[] { CreateCommentToken("comment", 1) } },
            { " %comment\r", new CommentToken[] { CreateCommentToken("comment", 1) } },
            { " %comment\n", new CommentToken[] { CreateCommentToken("comment", 1) } },

            { " \n%comment\n", new CommentToken[] { CreateCommentToken("comment", 2) } },
            { " \r\n%comment\n", new CommentToken[] { CreateCommentToken("comment", 3) } },
            { " \r\n %comment\n", new CommentToken[] { CreateCommentToken("comment", 4) } },
        };

        private static Dictionary<string, HeaderCommentToken[]> _testHeaderComments = new Dictionary<string, HeaderCommentToken[]>
        {
            { "%PDF-1.0", new HeaderCommentToken[] { CreateHeaderCommentToken("PDF-1.0", "1.0", 0) } },
            { "%PDF-1.1 ", new HeaderCommentToken[] { CreateHeaderCommentToken("PDF-1.1 ", "1.1", 0) } },
            { "%PDF-1.2\r\n", new HeaderCommentToken[] { CreateHeaderCommentToken("PDF-1.2", "1.2", 0) } },
            { "%PDF-1.3\r", new HeaderCommentToken[] { CreateHeaderCommentToken("PDF-1.3", "1.3", 0) } },
            { "%PDF-1.4\n", new HeaderCommentToken[] { CreateHeaderCommentToken("PDF-1.4", "1.4", 0) } },
            { "%PDF-1.5 \n", new HeaderCommentToken[] { CreateHeaderCommentToken("PDF-1.5 ", "1.5", 0) } },
            { "%PDF-1.6", new HeaderCommentToken[] { CreateHeaderCommentToken("PDF-1.6", "1.6", 0) } },
            { "%PDF-1.7", new HeaderCommentToken[] { CreateHeaderCommentToken("PDF-1.7", "1.7", 0) } },
            { "%PDF-2.0", new HeaderCommentToken[] { CreateHeaderCommentToken("PDF-2.0", "2.0", 0) } },
        };


        private static PdfTokeniser GetTokeniserForInput(string input)
        {
            StringByteProviderSource byteSource = new StringByteProviderSource(input, 0, ExtractionDirection.Forward);

            BufferedItemProvider<byte> byteProvider = new BufferedItemProvider<byte>(byteSource, 16);

            PdfTokeniser tokeniser = new PdfTokeniser(byteProvider);

            return tokeniser;
        }


        [Fact]
        public void TestLiteralStrings()
        {
            foreach (string input in _testLiteralStrings.Keys)
            {
                Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

                Token[] expected = _testLiteralStrings[input];

                Assert.Equal(expected.Length, actual.Length);

                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.True(expected[i].Equals(actual[i]));

                    Assert.Equal(expected[i].StartIndex, actual[i].StartIndex);
                }
            }
        }

        [Fact]
        public void TestHexadecimalStrings()
        {
            foreach (string input in _testHexadecimalStrings.Keys)
            {
                Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

                Token[] expected = _testHexadecimalStrings[input];

                Assert.Equal(expected.Length, actual.Length);

                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.True(expected[i].Equals(actual[i]));

                    Assert.Equal(expected[i].StartIndex, actual[i].StartIndex);
                }
            }
        }

        [Fact]
        public void TestNames()
        {
            foreach (string input in _testNames.Keys)
            {
                Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

                Token[] expected = _testNames[input];

                Assert.Equal(expected.Length, actual.Length);

                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.True(expected[i].Equals(actual[i]));

                    Assert.Equal(expected[i].StartIndex, actual[i].StartIndex);
                }
            }
        }

        [Fact]
        public void TestComments()
        {
            foreach (string input in _testComments.Keys)
            {
                Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

                Token[] expected = _testComments[input];

                Assert.Equal(expected.Length, actual.Length);

                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.True(expected[i].Equals(actual[i]));

                    Assert.Equal(expected[i].StartIndex, actual[i].StartIndex);
                }
            }
        }

        [Fact]
        public void TestHeaderComments()
        {
            foreach (string input in _testHeaderComments.Keys)
            {
                Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

                HeaderCommentToken[] expected = _testHeaderComments[input];

                Assert.Equal(expected.Length, actual.Length);

                Assert.True(actual.All(token => token is HeaderCommentToken));

                var headerTokens = actual.Cast<HeaderCommentToken>().ToArray();

                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.True(expected[i].Equals(headerTokens[i]));

                    Assert.Equal(expected[i].StartIndex, headerTokens[i].StartIndex);

                    Assert.Equal(expected[i].Version, headerTokens[i].Version);

                    Assert.Equal(expected[i].DecimalVersion, headerTokens[i].DecimalVersion);
                }
            }
        }

        [Fact]
        public void TestBinaryIndicatorComment()
        {
            string input = " %\u00E2\u00E3\u00CF\u00D3\r\n";

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Assert.True(actual.Length == 1);

            Assert.True(actual[0] is BinaryIndicatorCommentToken);

            Assert.Equal(1, actual[0].StartIndex);
        }

        [Fact]
        public void TestNull()
        {
            string input = " null";

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Assert.True(actual.Length == 1);

            Assert.True(actual[0] is NullToken);

            Assert.Equal(1, actual[0].StartIndex);
        }

        [Fact]
        public void TestBooleanTrue()
        {
            string input = "  true";

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Assert.True(actual.Length == 1);

            Assert.True(actual[0] is BooleanToken);

            Assert.True((actual[0] as BooleanToken)!.BooleanValue);

            Assert.Equal(2, actual[0].StartIndex);
        }

        [Fact]
        public void TestBooleanFalse()
        {
            string input = " \r\n false";

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Assert.True(actual.Length == 1);

            Assert.True(actual[0] is BooleanToken);

            Assert.False((actual[0] as BooleanToken)!.BooleanValue);

            Assert.Equal(4, actual[0].StartIndex);
        }

        [Fact]
        public void TestNumericIntegers()
        {
            string input = " 123 43445 +17 -98 0";

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Assert.True(actual.Length == input.Split(' ').Where(x => x.Trim() != string.Empty).Count());

            Assert.True(actual.All(token => token is NumericIntegerToken));

            var integerTokens = actual.Cast<NumericIntegerToken>().ToArray();

            Assert.Equal(123, integerTokens[0].IntegerValue);
            Assert.Equal(1, integerTokens[0].StartIndex);

            Assert.Equal(43445, integerTokens[1].IntegerValue);
            Assert.Equal(5, integerTokens[1].StartIndex);

            Assert.Equal(17, integerTokens[2].IntegerValue);
            Assert.Equal(11, integerTokens[2].StartIndex);

            Assert.Equal(-98, integerTokens[3].IntegerValue);
            Assert.Equal(15, integerTokens[3].StartIndex);

            Assert.Equal(0, integerTokens[4].IntegerValue);
            Assert.Equal(19, integerTokens[4].StartIndex);
        }

        [Fact]
        public void TestNumericReals()
        {
            string input = " 34.5 -3.62 +123.6 4. -.002 0.0";

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Assert.True(actual.Length == input.Split(' ').Where(x => x.Trim() != string.Empty).Count());

            Assert.True(actual.All(token => token is NumericRealToken));

            var realTokens = actual.Cast<NumericRealToken>().ToArray();

            Assert.Equal("34.5", realTokens[0].RealValue.ToString());
            Assert.Equal(1, realTokens[0].StartIndex);

            Assert.Equal("-3.62", realTokens[1].RealValue.ToString());
            Assert.Equal(6, realTokens[1].StartIndex);

            Assert.Equal("123.6", realTokens[2].RealValue.ToString());
            Assert.Equal(12, realTokens[2].StartIndex);

            Assert.Equal("4", realTokens[3].RealValue.ToString());
            Assert.Equal(19, realTokens[3].StartIndex);

            Assert.Equal("-0.002", realTokens[4].RealValue.ToString());
            Assert.Equal(22, realTokens[4].StartIndex);

            Assert.Equal("0.0", realTokens[5].RealValue.ToString());
            Assert.Equal(28, realTokens[5].StartIndex);
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
                Assert.True(expected[i].Equals(actual[i]));

                Assert.Equal(expected[i].StartIndex, actual[i].StartIndex);
            }
        }

        [Fact]
        public void TestBreakAtStreamMarker()
        {
            string input = " <</Length 42>>stream $$$$$";
            //              01234567890123456789012345678901234567
            //              0         10        20        30

            Token[] actual = GetTokeniserForInput(input).Tokenise().ToArray();

            Token[] expected =
            {
                new DictionaryBeginToken(1),
                CreateNameToken("Length", 3),
                CreateNumericIntegerToken(42, 11),
                new DictionaryEndToken(13),
                new StreamBeginToken(15),
            };

            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.True(expected[i].Equals(actual[i]));

                Assert.Equal(expected[i].StartIndex, actual[i].StartIndex);
            }
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
                Assert.True(expected[i].Equals(actual[i]));

                Assert.Equal(expected[i].StartIndex, actual[i].StartIndex);
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
                Assert.True(expected[i].Equals(actual[i]));

                Assert.Equal(expected[i].StartIndex, actual[i].StartIndex);
            }
        }

        [Fact]
        public void TestDictionaryFromSampleFile()
        {
            string input = " 699 0 obj<</First 700 0 R/Count 13/Last 701 0 R>>endobj\r\n1129 0 obj\r\n<</Subtype/XML/Length 3649/Type/Metadata>>stream";
            //              012345678901234567890123456789012345678901234567890123456 7 89012345678 9 01234567890123456789012345678901234567890123456
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
                new StreamBeginToken(112),
            };

            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.True(expected[i].Equals(actual[i]));

                Assert.Equal(expected[i].StartIndex, actual[i].StartIndex);
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
                Assert.True(expected[i].Equals(actual[i]));

                Assert.Equal(expected[i].StartIndex, actual[i].StartIndex);
            }
        }

    }
}
