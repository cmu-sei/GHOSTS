// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Ghosts.Client.Infrastructure
{
    /// <summary>
    /// Creates random text for emails - uses dictionary.json in the config folder
    /// </summary>
    public class RandomText
    {
        private static readonly Random _random = new Random();
        private readonly StringBuilder _builder;
        private readonly string[] _words;

        public static char GetRandomCapitalLetter()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var rand = new Random();
            return chars[rand.Next(0, chars.Length)];
        }

        public static char GetRandomCapitalLetter(char after)
        {
            after = char.ToUpper(after);
            var index = (int)after % 32;

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var rand = new Random();
            return chars[rand.Next(index, chars.Length)];
        }

        public RandomText(string[] words)
        {
            _builder = new StringBuilder();
            _words = words;
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
            var b = new StringBuilder();
            // Add n words together.
            for (var i = 0; i < numberWords; i++) // Number of words
            {
                b.Append(_words[_random.Next(_words.Length)]).Append(" ");
            }
            var sentence = b.ToString().Trim() + ". ";
            // Uppercase sentence
            sentence = char.ToUpper(sentence[0]) + sentence.Substring(1);
            // Add this sentence to the class
            _builder.Append(sentence);
        }

        public string Content => _builder.ToString();


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
