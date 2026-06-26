namespace BuildCv.Domain.Resumes;

/// <summary>
/// Origen declarado de cada campo del CV. La regla constitucional Art. I
/// (cero invención) exige que solo el editor promueva un campo a
/// <see cref="UserConfirmed"/>; los parsers solo emiten
/// <see cref="Inferred"/> o <see cref="Explicit"/>.
/// </summary>
public enum ConfidenceMarker
{
    Inferred,
    Explicit,
    UserConfirmed,
}

/// <summary>
/// Sub-formulario colombiano bajo <c>basics.datosPersonales</c>. Todos los
/// campos son opcionales: solo se incluyen cuando el parser detectó el dato
/// en el CV original. La Constitución Art. III prohíbe inventar valores.
/// </summary>
public sealed record DatosPersonales(
    string? Cedula,
    string? Nacionalidad,
    string? EstadoCivil,
    string? LibretaMilitar,
    string? Rh);

public sealed record ResumeProfile(string Network, string Url);

public sealed record BasicsConfidence(
    ConfidenceMarker Name,
    ConfidenceMarker Email,
    ConfidenceMarker Phone,
    ConfidenceMarker Location,
    ConfidenceMarker Url,
    ConfidenceMarker Profiles,
    ConfidenceMarker Summary,
    ConfidenceMarker DatosPersonales);

public sealed record Basics(
    string Name,
    string Email,
    string? Phone,
    string? Location,
    string? Url,
    IReadOnlyList<ResumeProfile> Profiles,
    string? Summary,
    DatosPersonales? DatosPersonales,
    BasicsConfidence Confidence);

public sealed record ResumeWorkEntry(
    string Name,
    string Position,
    string StartDate,
    string? EndDate,
    string? Summary,
    IReadOnlyList<string>? Highlights);

public sealed record WorkConfidence(
    ConfidenceMarker Name,
    ConfidenceMarker Position,
    ConfidenceMarker StartDate,
    ConfidenceMarker EndDate,
    ConfidenceMarker Summary,
    ConfidenceMarker Highlights);

public sealed record TaggedResumeWork(ResumeWorkEntry Entry, WorkConfidence Confidence);

public sealed record ResumeEducationEntry(
    string Institution,
    string? Area,
    string? StudyType,
    string StartDate,
    string? EndDate,
    string? Score);

public sealed record EducationConfidence(
    ConfidenceMarker Institution,
    ConfidenceMarker Area,
    ConfidenceMarker StudyType,
    ConfidenceMarker StartDate,
    ConfidenceMarker EndDate,
    ConfidenceMarker Score);

public sealed record TaggedResumeEducation(ResumeEducationEntry Entry, EducationConfidence Confidence);

public sealed record ResumeSkillEntry(string Name, string? Level);

public sealed record SkillConfidence(ConfidenceMarker Name, ConfidenceMarker Level);

public sealed record TaggedResumeSkill(ResumeSkillEntry Entry, SkillConfidence Confidence);

public sealed record ResumeProjectEntry(
    string Name,
    string? Description,
    IReadOnlyList<string>? Highlights,
    IReadOnlyList<string>? Keywords,
    string? StartDate,
    string? EndDate,
    string? Url);

public sealed record ProjectConfidence(
    ConfidenceMarker Name,
    ConfidenceMarker Description,
    ConfidenceMarker Highlights,
    ConfidenceMarker Keywords,
    ConfidenceMarker StartDate,
    ConfidenceMarker EndDate,
    ConfidenceMarker Url);

public sealed record TaggedResumeProject(ResumeProjectEntry Entry, ProjectConfidence Confidence);

public sealed record ResumeCertificateEntry(
    string Name,
    string? Issuer,
    string? Date,
    string? Url);

public sealed record CertificateConfidence(
    ConfidenceMarker Name,
    ConfidenceMarker Issuer,
    ConfidenceMarker Date,
    ConfidenceMarker Url);

public sealed record TaggedResumeCertificate(ResumeCertificateEntry Entry, CertificateConfidence Confidence);

public sealed record ResumeLanguageEntry(string Language, string? Fluency);

public sealed record LanguageConfidence(
    ConfidenceMarker Language,
    ConfidenceMarker Fluency);

public sealed record TaggedResumeLanguage(ResumeLanguageEntry Entry, LanguageConfidence Confidence);

public sealed record CvMeta(string EngineVersion);

/// <summary>
/// CV en formato JSON Resume (https://jsonresume.org/schema) extendido con
/// sub-formulario colombiano <see cref="DatosPersonales"/>. Cada campo
/// lleva su <see cref="ConfidenceMarker"/> para que el motor de puntaje y
/// el editor puedan aplicar la regla de cero invención (Constitution
/// Art. I). Esta es la entrada del motor v2 (<c>engineVersion: "2.0.0"</c>).
/// </summary>
public sealed record CvDocument(
    Basics Basics,
    IReadOnlyList<TaggedResumeWork> Work,
    IReadOnlyList<TaggedResumeEducation> Education,
    IReadOnlyList<TaggedResumeSkill> Skills,
    IReadOnlyList<TaggedResumeProject> Projects,
    IReadOnlyList<TaggedResumeCertificate> Certificates,
    IReadOnlyList<TaggedResumeLanguage> Languages,
    CvMeta Meta);
