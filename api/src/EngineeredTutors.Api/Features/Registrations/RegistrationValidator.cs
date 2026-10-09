using System.Net.Mail;
using EngineeredTutors.Api.Data;
using EngineeredTutors.Api.Domain;

namespace EngineeredTutors.Api.Features.Registrations;

/// <summary>A cleaned, validated registration. Only built when every field passed.</summary>
public sealed record ValidRegistration(
    string ParentName, string Phone, string Email, string Source, string ConsentVersion,
    IReadOnlyList<ValidLearner> Learners);

public sealed record ValidLearner(
    int Index, string FirstName, string Surname, string School, int Grade, string Goal, string NameKey,
    IReadOnlyList<SubjectInput> Subjects, IdType? IdType, string? IdNumber, DateOnly? DateOfBirth);

/// <summary>Field-level validation for REG-01 to REG-04 and REG-09. Error keys match the JSON paths the form uses.</summary>
public static class RegistrationValidator
{
    public static (ValidRegistration? Value, Dictionary<string, string[]> Errors) Validate(
        RegistrationRequest r, SiteOptions site, DateOnly today)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string key, string msg)
        {
            if (!errors.TryGetValue(key, out var list)) errors[key] = list = [];
            list.Add(msg);
        }

        var parentName = Normalise.Text(r.Parent.FullName);
        if (parentName.Length is < 2 or > 120) Add("parent.fullName", "Enter your full name.");

        var phone = Normalise.SaMobile(r.Parent.Phone);
        if (phone is null) Add("parent.phone", "Enter a South African mobile number, e.g. 082 123 4567.");

        var email = (r.Parent.Email ?? "").Trim();
        if (!IsEmail(email)) Add("parent.email", "Enter a valid email address.");

        var source = (r.Parent.Source ?? "").Trim();
        if (!RegistrationLists.Sources.Contains(source)) Add("parent.source", "Tell us how you heard about us.");

        if (!r.IsGuardianOver18) Add("isGuardianOver18", "Please confirm you are the parent or legal guardian and over 18.");
        if (!r.ConsentAccepted) Add("consentAccepted", "Please accept the consent statement to continue.");

        var consentVersion = string.IsNullOrWhiteSpace(r.ConsentTextVersion) ? site.ConsentTextVersion : r.ConsentTextVersion.Trim();
        if (consentVersion.Length > 32) Add("consentTextVersion", "Invalid consent version.");

        if (r.Learners.Count == 0) Add("learners", "Add at least one learner.");
        if (r.Learners.Count > site.MaxLearnersPerRegistration)
            Add("learners", $"You can register up to {site.MaxLearnersPerRegistration} learners at a time.");

        var learners = new List<ValidLearner>();
        var seenIds = new Dictionary<string, int>();
        var seenNames = new Dictionary<string, int>();
        for (var i = 0; i < r.Learners.Count && i < site.MaxLearnersPerRegistration; i++)
        {
            var l = r.Learners[i];
            var p = $"learners[{i}]";

            var first = Normalise.Text(l.FirstName);
            var surname = Normalise.Text(l.Surname);
            var school = Normalise.Text(l.School);
            if (first.Length is < 1 or > 60) Add($"{p}.firstName", "Enter the learner's first name.");
            if (surname.Length is < 1 or > 60) Add($"{p}.surname", "Enter the learner's surname.");
            if (school.Length is < 2 or > 120) Add($"{p}.school", "Enter the learner's school.");
            if (l.Grade is not (10 or 11 or 12)) Add($"{p}.grade", "Choose Grade 10, 11 or 12.");

            var goal = (l.Goal ?? "").Trim();
            if (!RegistrationLists.Goals.Contains(goal)) Add($"{p}.goal", "Choose a goal.");

            if (l.Subjects.Count == 0) Add($"{p}.subjects", "Choose at least one subject.");
            if (l.Subjects.Select(s => s.Subject).Distinct().Count() != l.Subjects.Count)
                Add($"{p}.subjects", "Each subject can only be chosen once.");
            if (l.Subjects.Any(s => !Enum.IsDefined(s.Subject))) Add($"{p}.subjects", "Unknown subject.");
            for (var j = 0; j < l.Subjects.Count; j++)
                if (l.Subjects[j].LatestMark is < 0 or > 100)
                    Add($"{p}.subjects[{j}].latestMark", "Enter a mark from 0 to 100, or leave it blank.");

            string? id = null;
            DateOnly? dob = null;
            if (!string.IsNullOrWhiteSpace(l.IdNumber))
            {
                if (l.IdType is null || !Enum.IsDefined(l.IdType.Value))
                    Add($"{p}.idType", "Choose SA ID or passport.");
                else
                {
                    var res = IdNumbers.Validate(l.IdType.Value, l.IdNumber, today);
                    if (!res.IsValid) Add($"{p}.idNumber", res.Error!);
                    else if (seenIds.ContainsKey(res.Normalised!))
                        Add($"{p}.idNumber", "This ID number is already entered for another learner on this form.");
                    else
                    {
                        seenIds[res.Normalised!] = i;
                        id = res.Normalised;
                        dob = res.DateOfBirth;
                    }
                }
            }

            var nameKey = Normalise.NameKey(first, surname);
            if (nameKey.Length > 0 && seenNames.ContainsKey(nameKey))
                Add($"{p}.firstName", "This learner is already on the form.");
            seenNames.TryAdd(nameKey, i);

            learners.Add(new ValidLearner(i, first, surname, school, l.Grade ?? 0, goal, nameKey,
                l.Subjects, id is null ? null : l.IdType, id, dob));
        }

        var result = errors.ToDictionary(e => e.Key, e => e.Value.Distinct().ToArray());
        if (result.Count > 0) return (null, result);
        return (new ValidRegistration(parentName, phone!, email, source, consentVersion, learners), result);
    }

    private static bool IsEmail(string s) =>
        s.Length is > 3 and <= 254 &&
        MailAddress.TryCreate(s, out var a) && a.Address == s &&
        s.IndexOf('@') is var at && at > 0 && s.IndexOf('.', at) > at + 1 && !s.EndsWith('.');
}
