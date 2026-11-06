using HtmlAgilityPack;
using NOF.Domain;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Quantum.OfficialPlugins.Zjuam;

internal static class ZdbkResourceReader
{
    private const string Root = "https://zdbk.zju.edu.cn/jwglxt";

    internal static async Task<JsonElement> ReadAsync(
        HttpClient client, string username, ReadZjuamResourceRequest request, CancellationToken cancellationToken)
    {
        var user = Uri.EscapeDataString(username);
        var category = request.Resource == "zdbk-courses" ? GetCategory(request.Category) : default;
        if (request.Resource == "zdbk-courses" && (request.Start < 1 || request.End < request.Start
            || request.End > 1000 || request.End - request.Start > 20))
        {
            throw new DomainException("zdbk_invalid_pages", "The course page range is invalid.");
        }
        if (request.Resource is "zdbk-sections" or "zdbk-introduction" && string.IsNullOrWhiteSpace(request.Course))
        {
            throw new DomainException("zdbk_course_required", "A course identifier is required.");
        }

        var profileUrl = $"{Root}/xsxk/zzxkghb_cxZzxkGhbIndex.html?gnmkdm=N253530&su={user}";
        var profile = await ReadHtmlAsync(client, profileUrl, cancellationToken);
        if (profile.DocumentNode.SelectSingleNode("//*[@id='xkxn']") is null)
        {
            var service = Uri.EscapeDataString($"{Root}/xtgl/login_ssologin.html");
            using var signIn = await client.GetAsync($"https://zjuam.zju.edu.cn/cas/login?service={service}", cancellationToken);
            signIn.EnsureSuccessStatusCode();
            profile = await ReadHtmlAsync(client, profileUrl, cancellationToken);
        }

        var year = Field(profile, "xkxn");
        var semester = Field(profile, "xq");
        var form = new Dictionary<string, string> { ["xn"] = year, ["xq"] = semester };
        var query = $"gnmkdm=N253530&su={user}";
        HttpResponseMessage response;
        switch (request.Resource)
        {
            case "zdbk-courses":
                var grade = Field(profile, "nj");
                var major = Field(profile, "zydm");
                form["dl"] = category.Code;
                form["lx"] = category.Kind;
                form["xkmc"] = category.Label;
                form["nj"] = grade;
                form["zydm"] = major;
                form["jxjhh"] = grade + major;
                form["xnxq"] = $"({year}-{semester})-";
                form["kspage"] = request.Start.ToString(CultureInfo.InvariantCulture);
                form["jspage"] = request.End.ToString(CultureInfo.InvariantCulture);
                response = await PostAsync(client, $"{Root}/xsxk/zzxkghb_cxZzxkGhbKcList.html?{query}", form, cancellationToken);
                break;
            case "zdbk-sections":
                form["xkkh"] = request.Course!;
                response = await PostAsync(client, $"{Root}/xsxk/zzxkghb_cxZzxkGhbJxbList.html?{query}", form, cancellationToken);
                break;
            case "zdbk-selected":
                response = await PostAsync(client, $"{Root}/xsxk/zzxkghb_cxZzxkGhbChoosed.html?{query}", form, cancellationToken);
                break;
            case "zdbk-graduation":
                response = await client.GetAsync($"{Root}/bysh/byshck_cxByshzsIndex.html?doType=query&gnmkdm=N6025&su={user}", cancellationToken);
                break;
            default:
                var id = Uri.EscapeDataString(request.Course!);
                var introduction = await ReadHtmlAsync(client, $"{Root}/xkjjsc/kcjjck_cxXkjjPage.html?xkjjid={id}&htmlType=kcjj", cancellationToken);
                var content = introduction.DocumentNode.SelectSingleNode("//input[@name='xkjjHtml']")
                    ?.GetAttributeValue("value", "") ?? "";
                return JsonSerializer.SerializeToElement(new
                {
                    Text = HtmlEntity.DeEntitize(content)
                }, JsonSerializerOptions.Web);
        }

        using (response)
        {
            response.EnsureSuccessStatusCode();
            var rows = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (rows.ValueKind != JsonValueKind.Array)
            {
                throw new DomainException("zdbk_invalid_response", "The academic system did not return a list.");
            }

            return JsonSerializer.SerializeToElement(new
            {
                Year = year,
                Semester = semester,
                Rows = rows
            }, JsonSerializerOptions.Web);
        }
    }

    private static async Task<HtmlDocument> ReadHtmlAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        var document = new HtmlDocument();
        document.LoadHtml(await client.GetStringAsync(url, cancellationToken));
        return document;
    }

    private static string Field(HtmlDocument document, string id)
    {
        var node = document.DocumentNode.SelectSingleNode($"//*[@id='{id}']");
        var value = node?.GetAttributeValue("value", node.InnerText).Trim();
        return string.IsNullOrEmpty(value)
            ? throw new DomainException("zdbk_session_unavailable", "The academic profile is unavailable. Check the ZJU sign-in and network.")
            : value;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url,
        Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        return await client.PostAsync(url, content, cancellationToken);
    }

    private static (string Code, string Kind, string Label) GetCategory(string? category) => category switch
    {
        "MyCategory" => ("xk_1", "bl", "本类(专业)选课"),
        "CompulsoryAll" => ("xk_b", "bl", "全部课程"),
        "CompulsoryIpm" => ("E", "zl", "思政类\\军体类"),
        "CompulsoryLan" => ("F", "zl", "外语类"),
        "CompulsoryCom" => ("G", "zl", "计算机类"),
        "CompulsoryEtp" => ("P", "zl", "创新创业类"),
        "CompulsorySci" => ("T", "zl", "自然科学通识类"),
        "ElectiveAll" => ("xk_n", "bl", "全部课程"),
        "ElectiveChC" => ("zhct", "zl", "中华传统"),
        "ElectiveGlC" => ("sjwm", "zl", "世界文明"),
        "ElectiveSoc" => ("ddsh", "zl", "当代社会"),
        "ElectiveSci" => ("kjcx", "zl", "科技创新"),
        "ElectiveArt" => ("wysm", "zl", "文艺审美"),
        "ElectiveBio" => ("smts", "zl", "生命探索"),
        "ElectiveTec" => ("byjy", "zl", "博雅技艺"),
        "ElectiveGec" => ("xhxk", "zl", "通识核心课程"),
        "PhysicalEdu" => ("xk_8", "bl", "体育课程"),
        "MajorFundation" => ("xk_zyjckc", "bl", "专业基础课程"),
        "MyMajor" => ("zy_b", "bl", "本类(专业)"),
        "AllMajor" => ("zy_qb", "bl", "所有类(专业)"),
        "AccreditedAll" => ("xk_rdxkc", "bl", "qbkc"),
        "AccreditedArt" => ("xk_rdxkc", "zl", "美育类"),
        "AccreditedLbr" => ("xk_rdxkc", "zl", "劳育类"),
        "International" => ("gjhkc", "zl", "国际化课程"),
        "Ckc" => ("Z", "bl", "竺可桢学院课程"),
        "Honor" => ("R", "bl", "荣誉课程"),
        _ => throw new DomainException("zdbk_invalid_category", "The course category is invalid.")
    };
}
