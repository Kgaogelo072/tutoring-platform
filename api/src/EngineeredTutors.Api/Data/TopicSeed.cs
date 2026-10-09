namespace EngineeredTutors.Api.Data;

/// <summary>
/// Topic codes used by the Maths and Physical Sciences diagnostics v1.1. Question imports must use one of these
/// (AD-06). Add new topics here and create a migration.
/// </summary>
public static class TopicSeed
{
    public static readonly Topic[] All =
    [
        new() { Id = 1, Subject = Subject.Maths, Grade = 10, Code = "MATH-10-ALG", Name = "Algebra (Grade 10)" },
        new() { Id = 2, Subject = Subject.Maths, Grade = 11, Code = "MATH-11-ALG", Name = "Algebra" },
        new() { Id = 3, Subject = Subject.Maths, Grade = 11, Code = "MATH-11-PAT", Name = "Number patterns" },
        new() { Id = 4, Subject = Subject.Maths, Grade = 11, Code = "MATH-11-FUN", Name = "Functions" },
        new() { Id = 5, Subject = Subject.Maths, Grade = 11, Code = "MATH-11-FIN", Name = "Finance" },
        new() { Id = 6, Subject = Subject.Maths, Grade = 11, Code = "MATH-11-TRIG", Name = "Trigonometry" },
        new() { Id = 7, Subject = Subject.Maths, Grade = 11, Code = "MATH-11-AG", Name = "Analytical geometry" },
        new() { Id = 8, Subject = Subject.Maths, Grade = 11, Code = "MATH-11-GEO", Name = "Euclidean geometry" },
        new() { Id = 9, Subject = Subject.Maths, Grade = 11, Code = "MATH-11-PROB", Name = "Probability" },
        new() { Id = 10, Subject = Subject.PhysicalSciences, Grade = 10, Code = "PHYS-10-MATH", Name = "Maths in physics" },
        new() { Id = 11, Subject = Subject.PhysicalSciences, Grade = 11, Code = "PHYS-11-VEC", Name = "Vectors" },
        new() { Id = 12, Subject = Subject.PhysicalSciences, Grade = 11, Code = "PHYS-11-NEW", Name = "Newton's laws" },
        new() { Id = 13, Subject = Subject.PhysicalSciences, Grade = 11, Code = "PHYS-11-ES", Name = "Electrostatics" },
        new() { Id = 14, Subject = Subject.PhysicalSciences, Grade = 11, Code = "PHYS-11-ELC", Name = "Electric circuits" },
        new() { Id = 15, Subject = Subject.PhysicalSciences, Grade = 11, Code = "CHEM-11-STO", Name = "Stoichiometry" },
        new() { Id = 16, Subject = Subject.PhysicalSciences, Grade = 11, Code = "CHEM-11-GAS", Name = "Ideal gases" },
        new() { Id = 17, Subject = Subject.PhysicalSciences, Grade = 11, Code = "CHEM-11-IMF", Name = "Intermolecular forces" },
    ];
}
