// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ghosts.Domain.Code
{
    public class BuildHandlerArgVariables
    {
        private static readonly Random Random = new Random();

        public static Dictionary<string, string> BuildHandlerArgs(TimelineHandler handler)
        {
            var handlerArgs = handler.HandlerArgs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

            // Process each placeholder in HandlerArgs
            foreach (var key in handlerArgs.Keys.ToList())
            {
                handlerArgs[key] = ProcessPlaceholder(handlerArgs[key]);
            }

            return handlerArgs;
        }

        private static string ProcessPlaceholder(string value)
        {
            // Replace {guid} placeholders
            value = Regex.Replace(value, @"\{guid\}", match => Guid.NewGuid().ToString());

            // Replace {random_file:directory} placeholders
            value = Regex.Replace(value, @"\{random_file:([^}]+)\}", match =>
            {
                var directoryPath = match.Groups[1].Value;
                directoryPath = Environment.ExpandEnvironmentVariables(directoryPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
                return GetRandomFile(directoryPath);
            });

            // Replace {name} placeholders
            value = Regex.Replace(value, @"\{name\}", match => $"file_{Guid.NewGuid()}_{DateTime.Now:yyyyMMddHHmmss}");

            return value;
        }


        private static string GetRandomFile(string directoryPath)
        {
            var files = Directory.GetFiles(directoryPath);
            if (files.Length == 0)
            {
                throw new InvalidOperationException("No files found in the specified directory.");
            }

            var randomIndex = Random.Next(files.Length);
            return files[randomIndex];
        }

        public static string ReplaceCommandVariables(string command, Dictionary<string, string> variables)
        {
            foreach (var variable in variables)
            {
                command = command.Replace($"{{{variable.Key}}}", variable.Value);
            }
            return command;
        }
    }
}
