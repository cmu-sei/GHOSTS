// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Comms;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Comms;

public class CheckIdTests
{
    [Fact]
    public void Id_MissingFileOnFirstAccess_RetrievesId()
    {
        var idFile = Path.GetTempFileName();
        File.Delete(idFile);
        var checks = 0;
        var checkId = new CheckId(idFile, () =>
        {
            checks++;
            return Task.FromResult("first-start-id");
        });

        Assert.Equal("first-start-id", checkId.Id);
        Assert.Equal(1, checks);
    }

    [Fact]
    public void Id_ExistingFile_ReturnsStoredIdWithoutRetrieval()
    {
        var idFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(idFile, "existing-id");
            var checkId = new CheckId(idFile, () => throw new InvalidOperationException());

            Assert.Equal("existing-id", checkId.Id);
        }
        finally
        {
            File.Delete(idFile);
        }
    }

    [Fact]
    public void Id_UnsuccessfulInitialRetrieval_ReturnsEmptyAndWaitsBeforeRetrying()
    {
        var idFile = Path.GetTempFileName();
        File.Delete(idFile);
        var checks = 0;
        var checkId = new CheckId(idFile, () =>
        {
            checks++;
            return Task.FromResult(string.Empty);
        });

        Assert.Equal(string.Empty, checkId.Id);
        Assert.Equal(string.Empty, checkId.Id);
        Assert.Equal(1, checks);
    }
}
