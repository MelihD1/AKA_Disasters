public interface IInteractable
{
    string InteractionPrompt { get; }
    void Interact(InteractionContext context);
}