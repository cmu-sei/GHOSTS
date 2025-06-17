// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json;

namespace Ghosts.Client.Infrastructure
{
    /// <summary>
    /// Creates random text for emails - uses dictionary.json in the config folder
    /// </summary>
    public class RandomText : IDisposable
    {
        private static readonly Random _random = new Random();
        private StringBuilder _builder;
        private IEnumerable<string> _words;

        public string Content => _builder.ToString();

        public void Dispose()
        {
            this._builder = null;
            this._words = null;
        }

        public static char GetRandomCapitalLetter()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            return chars[_random.Next(0, chars.Length)];
        }

        public static char GetRandomCapitalLetter(char after)
        {
            after = char.ToUpper(after);
            var index = after % 32;

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            return chars[_random.Next(index, chars.Length)];
        }

        public RandomText(IEnumerable<string> words)
        {
            _builder = new StringBuilder();
            _words = words;
        }

        public string FormatContent(int SentenceLengthMin, int SentenceLengthMax)
        {
            //format the content
            StringBuilder builder = new StringBuilder();
            string[] paragraphs = _builder.ToString().Split('\n');
            string newLine = Environment.NewLine;  //format using platform new line
            foreach (var pstring in paragraphs)
            {
                if (pstring == "")
                {
                    //marks the end of a paragraph
                    builder.Append(newLine);
                    builder.Append(newLine);
                }
                else
                {
                    //format
                    var index = 0;
                    while (true)
                    {
                        var sentenceLength = _random.Next(SentenceLengthMin, SentenceLengthMax);
                        var endIndex = index + sentenceLength;
                        if (endIndex >= pstring.Length) endIndex = pstring.Length;
                        while (endIndex < pstring.Length && pstring[endIndex] != ' ') endIndex++;
                        if (endIndex == pstring.Length)
                        {
                            builder.Append(pstring.Substring(index).Trim());
                            builder.Append(newLine);
                            break;
                        }
                        else
                        {
                            builder.Append(pstring.Substring(index, endIndex - index).Trim());
                            builder.Append(newLine);
                            index = endIndex;
                        }

                    }
                }
            }
            return builder.ToString();
        }

        public void AddContentParagraphs(int minParagraphs, int maxParagraphs)
        {
            var paragraphs = _random.Next(minParagraphs, maxParagraphs);
            AddContentParagraphs(paragraphs, paragraphs, (paragraphs + 10), (paragraphs * 10), (paragraphs * 25));
        }

        public void AddContentParagraphs(int numberParagraphs, int minSentences, int maxSentences, int minWords, int maxWords)
        {
            for (var i = 0; i < numberParagraphs; i++)
            {
                AddParagraph(_random.Next(minSentences, maxSentences + 1), minWords, maxWords);
                _builder.Append("\n\n");
            }
        }

        private void AddParagraph(int numberSentences, int minWords, int maxWords)
        {
            for (var i = 0; i < numberSentences; i++)
            {
                var count = _random.Next(minWords, maxWords + 1);
                AddSentence(count);
            }
        }

        public void AddSentence(int numberWords)
        {
            var sentence = string.Join(" ", _words.PickRandom(numberWords)).Trim() + ". ";
            // Uppercase sentence
            sentence = char.ToUpper(sentence[0]) + sentence.Substring(1);
            // Add this sentence to the class
            _builder.Append(sentence);
        }

        public static class GetDictionary
        {
            public static List<string> GetDictionaryList()
            {
                var list = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(ClientConfigurationResolver.Dictionary));
                return list;
            }
        }
    }
}