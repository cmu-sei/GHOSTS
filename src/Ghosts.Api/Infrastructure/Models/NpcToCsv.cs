// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using FileHelpers;

namespace Ghosts.Api.Infrastructure.Models;

[DelimitedRecord(",")]
public class NpcToCsv
{
    public Guid Id { get; set; }
    public string Email { get; set; }
}
