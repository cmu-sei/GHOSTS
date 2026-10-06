// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

/// <summary>
/// RFC 6902 JSON Patch over System.Text.Json nodes: add, remove, replace, move, copy and test. The
/// authoring agent revises a validated document with one (D1), so a revision costs the change, not the
/// whole document again.
/// </summary>
public static class ScenarioDocumentPatch
{
    /// <summary>A patched copy of the document. The document itself is not changed.</summary>
    public static JsonNode Apply(JsonNode document, JsonArray patch)
    {
        var root = document?.DeepClone();
        for (var i = 0; i < patch.Count; i++)
        {
            if (patch[i] is not JsonObject op) throw new ScenarioPatchException($"Operation {i} is not an object.");
            var name = Str(op, "op");
            var path = Str(op, "path") ?? throw new ScenarioPatchException($"Operation {i} ({name}) has no path.");
            try
            {
                switch (name)
                {
                    case "add":
                        root = Add(root, path, Value(op));
                        break;
                    case "remove":
                        root = Remove(root, path, out _);
                        break;
                    case "replace":
                        root = Replace(root, path, Value(op));
                        break;
                    case "move":
                        root = Remove(root, From(op), out var moved);
                        root = Add(root, path, moved);
                        break;
                    case "copy":
                        root = Add(root, path, Get(root, From(op))?.DeepClone());
                        break;
                    case "test":
                        if (!JsonNode.DeepEquals(Get(root, path), Value(op)))
                            throw new ScenarioPatchException("the value is not the one the test expects");
                        break;
                    default:
                        throw new ScenarioPatchException($"\"{name}\" is not a JSON Patch operation");
                }
            }
            catch (ScenarioPatchException ex)
            {
                throw new ScenarioPatchException($"Operation {i} ({name} {path}) failed: {ex.Message}.");
            }
        }
        return root;
    }

    private static JsonNode Get(JsonNode root, string path)
    {
        var node = root;
        foreach (var token in Tokens(path))
        {
            node = node switch
            {
                JsonObject o when o.TryGetPropertyValue(token, out var child) => child,
                JsonArray a when int.TryParse(token, out var i) && i >= 0 && i < a.Count => a[i],
                _ => throw new ScenarioPatchException($"nothing is at {path}")
            };
        }
        return node;
    }

    private static JsonNode Add(JsonNode root, string path, JsonNode value)
    {
        if (path.Length == 0) return value;
        var (parent, last) = Parent(root, path);
        switch (parent)
        {
            case JsonObject o:
                o[last] = value;
                break;
            case JsonArray a when last == "-":
                a.Add(value);
                break;
            case JsonArray a when int.TryParse(last, out var i) && i >= 0 && i <= a.Count:
                a.Insert(i, value);
                break;
            default:
                throw new ScenarioPatchException($"cannot add at {path}");
        }
        return root;
    }

    private static JsonNode Replace(JsonNode root, string path, JsonNode value)
    {
        if (path.Length == 0) return value;
        var (parent, last) = Parent(root, path);
        switch (parent)
        {
            case JsonObject o when o.ContainsKey(last):
                o[last] = value;
                break;
            case JsonArray a when int.TryParse(last, out var i) && i >= 0 && i < a.Count:
                a[i] = value;
                break;
            default:
                throw new ScenarioPatchException($"nothing is at {path}");
        }
        return root;
    }

    private static JsonNode Remove(JsonNode root, string path, out JsonNode removed)
    {
        if (path.Length == 0) throw new ScenarioPatchException("cannot remove the whole document");
        var (parent, last) = Parent(root, path);
        switch (parent)
        {
            case JsonObject o when o.TryGetPropertyValue(last, out removed):
                o.Remove(last);
                break;
            case JsonArray a when int.TryParse(last, out var i) && i >= 0 && i < a.Count:
                removed = a[i];
                a.RemoveAt(i);
                break;
            default:
                throw new ScenarioPatchException($"nothing is at {path}");
        }
        return root;
    }

    private static (JsonNode Parent, string Last) Parent(JsonNode root, string path)
    {
        if (path[0] != '/') throw new ScenarioPatchException($"\"{path}\" is not a JSON Pointer");
        var cut = path.LastIndexOf('/');
        return (Get(root, path[..cut]), Unescape(path[(cut + 1)..]));
    }

    /// <summary>A JSON Pointer's reference tokens (RFC 6901): "" is the whole document.</summary>
    private static string[] Tokens(string path)
    {
        if (path.Length == 0) return [];
        if (path[0] != '/') throw new ScenarioPatchException($"\"{path}\" is not a JSON Pointer");
        return path[1..].Split('/').Select(Unescape).ToArray();
    }

    private static string Unescape(string token) => token.Replace("~1", "/").Replace("~0", "~");

    private static JsonNode Value(JsonObject op) =>
        op.TryGetPropertyValue("value", out var value) ? value?.DeepClone() : throw new ScenarioPatchException("it has no value");

    private static string From(JsonObject op) => Str(op, "from") ?? throw new ScenarioPatchException("it has no from");

    private static string Str(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}

public class ScenarioPatchException(string message) : Exception(message);
