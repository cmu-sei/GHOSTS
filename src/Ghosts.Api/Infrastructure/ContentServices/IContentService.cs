// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading.Tasks;

namespace Ghosts.Api.Infrastructure.ContentServices;

public interface IContentService
{
    Task<string> ExecuteQuery(string prompt);
}
