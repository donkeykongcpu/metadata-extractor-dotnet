// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace MetadataExtractor.Formats.Pdf
{
    public class PdfParser
    {
        private class ParseContext
        {
            private readonly Stack<PdfObject> _stack;

            public bool IsRoot => _stack.Peek().Type == "root";

            private string ContextType => _stack.Peek().Type;

            public ParseContext()
            {
                _stack = new Stack<PdfObject>();
                _stack.Push(new PdfRoot()); // make sure stack is never empty
            }

            public void Add(PdfObject pdfObject)
            {
                _stack.Peek().Add(pdfObject);
            }

            public void StartContext(string type)
            {
                PdfObject pdfObject;
                switch (type)
                {
                    case "root": throw new Exception("Cannot start root context");
                    case "indirect-object": pdfObject = new PdfIndirectObject(); break;
                    case "array": pdfObject = new PdfArray(); break;
                    case "dictionary": pdfObject = new PdfDictionary(); break;
                    default: throw new Exception("Invalid type");
                }
                Add(pdfObject);
                _stack.Push(pdfObject);
            }

            public void EndContext(string type)
            {
                if (ContextType != type)
                {
                    throw new Exception("Context type mismatch");
                }
                _stack.Pop();
                if (_stack.Count < 1)
                {
                    throw new Exception("Context underflow");
                }
            }

            public PdfObject GetValue()
            {
                if (_stack.Count != 1)
                {
                    throw new Exception("Invalid context count");
                }
                if (_stack.Peek() is PdfRoot pdfRoot)
                {
                    return pdfRoot.GetRootValue(); // a PdfObject
                }
                else
                {
                    throw new Exception("Invalid context root");
                }
            }
        }

        public static PdfObject ParseObject(string input, int startIndex)
        {
            StringByteProviderSource byteProviderSource = new StringByteProviderSource(input, startIndex, ExtractionDirection.Forward);

            BufferedItemProvider<byte> byteProvider = new BufferedItemProvider<byte>(byteProviderSource, 1024);

            ItemProvider<Token> tokenProvider = GetTokenProvider(byteProvider);

            return ParseObject(tokenProvider);
        }

        public static PdfObject ParseObject(IndexedReader reader, int startIndex)
        {
            IndexedReaderByteProviderSource byteProviderSource = new IndexedReaderByteProviderSource(reader, startIndex, ExtractionDirection.Forward);

            BufferedItemProvider<byte> byteProvider = new BufferedItemProvider<byte>(byteProviderSource, 1024);

            ItemProvider<Token> tokenProvider = GetTokenProvider(byteProvider);

            return ParseObject(tokenProvider);
        }

        private static ItemProvider<Token> GetTokenProvider(ItemProvider<byte> byteProvider)
        {
            PdfTokeniser tokeniser = new PdfTokeniser(byteProvider);

            IEnumerable<Token> tokens = tokeniser.Tokenise();

            EnumeratedItemProviderSource<Token> tokenSource = new EnumeratedItemProviderSource<Token>(tokens, new DummyToken());

            BufferedItemProvider<Token> tokenProvider = new BufferedItemProvider<Token>(tokenSource, 5);

            return tokenProvider;
        }

        public static PdfObject ParseObject(ItemProvider<Token> tokenProvider)
        {
            var parseContext = new ParseContext();

            while (tokenProvider.HasNextItem)
            {
                var nextToken = tokenProvider.GetNextItem();

                // first check if we have either an indirect object (objectNumber generation "obj" ... "endobj")
                // or an indirect reference to an object (objectNumber generation "R")
                // (which spans 3 tokens)

                if (nextToken is NumericIntegerToken
                    && tokenProvider.PeekNextItem(0) is NumericIntegerToken
                    && tokenProvider.PeekNextItem(1) is IndirectObjectBeginToken
                    )
                {
                    tokenProvider.Consume(2);
                    parseContext.StartContext("indirect-object");
                }
                else if (nextToken is NumericIntegerToken objectNumberToken
                    && tokenProvider.PeekNextItem(0) is NumericIntegerToken generationToken
                    && tokenProvider.PeekNextItem(1) is IndirectReferenceMarkerToken
                    )
                {
                    tokenProvider.Consume(2);
                    parseContext.Add(PdfScalarValue.FromIndirectReference(objectNumberToken.IntegerValue, generationToken.IntegerValue));
                }
                else if (nextToken is ArrayBeginToken)
                {
                    parseContext.StartContext("array");
                }
                else if (nextToken is ArrayEndToken)
                {
                    parseContext.EndContext("array");
                }
                else if (nextToken is DictionaryBeginToken)
                {
                    parseContext.StartContext("dictionary");
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
                else
                {
                    parseContext.Add(PdfScalarValue.FromToken(nextToken));
                }

                if (parseContext.IsRoot)
                {
                    break;
                }

              


                //if (nextToken == "stream")
                //{
                //    // TODO implement stream objects

                //    break;
                //}

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

            return parseContext.GetValue(); // throws if no value has been set
        }



    }
}
