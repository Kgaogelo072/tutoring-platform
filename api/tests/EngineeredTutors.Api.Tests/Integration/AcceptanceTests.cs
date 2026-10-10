using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EngineeredTutors.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeredTutors.Api.Tests.Integration;

/// <summary>Section 8 "Key acceptance criteria" of the Diagnostic MVP Requirements v1.2, plus related rules.</summary>
[Collection(ApiCollection.Name)]
public class AcceptanceTests(ApiFixture api)
{
    private readonly HttpClient _client = api.CreateClient();
    private static readonly JsonSerializerOptions J = ApiFixture.Json;

    // ---------- helpers ----------

    private static object Learner(string first = "Thabo", string surname = "Mokoena", string? id = null,
        string[]? subjects = null) => new
    {
        firstName = first,
        surname,
        school = "Parktown Boys",
        grade = 11,
        subjects = (subjects ?? ["Maths"]).Select(s => new { subject = s, latestMark = (int?)55 }).ToArray(),
        goal = "Improve my mark",
        idType = id is null ? null : "SA_ID",
        idNumber = id,
    };

    private static object Registration(string phone, string email, params object[] learners) => new
    {
        parent = new { fullName = "Lerato Dlamini", phone, email, source = "Facebook" },
        learners,
        isGuardianOver18 = true,
        consentAccepted = true,
        consentTextVersion = "0.3-draft",
        turnstileToken = "test",
    };

