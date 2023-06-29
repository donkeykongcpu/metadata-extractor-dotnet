// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace MetadataExtractor.Formats.Pdf
{
    public class PdfParser
    {
        private class ParseContext
        {
            private readonly Stack<IPdfContainer> _stack;

            public bool IsRoot => _stack.Peek().Type == "root";

            private string ContextType => _stack.Peek().Type;

            public ParseContext()
            {
                _stack = new Stack<IPdfContainer>();
                _stack.Push(new PdfRoot()); // make sure stack is never empty
            }

            private void AddScalarObject<T>(PdfScalarObject<T> scalarObject)
            {
                _stack.Peek().Add(scalarObject);
            }

            public void AddToken(NullToken _)
            {
                _stack.Peek().Add(new PdfNull());
            }

            public void AddToken(BooleanToken token)
            {
                AddScalarObject(new PdfBoolean(token.BooleanValue));
            }

            public void AddToken(NumericIntegerToken token)
            {
                AddScalarObject(new PdfNumericInteger(token.IntegerValue));
            }

            public void AddToken(NumericRealToken token)
            {
                AddScalarObject(new PdfNumericReal(token.RealValue));
            }

            public void AddToken(StringToken token)
            {
                AddScalarObject(new PdfString(token.StringValue));
            }

            public void AddToken(NameToken token)
            {
                AddScalarObject(new PdfName(token.StringValue));
            }

            public void AddIndirectReference(NumericIntegerToken objectNumberToken, NumericIntegerToken generationNumberToken)
            {
                ObjectIdentifier objectIdentifier = new ObjectIdentifier(objectNumberToken.IntegerValue, generationNumberToken.IntegerValue);
                AddScalarObject(new PdfIndirectReference(objectIdentifier));
            }

            public void StartContext<T>(PdfContainer<T> pdfContainer)
            {
                _stack.Peek().Nest(pdfContainer);
                _stack.Push(pdfContainer);
            }

            public void EndContext(string type)
            {
                if (ContextType != type)
                {
                    throw new Exception("Context type mismatch");
                }
                _stack.Pop();
                Debug.Assert(_stack.Count > 0);
            }

            public PdfIndirectObject GetIndirectObject()
            {
                if (_stack.Peek() is PdfIndirectObject pdfIndirectObject)
                {
                    return pdfIndirectObject;
                }
                else
                {
                    throw new Exception("Invalid context type");
                }
            }

            public void ReplaceIndirectObjectValue(PdfStream pdfStream)
            {
                if (_stack.Peek() is PdfIndirectObject pdfIndirectObject)
                {
                    pdfIndirectObject.ReplaceValue(pdfStream);
                }
                else
                {
                    throw new Exception("Invalid context type");
                }
            }

            public PdfObject GetRootValue()
            {
                if (_stack.Count != 1)
                {
                    throw new Exception("Invalid context count");
                }
                if (_stack.Peek() is PdfRoot pdfRoot)
                {
                    return pdfRoot.GetValue();
                }
                else
                {
                    throw new Exception("Invalid context root");
                }
            }
        }

        private static ItemProvider<Token> GetTokenProvider(string input, int startIndex)
        {
            StringByteProviderSource byteProviderSource = new StringByteProviderSource(input, startIndex, ExtractionDirection.Forward);

            BufferedItemProvider<byte> byteProvider = new BufferedItemProvider<byte>(byteProviderSource, 1024);

            return GetTokenProvider(byteProvider);
        }

        private static ItemProvider<Token> GetTokenProvider(IndexedReader reader, int startIndex)
        {
            IndexedReaderByteProviderSource byteProviderSource = new IndexedReaderByteProviderSource(reader, startIndex, ExtractionDirection.Forward);

            BufferedItemProvider<byte> byteProvider = new BufferedItemProvider<byte>(byteProviderSource, 1024);

            return GetTokenProvider(byteProvider);
        }

        private static ItemProvider<Token> GetTokenProvider(ItemProvider<byte> byteProvider)
        {
            PdfTokeniser tokeniser = new PdfTokeniser(byteProvider);

            IEnumerable<Token> tokens = tokeniser.Tokenise();

            EnumeratedItemProviderSource<Token> tokenSource = new EnumeratedItemProviderSource<Token>(tokens, new DummyToken());

            BufferedItemProvider<Token> tokenProvider = new BufferedItemProvider<Token>(tokenSource, 5);

            return tokenProvider;
        }

        public static T ParseIndirectObject<T>(string input, int startIndex, int objectNumber, int generationNumber) where T : PdfObject
        {
            return ParseIndirectObject<T>(GetTokenProvider(input, startIndex), objectNumber, generationNumber);
        }

        public static T ParseIndirectObject<T>(IndexedReader reader, int startIndex, int objectNumber, int generationNumber) where T : PdfObject
        {
            return ParseIndirectObject<T>(GetTokenProvider(reader, startIndex), objectNumber, generationNumber);
        }

        public static T ParseIndirectObject<T>(ItemProvider<Token> tokenProvider, int objectNumber, int generationNumber) where T : PdfObject
        {
            // unwraps the indirect object
            // (after checking, in Debug mode, that the requested object number and generation number match)

            PdfIndirectObject indirectObject = ParseObject<PdfIndirectObject>(tokenProvider);

            Debug.Assert(indirectObject.Identifier.ObjectNumber == objectNumber);

            Debug.Assert(indirectObject.Identifier.GenerationNumber == generationNumber);

            if (indirectObject.GetValue() is T obj)
            {
                return obj;
            }
            else
            {
                throw new Exception($"Unexpected object type {indirectObject.GetValue().GetType()}");
            }
        }

        public static T ParseObject<T>(string input, int startIndex) where T : PdfObject
        {
            return ParseObject<T>(GetTokenProvider(input, startIndex));
        }

        public static T ParseObject<T>(IndexedReader reader, int startIndex) where T : PdfObject
        {
            return ParseObject<T>(GetTokenProvider(reader, startIndex));
        }

        public static T ParseObject<T>(ItemProvider<Token> tokenProvider) where T : PdfObject
        {
            var parseContext = new ParseContext();

            while (tokenProvider.HasNextItem)
            {
                var nextToken = tokenProvider.GetNextItem();

                // first check if we have either an indirect object (objectNumber generationNumber "obj" ... "endobj")
                // or an indirect reference to an object (objectNumber generationNumber "R")
                // (which spans 3 tokens)

                if (nextToken is NumericIntegerToken objectNumberToken
                    && tokenProvider.PeekNextItem(0) is NumericIntegerToken generationNumberToken
                    && tokenProvider.PeekNextItem(1) is (IndirectObjectBeginToken or IndirectReferenceMarkerToken)
                    )
                {
                    if (tokenProvider.PeekNextItem(1) is IndirectObjectBeginToken)
                    {
                        parseContext.StartContext(new PdfIndirectObject(objectNumberToken.IntegerValue, generationNumberToken.IntegerValue));
                    }
                    else
                    {
                        parseContext.AddIndirectReference(objectNumberToken, generationNumberToken);
                    }
                    tokenProvider.Consume(2);
                }
                else if (nextToken is StreamBeginToken streamBeginToken)
                {
                    // all stream objects are indirect objects, so we must be within an "indirect-object" context
                    PdfIndirectObject indirectObject = parseContext.GetIndirectObject();
                    // the current value of the indirect object context must be a dictionary, which represents the stream dictionary
                    // create a PDF stream with this dictionary as stream dictionary, then replace the value of the indirect object context with it
                    if (indirectObject.GetValue() is PdfDictionary streamDictionary)
                    {
                        PdfStream pdfStream = new PdfStream(indirectObject.Identifier, streamDictionary, streamBeginToken.StreamStartIndex);
                        parseContext.ReplaceIndirectObjectValue(pdfStream);
                        // the stream most likely contains invalid tokens, so tokenisation ends here
                        // the "stream" keyword is followed the stream data then by "endstream",
                        // which must always be immediately followed by "endobj", meaning the end of the indirect object data
                        parseContext.EndContext("indirect-object");
                        break;
                    }
                    else
                    {
                        throw new Exception("Invalid stream dictionary");
                    }
                }
                else if (nextToken is ArrayBeginToken)
                {
                    parseContext.StartContext(new PdfArray());
                }
                else if (nextToken is ArrayEndToken)
                {
                    parseContext.EndContext("array");
                }
                else if (nextToken is DictionaryBeginToken)
                {
                    parseContext.StartContext(new PdfDictionary());
                }
                else if (nextToken is DictionaryEndToken)
                {
                    parseContext.EndContext("dictionary");
                }
                else if (nextToken is IndirectObjectEndToken)
                {
                    parseContext.EndContext("indirect-object");
                }
                else if (nextToken is CommentToken)
                {
                    continue; // ignore comments
                }
                else if (nextToken is NullToken nullToken)
                {
                    parseContext.AddToken(nullToken);
                }
                else if (nextToken is BooleanToken booleanToken)
                {
                    parseContext.AddToken(booleanToken);
                }
                else if (nextToken is NumericIntegerToken numericIntegerToken)
                {
                    parseContext.AddToken(numericIntegerToken);
                }
                else if (nextToken is NumericRealToken numericRealToken)
                {
                    parseContext.AddToken(numericRealToken);
                }
                else if (nextToken is StringToken stringToken)
                {
                    parseContext.AddToken(stringToken);
                }
                else if (nextToken is NameToken nameToken)
                {
                    parseContext.AddToken(nameToken);
                }
                else
                {
                    throw new Exception("Unknown token");
                }

                if (parseContext.IsRoot)
                {
                    break;
                }

              




                //if (nextToken == "xref")
                //{
                //    // TODO implement cross-reference table objects

                //    break;
                //}

                //if (nextToken == "trailer")
                //{
                //    // TODO implement trailer objects

                //    break;
                //}


            }

            PdfObject result = parseContext.GetRootValue(); // throws if no value has been set

            if (result is T obj)
            {
                return obj;
            }
            else
            {
                throw new Exception($"Unexpected object type {result.GetType()}");
            }
        }



    }
}
