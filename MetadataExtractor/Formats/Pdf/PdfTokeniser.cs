// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace MetadataExtractor.Formats.Pdf
{
    internal class PdfTokeniser
    {
        private readonly ByteStreamBufferedProvider _byteProvider;

        public PdfTokeniser(ByteStreamBufferedProvider byteProvider)
        {
            _byteProvider = byteProvider;
        }

        public IEnumerable<Token> Tokenise()
        {
            while (_byteProvider.HasNextItem)
            {
                int currentIndex = _byteProvider.CurrentIndex;

                if (MatchWhitespace()) continue;
                else if (MatchToken("stream"))
                {
                    yield return new StreamBeginToken(currentIndex);
                    yield break; // do not tokenise further, since the stream most likely contains invalid tokens
                }
                else if (MatchToken("R")) yield return new IndirectReferenceMarkerToken(currentIndex);
                else if (MatchToken("obj")) yield return new IndirectObjectBeginToken(currentIndex);
                else if (MatchToken("endobj")) yield return new IndirectObjectEndToken(currentIndex);
                else if (MatchToken("null")) yield return new NullToken(currentIndex);
                else if (MatchToken("true")) yield return new BooleanToken(true, currentIndex);
                else if (MatchToken("false")) yield return new BooleanToken(false, currentIndex);
                else if (TryMatchNumericToken(out Token? numericToken)) yield return numericToken;
                else if (TryMatchLiteralString(out StringToken? literalStringToken)) yield return literalStringToken;
                else if (MatchDelimiter("[")) yield return new ArrayBeginToken(currentIndex);
                else if (MatchDelimiter("]")) yield return new ArrayEndToken(currentIndex);
                else if (MatchDelimiter("<<")) yield return new DictionaryBeginToken(currentIndex);
                else if (MatchDelimiter(">>")) yield return new DictionaryEndToken(currentIndex);
                else if (TryMatchHexadecimalString(out StringToken? hexStringToken)) yield return hexStringToken;
                else if (TryMatchName(out NameToken? nameToken)) yield return nameToken;
                else if (TryMatchComment(out CommentToken? commentToken)) yield return commentToken;
                else throw new Exception("Invalid character in input");
            }
        }

        private bool MatchToken(string asciiToken)
        {
            byte[] tokenBytes = Encoding.ASCII.GetBytes(asciiToken);
            byte byteAfterLast = _byteProvider.PeekNextItem(tokenBytes.Length);
            // must be followed by whitespace (or EOF), array end marker, dictionary end marker or name marker
            if (!PdfReader.WhitespaceChars.Contains(byteAfterLast)
                && byteAfterLast != (byte)']' && byteAfterLast != (byte)'>' && byteAfterLast != (byte)'/')
            {
                return false;
            }
            for (int i = 0; i < tokenBytes.Length; i++)
            {
                if (tokenBytes[i] != _byteProvider.PeekNextItem(i))
                {
                    return false;
                }
            }
            _byteProvider.Consume(tokenBytes.Length);
            return true;
        }

        private bool MatchDelimiter(string asciiDelimiter)
        {
            byte[] tokenBytes = Encoding.ASCII.GetBytes(asciiDelimiter);
            for (int i = 0; i < tokenBytes.Length; i++)
            {
                if (tokenBytes[i] != _byteProvider.PeekNextItem(i))
                {
                    return false;
                }
            }
            _byteProvider.Consume(tokenBytes.Length);
            return true;
        }

        private bool MatchWhitespace()
        {
            bool matched = false;
            while (true)
            {
                if (!_byteProvider.HasNextItem)
                {
                    // NOTE zero bytes are returned once end is reached, which are also considered whitespace
                    matched = true;
                    break;
                }
                else if (PdfReader.WhitespaceChars.Contains(_byteProvider.PeekNextItem(0)))
                {
                    matched = true;
                    _byteProvider.Consume(1);
                }
                else
                {
                    break;
                }
            }
            return matched;
        }

        private bool MatchEndOfLine()
        {
            if (_byteProvider.PeekNextItem(0) == (byte)'\r' && _byteProvider.PeekNextItem(1) == (byte)'\n')
            {
                _byteProvider.Consume(2);
                return true;
            }
            else if (_byteProvider.PeekNextItem(0) == (byte)'\r' || _byteProvider.PeekNextItem(0) == (byte)'\n')
            {
                _byteProvider.Consume(1);
                return true;
            }
            return false;
        }

        private bool TryMatchNumericToken([NotNullWhen(true)] out Token? token)
        {
            int currentIndex = _byteProvider.CurrentIndex;
            List<byte> bytes = new List<byte>();
            bool isDecimal = false;
            for (int i = 0; ; i++)
            {
                byte nextByte = _byteProvider.PeekNextItem(i);
                if ((nextByte >= (byte)'0' && nextByte <= (byte)'9') || nextByte == (byte)'-' || nextByte == (byte)'+')
                {
                    bytes.Add(nextByte);
                }
                else if (nextByte == (byte)'.')
                {
                    bytes.Add(nextByte);
                    isDecimal = true;
                }
                else
                {
                    byte[] byteArray = bytes.ToArray();
                    string strValue = Encoding.ASCII.GetString(byteArray);
                    if (isDecimal && decimal.TryParse(strValue, out var decimalValue))
                    {
                        token = new NumericRealToken(decimalValue, byteArray, currentIndex);
                        _byteProvider.Consume(byteArray.Length);
                        return true;
                    }
                    else if (int.TryParse(strValue, out var intValue))
                    {
                        token = new NumericIntegerToken(intValue, byteArray, currentIndex);
                        _byteProvider.Consume(byteArray.Length);
                        return true;
                    }
                    else
                    {
                        token = null;
                        return false;
                    }
                }
            }
        }

        private bool TryMatchOctalDigit(out byte result)
        {
            byte test = _byteProvider.PeekNextItem(0);
            if (test >= (byte)'0' && test <= (byte)'7')
            {
                result = (byte)(test - (byte)'0');
                _byteProvider.Consume(1);
                return true;
            }
            else
            {
                result = 0;
                return false;
            }
        }

        private bool TryMatchHexadecimalDigit(out byte result)
        {
            byte test = _byteProvider.PeekNextItem(0);
            if (test >= (byte)'0' && test <= (byte)'9')
            {
                result = (byte)(test - (byte)'0');
                _byteProvider.Consume(1);
                return true;
            }
            else if (test >= (byte)'A' && test <= (byte)'F')
            {
                result = (byte)(test - (byte)'A' + 10);
                _byteProvider.Consume(1);
                return true;
            }
            else if (test >= (byte)'a' && test <= (byte)'f')
            {
                result = (byte)(test - (byte)'a' + 10);
                _byteProvider.Consume(1);
                return true;
            }
            else
            {
                result = 0;
                return false;
            }
        }

        private bool TryMatchHexadecimalString([NotNullWhen(true)] out StringToken? token)
        {
            int currentIndex = _byteProvider.CurrentIndex;
            List<byte> bytes = new List<byte>();
            if (_byteProvider.PeekNextItem(0) != (byte)'<')
            {
                token = null;
                return false;
            }
            _byteProvider.Consume(1);
            while (true)
            {
                if (!_byteProvider.HasNextItem)
                {
                    throw new Exception("Unexpected end of input");
                }

                if (_byteProvider.PeekNextItem(0) == (byte)'>')
                {
                    _byteProvider.Consume(1);
                    if (bytes.Count % 2 != 0)
                    {
                        bytes.Add(0); // odd number of digits => append 0
                    }
                    List<byte> result = new List<byte>();
                    for (int i = 0; i < bytes.Count; i += 2)
                    {
                        byte value = (byte)(bytes[i] * 16 + bytes[i + 1]);
                        result.Add(value);
                    }
                    token = new StringToken(result.ToArray(), currentIndex);
                    return true; // success!
                }
                else if (TryMatchHexadecimalDigit(out byte result))
                {
                    bytes.Add(result);
                }
                else if (MatchWhitespace())
                {
                    // ignore whitespace within
                }
                else
                {
                    throw new Exception("Unexpected byte in hexadecimal string");
                }
            }
        }

        private bool TryMatchLiteralString([NotNullWhen(true)] out StringToken? token)
        {
            int currentIndex = _byteProvider.CurrentIndex;
            List<byte> bytes = new List<byte>();
            int balanceCounter = 0;
            if (_byteProvider.PeekNextItem(0) != (byte)'(')
            {
                token = null;
                return false;
            }
            _byteProvider.Consume(1);
            balanceCounter++;
            while (true)
            {
                if (!_byteProvider.HasNextItem)
                {
                    throw new Exception("Unexpected end of input");
                }

                byte nextByte = _byteProvider.GetNextItem();

                if (nextByte == (byte)'(')
                {
                    balanceCounter++;
                    bytes.Add((byte)'(');
                }
                else if (nextByte == (byte)')')
                {
                    balanceCounter--;
                    if (balanceCounter == 0)
                    {
                        token = new StringToken(bytes.ToArray(), currentIndex);
                        return true; // success!
                    }
                    else
                    {
                        bytes.Add((byte)')');
                    }
                }
                else if (nextByte == (byte)'\r' && _byteProvider.PeekNextItem(0) == (byte)'\n')
                {
                    bytes.Add((byte)'\n'); // CRLF => LF
                    _byteProvider.Consume(1);
                }
                else if (nextByte == (byte)'\r')
                {
                    bytes.Add((byte)'\n'); // CR => LF
                    _byteProvider.Consume(0); // already consumed
                }
                else if (nextByte == (byte)'\\')
                {
                    // escape sequences
                    byte peekByte = _byteProvider.PeekNextItem(0);
                    if (peekByte == (byte)'\r' && _byteProvider.PeekNextItem(1) == (byte)'\n')
                    {
                        // \CRLF => ignored
                        _byteProvider.Consume(2);
                    }
                    else if (TryMatchOctalDigit(out byte digit1))
                    {
                        // octal character codes
                        // can consist of one, two or three digits
                        // high-order overflow shall be ignored
                        List<byte> digits = new List<byte> { digit1 };

                        if (TryMatchOctalDigit(out byte digit2))
                        {
                            digits.Add(digit2);
                        }

                        if (TryMatchOctalDigit(out byte digit3))
                        {
                            digits.Add(digit3);
                        }

                        int value = 0;
                        for (int i = 0; i < digits.Count; i++)
                        {
                            value += digits[digits.Count - 1 - i] * (int)Math.Pow(8, i);
                        }
                        bytes.Add((byte)value);
                    }
                    else
                    {
                        switch (peekByte)
                        {
                            case (byte)'n': bytes.Add((byte)'\n'); _byteProvider.Consume(1); break;
                            case (byte)'r': bytes.Add((byte)'\r'); _byteProvider.Consume(1); break;
                            case (byte)'t': bytes.Add((byte)'\t'); _byteProvider.Consume(1); break;
                            case (byte)'b': bytes.Add((byte)'\b'); _byteProvider.Consume(1); break;
                            case (byte)'f': bytes.Add((byte)'\f'); _byteProvider.Consume(1); break;
                            case (byte)'(': bytes.Add((byte)'('); _byteProvider.Consume(1); break;
                            case (byte)')': bytes.Add((byte)')'); _byteProvider.Consume(1); break;
                            case (byte)'\\': bytes.Add((byte)'\\'); _byteProvider.Consume(1); break;
                            case (byte)'\n': _byteProvider.Consume(1); break; // \LF => ignored
                            case (byte)'\r': _byteProvider.Consume(1); break; // \CR => ignored
                            default: break; // ignore \ and process next byte
                        }
                    }
                }
                else
                {
                    bytes.Add(nextByte);
                }
            }
        }

        private bool TryMatchName([NotNullWhen(true)] out NameToken? token)
        {
            int currentIndex = _byteProvider.CurrentIndex;
            List<byte> bytes = new List<byte>();
            if (_byteProvider.PeekNextItem(0) != (byte)'/')
            {
                token = null;
                return false;
            }
            _byteProvider.Consume(1);
            while (true)
            {
                if (!_byteProvider.HasNextItem || MatchWhitespace()
                    || _byteProvider.PeekNextItem(0) == (byte)'/' || _byteProvider.PeekNextItem(0) == (byte)'>')
                {
                    token = new NameToken(bytes.ToArray(), currentIndex); // does not include the leading slash (/)
                    return true; // success!
                }
                else if (_byteProvider.PeekNextItem(0) == (byte)'#')
                {
                    _byteProvider.Consume(1);
                    // the following two bytes must be hex digits
                    byte digit1;
                    byte digit2;
                    if (!TryMatchHexadecimalDigit(out digit1))
                    {
                        throw new Exception("Unexpected byte");
                    }
                    if (!TryMatchHexadecimalDigit(out digit2))
                    {
                        throw new Exception("Unexpected byte");
                    }
                    byte value = (byte)(digit1 * 16 + digit2);
                    bytes.Add(value);
                }
                else
                {
                    byte nextByte = _byteProvider.GetNextItem();
                    bytes.Add(nextByte);
                }
            }
        }

        private bool TryMatchComment([NotNullWhen(true)] out CommentToken? token)
        {
            int currentIndex = _byteProvider.CurrentIndex;
            List<byte> bytes = new List<byte>();
            if (_byteProvider.PeekNextItem(0) != (byte)'%')
            {
                token = null;
                return false;
            }
            _byteProvider.Consume(1);
            while (true)
            {
                if (!_byteProvider.HasNextItem || MatchEndOfLine())
                {
                    token = new CommentToken(bytes.ToArray(), currentIndex); // does not include the leading percent sign (%)
                    return true; // success!
                }
                else
                {
                    byte nextByte = _byteProvider.GetNextItem();
                    bytes.Add(nextByte);
                }
            }
        }
    }
}
