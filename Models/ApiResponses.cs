namespace AzureDevOpsAuditAgent.Models
{
    /// <summary>
    /// Resposta com a contagem de projetos
    /// </summary>
    public class ProjectCountResponse
    {
        /// <summary>
        /// Número total de projetos na organização
        /// </summary>
        /// <example>15</example>
        public required int Projects { get; set; }
    }

    /// <summary>
    /// Resposta com a contagem de usuários
    /// </summary>
    public class UserCountResponse
    {
        /// <summary>
        /// Número total de usuários cadastrados
        /// </summary>
        /// <example>42</example>
        public required int Users { get; set; }
    }

    /// <summary>
    /// Resposta com informações de licença do usuário
    /// </summary>
    public class UserLicenseResponse
    {
        /// <summary>
        /// Email ou User Principal Name do usuário
        /// </summary>
        /// <example>usuario@exemplo.com</example>
        public required string User { get; set; }

        /// <summary>
        /// Tipo de licença do usuário
        /// </summary>
        /// <example>Visual Studio Enterprise</example>
        public required string License { get; set; }
    }

    /// <summary>
    /// Resposta com a lista de administradores do projeto
    /// </summary>
    public class ProjectAdminsResponse
    {
        /// <summary>
        /// ID ou nome do projeto
        /// </summary>
        /// <example>MeuProjeto</example>
        public required string Project { get; set; }

        /// <summary>
        /// Lista de administradores (descritores ou nomes)
        /// </summary>
        /// <example>["admin@exemplo.com", "gerente@exemplo.com"]</example>
        public required IEnumerable<string> Administrators { get; set; }
    }

    /// <summary>
    /// Resposta com a lista de usuários
    /// </summary>
    public class UsersListResponse
    {
        /// <summary>
        /// Lista de usuários
        /// </summary>
        public required List<AzureDevOpsAuditAgent.Class.GraphUser> Users { get; set; }

        /// <summary>
        /// Número total de usuários
        /// </summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Resposta com a lista de grupos
    /// </summary>
    public class GroupsListResponse
    {
        /// <summary>
        /// Lista de grupos
        /// </summary>
        public required List<AzureDevOpsAuditAgent.Class.GraphGroup> Groups { get; set; }

        /// <summary>
        /// Número total de grupos
        /// </summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Resposta com a lista de membros de um grupo
    /// </summary>
    public class GroupMembersResponse
    {
        /// <summary>
        /// Descriptor do grupo
        /// </summary>
        public required string GroupDescriptor { get; set; }

        /// <summary>
        /// Lista de membros
        /// </summary>
        public required List<AzureDevOpsAuditAgent.Class.GraphMember> Members { get; set; }

        /// <summary>
        /// Número total de membros
        /// </summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Resposta de operações em grupos (adicionar/remover membros)
    /// </summary>
    public class GroupOperationResponse
    {
        /// <summary>
        /// Indica se a operação foi bem-sucedida
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Mensagem descritiva do resultado
        /// </summary>
        public required string Message { get; set; }

        /// <summary>
        /// Descriptor do grupo
        /// </summary>
        public required string GroupDescriptor { get; set; }

        /// <summary>
        /// Descriptor do usuário
        /// </summary>
        public required string UserDescriptor { get; set; }
    }
}