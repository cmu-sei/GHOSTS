// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using Ghosts.Client.Universal.Handlers;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Handlers;

public class GuacamoleHelperTests
{
    [Fact]
    public void ParseEntry_ThreeParts_ReturnsAllFields()
    {
        Assert.True(GuacamoleHelper.ParseEntry("OT-scada-ssh|engineer|whoami;hostname", out var connection, out var credKey, out var commands));
        Assert.Equal("OT-scada-ssh", connection);
        Assert.Equal("engineer", credKey);
        Assert.Equal("whoami;hostname", commands);
    }

    [Fact]
    public void ParseEntry_TwoParts_HasNoCommands()
    {
        Assert.True(GuacamoleHelper.ParseEntry("OT-hmi-rdp|", out var connection, out var credKey, out var commands));
        Assert.Equal("OT-hmi-rdp", connection);
        Assert.Equal(string.Empty, credKey);
        Assert.Equal(string.Empty, commands);
        Assert.Empty(GuacamoleHelper.SplitCommands(commands));
    }

    [Fact]
    public void ParseEntry_EmptyCredKey_IsAccepted()
    {
        Assert.True(GuacamoleHelper.ParseEntry("ssh-stored||touch /tmp/m2", out _, out var credKey, out var commands));
        Assert.Equal(string.Empty, credKey);
        Assert.Equal("touch /tmp/m2", commands);
    }

    [Fact]
    public void ParseEntry_PipeInCommands_IsKept()
    {
        Assert.True(GuacamoleHelper.ParseEntry("ssh|user|echo x | tr x y > /tmp/pipe", out _, out _, out var commands));
        Assert.Equal("echo x | tr x y > /tmp/pipe", commands);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-separator")]
    [InlineData("|user|whoami")]
    public void ParseEntry_Invalid_ReturnsFalse(string entry)
    {
        Assert.False(GuacamoleHelper.ParseEntry(entry, out _, out _, out _));
    }

    [Fact]
    public void SplitCommands_SplitsTrimsAndDropsEmpty()
    {
        Assert.Equal(new List<string> { "whoami", "hostname" }, GuacamoleHelper.SplitCommands(" whoami ; ;hostname;"));
    }

    [Fact]
    public void SplitCommands_EscapedSemicolon_IsLiteral()
    {
        Assert.Equal(new List<string> { "echo m1 > /tmp/m1", "echo a;b > /tmp/semi" },
            GuacamoleHelper.SplitCommands(@"echo m1 > /tmp/m1;echo a\;b > /tmp/semi"));
    }

    [Fact]
    public void ToKeysyms_Ascii_MapsToItself()
    {
        Assert.Equal(new List<int> { 'l', 's', ' ', '-', '~' }, GuacamoleHelper.ToKeysyms("ls -~"));
    }

    [Fact]
    public void ToKeysyms_EnterAndTab_MapToSpecialKeys()
    {
        Assert.Equal(new List<int> { 0xff09, 'a', 0xff0d }, GuacamoleHelper.ToKeysyms("\ta\n"));
    }

    [Fact]
    public void ToKeysyms_Latin1_MapsToItself()
    {
        Assert.Equal(new List<int> { 0xe9 }, GuacamoleHelper.ToKeysyms("é"));
    }

    [Fact]
    public void ToKeysyms_AboveLatin1_UsesUnicodeKeysym()
    {
        Assert.Equal(new List<int> { 0x010020ac }, GuacamoleHelper.ToKeysyms("€"));
    }

    [Fact]
    public void ToKeysyms_OtherControlChars_AreSkipped()
    {
        Assert.Equal(new List<int> { 'a', 0xff0d }, GuacamoleHelper.ToKeysyms("\u0001a\r\n\u007f\u0085"));
    }

    [Fact]
    public void SplitCommands_DoubleBackslashSemicolon_KeepsOneBackslash()
    {
        Assert.Equal(new List<string> { @"find . -exec ls {} \;" }, GuacamoleHelper.SplitCommands(@"find . -exec ls {} \\;"));
    }

    [Fact]
    public void ToKeysyms_OutsideBmp_IsSkipped()
    {
        Assert.Equal(new List<int> { 'a' }, GuacamoleHelper.ToKeysyms("a\U0001F600"));
    }
}
