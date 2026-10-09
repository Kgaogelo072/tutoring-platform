using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EngineeredTutors.Api.Data;

namespace EngineeredTutors.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class AdminTests(ApiFixture api)
{
    private static readonly JsonSerializerOptions J = ApiFixture.Json;

    private async Task<(string Phone, string Id, Guid LearnerId, string Token)> RegisterAsync(string first, string surname)
    {
        var phone = TestIds.Phone();
        var id = TestIds.SaId();
        var client = api.CreateClient();
        using var res = await client.PostAsJsonAsync("/api/registrations", new
        {
            parent = new { fullName = "Admin Test Parent", phone, email = $"{Guid.NewGuid():N}@example.com", source = "School" },
            learners = new[]
            {
                new
                {
                    firstName = first, surname, school = "Test High", grade = 11,
                    subjects = new[] { new { subject = "Maths", latestMark = (int?)61 } },
                    goal = "Get a distinction", idType = "SA_ID", idNumber = id,
                },
            },
            isGuardianOver18 = true, consentAccepted = true,
        }, J);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(J);
        var url = body.GetProperty("links")[0].GetProperty("tests")[0].GetProperty("url").GetString()!;
        var learnerId = api.WithDb(db => db.Learners.Single(l => l.FirstName == first && l.Surname == surname).Id);
        return (phone, id, learnerId, url[(url.LastIndexOf('/') + 1)..]);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement>(J);
    }

