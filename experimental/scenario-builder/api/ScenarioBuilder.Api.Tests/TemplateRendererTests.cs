// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Tests;

public class TemplateRendererTests
{
    private static readonly Domain.Kind Tabletop = new KindCatalog(ApiFactory.KindsDirectory).Find(TestApi.Kind)!;

    private static TemplateSpec Spec(string body, TemplateFormat format = TemplateFormat.Html, string name = "Brief") =>
        new("brief", name, Audience.Designers, format, body);

    private static RenderInput Input(JsonObject? scenario = null, int openFindings = 2) => new(
        scenario ?? new JsonObject { ["name"] = "Harbor", ["objectives"] = new JsonArray(new JsonObject { ["id"] = "O1", ["text"] = "Assess" }) },
        new Dictionary<string, string> { ["timeSlot"] = "Time slot" },
        Tabletop, "Harbor", "Basics", 3, openFindings, new DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Renders_liquid_against_the_scenario_and_labels()
    {
        var text = TemplateRenderer.Render(
            Spec("{{ scenario.name }} {% for o in scenario.objectives %}[{{ o.text }}]{% endfor %} {{ labels.timeSlot }} {{ library.version }}"), Input());

        Assert.Equal("Harbor [Assess] Time slot 3", text);
    }

    [Fact]
    public void Html_values_are_escaped()
    {
        var input = Input(new JsonObject { ["name"] = "<b>x</b>" });

        Assert.Equal("&lt;b&gt;x&lt;/b&gt;", TemplateRenderer.Render(Spec("{{ scenario.name }}"), input));
    }

    [Fact]
    public void Markdown_values_are_raw()
    {
        var input = Input(new JsonObject { ["name"] = "<b>x</b>" });

        Assert.Equal("<b>x</b>", TemplateRenderer.Render(Spec("{{ scenario.name }}", TemplateFormat.Markdown), input));
    }

    [Fact]
    public void Missing_values_render_empty()
    {
        Assert.Equal("[]", TemplateRenderer.Render(Spec("[{{ scenario.nope.deeper }}]"), Input()));
    }

    [Fact]
    public void Draft_is_true_while_findings_are_open()
    {
        Assert.Equal("draft", TemplateRenderer.Render(Spec("{% if draft %}draft{% else %}final{% endif %}"), Input()));
        Assert.Equal("final", TemplateRenderer.Render(Spec("{% if draft %}draft{% else %}final{% endif %}"), Input(openFindings: 0)));
    }

    [Fact]
    public void Parse_error_names_the_template_and_line()
    {
        var error = TemplateRenderer.Check(Spec("ok\n{% for x in y %}never closed", name: "Design summary"));

        Assert.NotNull(error);
        Assert.StartsWith("Design summary, line 2:", error);
    }

    [Fact]
    public void Loop_limit_stops_a_runaway_template()
    {
        var error = Assert.Throws<TemplateError>(() => TemplateRenderer.Render(Spec("{% for i in (1..100000) %}x{% endfor %}"), Input()));

        Assert.Contains("Brief", error.Message);
    }

    [Fact]
    public void Oversized_body_is_refused()
    {
        Assert.Contains("200,000", TemplateRenderer.Check(Spec(new string('x', 200_001))));
        Assert.Null(TemplateRenderer.Check(Spec("fine")));
    }

    [Fact]
    public void House_page_has_title_banner_and_footer()
    {
        var page = TemplateRenderer.Page(Spec("<p>Hi</p>"), Input(), "<p>Hi</p>");
        var final = TemplateRenderer.Page(Spec("<p>Hi</p>"), Input(openFindings: 0), "<p>Hi</p>");

        Assert.Contains("<title>Brief for Harbor</title>", page);
        Assert.Contains("charset=\"utf-8\"", page);
        Assert.Contains("Draft: 2 findings open.", page);
        Assert.Contains("Made from Basics, version 3, on 30 October 2026.", page);
        Assert.Contains("<p>Hi</p>", page);
        Assert.DoesNotContain("Draft:", final);
    }

    [Fact]
    public void Liquid_filters_are_available()
    {
        var input = Input(new JsonObject { ["terms"] = new JsonArray("grid", "feeder") });

        Assert.Equal("grid, feeder 2", TemplateRenderer.Render(Spec("{{ scenario.terms | join: ', ' }} {{ scenario.terms | size }}"), input));
    }

    [Fact]
    public void Includes_are_refused()
    {
        var error = Assert.Throws<TemplateError>(() => TemplateRenderer.Render(Spec("{% include '/etc/passwd' %}"), Input()));

        Assert.StartsWith("Brief, line 1:", error.Message);
    }

    [Fact]
    public void Regex_builtins_are_not_available()
    {
        // A backtracking pattern runs inside one expression, where the time limit can't stop it.
        var error = Assert.Throws<TemplateError>(() =>
            TemplateRenderer.Render(Spec("{{ 'aaaaaaaaaaaaaaaaaaaaaaaa!' | regex.replace '(a+)+$' 'x' }}"), Input()));

        Assert.Contains("regex.replace", error.Message);
    }

    [Fact]
    public void A_slow_template_is_stopped_at_two_seconds()
    {
        // Under the loop limit, but each pass rewrites a megabyte.
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var error = Assert.Throws<TemplateError>(() => TemplateRenderer.Render(
            Spec("{% assign s = 'x' | string.pad_left 1000000 %}{% for i in (1..9000) %}{% assign t = s | string.replace 'x' 'yy' %}{% endfor %}"), Input()));

        Assert.InRange(clock.Elapsed.TotalSeconds, 1.5, 5);
        Assert.Equal("Brief: it ran longer than 2 seconds.", error.Message);
    }

    [Fact]
    public void Markdown_shows_raw_html_from_answers_as_text()
    {
        var spec = Spec("# Brief\n\n{{ scenario.problem }}", TemplateFormat.Markdown);
        var input = Input(new JsonObject { ["problem"] = "Escalate to <SOC lead>. <meta http-equiv=\"refresh\" content=\"0;url=https://example.invalid/\">" });

        var page = TemplateRenderer.Page(spec, input, TemplateRenderer.Render(spec, input));

        Assert.DoesNotContain("<meta http-equiv", page);
        Assert.Contains("Escalate to &lt;SOC lead&gt;.", page);
    }

    [Fact]
    public void A_preview_footer_names_the_draft_not_a_version()
    {
        var page = TemplateRenderer.Page(Spec("<p>Hi</p>"), Input() with { LibraryVersion = 0 }, "<p>Hi</p>");

        Assert.Contains("Previewed from the draft of Basics on 30 October 2026.", page);
        Assert.DoesNotContain("version 0", page);
    }

    [Fact]
    public void A_full_page_template_keeps_its_own_page_and_gets_the_banner()
    {
        const string own = "<!doctype html><html><head><title>Mine</title></head><body class=\"x\"><p>Hi</p></body></html>";

        var page = TemplateRenderer.Page(Spec(own), Input(), own);

        Assert.Contains("<title>Mine</title>", page);
        Assert.DoesNotContain("Made from", page);
        Assert.Contains("<body class=\"x\"><div class=\"draft\">Draft: 2 findings open.</div>", page);
    }

    [Fact]
    public void Markdown_preview_is_rendered_and_the_file_starts_with_the_draft_line()
    {
        var spec = Spec("# Brief", TemplateFormat.Markdown);

        Assert.Contains("<h1>Brief</h1>", TemplateRenderer.Page(spec, Input(), "# Brief"));
        Assert.StartsWith("> Draft: 2 findings open.\n\n# Brief", TemplateRenderer.MarkdownFile(Input(), "# Brief"));
        Assert.Equal("# Brief", TemplateRenderer.MarkdownFile(Input(openFindings: 0), "# Brief"));
    }
}
