using System.ComponentModel.DataAnnotations;

namespace OrchardCore.Crest.Workflows.Controllers.WorkflowDefinitions.Edit;

public class EditViewModel
{
    [Required] public string DefinitionId { get; set; } = null!;
}