using Platform.Infrastructure.Tvan.Templating;

namespace Platform.Tvan.Tests;

public class TemplateRendererTests
{
    private readonly ITemplateRenderer _renderer = new TokenTemplateRenderer();

    private static TemplateScope Scope(string payload = "PAYLOAD") => new(
        Msg: new Dictionary<string, string?> { ["MST"] = "0101360697", ["Xml"] = "<HDon a=\"1\"/>" },
        Cred: new Dictionary<string, string?> { ["PartnerGUID"] = "guid-1", ["ma_dvcs"] = "0107726161" },
        Args: new Dictionary<string, string?> { ["CmdType"] = "501" },
        Payload: payload);

    [Fact]
    public void Renders_all_four_scopes()
    {
        var result = _renderer.Render(
            "{{ msg.MST }}|{{ cred.PartnerGUID }}|{{ args.CmdType }}|{{ payload }}", Scope());

        Assert.Equal("0101360697|guid-1|501|PAYLOAD", result);
    }

    [Fact]
    public void Missing_variable_renders_empty_instead_of_throwing()
    {
        // Nhiều trường của QĐ 1450 là optional — thiếu biến không được làm hỏng cả lô hoá đơn.
        Assert.Equal("[]", _renderer.Render("[{{ msg.MTDTChieu }}]", Scope()));
    }

    [Fact]
    public void Json_string_filter_produces_quoted_and_escaped_json()
    {
        var result = _renderer.Render("{\"Xml\":{{ msg.Xml | json_string }}}", Scope());

        Assert.Equal("{\"Xml\":\"\\u003CHDon a=\\u00221\\u0022/\\u003E\"}", result);
        using var parsed = System.Text.Json.JsonDocument.Parse(result);
        Assert.Equal("<HDon a=\"1\"/>", parsed.RootElement.GetProperty("Xml").GetString());
    }

    [Fact]
    public void Minvoice_header_format_is_pure_configuration()
    {
        // Đúng dạng tài liệu Minvoice: "Bear" + khoảng trắng + token + ";" + ma_dvcs
        var result = _renderer.Render(
            "Bear {{ msg.token }};{{ cred.ma_dvcs }}",
            TemplateScope.ForAuth("TOKEN123", Scope().Cred));

        Assert.Equal("Bear TOKEN123;0107726161", result);
    }

    [Fact]
    public void Hilo_header_format_uses_the_same_renderer()
    {
        var result = _renderer.Render(
            "Bearer {{ msg.token }}", TemplateScope.ForAuth("TOKEN123", Scope().Cred));

        Assert.Equal("Bearer TOKEN123", result);
    }

    [Fact]
    public void Unknown_scope_is_rejected_at_render_time()
    {
        var error = Assert.Throws<TvanConfigurationException>(
            () => _renderer.Render("{{ nope.X }}", Scope()));

        Assert.Contains("nope", error.Message);
    }

    [Fact]
    public void Unknown_filter_is_rejected()
    {
        Assert.Throws<TvanConfigurationException>(
            () => _renderer.Render("{{ msg.MST | sha256 }}", Scope()));
    }
}
