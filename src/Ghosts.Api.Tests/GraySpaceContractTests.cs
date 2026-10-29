// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

namespace Ghosts.Api.Tests;

/// <summary>The contract checker accepts GraySpace-shaped JSON and refuses JSON GraySpace would not send or accept.</summary>
public class GraySpaceContractTests
{
    private const string Job = """
        {"id":"j-1","kind":"create_instance","target":"g1","state":"running","stages":["validate","allocate","ready"],
         "stage":"allocate","created":"2026-10-29T09:00:00Z"}
        """;

    [Fact]
    public void A_job_record_shaped_like_graySpaces_is_valid() =>
        Assert.Empty(GraySpaceContract.Errors("JobRecord", Job));

    [Fact]
    public void A_job_record_with_an_unknown_state_is_not() =>
        Assert.NotEmpty(GraySpaceContract.Errors("JobRecord", Job.Replace("\"running\"", "\"building\"")));

    [Fact]
    public void A_minimal_slice_spec_is_valid_and_one_without_a_prefix_is_not()
    {
        const string spec = """{"apiVersion":"grayspace/v1","kind":"Slice","slug":"office-day","prefix":"od"}""";

        Assert.Empty(GraySpaceContract.SliceErrors(spec));
        Assert.NotEmpty(GraySpaceContract.SliceErrors(spec.Replace(",\"prefix\":\"od\"", "")));
    }

    [Fact]
    public void A_hello_message_is_a_valid_event() =>
        Assert.Empty(GraySpaceContract.EventErrors("""{"type":"hello","schema":"grayspace-events-v1"}"""));
}