    [Theory]
    [InlineData("/api/admin/leads")]
    [InlineData("/api/admin/assessments")]
    [InlineData("/api/admin/export/leads.csv")]
    public async Task AD01_admin_routes_require_sign_in(string url)
    {
        var anon = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(url)).StatusCode);

        var wrongKey = api.CreateClient();
        wrongKey.DefaultRequestHeaders.Add("X-Dev-Admin-Key", "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrongKey.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task AD02_leads_search_by_learner_name_parent_phone_or_ID_number()
    {
        var unique = "Zz" + Guid.NewGuid().ToString("N")[..8];
        var (phone, id, _, _) = await RegisterAsync("Mpho", unique);
        var admin = api.Admin();

        async Task<int> Count(string q) =>
            (await Json(await admin.GetAsync($"/api/admin/leads?search={Uri.EscapeDataString(q)}")))
            .EnumerateArray().Count(l => l.GetProperty("learners").EnumerateArray()
                .Any(x => x.GetProperty("name").GetString()!.Contains(unique)));

        Assert.Equal(1, await Count(unique.ToLowerInvariant()));
        Assert.Equal(1, await Count(phone));
        Assert.Equal(1, await Count("+27" + phone[1..]));
        Assert.Equal(1, await Count(id));
        Assert.Equal(0, await Count(TestIds.SaId()));

        var lead = (await Json(await admin.GetAsync($"/api/admin/leads?search={unique}")))[0];
        var learner = lead.GetProperty("learners")[0];
        Assert.Equal("•••• " + id[^4..], learner.GetProperty("idMasked").GetString());
        Assert.Equal("NotStarted", learner.GetProperty("tests")[0].GetProperty("status").GetString());
        Assert.DoesNotContain(id, lead.GetRawText());
    }

    [Fact]
    public async Task AD03_lead_status_and_notes_can_be_updated()
    {
        var unique = "Yy" + Guid.NewGuid().ToString("N")[..8];
        await RegisterAsync("Karabo", unique);
        var admin = api.Admin();
        var lead = (await Json(await admin.GetAsync($"/api/admin/leads?search={unique}")))[0];
        var leadId = lead.GetProperty("id").GetGuid();

        var updated = await Json(await admin.PatchAsJsonAsync($"/api/admin/leads/{leadId}",
            new { status = "CallBooked", notes = "Call Tuesday 18:00" }, J));
        Assert.Equal("CallBooked", updated.GetProperty("status").GetString());

        var filtered = await Json(await admin.GetAsync("/api/admin/leads?status=CallBooked"));
        Assert.Contains(filtered.EnumerateArray(), l => l.GetProperty("id").GetGuid() == leadId &&
                                                        l.GetProperty("notes").GetString() == "Call Tuesday 18:00");
    }

    [Fact]
    public async Task AD04_learner_detail_shows_topics_answers_and_masked_ID()
    {
        var (_, id, learnerId, token) = await RegisterAsync("Bongani", "Detail" + Guid.NewGuid().ToString("N")[..6]);
        var client = api.CreateClient();
        (await client.PostAsync($"/api/attempts/{token}/start", null)).EnsureSuccessStatusCode();
        var qs = (await Json(await client.GetAsync($"/api/attempts/{token}/questions"))).GetProperty("questions");
        await client.PutAsJsonAsync($"/api/attempts/{token}/responses/{qs[1].GetProperty("id").GetGuid()}", new { numericRaw = "5,05" }, J);
        api.Clock.Advance(TimeSpan.FromMinutes(3));
        await client.PostAsync($"/api/attempts/{token}/submit", null);

        var detail = await Json(await api.Admin().GetAsync($"/api/admin/learners/{learnerId}"));
        Assert.Equal("•••• " + id[^4..], detail.GetProperty("learner").GetProperty("idMasked").GetString());
        Assert.DoesNotContain(id, detail.GetRawText());

        var attempt = detail.GetProperty("attempts")[0];
        Assert.Equal(33.33m, attempt.GetProperty("overallPct").GetDecimal());
        Assert.Equal(180, attempt.GetProperty("timeTakenSeconds").GetInt32());
        var topics = attempt.GetProperty("topics").EnumerateArray().ToList();
        Assert.Contains(topics, t => t.GetProperty("name").GetString() == "Trigonometry" && t.GetProperty("band").GetString() == "Strong");
        Assert.Contains(topics, t => t.GetProperty("band").GetString() == "PriorityGap");

        var row = attempt.GetProperty("questions")[1];
        Assert.Equal("5,05", row.GetProperty("answerGiven").GetString());
        Assert.Equal("5.05 ± 0.01 N", row.GetProperty("correctAnswer").GetString());
        Assert.True(row.GetProperty("isCorrect").GetBoolean());
        Assert.Equal("Trigonometry", row.GetProperty("section").GetString());
    }

    [Fact]
    public async Task AD07_preview_shows_the_learner_view_without_answers_or_an_attempt()
    {
        var before = api.WithDb(db => db.Attempts.Count());
        using var res = await api.Admin().GetAsync($"/api/admin/assessments/{api.MathsAssessmentId}/preview");
        var raw = await res.Content.ReadAsStringAsync();
        res.EnsureSuccessStatusCode();
        Assert.Contains("Simplify", raw);
        Assert.DoesNotContain("isCorrect", raw);
        Assert.DoesNotContain("tolerance", raw);
        Assert.Equal(before, api.WithDb(db => db.Attempts.Count()));
    }

    [Fact]
    public async Task AD06_SC06_import_creates_a_new_draft_version_and_publishing_twice_is_refused()
    {
        var admin = api.Admin();
        using var res = await ApiFixture.PostCsvAsync(admin, Subject.Maths, TestCsv.Maths, 45);
        var body = await Json(res);
        var id = body.GetProperty("assessmentId").GetGuid();
        Assert.True(body.GetProperty("version").GetInt32() >= 2);
        Assert.Equal(6, body.GetProperty("totalMarks").GetInt32());

        var patched = await admin.PatchAsJsonAsync($"/api/admin/assessments/{id}", new { timeLimitMinutes = 50 }, J);
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        // Put it back: other tests rely on the 45-minute Maths default.
        await admin.PatchAsJsonAsync($"/api/admin/assessments/{id}", new { timeLimitMinutes = 45 }, J);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/assessments/{id}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/admin/assessments/{id}/publish", null)).StatusCode);
        var locked = await admin.PatchAsJsonAsync($"/api/admin/assessments/{id}", new { timeLimitMinutes = 30 }, J);
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);

        // New registrations use the latest published version.
        var (_, _, _, token) = await RegisterAsync("Version", "Check" + Guid.NewGuid().ToString("N")[..6]);
        var hash = Security.AttemptTokens.Hash(token);
        Assert.Equal(id, api.WithDb(db => db.Attempts.Single(a => a.TokenHash == hash).AssessmentId));
    }

    [Fact]
    public async Task AD08_export_has_a_row_per_learner_test_and_no_full_ID()
    {
        var unique = "Xx" + Guid.NewGuid().ToString("N")[..8];
        var (_, id, _, _) = await RegisterAsync("Export", unique);
        using var res = await api.Admin().GetAsync("/api/admin/export/leads.csv");
        res.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", res.Content.Headers.ContentType?.MediaType);
        var csv = await res.Content.ReadAsStringAsync();
        Assert.StartsWith("\"registered_sast\"", csv.TrimStart('﻿'));
        Assert.Contains(unique, csv);
        Assert.Contains("•••• " + id[^4..], csv);
        Assert.DoesNotContain(id, csv);
    }
}
