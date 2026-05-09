using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Controllers.Api;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Ghosts.Domain;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Ghosts.Api.Tests.Controllers;

public class ClientIdControllerTests
{
    private readonly Mock<IClientIdService> _service = new();
    private readonly ClientIdController _controller;

    public ClientIdControllerTests()
    {
        _controller = new ClientIdController(_service.Object);
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    [Fact]
    public async Task Get_ReturnsOk_WhenServiceSucceeds()
    {
        var expected = Guid.NewGuid();
        _service.Setup(s => s.GetMachineIdAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, expected, string.Empty));

        var result = await _controller.Index(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expected, ok.Value);
    }

    [Fact]
    public async Task Get_ReturnsNewId_WhenClientHasNoIdButValidHeaders()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["ghosts-name"] = "workstation1";
        httpContext.Request.Headers["ghosts-fqdn"] = "workstation1.corp.local";
        httpContext.Request.Headers["ghosts-host"] = "workstation1";
        httpContext.Request.Headers["ghosts-domain"] = "corp.local";
        httpContext.Request.Headers["ghosts-resolvedhost"] = "workstation1";
        httpContext.Request.Headers["ghosts-ip"] = "10.0.0.5";
        httpContext.Request.Headers["ghosts-user"] = "DavidLightman";
        httpContext.Request.Headers["ghosts-version"] = "8.0.0";
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        _service.Setup(s => s.GetMachineIdAsync(httpContext, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, Guid.NewGuid(), string.Empty));

        var result = await _controller.Index(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returnedId = Assert.IsType<Guid>(ok.Value);
        Assert.NotEqual(Guid.Empty, returnedId);
    }

    [Fact]
    public async Task Get_Returns401_WhenServiceFails()
    {
        _service.Setup(s => s.GetMachineIdAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, Guid.Empty, "Missing headers"));

        var result = await _controller.Index(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
    }
}

public class ClientResultsControllerTests
{
    private readonly Mock<IClientResultsService> _service = new();
    private readonly ClientResultsController _controller;

    public ClientResultsControllerTests()
    {
        _controller = new ClientResultsController(_service.Object);
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    [Fact]
    public async Task Post_ReturnsNoContent_WhenResultProcessed()
    {
        _service.Setup(s => s.ProcessResultAsync(It.IsAny<HttpContext>(), It.IsAny<TransferLogDump>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.Index(new TransferLogDump { Log = "test" }, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Post_ReturnsUnauthorized_WhenResultRejected()
    {
        _service.Setup(s => s.ProcessResultAsync(It.IsAny<HttpContext>(), It.IsAny<TransferLogDump>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.Index(new TransferLogDump { Log = "test" }, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid machine or payload", unauthorized.Value);
    }

    [Fact]
    public async Task PostSecure_ReturnsNoContent_WhenEncryptedPayloadProcessed()
    {
        _service.Setup(s => s.ProcessEncryptedAsync(It.IsAny<HttpContext>(), It.IsAny<EncryptedPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.Secure(new EncryptedPayload { Payload = "encrypted" }, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task PostSecure_ReturnsUnauthorized_WhenEncryptedPayloadRejected()
    {
        _service.Setup(s => s.ProcessEncryptedAsync(It.IsAny<HttpContext>(), It.IsAny<EncryptedPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.Secure(new EncryptedPayload { Payload = "bad" }, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid machine or payload", unauthorized.Value);
    }
}

public class ClientSurveyControllerTests
{
    private readonly Mock<IClientSurveyService> _service = new();
    private readonly ClientSurveyController _controller;

    public ClientSurveyControllerTests()
    {
        _controller = new ClientSurveyController(_service.Object);
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    [Fact]
    public async Task Post_ReturnsNoContent_WhenSurveyProcessed()
    {
        _service.Setup(s => s.ProcessSurveyAsync(It.IsAny<HttpContext>(), It.IsAny<Survey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.Index(new Survey(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Post_ReturnsUnauthorized_WhenSurveyRejected()
    {
        _service.Setup(s => s.ProcessSurveyAsync(It.IsAny<HttpContext>(), It.IsAny<Survey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.Index(new Survey(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid survey request", unauthorized.Value);
    }

    [Fact]
    public async Task PostSecure_ReturnsNoContent_WhenEncryptedSurveyProcessed()
    {
        _service.Setup(s => s.ProcessEncryptedSurveyAsync(It.IsAny<HttpContext>(), It.IsAny<EncryptedPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.Secure(new EncryptedPayload { Payload = "enc" }, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task PostSecure_ReturnsBadRequest_WhenEncryptedSurveyRejected()
    {
        _service.Setup(s => s.ProcessEncryptedSurveyAsync(It.IsAny<HttpContext>(), It.IsAny<EncryptedPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.Secure(new EncryptedPayload { Payload = "bad" }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Malformed or unauthorized encrypted survey", badRequest.Value);
    }
}

public class ClientTimelineControllerTests
{
    private readonly Mock<IClientTimelineService> _service = new();
    private readonly ClientTimelineController _controller;

    public ClientTimelineControllerTests()
    {
        _controller = new ClientTimelineController(_service.Object);
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    [Fact]
    public async Task Post_ReturnsOk_WhenTimelineProcessed()
    {
        var payload = new { saved = true };
        _service.Setup(s => s.ProcessTimelineAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (object)payload, string.Empty));

        var result = await _controller.Index("{}", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(payload, ok.Value);
    }

    [Fact]
    public async Task Post_ReturnsBadRequest_WhenTimelineRejected()
    {
        _service.Setup(s => s.ProcessTimelineAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, null, "Invalid timeline json"));

        var result = await _controller.Index("bad", CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid timeline json", badRequest.Value);
    }
}

public class ClientUpdatesControllerTests
{
    private readonly Mock<IClientUpdateService> _service = new();
    private readonly ClientUpdatesController _controller;

    public ClientUpdatesControllerTests()
    {
        _controller = new ClientUpdatesController(_service.Object);
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    [Fact]
    public async Task Get_ReturnsJson_WhenUpdateAvailable()
    {
        var update = new UpdateClientConfig { Type = UpdateClientConfig.UpdateType.Timeline, Update = "{}" };
        _service.Setup(s => s.GetUpdateAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, update, StatusCodes.Status200OK, string.Empty));

        var result = await _controller.Index(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(update, json.Value);
    }

    [Fact]
    public async Task Get_Returns401_WhenUnauthorized()
    {
        _service.Setup(s => s.GetUpdateAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, null, StatusCodes.Status401Unauthorized, "Invalid machine"));

        var result = await _controller.Index(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
        Assert.Equal("Invalid machine", status.Value);
    }

    [Fact]
    public async Task Get_Returns404_WhenNoUpdateAvailable()
    {
        _service.Setup(s => s.GetUpdateAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, null, StatusCodes.Status404NotFound, "No update available"));

        var result = await _controller.Index(CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("No update available", notFound.Value);
    }
}
