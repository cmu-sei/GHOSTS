using Microsoft.AspNetCore.Http;
using Ghosts.Api.Infrastructure;
using Xunit;

namespace Ghosts.Api.Tests;

public class WebRequestReaderTests
{
    [Fact]
    public void GetMachine_MissingHeaders_ShouldHandleGracefully()
    {
        // ARRANGE: A context with absolutely no identity headers
        var context = new DefaultHttpContext(); 

        // ACT
        // On the virgin code, this line will trigger the ArgumentNullException 
        // (and eventually a NullReferenceException).
        var result = WebRequestReader.GetMachine(context);

        // ASSERT: We expect a valid object back, even if fields are null
        Assert.NotNull(result);
    }
}
