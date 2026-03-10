namespace AzureDevOpsAuditAgent.Class;

/// <summary>
/// Representa um pipeline do Azure DevOps
/// </summary>
public class Pipeline
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Folder { get; set; }
    public int Revision { get; set; }
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Representa uma execução (run) de um pipeline
/// </summary>
public class PipelineRun
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? FinishedDate { get; set; }
    public string Url { get; set; } = string.Empty;
    public Pipeline? Pipeline { get; set; }
}

/// <summary>
/// Representa um log de execução de pipeline
/// </summary>
public class PipelineLog
{
    public int Id { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastChangedOn { get; set; }
    public long LineCount { get; set; }
    public string Url { get; set; } = string.Empty;
    public SignedUrl? SignedContent { get; set; }
}

/// <summary>
/// Coleção de logs
/// </summary>
public class LogCollection
{
    public List<PipelineLog> Logs { get; set; } = new();
    public string Url { get; set; } = string.Empty;
    public SignedUrl? SignedContent { get; set; }
}

/// <summary>
/// URL assinada para acesso limitado a recursos privados
/// </summary>
public class SignedUrl
{
    public string Url { get; set; } = string.Empty;
    public DateTime SignatureExpires { get; set; }
}