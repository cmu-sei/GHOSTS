// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Domain;

namespace Ghosts.Api.Infrastructure.Extensions;

public static class HandlerTypeExtensions
{
    public static bool TryParseHandlerType(this string value, out HandlerType result)
    {
        if (Enum.TryParse(typeof(HandlerType), value, ignoreCase: true, out var parsed) &&
            Enum.IsDefined(typeof(HandlerType), parsed))
        {
            result = (HandlerType)parsed;
            return true;
        }

        result = default;
        return false;
    }
}
