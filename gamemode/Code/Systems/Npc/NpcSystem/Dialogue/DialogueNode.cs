namespace OpenFramework.Dialog;

public class DialogueNode
{
	public string Message { get; set; } 
	public List<DialogueChoice> Choices { get; set; } = new();
}

public class DialogueChoice
{
	public string Text { get; set; } 
	public DialogueNode NextNode { get; set; } 
	public System.Action Action { get; set; } 
}