    private async Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(string url, object? body, string? ip = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body, options: J) };
        if (ip is not null) req.Headers.Add("X-Test-IP", ip);
        using var res = await _client.SendAsync(req);
        return (res.StatusCode, await Body(res));
    }

    private async Task<(HttpStatusCode Status, JsonElement Body)> PutAsync(string url, object body)
    {
        using var res = await _client.PutAsJsonAsync(url, body, J);
        return (res.StatusCode, await Body(res));
    }

    private async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync(string url, HttpClient? client = null)
    {
        using var res = await (client ?? _client).GetAsync(url);
        return (res.StatusCode, await Body(res));
    }

    private static async Task<JsonElement> Body(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>Registers one learner and returns the token for their Maths test.</summary>
    private async Task<string> RegisterAndGetMathsTokenAsync(string? id = null)
    {
        var (status, body) = await PostAsync("/api/registrations",
            Registration(TestIds.Phone(), $"p{Guid.NewGuid():N}@example.com", Learner(id: id)));
        Assert.Equal(HttpStatusCode.OK, status);
        var url = body.GetProperty("links")[0].GetProperty("tests")[0].GetProperty("url").GetString()!;
        return url[(url.LastIndexOf('/') + 1)..];
    }

    private async Task<JsonElement[]> StartAndGetQuestionsAsync(string token)
    {
        var (s, _) = await PostAsync($"/api/attempts/{token}/start", null);
        Assert.Equal(HttpStatusCode.OK, s);
        var (qs, q) = await GetAsync($"/api/attempts/{token}/questions");
        Assert.Equal(HttpStatusCode.OK, qs);
        return q.GetProperty("questions").EnumerateArray().ToArray();
    }

    private Task<(HttpStatusCode, JsonElement)> SaveNumericAsync(string token, JsonElement question, string raw) =>
        PutAsync($"/api/attempts/{token}/responses/{question.GetProperty("id").GetGuid()}", new { numericRaw = raw });

    private Task<(HttpStatusCode, JsonElement)> SaveOptionAsync(string token, JsonElement question, Guid optionId) =>
        PutAsync($"/api/attempts/{token}/responses/{question.GetProperty("id").GetGuid()}", new { selectedOptionId = optionId });

    private Guid CorrectOptionId(JsonElement question)
    {
        var qid = question.GetProperty("id").GetGuid();
        return api.WithDb(db => db.QuestionOptions.Single(o => o.QuestionId == qid && o.IsCorrect).Id);
    }

    private Attempt LoadAttempt(string token)
    {
        var hash = Security.AttemptTokens.Hash(token);
        return api.WithDb(db => db.Attempts.AsNoTracking()
            .Include(a => a.TopicScores).Include(a => a.Responses)
            .Single(a => a.TokenHash == hash));
    }

    // ---------- section 8 ----------

    [Fact]
    public async Task Resume_after_refresh_keeps_answers_and_remaining_time()
    {
        var token = await RegisterAndGetMathsTokenAsync();
        var questions = await StartAndGetQuestionsAsync(token);
        await SaveOptionAsync(token, questions[0], CorrectOptionId(questions[0]));
        await SaveNumericAsync(token, questions[1], "5,05");

        api.Clock.Advance(TimeSpan.FromMinutes(10));

        var (status, state) = await GetAsync($"/api/attempts/{token}");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("InProgress", state.GetProperty("status").GetString());
        Assert.InRange(state.GetProperty("remainingSeconds").GetInt32(), 35 * 60 - 1, 35 * 60);

        var (_, q) = await GetAsync($"/api/attempts/{token}/questions");
        var list = q.GetProperty("questions");
        Assert.Equal(CorrectOptionId(questions[0]), list[0].GetProperty("selectedOptionId").GetGuid());
        Assert.Equal("5,05", list[1].GetProperty("numericRaw").GetString());

        // Starting again doesn't reset the timer (DX-08).
        var (_, again) = await PostAsync($"/api/attempts/{token}/start", null);
        Assert.InRange(again.GetProperty("remainingSeconds").GetInt32(), 35 * 60 - 1, 35 * 60);
    }

    [Fact]
    public async Task Auto_submit_scores_when_deadline_passes_with_page_closed_and_rejects_later_answers()
    {
        var token = await RegisterAndGetMathsTokenAsync();
        var questions = await StartAndGetQuestionsAsync(token);
        await SaveOptionAsync(token, questions[0], CorrectOptionId(questions[0]));

        api.Clock.Advance(TimeSpan.FromMinutes(45) + TimeSpan.FromSeconds(31));
        Assert.True(await api.RunAutoSubmitAsync() >= 1);

        var attempt = LoadAttempt(token);
        Assert.Equal(AttemptStatus.Submitted, attempt.Status);
        Assert.Equal(attempt.DeadlineAt, attempt.SubmittedAt);
        Assert.Equal(33.33m, attempt.OverallPct); // 2 of 6 marks
        Assert.Equal(3, attempt.TopicScores.Count);

        var (status, body) = await SaveNumericAsync(token, questions[1], "5.05");
        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("time_up", body.GetProperty("code").GetString());
        Assert.Contains("Time is up", body.GetProperty("detail").GetString());

        // DX-12: the link now shows the thank-you state.
        var (_, state) = await GetAsync($"/api/attempts/{token}");
        Assert.Equal("Submitted", state.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DX09_answers_within_grace_are_accepted_and_after_grace_rejected()
    {
        var token = await RegisterAndGetMathsTokenAsync();
        var questions = await StartAndGetQuestionsAsync(token);

        api.Clock.Advance(TimeSpan.FromMinutes(45) + TimeSpan.FromSeconds(20));
        var (inGrace, _) = await SaveNumericAsync(token, questions[1], "5.05");
        Assert.Equal(HttpStatusCode.OK, inGrace);

        api.Clock.Advance(TimeSpan.FromSeconds(15));
        var (late, body) = await SaveNumericAsync(token, questions[2], "-4");
        Assert.Equal(HttpStatusCode.Conflict, late);
        Assert.Equal("time_up", body.GetProperty("code").GetString());

        var attempt = LoadAttempt(token);
        Assert.Equal(AttemptStatus.Submitted, attempt.Status);
        Assert.Equal(2, attempt.Responses.Sum(r => r.MarksAwarded ?? 0)); // the in-grace answer counted, the late one didn't
    }

    [Fact]
    public async Task No_answer_leak_from_the_questions_endpoint()
    {
        var token = await RegisterAndGetMathsTokenAsync();
        await PostAsync($"/api/attempts/{token}/start", null);
        using var res = await _client.GetAsync($"/api/attempts/{token}/questions");
        var raw = (await res.Content.ReadAsStringAsync()).ToLowerInvariant();

        foreach (var forbidden in new[] { "iscorrect", "answervalue", "tolerance", "explanation", "label", "5.05", "worked answer" })
            Assert.DoesNotContain(forbidden, raw);
    }

    [Fact]
    public async Task Numeric_input_rules_match_the_spec()
    {
        var token = await RegisterAndGetMathsTokenAsync();
        var questions = await StartAndGetQuestionsAsync(token);
        var num = questions[1]; // 5.05 ± 0.01
        Assert.Equal("N", num.GetProperty("unit").GetString());

        var (flagged, body) = await SaveNumericAsync(token, num, "5.05m");
        Assert.Equal(HttpStatusCode.BadRequest, flagged);
        Assert.Equal("invalid_number", body.GetProperty("code").GetString());
        Assert.Null(LoadAttempt(token).Responses.Single(r => r.QuestionId == num.GetProperty("id").GetGuid()).NumericRaw);

        foreach (var (input, correct) in new[] { ("5,05", true), ("5.05", true), (" 5.05 ", true), ("5,06", true), ("5,07", false) })
        {
            var t = await RegisterAndGetMathsTokenAsync();
            var qs = await StartAndGetQuestionsAsync(t);
            var (ok, _) = await SaveNumericAsync(t, qs[1], input);
            Assert.Equal(HttpStatusCode.OK, ok);
            await PostAsync($"/api/attempts/{t}/submit", null);
            var r = LoadAttempt(t).Responses.Single(x => x.QuestionId == qs[1].GetProperty("id").GetGuid());
            Assert.Equal(correct, r.IsCorrect);
            Assert.Equal(input, r.NumericRaw); // raw input stored alongside the parsed value (SC-02)
        }
    }

    [Fact]
    public async Task Invalid_ID_number_shows_field_error_and_saves_nothing()
    {
        var phone = TestIds.Phone();
        var (status, body) = await PostAsync("/api/registrations",
            Registration(phone, "x@example.com", Learner(id: "8001015009088")));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.True(body.GetProperty("errors").TryGetProperty("learners[0].idNumber", out _));
        var normalised = "+27" + phone[1..];
        Assert.False(api.WithDb(db => db.Parents.Any(p => p.Phone == normalised)));
    }

    [Fact]
    public async Task Version_safety_published_questions_cannot_change_and_scores_stay()
    {
        var token = await RegisterAndGetMathsTokenAsync();
        var questions = await StartAndGetQuestionsAsync(token);
        await SaveOptionAsync(token, questions[0], CorrectOptionId(questions[0]));
        await PostAsync($"/api/attempts/{token}/submit", null);
        var before = LoadAttempt(token).OverallPct;

        var qid = questions[0].GetProperty("id").GetGuid();
        Assert.Throws<PublishedAssessmentException>(() => api.WithDb(db =>
        {
            db.Questions.Single(q => q.Id == qid).StemLatex = "Edited";
            return db.SaveChanges();
        }));
        Assert.Throws<PublishedAssessmentException>(() => api.WithDb(db =>
        {
            db.QuestionOptions.First(o => o.QuestionId == qid && !o.IsCorrect).IsCorrect = true;
            return db.SaveChanges();
        }));

        Assert.Equal(before, LoadAttempt(token).OverallPct);
    }

    [Fact]
    public async Task Atomic_import_one_bad_row_imports_nothing_and_reports_the_row()
    {
        var admin = api.Admin();
        var countBefore = api.WithDb(db => db.Assessments.Count());
        var csv = TestCsv.Maths + TestCsv.NumRow("T04", "MATH-11-ALG", "Bad", "abc", "0");

        using var res = await ApiFixture.PostCsvAsync(admin, Subject.Maths, csv, 45);
        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var body = await Body(res);
        var error = body.GetProperty("errors")[0];
        Assert.Equal(5, error.GetProperty("row").GetInt32());
        Assert.Equal("answer_value", error.GetProperty("column").GetString());
        Assert.Equal(countBefore, api.WithDb(db => db.Assessments.Count()));
    }

    [Fact]
    public async Task No_ID_number_registration_succeeds_with_links()
    {
        var (status, body) = await PostAsync("/api/registrations",
            Registration(TestIds.Phone(), "noid@example.com", Learner("Ayanda", "Khumalo", subjects: ["Maths", "PhysicalSciences"])));

        Assert.Equal(HttpStatusCode.OK, status);
        var tests = body.GetProperty("links")[0].GetProperty("tests");
        Assert.Equal(2, tests.GetArrayLength());
        Assert.StartsWith("https://test.example/d/", tests[0].GetProperty("url").GetString());
        Assert.Equal(45, tests[0].GetProperty("minutes").GetInt32());
        Assert.Equal(40, tests[1].GetProperty("minutes").GetInt32());
    }

    [Fact]
    public async Task Duplicate_by_ID_creates_no_second_learner_and_resends_to_email_on_file()
    {
        var id = TestIds.SaId();
        var first = Registration(TestIds.Phone(), "first@example.com", Learner("Sipho", "Ndlovu", id));
        var (s1, b1) = await PostAsync("/api/registrations", first);
        Assert.Equal(HttpStatusCode.OK, s1);

        // A different parent phone and email, same ID number, after the recent-link window.
        api.Clock.Advance(TimeSpan.FromMinutes(20));
        var second = Registration(TestIds.Phone(), "someone-else@example.com", Learner("S", "Ndlovu", id));
        var (s2, b2) = await PostAsync("/api/registrations", second);

        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.Equal(b1.GetProperty("message").GetString(), b2.GetProperty("message").GetString());
        var last4 = id[^4..];
        Assert.Equal(1, api.WithDb(db => db.Learners.Count(l => l.Surname == "Ndlovu" && l.IdLast4 == last4)));

        var queued = api.WithDb(db => db.EmailMessages.Where(m => m.To == "first@example.com").ToList());
        Assert.Equal(2, queued.Count); // original confirmation + resend
        Assert.False(api.WithDb(db => db.EmailMessages.Any(m => m.To == "someone-else@example.com")));
        Assert.False(api.WithDb(db => db.Parents.Any(p => p.Email == "someone-else@example.com")));
    }

    [Fact]
    public async Task Duplicate_without_ID_matches_on_phone_and_name_ignoring_case()
    {
        var phone = TestIds.Phone();
        await PostAsync("/api/registrations", Registration(phone, "dup@example.com", Learner("thabo", "Mokoena")));
        api.Clock.Advance(TimeSpan.FromMinutes(20));
        var (status, _) = await PostAsync("/api/registrations", Registration(phone, "dup@example.com", Learner("Thabo", "Mokoena")));

        Assert.Equal(HttpStatusCode.OK, status);
        var normalised = "+27" + phone[1..];
        Assert.Equal(1, api.WithDb(db => db.Learners.Count(l => l.Parent.Phone == normalised)));
    }

    // ---------- other rules ----------

    [Fact]
    public async Task REG07_resend_rotates_old_links_but_not_within_a_few_minutes()
    {
        var phone = TestIds.Phone();
        var reg = Registration(phone, "rot@example.com", Learner("Kea", "Molefe"));
        var (_, first) = await PostAsync("/api/registrations", reg);
        var url = first.GetProperty("links")[0].GetProperty("tests")[0].GetProperty("url").GetString()!;
        var token = url[(url.LastIndexOf('/') + 1)..];

        // A double-tapped submit must not break the links on screen.
        await PostAsync("/api/registrations", reg);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync($"/api/attempts/{token}")).Status);

        api.Clock.Advance(TimeSpan.FromMinutes(20));
        await PostAsync("/api/registrations", reg);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"/api/attempts/{token}")).Status);
    }

    [Fact]
    public async Task REG07_adding_an_ID_on_a_name_match_stores_it()
    {
        var phone = TestIds.Phone();
        await PostAsync("/api/registrations", Registration(phone, "addid@example.com", Learner("Naledi", "Sithole")));
        var id = TestIds.SaId();
        await PostAsync("/api/registrations", Registration(phone, "addid@example.com", Learner("Naledi", "Sithole", id)));

        var normalised = "+27" + phone[1..];
        var learner = api.WithDb(db => db.Learners.Single(l => l.Parent.Phone == normalised));
        Assert.Equal(id[^4..], learner.IdLast4);
        Assert.NotNull(learner.IdNumberEncrypted);
        Assert.DoesNotContain(id, learner.IdNumberEncrypted);
    }

    [Fact]
    public async Task REG09_same_ID_twice_in_one_registration_is_rejected()
    {
        var id = TestIds.SaId();
        var (status, body) = await PostAsync("/api/registrations",
            Registration(TestIds.Phone(), "twice@example.com", Learner("A", "One", id), Learner("B", "Two", id)));
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.True(body.GetProperty("errors").TryGetProperty("learners[1].idNumber", out _));
    }

    [Fact]
    public async Task REG03_at_most_three_learners()
    {
        var (status, body) = await PostAsync("/api/registrations", Registration(TestIds.Phone(), "four@example.com",
            Learner("A", "A"), Learner("B", "B"), Learner("C", "C"), Learner("D", "D")));
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.True(body.GetProperty("errors").TryGetProperty("learners", out _));
    }

    [Fact]
    public async Task REG04_both_checkboxes_are_required()
    {
        var json = JsonSerializer.SerializeToNode(Registration(TestIds.Phone(), "cb@example.com", Learner()), J)!;
        json["isGuardianOver18"] = false;
        json["consentAccepted"] = false;
        var (status, body) = await PostAsync("/api/registrations", json);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.True(body.GetProperty("errors").TryGetProperty("isGuardianOver18", out _));
        Assert.True(body.GetProperty("errors").TryGetProperty("consentAccepted", out _));
    }

    [Fact]
    public async Task REG05_consent_version_time_and_IP_are_stored()
    {
        var phone = TestIds.Phone();
        await PostAsync("/api/registrations", Registration(phone, "consent@example.com", Learner()), ip: "196.25.1.7");
        var normalised = "+27" + phone[1..];
        var consent = api.WithDb(db => db.Consents.Single(c => db.Parents.Any(p => p.Id == c.ParentId && p.Phone == normalised)));
        Assert.Equal("0.3-draft", consent.TextVersion);
        Assert.Equal("196.25.1.7", consent.IpAddress);
        Assert.Equal(api.Clock.GetUtcNow().UtcDateTime, consent.AcceptedAt);
    }

    [Fact]
    public async Task REG06_honeypot_stores_nothing_but_looks_successful()
    {
        var phone = TestIds.Phone();
        var json = JsonSerializer.SerializeToNode(Registration(phone, "bot@example.com", Learner()), J)!;
        json["website"] = "http://spam.example";
        var (status, body) = await PostAsync("/api/registrations", json);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(0, body.GetProperty("links").GetArrayLength());
        Assert.False(api.WithDb(db => db.Parents.Any(p => p.Email == "bot@example.com")));
    }

    [Fact]
    public async Task REG06_five_registrations_per_IP_per_hour()
    {
        const string ip = "41.13.200.9";
        for (var i = 0; i < 5; i++)
        {
            var (ok, _) = await PostAsync("/api/registrations",
                Registration(TestIds.Phone(), $"rl{i}@example.com", Learner()), ip);
            Assert.Equal(HttpStatusCode.OK, ok);
        }
        var (limited, _) = await PostAsync("/api/registrations", Registration(TestIds.Phone(), "rl5@example.com", Learner()), ip);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited);

        api.Clock.Advance(TimeSpan.FromMinutes(61));
        var (later, _) = await PostAsync("/api/registrations", Registration(TestIds.Phone(), "rl6@example.com", Learner()), ip);
        Assert.Equal(HttpStatusCode.OK, later);
    }

    [Fact]
    public async Task DX01_tokens_expire_after_30_days_if_unused()
    {
        var token = await RegisterAndGetMathsTokenAsync();
        api.Clock.Advance(TimeSpan.FromDays(30) + TimeSpan.FromMinutes(1));
        var (status, body) = await GetAsync($"/api/attempts/{token}");
        Assert.Equal(HttpStatusCode.Gone, status);
        Assert.Equal("expired", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DX05_option_order_is_stored_and_stable_per_attempt()
    {
        var token = await RegisterAndGetMathsTokenAsync();
        var first = (await StartAndGetQuestionsAsync(token))[0].GetProperty("options").EnumerateArray()
            .Select(o => o.GetProperty("id").GetGuid()).ToList();
        var (_, again) = await GetAsync($"/api/attempts/{token}/questions");
        var second = again.GetProperty("questions")[0].GetProperty("options").EnumerateArray()
            .Select(o => o.GetProperty("id").GetGuid()).ToList();

        Assert.Equal(4, first.Count);
        Assert.Equal(first, second);
        Assert.Equal(string.Join(',', first), LoadAttempt(token).Responses.OrderBy(r => r.Order).First().OptionOrderShown);
    }

    [Fact]
    public async Task DX12_submitted_attempt_is_locked()
    {
        var token = await RegisterAndGetMathsTokenAsync();
        var questions = await StartAndGetQuestionsAsync(token);
        var (s1, _) = await PostAsync($"/api/attempts/{token}/submit", null);
        var (s2, again) = await PostAsync($"/api/attempts/{token}/submit", null);
        Assert.Equal(HttpStatusCode.OK, s1);
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.Equal("Submitted", again.GetProperty("status").GetString());

        var (save, body) = await SaveNumericAsync(token, questions[1], "5.05");
        Assert.Equal(HttpStatusCode.Conflict, save);
        Assert.Equal("submitted", body.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await GetAsync($"/api/attempts/{token}/questions")).Status);
    }

    [Fact]
    public async Task Unknown_or_malformed_tokens_are_not_found()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync("/api/attempts/not-a-token")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"/api/attempts/{Security.AttemptTokens.New()}")).Status);
    }

    [Fact]
    public async Task NT01_NT02_NT03_emails_are_queued_encrypted_and_sent_with_retry()
    {
        var id = TestIds.SaId();
        var email = $"mail{Guid.NewGuid():N}@example.com";
        var (_, reg) = await PostAsync("/api/registrations", Registration(TestIds.Phone(), email, Learner("Lindiwe", "Zulu", id)));
        var url = reg.GetProperty("links")[0].GetProperty("tests")[0].GetProperty("url").GetString()!;

        var queued = api.WithDb(db => db.EmailMessages.Single(m => m.To == email));
        Assert.DoesNotContain("https://", queued.BodyEncrypted); // links (tokens) are not stored readable

        api.Email.Fail = true;
        while (await api.RunEmailDispatchAsync() > 0) { }
        var failed = api.WithDb(db => db.EmailMessages.Single(m => m.To == email));
        Assert.Null(failed.SentAt);
        Assert.Equal(1, failed.AttemptCount);

        api.Email.Fail = false;
        api.Clock.Advance(TimeSpan.FromMinutes(2));
        while (await api.RunEmailDispatchAsync() > 0) { }
        var sent = api.WithDb(db => db.EmailMessages.Single(m => m.To == email));
        Assert.NotNull(sent.SentAt);
        Assert.Null(sent.BodyEncrypted);

        var (to, subject, html) = api.Email.Sent.Last(m => m.To == email);
        Assert.Contains("Lindiwe", subject);
        Assert.Contains(url, html);
        Assert.Contains("45 minutes", html);
        Assert.DoesNotContain(id, html); // NF-09

        // NT-02: admin alert on submit.
        var token = url[(url.LastIndexOf('/') + 1)..];
        await StartAndGetQuestionsAsync(token);
        await PostAsync($"/api/attempts/{token}/submit", null);
        Assert.True(api.WithDb(db => db.EmailMessages.Any(m => m.To == "tutor@example.com" && m.Subject.Contains("Lindiwe Zulu"))));
    }
}
